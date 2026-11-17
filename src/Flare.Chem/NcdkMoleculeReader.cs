using NCDK;
using NCDK.Aromaticities;
using NCDK.Graphs;
using NCDK.Smiles;
using NCDK.Stereo;

namespace Flare.Chem;

/// <summary>
/// Reads a molecule with NCDK and derives the exact per-atom values RDKit would report, including its
/// quirks (aromatic-bond reduction in the explicit valence, implicit valence forced to 0 for bracket
/// atoms, conjugation-driven SP2 hybridisation, isotope masses, stereo sense).
/// </summary>
public static class NcdkMoleculeReader
{
    /// <summary>Placeholder for the implicit hydrogen in a stereo ligand list.</summary>
    private const int HydrogenSlot = -1;

    /// <summary>Placeholder for a ligand NCDK knows but the SMILES walk could not place.</summary>
    private const int UnknownSlot = -2;

    public static MoleculeData Read(string smiles)
    {
        var container = new SmilesParser(NCDK.Silent.ChemObjectBuilder.Instance).ParseSmiles(smiles)
            ?? throw new ArgumentException($"Invalid SMILES '{smiles}'");
        if (container.Atoms.Count == 0) throw new ArgumentException($"SMILES '{smiles}' has no atoms");

        SeparateHalogenOxideCharges(container);
        new Aromaticity(ElectronDonation.DaylightModel, Cycles.RelevantFinder).Apply(container);
        Cycles.MarkRingAtomsAndBonds(container);

        var scan = SmilesScan.Walk(smiles);
        if (scan.Bracket.Length != container.Atoms.Count)
            throw new ArgumentException(
                $"SMILES scan found {scan.Bracket.Length} atoms but NCDK parsed {container.Atoms.Count} for '{smiles}'");

        RestoreDeclaredAromaticity(container, scan);

        var atomIndex = new Dictionary<IAtom, int>(container.Atoms.Count);
        for (int i = 0; i < container.Atoms.Count; i++) atomIndex[container.Atoms[i]] = i;

        var neighbours = NeighboursInRdkitOrder(scan, container.Atoms.Count);
        var chiral = ResolveChirality(container, atomIndex, neighbours, scan.Chiral);

        var radicals = new int[container.Atoms.Count];
        foreach (var singleElectron in container.SingleElectrons)
            if (atomIndex.TryGetValue(singleElectron.Atom, out int idx)) radicals[idx]++;

        var atoms = new List<MoleculeAtomData>(container.Atoms.Count);
        for (int i = 0; i < container.Atoms.Count; i++)
        {
            var atom = container.Atoms[i];
            int z = atom.AtomicNumber;
            int charge = atom.FormalCharge ?? 0;
            int totalH = atom.ImplicitHydrogenCount ?? 0;
            int explicitH = scan.Bracket[i] ? totalH : 0;

            double accumulated = explicitH;
            int single = 0, dbl = 0, tri = 0, aromatic = 0;
            foreach (var bond in atom.Bonds)
            {
                if (bond.IsAromatic)
                {
                    aromatic++;
                    accumulated += 1.5;
                }
                else
                {
                    switch (bond.Order)
                    {
                        case BondOrder.Single: single++; accumulated += 1; break;
                        case BondOrder.Double: dbl++; accumulated += 2; break;
                        case BondOrder.Triple: tri++; accumulated += 3; break;
                        case BondOrder.Quadruple: accumulated += 4; break;
                    }
                }
            }

            int defaultValence = AtomicProperties.GetDefaultValence(z, charge);
            for (int remaining = aromatic; remaining > 0 && accumulated > defaultValence; remaining--)
                accumulated -= 0.5;

            int explicitValence = (int)(accumulated + 0.1);

            // RDKit turns a hydrogen-less atom whose valence falls short of the element's default into a
            // radical, so `[131I]`, `[CH3]` and `[O]` carry 1, 1 and 2 single electrons. Elements without
            // a default valence fall back on the parity of their outer shell, and a deficit larger than a
            // half-filled shell is no radical at all (Al³⁺).
            if (radicals[i] == 0 && !atom.IsAromatic && (scan.Bracket[i] || totalH == 0))
            {
                int valence = AtomicProperties.GetRadicalValence(z, charge);
                int deficit = valence - explicitValence;
                radicals[i] = valence > 0
                    ? deficit is > 0 and <= 4 ? deficit : 0
                    : explicitValence == 0 ? (AtomicProperties.GetOuterElectrons(z) - charge) & 1 : 0;
            }

            // RDKit keeps implicit valence 0 for bracket atoms (it cannot know the writer's intent) and
            // derives it from the default valence for aromatic atoms; otherwise it equals the H count.
            int implicitValence;
            if (scan.Bracket[i]) implicitValence = 0;
            else if (atom.IsAromatic && defaultValence > 0) implicitValence = Math.Max(0, defaultValence - explicitValence);
            else implicitValence = totalH;

            int mask = 0;
            if (single > 0) mask |= MolGraphBuilder.BondSingle;
            if (dbl > 0) mask |= MolGraphBuilder.BondDouble;
            if (tri > 0) mask |= MolGraphBuilder.BondTriple;
            if (aromatic > 0) mask |= MolGraphBuilder.BondAromatic;

            atoms.Add(new MoleculeAtomData(
                Symbol: atom.Symbol,
                AtomicNumber: z,
                Mass: AtomicProperties.GetMass(z, scan.Isotope[i]),
                Degree: atom.Bonds.Count,
                TotalDegree: atom.Bonds.Count + totalH,
                ExplicitValence: explicitValence,
                ImplicitValence: implicitValence,
                Hybridization: Hybridization(z, atom, totalH, tri, dbl, single, radicals[i]),
                TotalHydrogens: totalH,
                FormalCharge: charge,
                RadicalElectrons: radicals[i],
                IsAromatic: atom.IsAromatic,
                IsInRing: atom.IsInRing,
                ChiralTag: chiral[i],
                BondTypeMask: mask));
        }

        var bonds = new List<MoleculeBondData>(container.Bonds.Count);
        foreach (var bond in container.Bonds)
        {
            bonds.Add(new MoleculeBondData(
                atomIndex[bond.Begin],
                atomIndex[bond.End],
                bond.IsAromatic ? 0 : bond.Order == BondOrder.Single ? 1 : bond.Order == BondOrder.Double ? 2 : bond.Order == BondOrder.Triple ? 3 : 0,
                bond.IsAromatic));
        }

        return new MoleculeData(smiles, atoms, bonds);
    }

    /// <summary>
    /// Groups <see cref="SmilesScanResult.Bonds"/> per atom, so each atom sees its neighbours in the
    /// order RDKit assigns them (chain and branch bonds first, ring closures last).
    /// </summary>
    private static List<int>[] NeighboursInRdkitOrder(SmilesScanResult scan, int atomCount)
    {
        var neighbours = new List<int>[atomCount];
        for (int i = 0; i < atomCount; i++) neighbours[i] = [];

        foreach (var (a, b) in scan.Bonds)
        {
            if ((uint)a >= (uint)atomCount || (uint)b >= (uint)atomCount) continue;
            neighbours[a].Add(b);
            neighbours[b].Add(a);
        }

        return neighbours;
    }

    /// <summary>
    /// Translates NCDK's tetrahedral sense into RDKit's <c>CHI_TETRAHEDRAL_CW</c>/<c>CCW</c>.
    /// <para>
    /// Both libraries store the sense relative to the order they list the ligands in, and those orders
    /// differ: NCDK sorts the ligands by atom index and drops the implicit hydrogen into that sort,
    /// while RDKit keeps the SMILES bond order with the hydrogen last. Flipping the tag whenever the
    /// permutation between the two orders is odd makes the two representations agree.
    /// </para>
    /// </summary>
    private static int[] ResolveChirality(
        IAtomContainer container,
        Dictionary<IAtom, int> atomIndex,
        List<int>[] neighbours,
        int[] chiral)
    {
        foreach (var stereo in container.StereoElements)
        {
            if (stereo is not TetrahedralChirality tetrahedral) continue;
            if (!atomIndex.TryGetValue(tetrahedral.ChiralAtom, out int idx)) continue;

            int tag = tetrahedral.Stereo switch
            {
                TetrahedralStereo.Clockwise => SmilesScan.ChiralCw,
                TetrahedralStereo.AntiClockwise => SmilesScan.ChiralCcw,
                _ => SmilesScan.ChiralUnspecified,
            };
            if (tag == SmilesScan.ChiralUnspecified)
            {
                chiral[idx] = tag;
                continue;
            }

            var ncdkOrder = new int[tetrahedral.Ligands.Count];
            for (int i = 0; i < ncdkOrder.Length; i++)
            {
                IAtom ligand = tetrahedral.Ligands[i];
                ncdkOrder[i] = ReferenceEquals(ligand, tetrahedral.ChiralAtom)
                    ? HydrogenSlot
                    : atomIndex.TryGetValue(ligand, out int ligandIndex) ? ligandIndex : UnknownSlot;
            }

            var rdkitOrder = new List<int>(neighbours[idx]);
            while (rdkitOrder.Count < ncdkOrder.Length) rdkitOrder.Add(HydrogenSlot);

            chiral[idx] = PermutationIsOdd(ncdkOrder, rdkitOrder)
                ? tag == SmilesScan.ChiralCw ? SmilesScan.ChiralCcw : SmilesScan.ChiralCw
                : tag;
        }

        return chiral;
    }

    /// <summary>True when reordering <paramref name="from"/> into <paramref name="to"/> needs an odd number of swaps.</summary>
    private static bool PermutationIsOdd(int[] from, IReadOnlyList<int> to)
    {
        if (from.Length != to.Count) return false;

        var position = new Dictionary<int, int>(from.Length);
        foreach (int key in from)
            if (key == UnknownSlot) return false;

        for (int i = 0; i < from.Length; i++)
            if (!position.TryAdd(from[i], i)) return false;

        var permutation = new int[to.Count];
        for (int i = 0; i < to.Count; i++)
            if (!position.TryGetValue(to[i], out permutation[i])) return false;

        int inversions = 0;
        for (int i = 0; i < permutation.Length; i++)
            for (int j = i + 1; j < permutation.Length; j++)
                if (permutation[i] > permutation[j]) inversions++;

        return inversions % 2 == 1;
    }

    /// <summary>
    /// Reproduces RDKit's <c>MolOps::setHybridization</c> heuristic:
    /// <list type="bullet">
    ///   <item>hydrogen is left unassigned (<c>S</c>), even when it is written as an explicit atom;</item>
    ///   <item>aromatic atoms are SP2, triple bonds give SP;</item>
    ///   <item>only C and N escalate to SP on a second double bond (O=S=O stays SP2);</item>
    ///   <item>hypervalent P/S that still carry a single bond are reported SP3 (sulfones, phosphates);</item>
    ///   <item>a lone N/O that can donate into an adjacent C/N pi system is SP2;</item>
    ///   <item>more than four electron domains escalate to SP3D/SP3D2 (PF5, SF5, SF6).</item>
    /// </list>
    /// </summary>
    private static AtomHybridization Hybridization(
        int z,
        IAtom atom,
        int hydrogens,
        int tripleBonds,
        int doubleBonds,
        int singleBonds,
        int radicals)
    {
        if (z == 1) return AtomHybridization.Unset;

        int charge = atom.FormalCharge ?? 0;
        if (atom.Bonds.Count == 0) return VseprHybridization(z, charge, 0, hydrogens, radicals);

        if (atom.IsAromatic) return AtomHybridization.Sp2;
        if (tripleBonds > 0) return AtomHybridization.Sp;

        if (doubleBonds > 0)
        {
            if (doubleBonds >= 2 && (z == 6 || z == 7)) return AtomHybridization.Sp;
            if ((z == 15 || z == 16) && singleBonds > 0) return AtomHybridization.Sp3;
            return AtomHybridization.Sp2;
        }

        // A three-coordinate boron has an empty p orbital, so RDKit calls it SP2.
        if (z == 5 && singleBonds == 3) return AtomHybridization.Sp2;

        var vsepr = VseprHybridization(z, charge, atom.Bonds.Count, hydrogens, radicals);
        if (vsepr is AtomHybridization.Sp3d or AtomHybridization.Sp3d2) return vsepr;

        return IsConjugated(z, atom, radicals) ? AtomHybridization.Sp2 : AtomHybridization.Sp3;
    }

    /// <summary>
    /// RDKit's fallback when bond orders carry no geometry: count the electron domains and read the
    /// hybridisation off them. A filled octet is four domains (O²⁻, F⁻, Cl⁻ are SP3), five domains are
    /// SP3D (PF5, SF4), six are SP3D2 (SF5, SF6), and a bare cation or noble gas ends up on <c>S</c>,
    /// which the featurizer encodes as "no hybridisation at all".
    /// </summary>
    private static AtomHybridization VseprHybridization(int z, int charge, int degree, int hydrogens, int radicals)
    {
        int electrons = AtomicProperties.GetOuterElectrons(z) - charge;
        int domains = degree + hydrogens + radicals;
        domains += Math.Max(0, electrons - domains + 1) / 2;

        return domains switch
        {
            2 => AtomHybridization.Sp,
            3 => AtomHybridization.Sp2,
            4 => AtomHybridization.Sp3,
            5 => AtomHybridization.Sp3d,
            6 => AtomHybridization.Sp3d2,
            _ => AtomHybridization.Unset,
        };
    }

    /// <summary>
    /// RDKit's sanitisation rejects neutral halogen oxides, so it moves each double-bonded oxygen's
    /// electron pair onto the oxygen: <c>OCl(=O)(=O)=O</c> is stored as <c>[O-][Cl+3]([O-])([O-])O</c>.
    /// The halogen gains one positive charge per bond, the oxygen one negative charge, and every such
    /// bond becomes single, which is what the reference featurizer sees.
    /// </summary>
    private static void SeparateHalogenOxideCharges(IAtomContainer container)
    {
        var oxidised = new List<(IBond Bond, IAtom Halogen, IAtom Oxygen)>();
        foreach (var bond in container.Bonds)
        {
            if (bond.IsAromatic || bond.Order != BondOrder.Double) continue;

            IAtom? halogen = IsOxidisingHalogen(bond.Begin) ? bond.Begin
                : IsOxidisingHalogen(bond.End) ? bond.End
                : null;
            if (halogen is null) continue;

            var oxygen = bond.GetOther(halogen);
            if (oxygen.AtomicNumber != 8 || (oxygen.FormalCharge ?? 0) != 0) continue;
            oxidised.Add((bond, halogen, oxygen));
        }

        // Two passes, because raising the halogen's charge per bond would make the later bonds of the
        // same halogen look like a different, already charged atom.
        foreach (var (bond, halogen, oxygen) in oxidised)
        {
            bond.Order = BondOrder.Single;
            halogen.FormalCharge = (halogen.FormalCharge ?? 0) + 1;
            oxygen.FormalCharge = (oxygen.FormalCharge ?? 0) - 1;
        }
    }

    private static bool IsOxidisingHalogen(IAtom atom) =>
        atom.AtomicNumber is 17 or 35 or 53 && (atom.FormalCharge ?? 0) >= 0;

    /// <summary>
    /// NCDK's Daylight model does not perceive every fused heteroaromatic system RDKit accepts - the
    /// pteridine diones among them - but the SMILES itself says which atoms are aromatic, and the NCDK
    /// parser keeps those flags. They are restored here the way RDKit does it: a ring is aromatic when
    /// it carries 4n+2 pi electrons, and rings that only reach that count together are aromatic as a
    /// fused system. The bond such a pair shares stays single, which is why RDKit reports a single bond
    /// between two aromatic atoms there, and why the ring in biphenylene stays aliphatic.
    /// </summary>
    private static void RestoreDeclaredAromaticity(IAtomContainer container, SmilesScanResult scan)
    {
        var index = new Dictionary<IAtom, int>(container.Atoms.Count);
        for (int i = 0; i < container.Atoms.Count; i++) index[container.Atoms[i]] = i;

        bool[] declared = scan.Aromatic;
        if (!declared.Any(flag => flag)) return;

        var rings = new List<int[]>();
        foreach (var path in Cycles.FindSSSR(container).GetPaths())
        {
            // NCDK repeats the first atom at the end of the path.
            var ring = path.Distinct().ToArray();
            if (ring.Length > 2 && ring.All(atom => declared[atom])) rings.Add(ring);
        }
        if (rings.Count == 0) return;

        var ringBonds = rings.Select(ring => RingBonds(ring).ToHashSet()).ToList();
        var aromaticRing = new bool[rings.Count];
        for (int r = 0; r < rings.Count; r++)
            aromaticRing[r] = PiElectrons(container, rings[r], declared, index) % 4 == 2;

        var aromaticAtoms = new bool[declared.Length];
        var aromaticBonds = new HashSet<(int A, int B)>();

        void Mark(IEnumerable<int> atoms, IEnumerable<(int A, int B)> bonds)
        {
            foreach (int atom in atoms) aromaticAtoms[atom] = true;
            foreach (var bond in bonds) aromaticBonds.Add(bond);
        }

        for (int r = 0; r < rings.Count; r++)
            if (aromaticRing[r]) Mark(rings[r], ringBonds[r]);

        // Rings that fail on their own are retried as the fused system they belong to, and only the
        // bonds a single ring of that system owns are marked.
        var group = Enumerable.Range(0, rings.Count).ToArray();
        int Find(int r) => group[r] == r ? r : group[r] = Find(group[r]);
        for (int a = 0; a < rings.Count; a++)
            for (int b = a + 1; b < rings.Count; b++)
                if (!aromaticRing[a] && !aromaticRing[b] && ringBonds[a].Overlaps(ringBonds[b]))
                    group[Find(a)] = Find(b);

        foreach (var fused in Enumerable.Range(0, rings.Count)
                     .Where(r => !aromaticRing[r])
                     .GroupBy(Find)
                     .Select(members => members.ToArray())
                     .Where(fused => fused.Length > 1))
        {
            var atoms = fused.SelectMany(r => rings[r]).Distinct().ToArray();
            if (PiElectrons(container, atoms, declared, index) % 4 != 2) continue;

            Mark(atoms, fused.SelectMany(r => ringBonds[r]).Where(bond => fused.Count(r => ringBonds[r].Contains(bond)) == 1));
        }

        for (int i = 0; i < aromaticAtoms.Length; i++)
            if (aromaticAtoms[i]) container.Atoms[i].IsAromatic = true;

        foreach (var bond in container.Bonds)
            if (index.TryGetValue(bond.Begin, out int a) && index.TryGetValue(bond.End, out int b))
                if (aromaticBonds.Contains((Math.Min(a, b), Math.Max(a, b))))
                    bond.IsAromatic = true;
    }

    private static (int A, int B)[] RingBonds(int[] ring) =>
        Enumerable.Range(0, ring.Length)
            .Select(i => (A: Math.Min(ring[i], ring[(i + 1) % ring.Length]), B: Math.Max(ring[i], ring[(i + 1) % ring.Length])))
            .ToArray();

    private static int PiElectrons(
        IAtomContainer container,
        IEnumerable<int> atoms,
        bool[] declared,
        Dictionary<IAtom, int> index)
    {
        int total = 0;
        foreach (int atom in atoms) total += PiElectrons(container.Atoms[atom], declared, index);
        return total;
    }

    /// <summary>
    /// Electrons an atom contributes to its ring's pi system: an exocyclic double bond consumes the p
    /// electron, otherwise the electrons left after the sigma bonds fill one lone pair and the rest goes
    /// into the p orbital.
    /// </summary>
    private static int PiElectrons(IAtom atom, bool[] declared, Dictionary<IAtom, int> index)
    {
        int hydrogens = atom.ImplicitHydrogenCount ?? 0;
        int sigma = atom.Bonds.Count + hydrogens;

        foreach (var bond in atom.Bonds)
        {
            if (bond.IsAromatic || bond.Order != BondOrder.Double) continue;
            if (!index.TryGetValue(bond.GetOther(atom), out int other) || !declared[other]) return 0;
        }

        int left = AtomicProperties.GetOuterElectrons(atom.AtomicNumber) - (atom.FormalCharge ?? 0) - sigma;
        if (left < 0) return 0;
        return Math.Min(2, left <= 2 ? left : left - 2);
    }

    /// <summary>
    /// RDKit reports SP2 for N/O that donate a lone pair into an adjacent C/N pi system, and for the
    /// heavier chalcogen radicals (S, Se, Te) whose missing electron leaves them a p orbital next to an
    /// aromatic ring. Their closed-shell forms stay SP3.
    /// </summary>
    private static bool IsConjugated(int z, IAtom atom, int radicals)
    {
        bool lonePairDonor = z is 7 or 8;
        bool radicalDonor = radicals > 0 && z is 16 or 34 or 52;
        if (!lonePairDonor && !radicalDonor) return false;

        foreach (var bond in atom.Bonds)
        {
            var neighbour = bond.GetOther(atom);
            if (radicalDonor)
            {
                if (neighbour.IsAromatic) return true;
                continue;
            }

            int nz = neighbour.AtomicNumber;
            if (nz != 6 && nz != 7) continue;
            if (neighbour.IsAromatic) return true;
            foreach (var other in neighbour.Bonds)
                if (!other.IsAromatic && other.Order != BondOrder.Single) return true;
        }

        return false;
    }
}

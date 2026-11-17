namespace Flare.Chem;

/// <summary>
/// Per-element constants that reproduce RDKit's <c>PeriodicTable</c> exactly, so the C# port yields
/// bit-identical feature vectors. Values were extracted from RDKit 2024.09 (RDKit2DotNetStandard 1.0.42).
/// </summary>
public static class AtomicProperties
{
    /// <summary>RDKit average atomic weights indexed by atomic number (0 = dummy atom).</summary>
    private static readonly double[] AverageMass =
    [
        0, 1.008, 4.003, 6.941, 9.012, 10.812, 12.011, 14.007, 15.999, 18.998, 20.18, 22.99, 24.305,
        26.982, 28.086, 30.974, 32.067, 35.453, 39.948, 39.098, 40.078, 44.956, 47.867, 50.944,
        51.996, 54.938, 55.845, 58.933, 58.693, 63.546, 65.39, 69.723, 72.61, 74.922, 78.96, 79.904,
        83.8, 85.468, 87.62, 88.906, 91.224, 92.906, 95.94, 98, 101.07, 102.906, 106.42, 107.868,
        112.412, 114.818, 118.711, 121.76, 127.6, 126.904, 131.29, 132.905, 137.328, 138.906, 140.116,
        140.908, 144.24, 145, 150.36, 151.964, 157.25, 158.925, 162.5, 164.93, 167.26, 168.934,
        173.04, 174.967, 178.49, 180.948, 183.84, 186.207, 190.23, 192.217, 195.078, 196.967, 200.59,
        204.383, 207.2, 208.98, 209, 210, 222, 223, 226, 227, 232.038, 231.036, 238.029, 237, 244,
        243, 247, 247, 251, 252, 257, 258, 259, 262, 267, 268, 269, 270, 269, 278, 281, 281, 285,
        284, 289, 288, 293, 292, 294,
    ];

    /// <summary>RDKit default valence used by <c>calcExplicitValence</c> (before charge adjustment).</summary>
    private static readonly Dictionary<int, int> DefaultValences = new()
    {
        [1] = 1,   // H
        [5] = 3,   // B
        [6] = 4,   // C
        [7] = 3,   // N
        [8] = 2,   // O
        [9] = 1,   // F
        [13] = 3,  // Al
        [14] = 4,  // Si
        [15] = 3,  // P
        [16] = 2,  // S
        [17] = 1,  // Cl
        [31] = 3,  // Ga
        [32] = 4,  // Ge
        [33] = 3,  // As
        [34] = 2,  // Se
        [35] = 1,  // Br
        [49] = 3,  // In
        [50] = 4,  // Sn
        [51] = 3,  // Sb
        [52] = 2,  // Te
        [53] = 1,  // I
    };

    /// <summary>
    /// Exact monoisotopic masses RDKit uses once an isotope label is present, keyed by
    /// <c>(atomicNumber &lt;&lt; 8) | massNumber</c>. Covers the isotopes that occur in practice; any
    /// other isotope falls back to the average weight, exactly like an unlabelled atom.
    /// </summary>
    private static readonly Dictionary<int, double> IsotopeMass = new()
    {
        [(1 << 8) | 1] = 1.007825032,
        [(1 << 8) | 2] = 2.014101778,
        [(1 << 8) | 3] = 3.016049278,
        [(5 << 8) | 10] = 10.012937,
        [(5 << 8) | 11] = 11.0093054,
        [(6 << 8) | 12] = 12.0,
        [(6 << 8) | 13] = 13.00335484,
        [(6 << 8) | 14] = 14.00324199,
        [(7 << 8) | 14] = 14.003074,
        [(7 << 8) | 15] = 15.0001089,
        [(8 << 8) | 16] = 15.99491462,
        [(8 << 8) | 17] = 16.9991317,
        [(8 << 8) | 18] = 17.999161,
        [(9 << 8) | 19] = 18.99840322,
        [(14 << 8) | 28] = 27.97692653,
        [(14 << 8) | 29] = 28.9764947,
        [(14 << 8) | 30] = 29.97377017,
        [(15 << 8) | 31] = 30.97376163,
        [(15 << 8) | 32] = 31.97390727,
        [(16 << 8) | 32] = 31.972071,
        [(16 << 8) | 33] = 32.97145876,
        [(16 << 8) | 34] = 33.9678669,
        [(16 << 8) | 36] = 35.96708076,
        [(17 << 8) | 35] = 34.96885268,
        [(17 << 8) | 37] = 36.96590259,
        [(34 << 8) | 74] = 73.9224764,
        [(34 << 8) | 76] = 75.9192136,
        [(34 << 8) | 77] = 76.919914,
        [(34 << 8) | 78] = 77.9173091,
        [(34 << 8) | 80] = 79.9165213,
        [(34 << 8) | 82] = 81.9166994,
        [(35 << 8) | 79] = 78.9183371,
        [(35 << 8) | 81] = 80.9162906,
        [(53 << 8) | 127] = 126.904473,
        [(6 << 8) | 11] = 11.011434,
        [(6 << 8) | 14] = 14.003242,
        [(7 << 8) | 13] = 13.005739,
        [(8 << 8) | 15] = 15.003066,
        [(9 << 8) | 18] = 18.000938,
        [(11 << 8) | 22] = 21.994436,
        [(12 << 8) | 25] = 24.985837,
        [(15 << 8) | 32] = 31.973907,
        [(16 << 8) | 35] = 34.969032,
        [(26 << 8) | 55] = 54.938293,
        [(43 << 8) | 99] = 98.906255,
        [(53 << 8) | 123] = 122.905589,
        [(53 << 8) | 125] = 124.904630,
        [(53 << 8) | 131] = 130.906125,
    };

    /// <summary>RDKit <c>atom.getMass()</c>; 0 for unknown atomic numbers.</summary>
    public static double GetMass(int atomicNumber) =>
        atomicNumber >= 0 && atomicNumber < AverageMass.Length ? AverageMass[atomicNumber] : 0.0;

    /// <summary>
    /// RDKit <c>atom.getMass()</c> for an atom carrying an isotope label: the exact isotope mass when
    /// it is tabulated, otherwise the average weight.
    /// </summary>
    public static double GetMass(int atomicNumber, int massNumber) =>
        massNumber > 0 && IsotopeMass.TryGetValue((atomicNumber << 8) | massNumber, out double exact)
            ? exact
            : GetMass(atomicNumber);

    /// <summary>Number of outer-shell electrons, i.e. RDKit's <c>PeriodicTable::getNOuterElecs</c>.</summary>
    public static int GetOuterElectrons(int atomicNumber) => atomicNumber switch
    {
        < 1 => 0,
        <= 2 => atomicNumber,
        <= 10 => atomicNumber - 2,
        <= 18 => atomicNumber - 10,
        <= 36 => MainGroupElectrons(atomicNumber, 19),
        <= 54 => MainGroupElectrons(atomicNumber, 37),
        <= 86 => MainGroupElectrons(atomicNumber, 55),
        <= 118 => MainGroupElectrons(atomicNumber, 87),
        _ => 0,
    };

    /// <summary>
    /// Outer electrons of a period 4+ element from its group: groups 1 and 2 keep their s electrons,
    /// groups 13-18 lose the ten d electrons, group 12 contributes its two s electrons only, and the
    /// remaining d-block elements keep the shell offset RDKit reports (Fe → 8).
    /// </summary>
    private static int MainGroupElectrons(int atomicNumber, int periodStart)
    {
        int group = atomicNumber - periodStart + 1;
        if (group <= 2) return group;
        if (group == 12) return 2;
        if (group >= 13) return group - 10;
        return atomicNumber - (periodStart - 1);
    }

    /// <summary>
    /// RDKit's <c>PeriodicTable::getDefaultValence</c> adjusted for formal charge
    /// (N+ → 4, O+ → 3, O− → 1, …). Unknown elements fall back to 0, i.e. never reduce aromatic bonds.
    /// </summary>
    public static int GetDefaultValence(int atomicNumber, int formalCharge)
    {
        if (!DefaultValences.TryGetValue(atomicNumber, out int valence)) return 0;
        int adjusted = valence + formalCharge;
        return adjusted < 1 ? 1 : adjusted;
    }

    /// <summary>
    /// RDKit's valence deficit that turns a hydrogen-less atom into a radical: the tabulated default
    /// valence shifted by the formal charge, or 0 for elements without one (metals, noble gases).
    /// Unlike <see cref="GetDefaultValence"/> this is not clamped to 1, so a doubly reduced atom has
    /// no deficit left.
    /// </summary>
    public static int GetRadicalValence(int atomicNumber, int formalCharge) =>
        DefaultValences.TryGetValue(atomicNumber, out int valence) ? Math.Max(0, valence + formalCharge) : 0;
}

using System.Text.Json;
using Flare.Chem;
using Flare.Rdkit;

namespace Flare.Onnx.Tests;

/// <summary>
/// Differential tests: the RDKit-free NCDK port must produce exactly the same 78-feature atom vectors,
/// edge list and molecular formula as the RDKit reference implementation.
/// </summary>
public sealed class NcdkDifferentialTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly JsonElement Reference = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(RepoRoot, "tests", "Fixtures", "reference.json"))).RootElement;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "flare_onnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root with flare_onnx/ not found");
    }

    private static readonly string[] FeatureNames = BuildFeatureNames();

    private static string[] BuildFeatureNames()
    {
        var names = new List<string> { "mass" };
        names.AddRange(ChemUtils.FeatureElements.Select(e => "atom:" + e));
        names.Add("atom:?");
        names.AddRange(["bond:SINGLE", "bond:DOUBLE", "bond:TRIPLE", "bond:AROMATIC"]);
        names.AddRange(Enumerable.Range(0, 11).Select(v => $"degree={v}"));
        names.AddRange(Enumerable.Range(0, 6).Select(v => $"totalDegree={v}"));
        names.AddRange(Enumerable.Range(1, 6).Select(v => $"explicitValence={v}"));
        names.AddRange(Enumerable.Range(0, 7).Select(v => $"implicitValence={v}"));
        names.AddRange(["hyb:SP", "hyb:SP2", "hyb:SP3", "hyb:SP3D", "hyb:SP3D2"]);
        names.AddRange(Enumerable.Range(0, 5).Select(v => $"totalHs={v}"));
        names.AddRange(Enumerable.Range(-2, 5).Select(v => $"charge={v}"));
        names.AddRange(Enumerable.Range(0, 5).Select(v => $"radicals={v}"));
        names.AddRange(["aromatic=0", "aromatic=1"]);
        names.AddRange(["inRing=0", "inRing=1"]);
        names.AddRange(["chiral:UNSPECIFIED", "chiral:CW", "chiral:CCW", "chiral:OTHER"]);
        return names.ToArray();
    }

    /// <summary>Additional coverage beyond the 40 reference molecules: resonance, charge, stereo, rings.</summary>
    private static readonly string[] ExtraSmiles =
    [
        "CCO", "CCOC", "CC(=O)O", "CC(=O)N", "NC(=O)N", "CC(=O)OC", "CC(=O)Nc1ccc(O)cc1",
        "Nc1ccccc1", "N(C)(C)c1ccccc1", "Oc1ccccc1", "COc1ccccc1", "C=COC", "OCC(O)C(O)C(O)C(O)CO",
        "c1ccccc1", "C1=CC=CC=C1", "C1CCCCC1", "c1ccc2ccccc2c1", "c1ccc2c(c1)ccc1ccccc12",
        "c1ccc(cc1)-c1ccccc1", "c1ccncc1", "c1cc[nH]c1", "c1ccoc1", "c1ccsc1",
        "c1ccc2[nH]ccc2c1", "c1ccc2c(c1)[nH]c1ccccc12", "CN1C=NC2=C1C(=O)N(C)C(=O)N2C",
        "C#N", "CC#N", "N#CC#N", "O=C=O", "N=C=O", "C=C=C", "S=C=N", "[C-]#[O+]",
        "CS(=O)C", "CS(=O)(=O)C", "CP(=O)(O)O", "OP(=O)(O)O", "P(=O)(O)(O)O",
        "O=S(=O)(N)c1ccccc1", "O=S(=O)([O-])c1ccccc1", "O=[N+]([O-])c1ccccc1",
        "[O-][N+](=O)c1ccccc1", "CC(=O)[O-]", "C[N+](C)(C)C", "[NH4+]", "C[N+](C)(C)CC[O-]",
        "[O-]C(=O)CC[NH3+]", "[Na+].[Cl-]", "N=C(N)N", "O=CNC", "CC(C)(C)C",
        "N[C@@H](C)C(=O)O", "N[C@H](C)C(=O)O", "F/C=C/F", "F/C=C\\F",
        "OC[C@H]1O[C@@H](O)[C@H](O)[C@@H](O)[C@@H]1O", "c1ccc(cc1)C(=O)[O-]",
        "ClCCl", "BrCBr", "ICI", "FC(F)(F)F", "C[Si](C)(C)C", "C[As](C)C", "B(O)(O)O",
        "c1ccc(cc1)[Si](C)(C)C", "Oc1ccc(cc1)C#N", "CC(=O)OC1=CC=CC=C1", "[13CH4]",
        "C1=CCOC1", "O=C1NC(=O)C(=O)N1", "c1ccc(cc1)N=Nc1ccccc1", "CC(C)C(=O)O",
        "OC(=O)CCCCC(=O)O", "NCCc1ccc(O)c(O)c1", "c1ccc(cc1)C=O", "CC=O",
    ];

    /// <summary>Hard cases: poly-stereo natural products, isotopes, hypervalent centres, azides, radicals.</summary>
    private static readonly string[] ChallengeSmiles =
    [
        // poly-stereocentre natural products / drugs
        "CC1(C)S[C@@H]2[C@H](NC(=O)Cc3ccccc3)C(=O)N2[C@H]1C(=O)O",
        "CC1(C)S[C@@H]2[C@H](NC(=O)[C@H](N)c3ccc(O)cc3)C(=O)N2[C@H]1C(=O)O",
        "CC(C)CCC[C@@H](C)[C@H]1CC[C@H]2[C@@H]3CC=C4C[C@@H](O)CC[C@]4(C)[C@H]3CC[C@]12C",
        "CN1CC[C@]23c4c5ccc(O)c4O[C@H]2[C@@H](O)C=C[C@H]3[C@H]1C5",
        "COc1ccc2c3c1O[C@H]1[C@@H](O)C=C[C@@H]4[C@H](C2)N(C)CC[C@@]134",
        "CC(=O)[C@]1(CC[C@@H]2[C@@]1(CC(=O)[C@H]1[C@H]2CCC2=CC(=O)CC[C@]12C)C)C",
        "CC12CCC3C(CC=C4CC(O)CCC34C)C1CCC2O",
        "Nc1ncnc2c1ncn2[C@@H]1O[C@H](COP(=O)(O)OP(=O)(O)OP(=O)(O)O)[C@@H](O)[C@H]1O",
        "Cc1cc2nc3c(=O)[nH]c(=O)nc-3n(C[C@H](O)[C@H](O)[C@H](O)CO)c2cc1C",
        "CN1CCC[C@H]1c1cccnc1",
        "C[C@@H](NC)[C@H](O)c1ccccc1",
        "CN(C)C[C@H]1CCCC[C@@]1(O)c1cccc(OC)c1",
        "C[C@@H](O)[C@H](O)C",
        "N[C@@H](Cc1ccccc1)C(=O)O",
        "CC(C)[C@@H](N)C(=O)O",
        "C1[C@H]2CC[C@@H]1C2",
        "OC[C@H](O)[C@H]1OC(=O)C(O)=C1O",
        // large drug-like molecules
        "CC(C)Cc1ccc(cc1)C(C)C(=O)O",
        "CC(=O)CC(c1ccccc1)c1c(O)c2ccccc2oc1=O",
        "CN1C(=O)CN=C(c2ccccc2)c2ccc(Cl)cc21",
        "CNCCC(c1ccccc1)Oc1ccc(cc1)C(F)(F)F",
        "Cc1ccc(NC(=O)c2ccc(CN3CCN(C)CC3)cc2)cc1Nc1nccc(n1)c1cccnc1",
        "CN(C)C(=N)NC(=N)N",
        "CCN(CC)CC(=O)Nc1c(C)cccc1C",
        "CCOC(=O)c1ccc(N)cc1",
        "Cc1cc(NS(=O)(=O)c2ccc(N)cc2)no1",
        "COc1ccc2[nH]c(S(=O)Cc3ncc(C)c(OC)c3C)nc2c1",
        "Nc1nc2ncc(CNc3ccc(cc3)C(=O)NC(CCC(=O)O)C(=O)O)nc2c(=O)[nH]1",
        "CCCC1=NN(C)C2=C1N=C(NC(=O)c1cc(S(=O)(=O)N3CCN(C)CC3)ccc1OCC)NC2=O",
        "CNC1(c2ccccc2Cl)CCCCC1=O",
        // isotopes
        "[2H]O[2H]", "[13CH4]", "C[13CH3]", "[15NH3]", "[18OH2]", "[131I]", "[3H]C",
        "[13C](=O)=O", "[2H]C([2H])([2H])[2H]",
        // hypervalent / unusual valence
        "FS(F)(F)(F)(F)F", "FP(F)(F)(F)F", "OCl(=O)(=O)=O", "OI(=O)(=O)=O",
        "CS(=O)(=O)C", "OS(=O)(=O)O", "[Se]=C=[Se]", "c1ccc([Te])cc1",
        // azides / cumulated heteroatoms
        "[N-]=[N+]=NC", "[N-]=[N+]=Nc1ccccc1", "CN=C=O", "O=C=Nc1ccccc1", "N#CC#N",
        "O=C=NC(=O)NC=O",
        // charged aromatics and N-oxides
        "[O-][n+]1ccccc1", "c1cc[nH+]cc1", "c1cc[n+]([O-])cc1", "[nH]1cccc1", "c1ccccc1[O-]",
        "[O-]S(=O)(=O)c1ccc(N)cc1", "[NH3+]CC(=O)[O-]", "[O-]P(=O)([O-])[O-]",
        // radicals / bare atoms; isolated multi-charged transition-metal ions ([Cu+2]) are left out
        // because RDKit reads their hybridisation off an internal table rather than off the valence
        "[CH3]", "[CH2]", "[C]", "[O]", "[N]", "[NH]", "[Si]", "[Fe]", "[Fe+2]", "[Zn+2]", "[Al+3]",
        "[Mg+2]", "[Na+]", "[Cl-]", "[S-2]", "[P-3]", "[He]", "[Ar]", "[S]c1ccccc1", "[Se]CC",
        // fused / bridged / spiro rings
        "C1CC2CCC1CC2", "C1CCC2CCCCC2C1", "c1ccc2cc3ccccc3cc2c1", "C1=CC2=CC=CC=C2C=C1",
        "C1CC2(C1)CCCC2", "C1CC2CCC3CCCC3C2C1", "c1ccc2c(c1)-c1ccccc1-2",
        // boron / silicon / phosphorus organics
        "OB(O)c1ccccc1", "B(O)(O)O", "C[Si](C)(C)C", "c1ccc(cc1)[Si](C)(C)C",
        "CP(C)C", "OP(O)O", "C[SiH3]",
    ];

    private static IEnumerable<string> ReferenceSmiles =>
        Reference.GetProperty("molecules").EnumerateArray().Select(m => m.GetProperty("smiles").GetString()!);

    private static List<string> Compare(string smiles)
    {
        var diffs = new List<string>();
        var rdkit = MolFeaturizer.FromSmiles(smiles);
        var ncdk = new NcdkMoleculeFeaturizer().FromSmiles(smiles);

        if (rdkit.NumAtoms != ncdk.NumAtoms)
        {
            diffs.Add($"{smiles}: atom count rdkit={rdkit.NumAtoms} ncdk={ncdk.NumAtoms}");
            return diffs;
        }

        for (int atom = 0; atom < rdkit.NumAtoms; atom++)
        {
            for (int f = 0; f < MolFeaturizer.NumFeatures; f++)
            {
                int k = atom * MolFeaturizer.NumFeatures + f;
                if (Math.Abs(rdkit.AtomFeatures[k] - ncdk.AtomFeatures[k]) > 1e-6f)
                    diffs.Add($"{smiles}: atom {atom} [{FeatureNames[f]}] rdkit={rdkit.AtomFeatures[k]} ncdk={ncdk.AtomFeatures[k]}");
            }
        }

        // RDKit appends ring-closure bonds after the sequentially parsed ones, NCDK interleaves them.
        // Bond order does not change the graph, so the edge list is compared as a multiset.
        var rdkitEdges = rdkit.Edges.OrderBy(e => e.Src).ThenBy(e => e.Dst).ToArray();
        var ncdkEdges = ncdk.Edges.OrderBy(e => e.Src).ThenBy(e => e.Dst).ToArray();
        if (!rdkitEdges.SequenceEqual(ncdkEdges))
            diffs.Add($"{smiles}: edge lists differ");

        return diffs;
    }

    [Fact]
    public void Featurizer_MatchesRdkit_OnReferenceMolecules()
    {
        var diffs = ReferenceSmiles.SelectMany(Compare).ToList();
        Assert.True(diffs.Count == 0, $"{diffs.Count} mismatch(es):\n" + string.Join("\n", diffs.Take(60)));
    }

    [Fact]
    public void Featurizer_MatchesRdkit_OnExtendedCorpus()
    {
        var diffs = ExtraSmiles.SelectMany(Compare).ToList();
        Assert.True(diffs.Count == 0, $"{diffs.Count} mismatch(es):\n" + string.Join("\n", diffs.Take(60)));
    }

    private static bool RdkitAccepts(string smiles)
    {
        try { MolFeaturizer.FromSmiles(smiles); return true; }
        catch { return false; }
    }

    [Fact]
    public void Corpus_IsValidForRdkit()
    {
        var rejected = ChallengeSmiles.Concat(ExtraSmiles).Where(s => !RdkitAccepts(s)).ToArray();
        Assert.True(rejected.Length == 0, "RDKit rejects: " + string.Join(" | ", rejected));
    }

    [Fact]
    public void Featurizer_MatchesRdkit_OnChallengeCorpus()
    {
        var diffs = ChallengeSmiles.SelectMany(Compare).ToList();
        Assert.True(diffs.Count == 0, $"{diffs.Count} mismatch(es):\n" + string.Join("\n", diffs.Take(60)));
    }

    [Fact]
    public void Formula_MatchesRdkit()
    {
        var featurizer = new NcdkMoleculeFeaturizer();
        var diffs = new List<string>();
        foreach (var smiles in ReferenceSmiles.Concat(ExtraSmiles).Concat(ChallengeSmiles))
        {
            string expected = MolFeaturizer.CalcFormula(smiles);
            string actual = featurizer.CalculateFormula(smiles);
            if (expected != actual) diffs.Add($"{smiles}: rdkit={expected} ncdk={actual}");
        }
        Assert.True(diffs.Count == 0, string.Join("\n", diffs));
    }
}


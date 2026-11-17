namespace Flare.Chem;

/// <summary>Per-atom and per-bond data recovered from the SMILES text itself.</summary>
/// <param name="Bracket">True when the atom was written inside brackets.</param>
/// <param name="Isotope">Mass number from the bracket prefix, 0 when absent.</param>
/// <param name="Aromatic">True when the atom was written in lower case, i.e. declared aromatic.</param>
/// <param name="Chiral">
/// <see cref="SmilesScan.ChiralOther"/> for non-tetrahedral chirality classes, otherwise
/// <see cref="SmilesScan.ChiralUnspecified"/>; the tetrahedral sense is filled in from NCDK.
/// </param>
/// <param name="Bonds">
/// Bonds in RDKit's bond-index order: the chain and branch bonds in parse order, followed by the
/// ring-closure bonds ordered by ring number. RDKit creates the ring closures last, NCDK interleaves
/// them, and the difference changes the reference frame of a tetrahedral stereo centre.
/// </param>
public sealed record SmilesScanResult(
    bool[] Bracket,
    int[] Isotope,
    bool[] Aromatic,
    int[] Chiral,
    IReadOnlyList<(int A, int B)> Bonds);

/// <summary>
/// Walks a SMILES string to recover three pieces of information RDKit keeps but NCDK's
/// <c>SmilesParser</c> discards or orders differently:
/// <list type="bullet">
///   <item>whether the atom was written in brackets (RDKit derives implicit valence from that),</item>
///   <item>the isotope label (RDKit reports the exact isotope mass for it), and</item>
///   <item>the order RDKit assigns to the bonds, which fixes the reference frame of <c>@</c>/<c>@@</c>.</item>
/// </list>
/// Atom order produced here is the parse order, which is the same order NCDK and RDKit use.
/// </summary>
public static class SmilesScan
{
    public const int ChiralUnspecified = 0;
    public const int ChiralCw = 1;
    public const int ChiralCcw = 2;
    public const int ChiralOther = 3;

    private static readonly HashSet<string> TwoLetterElements = new(StringComparer.Ordinal)
    {
        "He", "Li", "Be", "Ne", "Na", "Mg", "Al", "Si", "Cl", "Ar", "Ca", "Sc", "Ti", "Cr", "Mn", "Fe",
        "Co", "Ni", "Cu", "Zn", "Ga", "Ge", "As", "Se", "Br", "Kr", "Rb", "Sr", "Zr", "Nb", "Mo", "Tc",
        "Ru", "Rh", "Pd", "Ag", "Cd", "In", "Sn", "Sb", "Te", "Xe", "Cs", "Ba", "La", "Ce", "Pr", "Nd",
        "Pm", "Sm", "Eu", "Gd", "Tb", "Dy", "Ho", "Er", "Tm", "Yb", "Lu", "Hf", "Ta", "Re", "Os", "Ir",
        "Pt", "Au", "Hg", "Tl", "Pb", "Bi", "Po", "At", "Rn", "Fr", "Ra", "Ac", "Th", "Pa", "Np", "Pu",
        "Am", "Cm", "Bk", "Cf", "Es", "Fm", "Md", "No", "Lr", "Rf", "Db", "Sg", "Bh", "Hs", "Mt", "Ds",
        "Rg", "Cn", "Nh", "Fl", "Mc", "Lv", "Ts", "Og",
    };

    public static SmilesScanResult Walk(string smiles)
    {
        var bracket = new List<bool>();
        var isotope = new List<int>();
        var aromatic = new List<bool>();
        var chiral = new List<int>();
        var bonds = new List<(int A, int B)>();
        var ringClosures = new List<(int Ring, int A, int B)>();
        var ringOpeners = new Dictionary<int, int>();
        var branches = new Stack<int>();
        int prev = -1;
        int i = 0;

        void AddAtom(bool inBrackets, int massNumber, bool isAromatic, int chiralTag)
        {
            int index = bracket.Count;
            bracket.Add(inBrackets);
            isotope.Add(massNumber);
            aromatic.Add(isAromatic);
            chiral.Add(chiralTag);
            if (prev >= 0) bonds.Add((prev, index));
            prev = index;
        }

        void CloseRing(int ring)
        {
            if (prev < 0) return;
            if (ringOpeners.Remove(ring, out int opener)) ringClosures.Add((ring, opener, prev));
            else ringOpeners[ring] = prev;
        }

        while (i < smiles.Length)
        {
            char c = smiles[i];
            switch (c)
            {
                case '[':
                {
                    int end = smiles.IndexOf(']', i + 1);
                    if (end < 0) throw new ArgumentException($"Unterminated bracket atom in '{smiles}'");
                    string inner = smiles[(i + 1)..end];
                    AddAtom(true, ParseIsotope(inner), IsLowercaseElement(inner), ParseChirality(inner));
                    i = end + 1;
                    break;
                }
                case '(':
                    branches.Push(prev);
                    i++;
                    break;
                case ')':
                    if (branches.Count > 0) prev = branches.Pop();
                    i++;
                    break;
                case '.':
                    prev = -1;
                    i++;
                    break;
                case '-' or '=' or '#' or '$' or ':' or '/' or '\\' or '~':
                    i++;
                    break;
                case '%':
                    if (i + 2 >= smiles.Length) throw new ArgumentException($"Truncated ring bond in '{smiles}'");
                    CloseRing(int.Parse(smiles.AsSpan(i + 1, 2)));
                    i += 3;
                    break;
                default:
                    if (char.IsDigit(c))
                    {
                        CloseRing(c - '0');
                        i++;
                    }
                    else if (c == '*')
                    {
                        AddAtom(false, 0, false, ChiralUnspecified);
                        i++;
                    }
                    else if (char.IsUpper(c))
                    {
                        i += i + 1 < smiles.Length && TwoLetterElements.Contains(smiles.Substring(i, 2)) ? 2 : 1;
                        AddAtom(false, 0, false, ChiralUnspecified);
                    }
                    else if (char.IsLower(c))
                    {
                        AddAtom(false, 0, true, ChiralUnspecified);
                        i++;
                    }
                    else
                    {
                        i++;
                    }
                    break;
            }
        }

        // RDKit appends the ring closures after every other bond, ordered by ring number; the sort is
        // stable so closures sharing a ring number keep the order they were closed in.
        foreach (var closure in ringClosures.OrderBy(x => x.Ring)) bonds.Add((closure.A, closure.B));

        return new SmilesScanResult(bracket.ToArray(), isotope.ToArray(), aromatic.ToArray(), chiral.ToArray(), bonds);
    }

    /// <summary>True when the bracket body names its element in lower case, e.g. <c>[nH]</c> or <c>[se]</c>.</summary>
    private static bool IsLowercaseElement(string inner)
    {
        foreach (char c in inner)
            if (char.IsLetter(c)) return char.IsLower(c);
        return false;
    }

    /// <summary>Returns one flag per parsed atom: true when the atom was written inside brackets.</summary>
    public static bool[] BracketAtoms(string smiles) => Walk(smiles).Bracket;

    /// <summary>
    /// Returns <see cref="ChiralOther"/> for atoms written with a non-tetrahedral chirality class and
    /// <see cref="ChiralUnspecified"/> for everything else (tetrahedral sense comes from NCDK).
    /// </summary>
    public static int[] ChiralTags(string smiles) => Walk(smiles).Chiral;

    /// <summary>Single pass returning the two per-atom flags.</summary>
    public static (bool[] Bracket, int[] Chiral) Scan(string smiles)
    {
        var result = Walk(smiles);
        return (result.Bracket, result.Chiral);
    }

    private static int ParseIsotope(string inner)
    {
        int end = 0;
        while (end < inner.Length && char.IsDigit(inner[end])) end++;
        return end == 0 ? 0 : int.Parse(inner.AsSpan(0, end));
    }

    private static int ParseChirality(string inner)
    {
        int at = inner.IndexOf('@');
        if (at < 0) return ChiralUnspecified;

        string tail = inner[(at + 1)..];

        // "@", "@@" and "@TH1"/"@TH2" are all tetrahedral; the rest (@AL, @SP, @TB, @OH) are not.
        if (tail.Length == 0 || tail[0] == '@') return ChiralUnspecified;
        if (tail.StartsWith("TH", StringComparison.Ordinal)) return ChiralUnspecified;

        return ChiralOther;
    }
}

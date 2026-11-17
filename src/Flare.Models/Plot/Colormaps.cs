namespace Flare.Models.Plot;

public enum ColormapType
{
    Viridis,
    Plasma,
    Inferno,
    Magma,
    Turbo,
}

public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}

/// <summary>Matplotlib colormaps, linearly interpolated between 9 anchors.</summary>
public static class Colormaps
{
    private static readonly Dictionary<ColormapType, uint[]> Anchors = new()
    {
        [ColormapType.Viridis] = [0x440154, 0x472D7B, 0x3B528B, 0x2C728E, 0x21918C, 0x28AE80, 0x5EC962, 0xADDC30, 0xFDE725],
        [ColormapType.Plasma] = [0x0D0887, 0x4C02A1, 0x7E03A8, 0xA92395, 0xCC4778, 0xE56B5D, 0xF89441, 0xFDC328, 0xF0F921],
        [ColormapType.Inferno] = [0x000004, 0x1F0C48, 0x550F6D, 0x88226A, 0xBA3655, 0xE35933, 0xF98E09, 0xF9C932, 0xFCFFA4],
        [ColormapType.Magma] = [0x000004, 0x1C1044, 0x4F127B, 0x812581, 0xB5367A, 0xE55064, 0xFB8761, 0xFEC287, 0xFCFDBF],
        [ColormapType.Turbo] = [0x30123B, 0x4662D7, 0x36AAF9, 0x1AE4B6, 0x72FE5E, 0xC8EF34, 0xFABA39, 0xF66B19, 0x7A0403],
    };

    /// <summary>Colour at position <paramref name="t"/> in [0, 1] (clamped).</summary>
    public static RgbColor Evaluate(ColormapType map, double t)
    {
        var a = Anchors[map];
        if (double.IsNaN(t)) t = 0;
        double x = Math.Clamp(t, 0, 1) * (a.Length - 1);
        int i = Math.Min((int)x, a.Length - 2);
        double f = x - i;
        return new RgbColor(Lerp(a[i] >> 16, a[i + 1] >> 16, f), Lerp(a[i] >> 8, a[i + 1] >> 8, f), Lerp(a[i], a[i + 1], f));
    }

    private static byte Lerp(uint a, uint b, double f) => (byte)Math.Round((a & 0xFF) + ((b & 0xFF) - (double)(a & 0xFF)) * f);
}

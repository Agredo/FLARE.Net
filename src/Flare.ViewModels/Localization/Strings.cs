using System.Globalization;
using System.Resources;

namespace Flare.ViewModels.Localization;

internal static class Strings
{
    public static ResourceManager ResourceManager { get; } =
        new("Flare.ViewModels.Localization.Strings", typeof(Strings).Assembly);

    public static CultureInfo? Culture { get; set; }

    public static string Get(string key) => ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}

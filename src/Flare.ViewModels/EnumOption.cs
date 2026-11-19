using CommunityToolkit.Mvvm.ComponentModel;

namespace Flare.ViewModels;

/// <summary>
/// A picker entry that wraps an enum value and shows a localized caption instead of the raw enum name.
/// <see cref="ToString"/> returns the caption so pickers render it even without an item display binding.
/// </summary>
public sealed partial class EnumOption<T>(T value) : ObservableObject where T : struct, Enum
{
    public T Value { get; } = value;

    [ObservableProperty]
    private string _displayName = value.ToString();

    public override string ToString() => DisplayName;
}

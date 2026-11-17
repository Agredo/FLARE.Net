using Flare.Maui.Views.Panes;

namespace Flare.Maui.Views;

public sealed class SettingsPage : Grid
{
    private SettingsPage(SettingsPane settingsPane, Action<SettingsPage> close)
    {
        BackgroundColor = Color.FromArgb("#0D1220");
        RowDefinitions = [new RowDefinition(GridLength.Star)];
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star)];
        settingsPane.DismissRequested += (_, _) => close(this);
        Children.Add(settingsPane);
    }

    public static SettingsPage Create(object? bindingContext, Action<SettingsPage> close) =>
        new(SettingsPane.Create(bindingContext), close);
}

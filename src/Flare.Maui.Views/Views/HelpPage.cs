using Flare.Maui.Views.Panes;

namespace Flare.Maui.Views;

public sealed class HelpPage : Grid
{
    private HelpPage(HelpPane helpPane, Action<HelpPage> close)
    {
        BackgroundColor = Color.FromArgb("#0D1220");
        RowDefinitions = [new RowDefinition(GridLength.Star)];
        ColumnDefinitions = [new ColumnDefinition(GridLength.Star)];
        helpPane.DismissRequested += (_, _) => close(this);
        Children.Add(helpPane);
    }

    public static HelpPage Create(object? bindingContext, Action<HelpPage> close) =>
        new(HelpPane.Create(bindingContext), close);
}

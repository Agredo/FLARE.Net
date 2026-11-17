namespace Flare.Maui.Views.Panes;

public partial class SidebarPane : ContentView
{
    public SidebarPane() => InitializeComponent();

    public event EventHandler? SettingsRequested;

    private void OnSettingsClicked(object? sender, EventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
}

namespace Flare.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Flare.Maui.App.LogCrash("WinUI.UnhandledException", e.Exception);
        };
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}

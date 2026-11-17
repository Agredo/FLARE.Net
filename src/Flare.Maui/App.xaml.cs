using Flare.Maui.Views;

namespace Flare.Maui;

public partial class App : Application
{
    private readonly MainPage _mainPage;

    public App(MainPage mainPage)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("UnobservedTask", e.Exception);
            e.SetObserved();
        };
        InitializeComponent();
        _mainPage = mainPage;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_mainPage) { Title = "FLARE – Spektrum ↔ Struktur" };
        // The three-column layout needs a desktop-sized window; phones/tablets use the full screen
        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
        {
            window.Width = 1500;
            window.Height = 950;
            // Keep a phone-like minimum so the compact tab layout can be tried on the desktop too
            window.MinimumWidth = 400;
            window.MinimumHeight = 600;
        }
        return window;
    }

    /// <summary>Writes unhandled exceptions to %LOCALAPPDATA%\Flare\crash.log.</summary>
    internal static void LogCrash(string source, Exception? ex)
    {
        string msg = $"{DateTime.Now:O} [{source}] {ex}\n\n";
        System.Diagnostics.Debug.WriteLine(msg);
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flare");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"), msg);
        }
        catch (IOException)
        {
        }
    }
}

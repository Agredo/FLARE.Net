namespace Flare.Maui.Views.Panes;

public partial class SettingsPane : ContentView
{
    public SettingsPane() : this(null) { }

    public static SettingsPane Create(object? bindingContext) => new(bindingContext);

    private SettingsPane(object? bindingContext)
    {
        InitializeComponent();
        BindingContext = bindingContext;
    }

    public event EventHandler? DismissRequested;

    private void OnDismissRequested(object? sender, EventArgs e) => DismissRequested?.Invoke(this, EventArgs.Empty);
}

namespace Flare.Maui.Views.Panes;

public partial class HelpPane : ContentView
{
    public HelpPane() : this(null) { }

    public static HelpPane Create(object? bindingContext) => new(bindingContext);

    private HelpPane(object? bindingContext)
    {
        InitializeComponent();
        BindingContext = bindingContext;
    }

    public event EventHandler? DismissRequested;

    private void OnDismissRequested(object? sender, EventArgs e) => DismissRequested?.Invoke(this, EventArgs.Empty);
}

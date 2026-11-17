namespace Flare.Maui.Views.Theme;

public partial class FlareTheme : ResourceDictionary
{
    public FlareTheme()
    {
        InitializeComponent();
        this["DarkMolecule"] = MolDraw.Skia.MoleculeRenderOptions.Dark;
    }
}

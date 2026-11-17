using Flare.Maui.Views.Panes;
using Flare.ViewModels;

namespace Flare.Maui.Views;

public partial class MainPage : ContentPage
{
    /// <summary>Below this width a single-pane layout with bottom navigation is used.</summary>
    private const double CompactWidthThreshold = 720;

    /// <summary>Below this width the desktop keeps two columns and tabs for the center workspace.</summary>
    private const double WideWidthThreshold = 1150;

    private static readonly Color AccentColor = Color.FromArgb("#282549");
    private static readonly Color TabColor = Colors.Transparent;
    private static readonly Color SelectedTextColor = Colors.White;
    private static readonly Color MutedColor = Color.FromArgb("#9AA8C3");

    private readonly MainViewModel _viewModel;
    private readonly Button[] _tabs;
    private readonly View[] _analysisPanes;
    private readonly SettingsPage _settingsPage;
    private LayoutKind? _layout;
    private bool _initialized;
    private int _selectedTab;

    private enum LayoutKind { Compact, NarrowDesktop, WideDesktop }

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _tabs = [Tab0, Tab1, Tab2, Tab3, Tab4, Tab5];
        _analysisPanes = [SpectraPane, PlotPane, PeaksPane, RankingPane, AnalysisPane];
        _settingsPage = SettingsPage.Create(BindingContext, CloseSettings);
        RootLayout.Children.Add(_settingsPage);
        Grid.SetRow(_settingsPage, 2);
        Grid.SetColumnSpan(_settingsPage, 2);
        _settingsPage.IsVisible = false;
        AnalysisPane.SettingsRequested += (_, _) => OpenSettings();
        SizeChanged += (_, _) => ApplyLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        ApplyLayout();
        if (_viewModel.InitializeCommand.CanExecute(null)) _viewModel.InitializeCommand.Execute(null);
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (!_initialized && width > 0 && height > 0) ApplyLayout();
    }

    /// <summary>Uses the existing panes in one, two, or three columns without duplicating live plots.</summary>
    private void ApplyLayout()
    {
        LayoutKind layout = Width > 0 && Width < CompactWidthThreshold
            ? LayoutKind.Compact
            : Width > 0 && Width < WideWidthThreshold
                ? LayoutKind.NarrowDesktop
                : DeviceInfo.Idiom == DeviceIdiom.Phone && Width <= 0
                    ? LayoutKind.Compact
                    : LayoutKind.WideDesktop;
        if (_layout != layout)
        {
            _layout = layout;
            WideDesktopLayout.IsVisible = layout == LayoutKind.WideDesktop;
            NarrowDesktopLayout.IsVisible = layout == LayoutKind.NarrowDesktop;
            PhoneLayout.IsVisible = layout == LayoutKind.Compact;
            PhoneNavigation.IsVisible = layout != LayoutKind.WideDesktop;
        }
        SelectTab(_selectedTab);
    }

    private static void MoveTo(Grid host, View child)
    {
        if (child.Parent == host) return;
        if (child.Parent is Layout oldParent) oldParent.Children.Remove(child);
        host.Children.Add(child);
    }

    private void OnTabClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string index } || !int.TryParse(index, out int tab)) return;
        if (tab == 5)
            OpenSettings();
        else
            SelectTab(tab);
    }

    private void OpenSettings()
    {
        WideDesktopLayout.IsVisible = false;
        NarrowDesktopLayout.IsVisible = false;
        PhoneLayout.IsVisible = false;
        PhoneNavigation.IsVisible = false;
        _settingsPage.IsVisible = true;
        RootLayout.RowDefinitions[1].Height = GridLength.Auto;
        RootLayout.RowDefinitions[2].Height = GridLength.Star;
        _tabs[5].BackgroundColor = AccentColor;
        _tabs[5].TextColor = SelectedTextColor;
    }

    private void CloseSettings(SettingsPage settings)
    {
        settings.IsVisible = false;
        RootLayout.RowDefinitions[1].Height = GridLength.Star;
        RootLayout.RowDefinitions[2].Height = GridLength.Auto;
        if (!_settingsPage.IsVisible) ApplyLayout();
        UpdateTabs();
    }

    private void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (_settingsPage.IsVisible) CloseSettings(_settingsPage);
        else OpenSettings();
    }

    private void SelectTab(int index)
    {
        _selectedTab = Math.Clamp(index, 0, _analysisPanes.Length - 1);
        if (_layout == LayoutKind.WideDesktop)
        {
            WideDesktopLayout.IsVisible = true;
            MoveTo(WideSpectraHost, SpectraPane);
            MoveTo(WidePlotHost, PlotPane);
            MoveTo(WidePeaksHost, PeaksPane);
            MoveTo(WideRankingHost, RankingPane);
            MoveTo(WideAnalysisHost, AnalysisPane);
        }
        else
        {
            Grid host = _layout == LayoutKind.NarrowDesktop ? NarrowWorkspaceHost : PhoneLayout;
            MoveTo(host, _analysisPanes[_selectedTab]);
        }

        if (_layout == LayoutKind.NarrowDesktop)
        {
            bool spectraSelected = _selectedTab == 0;
            MoveTo(spectraSelected ? NarrowWorkspaceHost : NarrowSpectraHost, SpectraPane);
            MoveTo(spectraSelected ? NarrowSpectraHost : NarrowWorkspaceHost, _analysisPanes[_selectedTab]);
        }
        UpdateTabs();
    }

    private void UpdateTabs()
    {
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool selected = i == _selectedTab;
            _tabs[i].BackgroundColor = selected ? AccentColor : TabColor;
            _tabs[i].TextColor = selected ? SelectedTextColor : MutedColor;
        }
    }
}

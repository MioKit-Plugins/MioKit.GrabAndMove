using Avalonia.Controls;
using GrabAndMove.Views;

namespace GrabAndMove.Preview.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        LoadPluginPreview();
    }

    private void LoadPluginPreview()
    {
        // Layout preview without host DI: bind empty VM shell (toggles won't persist).
        PluginPreviewHost.Content = new GrabAndMoveConfigPage
        {
            DataContext = GrabAndMoveConfigViewModel.CreateDesignPreview()
        };
    }
}

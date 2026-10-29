using Avalonia.Controls;
using Avalonia.Interactivity;
using Ti.Avalonia.Shadcn.Controls;

namespace GrabAndMove.Views;

public partial class GrabAndMoveConfigWindow : ShadWindow
{
    private GrabAndMoveConfigViewModel? _viewModel;

    public GrabAndMoveConfigWindow()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            _viewModel = GrabAndMoveRegister.Instance.ComponentContext.Resolve<GrabAndMoveConfigViewModel>();
            _viewModel.Window = this;
            DataContext = _viewModel;
            _viewModel.LoadFromPlugin();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _viewModel?.LoadFromPlugin();
    }
}

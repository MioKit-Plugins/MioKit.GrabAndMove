using Avalonia.Threading;
using GrabAndMove.Views;

namespace GrabAndMove.Services;

public sealed class GrabAndMoveWindowService
{
    public Task ShowConfigWindowAsync()
    {
        Dispatcher.UIThread.Post(() =>
            _ = WindowManager.ShowUniqueWindow<GrabAndMoveConfigWindow>(
                GrabAndMoveConst.PluginId,
                GrabAndMoveConst.PluginId));
        return Task.CompletedTask;
    }
}

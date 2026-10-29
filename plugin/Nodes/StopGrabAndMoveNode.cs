using GrabAndMove.Features;
using GrabAndMove.Services;

namespace GrabAndMove.Nodes;

[EavType(GrabAndMoveConst.StopNodeTypeId)]
public sealed class StopGrabAndMoveNode : MioObject,
    IGrabAndMoveFeature,
    IAliasNameFeature,
    IHotkeyFeature,
    IResultActionProviderFeature,
    IUseFeature
{
    public StopGrabAndMoveNode(string id) : base(id)
    {
        PreloadPropertySource =
        [
            ..PreloadPropertySource,
            AliasNameExtension.AliasNameProperty,
            HotkeyExtension.HotkeyProperty,
            RecentExtension.UseCountProperty,
            RecentExtension.LastUseTimeProperty
        ];
    }

    public override Guid MioType => GrabAndMoveConst.StopNodeType;

    public ValueTask<IEnumerable<ISearchResultAction>> GetActionAsync(SearchResult searchResult) =>
        ValueTask.FromResult<IEnumerable<ISearchResultAction>>(
        [
            AliasNameAction.Instance,
            HotKeyAction.Instance
        ]);

    public async Task InvokeAsync(InvokeContext context)
    {
        GrabAndMoveRegister.Instance.ComponentContext
            .Resolve<GrabAndMoveService>()
            .DisableFeature();

        if (context.Source == InvokeSource.SearchBox)
            await MioIoc.Resolve<ISearchBoxWindow>().HideWindowAsync().ConfigureAwait(false);

        await this.IncreaseAsync();
    }
}

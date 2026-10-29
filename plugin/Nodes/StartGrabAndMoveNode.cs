using GrabAndMove.Features;
using GrabAndMove.Services;

namespace GrabAndMove.Nodes;

[EavType(GrabAndMoveConst.StartNodeTypeId)]
public sealed class StartGrabAndMoveNode : MioObject,
    IGrabAndMoveFeature,
    IAliasNameFeature,
    IHotkeyFeature,
    IResultActionProviderFeature,
    IUseFeature
{
    public StartGrabAndMoveNode(string id) : base(id)
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

    public override Guid MioType => GrabAndMoveConst.StartNodeType;

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
            .EnableFeature();

        if (context.Source == InvokeSource.SearchBox)
            await MioIoc.Resolve<ISearchBoxWindow>().HideWindowAsync().ConfigureAwait(false);

        await this.IncreaseAsync();
    }
}

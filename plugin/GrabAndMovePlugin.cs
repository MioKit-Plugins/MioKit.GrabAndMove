using GrabAndMove.Nodes;
using GrabAndMove.Services;

namespace GrabAndMove;

[EavType(GrabAndMoveConst.PluginTypeId)]
public class GrabAndMovePlugin : PluginBase,
    IGrabAndMovePluginFeature,
    IPluginMainWindowFeature,
    IInvokeFeature,
    ISearchableFeature,
    IUseFeature,
    IHotkeyFeature,
    IAliasNameFeature,
    IResultActionProviderFeature
{
    public GrabAndMovePlugin() : base(GrabAndMoveConst.PluginId)
    {
        Name = "抓取移动";
        Description = "按住修饰键在窗口任意位置拖动以移动或调整大小";
        PreloadPropertySource =
        [
            ..PreloadPropertySource,
            GrabAndMoveExtension.FeatureEnabledProperty,
            GrabAndMoveExtension.ActivationModifierProperty,
            GrabAndMoveExtension.PreventAltWindowMenuProperty,
            GrabAndMoveExtension.RightClickResizeEnabledProperty,
            GrabAndMoveExtension.ExcludedAppsProperty,
            GrabAndMoveExtension.ModifierExcludedAppsProperty,
            AliasNameExtension.AliasNameProperty,
            HotkeyExtension.HotkeyProperty,
            RecentExtension.UseCountProperty,
            RecentExtension.LastUseTimeProperty
        ];
    }

    public override Guid MioType => GrabAndMoveConst.PluginType;

    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataProvider = MioIoc.Resolve<IMioDataProvider>();
        var groupExisted = await dataProvider.ExistsAsync(GrabAndMoveConst.SearchGroupId);
        var startExisted = await dataProvider.ExistsAsync(GrabAndMoveConst.StartNodeId);
        var stopExisted = await dataProvider.ExistsAsync(GrabAndMoveConst.StopNodeId);

        await EnsureSubTreeLoadedAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var defaults = new Dictionary<MioObject, Dictionary<IAttachEavProperty, object>>();
        if (!groupExisted)
        {
            var group = this.FindFirstDescendant<GrabAndMoveSearchGroup>(
                node => node.Id == GrabAndMoveConst.SearchGroupId)!;
            group.Name = "抓取移动";
            group.Description = "打开配置，或启动 / 停止手势";
            defaults[group] = CreateNameDefaults(group);
        }

        if (!startExisted)
        {
            var start = this.FindFirstDescendant<StartGrabAndMoveNode>(
                node => node.Id == GrabAndMoveConst.StartNodeId)!;
            start.Name = "启动抓取移动";
            start.Description = "启用手势，不打开配置窗口";
            defaults[start] = CreateNameDefaults(start);
        }

        if (!stopExisted)
        {
            var stop = this.FindFirstDescendant<StopGrabAndMoveNode>(
                node => node.Id == GrabAndMoveConst.StopNodeId)!;
            stop.Name = "停止抓取移动";
            stop.Description = "停用手势，不打开配置窗口";
            defaults[stop] = CreateNameDefaults(stop);
        }

        if (defaults.Count > 0)
            await dataProvider.SaveObjectsAsync(defaults.Keys.ToArray(), defaults);
    }

    public override Task EnsureSubTreeLoadedAsync()
        => EnsureTreeLoadedAsync<GrabAndMoveSearchGroup>(GrabAndMoveConst.SearchGroupId);

    public void OpenPluginMainWindow()
    {
        GrabAndMoveRegister.Instance.ComponentContext
            .Resolve<GrabAndMoveWindowService>()
            .ShowConfigWindowAsync();
    }

    public async Task InvokeAsync(InvokeContext context)
    {
        OpenPluginMainWindow();
        await this.IncreaseAsync();
    }

    public ValueTask SearchAsync(
        SearchRequest request,
        IWriteOnlyResultList<SearchResult> results,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.IsEmptyText)
            return ValueTask.CompletedTask;

        if (!this.TryMatch(request.SearchText!, out var match, out var title))
            return ValueTask.CompletedTask;

        results.Add(new SearchResult(request, this)
        {
            Title = title,
            Description = Description,
            MatchResult = match!.Value,
            Source = "抓取移动"
        });
        return ValueTask.CompletedTask;
    }

    public ValueTask<IEnumerable<ISearchResultAction>> GetActionAsync(SearchResult searchResult) =>
        ValueTask.FromResult<IEnumerable<ISearchResultAction>>(
        [
            AliasNameAction.Instance,
            HotKeyAction.Instance
        ]);

    private static Dictionary<IAttachEavProperty, object> CreateNameDefaults(MioObject node)
        => new()
        {
            [MioFeatureExtensions.NameProperty] = node.Name!,
            [MioFeatureExtensions.DescriptionProperty] = node.Description!
        };

    public override ValueTask<PluginDataCleanupPlan> CreateDataCleanupPlanAsync(
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PluginDataCleanupPlan
        {
            AdditionalEavRootObjectIds = []
        });
    }
}

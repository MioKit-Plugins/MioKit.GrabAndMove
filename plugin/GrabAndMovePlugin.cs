using GrabAndMove.Nodes;

namespace GrabAndMove;

public class GrabAndMovePlugin : PluginBase, IGrabAndMovePluginFeature
{
    public GrabAndMovePlugin() : base(GrabAndMoveConst.PluginId)
    {
        PreloadPropertySource =
        [
            ..PreloadPropertySource,
            GrabAndMoveExtension.FeatureEnabledProperty,
            GrabAndMoveExtension.ActivationModifierProperty,
            GrabAndMoveExtension.PreventAltWindowMenuProperty,
            GrabAndMoveExtension.RightClickResizeEnabledProperty,
            GrabAndMoveExtension.ExcludedAppsProperty,
            GrabAndMoveExtension.ModifierExcludedAppsProperty
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
            group.Description = "启动或停止按住修饰键拖动窗口";
            defaults[group] = CreateNameDefaults(group);
        }

        if (!startExisted)
        {
            var start = this.FindFirstDescendant<StartGrabAndMoveNode>(
                node => node.Id == GrabAndMoveConst.StartNodeId)!;
            start.Name = "启动抓取移动";
            start.Description = "启用修饰键拖动移动/调整窗口大小";
            defaults[start] = CreateNameDefaults(start);
        }

        if (!stopExisted)
        {
            var stop = this.FindFirstDescendant<StopGrabAndMoveNode>(
                node => node.Id == GrabAndMoveConst.StopNodeId)!;
            stop.Name = "停止抓取移动";
            stop.Description = "停用抓取移动手势";
            defaults[stop] = CreateNameDefaults(stop);
        }

        if (defaults.Count > 0)
            await dataProvider.SaveObjectsAsync(defaults.Keys.ToArray(), defaults);
    }

    public override Task EnsureSubTreeLoadedAsync()
        => EnsureTreeLoadedAsync<GrabAndMoveSearchGroup>(GrabAndMoveConst.SearchGroupId);

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

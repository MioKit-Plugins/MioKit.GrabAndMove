namespace GrabAndMove;

public class GrabAndMovePlugin : PluginBase
{
    public GrabAndMovePlugin() : base(GrabAndMoveConst.PluginId)
    {
        // 如需扩展基类预加载属性列表，在此用 spread 语法：
        // PreloadPropertySource = [..PreloadPropertySource, MyExtension.PathProperty];
    }

    public override Guid MioType => GrabAndMoveConst.PluginType;

    /// <summary>
    /// 在此加载搜索组子树。重写 EnsureSubTreeLoadedAsync 可集中管理多个子树加载。
    /// </summary>
    protected override async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        // await EnsureTreeLoadedAsync<MySearchGroup>(MySearchGroup.GroupId);
        await Task.CompletedTask;
    }

    public override ValueTask<PluginDataCleanupPlan> CreateDataCleanupPlanAsync(
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PluginDataCleanupPlan
        {
            // 仅填写未挂在插件根节点下的额外持久化根 ID。
            AdditionalEavRootObjectIds = []
        });
    }
}

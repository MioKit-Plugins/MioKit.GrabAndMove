using GrabAndMove.Nodes;
using GrabAndMove.Services;
using GrabAndMove.Views;

namespace GrabAndMove;

public class GrabAndMoveRegister : RegisterBase<GrabAndMoveRegister>
{
    public override void RegisterService(ContainerBuilder builder, IServiceCollection services)
    {
        var plugin = new GrabAndMovePlugin();

        builder.RegisterInstance(plugin)
            .As<IPlugin>()
            .Keyed<IPlugin>(GrabAndMoveConst.PluginId)
            .As(plugin.GetType());

        builder.RegisterType<GrabAndMoveSearchGroup>().AsSelf().InstancePerDependency();
        builder.RegisterType<StartGrabAndMoveNode>().AsSelf().InstancePerDependency();
        builder.RegisterType<StopGrabAndMoveNode>().AsSelf().InstancePerDependency();

        builder.Register(ctx => new ModifierKeyInjector(plugin.Context.Logger))
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<GrabAndMoveCore>()
            .AsSelf()
            .SingleInstance();

        builder.Register(ctx => new GrabAndMoveService(
                plugin,
                ctx.Resolve<GrabAndMoveCore>(),
                ctx.Resolve<IMouseHook>(),
                ctx.Resolve<IKeyboardHook>(),
                plugin.Context.Logger))
            .AsSelf()
            .As<IPluginLifecycleComponent>()
            .SingleInstance();

        builder.RegisterType<GrabAndMoveWindowService>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<GrabAndMoveConfigViewModel>()
            .AsSelf();
    }
}

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
    }
}

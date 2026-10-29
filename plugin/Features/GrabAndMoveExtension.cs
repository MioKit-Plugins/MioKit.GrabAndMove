using GrabAndMove.Models;

namespace GrabAndMove;

public static partial class GrabAndMoveExtension
{
    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static EavProperty<bool?> FeatureEnabledProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(GrabAndMoveConst.FeatureEnabledPropertyGuid)
            .WithName("FeatureEnabled")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static EavProperty<GrabAndMoveModifier?> ActivationModifierProperty { get; } =
        EavPropertyBuilder<GrabAndMoveModifier?>.Create()
            .WithId(GrabAndMoveConst.ActivationModifierPropertyGuid)
            .WithName("ActivationModifier")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Integer)
            .WithPropertyConvert(EnumIntegerConvert<GrabAndMoveModifier>.Instance)
            .WithDefaultValue(GrabAndMoveModifier.Alt)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static EavProperty<bool?> PreventAltWindowMenuProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(GrabAndMoveConst.PreventAltWindowMenuPropertyGuid)
            .WithName("PreventAltWindowMenu")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static EavProperty<bool?> RightClickResizeEnabledProperty { get; } =
        EavPropertyBuilder<bool?>.Create()
            .WithId(GrabAndMoveConst.RightClickResizeEnabledPropertyGuid)
            .WithName("RightClickResizeEnabled")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.Boolean)
            .WithPropertyConvert(BoolPropertyConvert.Instance)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static EavProperty<string?> ExcludedAppsProperty { get; } =
        EavPropertyBuilder<string?>.Create()
            .WithId(GrabAndMoveConst.ExcludedAppsPropertyGuid)
            .WithName("ExcludedApps")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.String)
            .WithDefaultValue(string.Empty)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static EavProperty<string?> ModifierExcludedAppsProperty { get; } =
        EavPropertyBuilder<string?>.Create()
            .WithId(GrabAndMoveConst.ModifierExcludedAppsPropertyGuid)
            .WithName("ModifierExcludedApps")
            .WithPolicy(EavCachePolicy.None)
            .WithStoreType(MioStoreType.String)
            .WithDefaultValue(string.Empty)
            .Build();
}

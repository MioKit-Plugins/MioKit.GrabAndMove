using GrabAndMove.Models;

namespace GrabAndMove;

public static partial class GrabAndMoveExtension
{
    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static SettingEavProperty<bool?> FeatureEnabledProperty { get; } =
        SettingEavPropertyBuilder<bool?>.Create()
            .WithId(GrabAndMoveConst.FeatureEnabledPropertyGuid)
            .WithName("FeatureEnabled")
            .WithDisplayName("启用抓取移动")
            .WithDescription("开启后，按住修饰键并在窗口内拖动即可移动或调整大小。")
            .WithGroup("功能")
            .WithOrder(10)
            .WithEditor(SettingEditorKind.Boolean)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static SettingEavProperty<GrabAndMoveModifier?> ActivationModifierProperty { get; } =
        SettingEavPropertyBuilder<GrabAndMoveModifier?>.Create()
            .WithId(GrabAndMoveConst.ActivationModifierPropertyGuid)
            .WithName("ActivationModifier")
            .WithDisplayName("激活修饰键")
            .WithDescription("按住此键后拖动窗口：Alt（默认）或 Windows 键。")
            .WithGroup("功能")
            .WithOrder(20)
            .WithEditor(SettingEditorKind.Enum)
            .WithDefaultValue(GrabAndMoveModifier.Alt)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static SettingEavProperty<bool?> PreventAltWindowMenuProperty { get; } =
        SettingEavPropertyBuilder<bool?>.Create()
            .WithId(GrabAndMoveConst.PreventAltWindowMenuPropertyGuid)
            .WithName("PreventAltWindowMenu")
            .WithDisplayName("拖动后抑制 Alt 窗口菜单")
            .WithDescription("使用 Alt 作为修饰键时，完成拖动或缩放后松开 Alt 不再弹出系统窗口菜单。")
            .WithGroup("功能")
            .WithOrder(30)
            .WithEditor(SettingEditorKind.Boolean)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static SettingEavProperty<bool?> RightClickResizeEnabledProperty { get; } =
        SettingEavPropertyBuilder<bool?>.Create()
            .WithId(GrabAndMoveConst.RightClickResizeEnabledPropertyGuid)
            .WithName("RightClickResizeEnabled")
            .WithDisplayName("修饰键 + 右键调整大小")
            .WithDescription("启用后，按住修饰键并用右键拖动可从最近边或角调整窗口大小。")
            .WithGroup("功能")
            .WithOrder(40)
            .WithEditor(SettingEditorKind.Boolean)
            .WithDefaultValue(true)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static SettingEavProperty<string?> ExcludedAppsProperty { get; } =
        SettingEavPropertyBuilder<string?>.Create()
            .WithId(GrabAndMoveConst.ExcludedAppsPropertyGuid)
            .WithName("ExcludedApps")
            .WithDisplayName("排除应用")
            .WithDescription("这些进程的窗口不会被抓取移动。每行一个名称，例如 notepad.exe。")
            .WithGroup("排除")
            .WithOrder(50)
            .WithEditor(SettingEditorKind.LongText)
            .WithDefaultValue(string.Empty)
            .Build();

    [EavRelation(typeof(IGrabAndMovePluginFeature))]
    public static SettingEavProperty<string?> ModifierExcludedAppsProperty { get; } =
        SettingEavPropertyBuilder<string?>.Create()
            .WithId(GrabAndMoveConst.ModifierExcludedAppsPropertyGuid)
            .WithName("ModifierExcludedApps")
            .WithDisplayName("排除应用（修饰键）")
            .WithDescription("这些应用在前台时不拦截修饰键，键事件交给应用本身。每行一个，例如 game.exe。")
            .WithGroup("排除")
            .WithOrder(60)
            .WithEditor(SettingEditorKind.LongText)
            .WithDefaultValue(string.Empty)
            .Build();
}

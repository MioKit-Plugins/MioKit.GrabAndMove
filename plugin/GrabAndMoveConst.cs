namespace GrabAndMove;

public static class GrabAndMoveConst
{
    public const string PluginId = "com.tinyisle.plugin.grab-and-move";

    public const string PluginTypeId = "8BECAB1A-0417-426C-8F12-3EA57BF4BAB7";
    public static readonly Guid PluginType = Guid.Parse(PluginTypeId);

    public const string SearchGroupTypeId = "21D984DE-5082-4738-9B39-C156C31A13A4";
    public static readonly Guid SearchGroupType = Guid.Parse(SearchGroupTypeId);

    public const string StartNodeTypeId = "576B2967-D3D8-42BA-A082-EFECBDC2ECFA";
    public static readonly Guid StartNodeType = Guid.Parse(StartNodeTypeId);

    public const string StopNodeTypeId = "347F8DEF-3AB9-4E91-9669-7982F933678E";
    public static readonly Guid StopNodeType = Guid.Parse(StopNodeTypeId);

    public const string SearchGroupId = PluginId + ".search-group";
    public const string StartNodeId = PluginId + ".start";
    public const string StopNodeId = PluginId + ".stop";

    public const string FeatureEnabledPropertyId = "E42F2453-E031-4169-8B99-BCA05531A158";
    public static readonly Guid FeatureEnabledPropertyGuid = Guid.Parse(FeatureEnabledPropertyId);

    public const string ActivationModifierPropertyId = "31E85E77-AD73-45CF-91E6-9B57B6A33F6E";
    public static readonly Guid ActivationModifierPropertyGuid = Guid.Parse(ActivationModifierPropertyId);

    public const string PreventAltWindowMenuPropertyId = "778B7B3E-66C1-4FE2-A16B-E63D1FF12F23";
    public static readonly Guid PreventAltWindowMenuPropertyGuid = Guid.Parse(PreventAltWindowMenuPropertyId);

    public const string RightClickResizeEnabledPropertyId = "19894D2F-AAFE-45A4-A900-F50F38D495C0";
    public static readonly Guid RightClickResizeEnabledPropertyGuid = Guid.Parse(RightClickResizeEnabledPropertyId);

    public const string ExcludedAppsPropertyId = "A8B9C540-254C-47A6-A637-30CC5611B8E1";
    public static readonly Guid ExcludedAppsPropertyGuid = Guid.Parse(ExcludedAppsPropertyId);

    public const string ModifierExcludedAppsPropertyId = "79889AAB-BA1F-42D2-8244-35FE9858CC2D";
    public static readonly Guid ModifierExcludedAppsPropertyGuid = Guid.Parse(ModifierExcludedAppsPropertyId);

    public const string InputHandlerId = PluginId + ".hooks";

    /// <summary>Before host global hotkey handler (order 100).</summary>
    public const ushort InputHandlerOrder = 40;
}

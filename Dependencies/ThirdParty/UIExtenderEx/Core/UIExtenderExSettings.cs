namespace Bannerlord.UIExtenderEx;

/// <summary>
/// Forked into TAOM. Replaces the full MCM-based settings system with a static stub.
/// DumpXML is always false — no need for the SettingsSubModuleTags/ModuleManager dependency.
/// </summary>
public class UIExtenderExSettings
{
    public static UIExtenderExSettings Instance { get; } = new UIExtenderExSettings();
    public bool DumpXML => false;
    private UIExtenderExSettings() { }
}

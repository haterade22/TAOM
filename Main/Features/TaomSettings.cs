using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace TAOM.Features;

public class TaomSettings : AttributeGlobalSettings<TaomSettings>
{
    public override string Id => "TAOM";
    public override string DisplayName => "TAOM - Tales From the Age of Men";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    // --- War of the Ring ---

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyBool("Enable War of the Ring", Order = 0,
        HintText = "When enabled, a scripted war will escalate between Free Peoples and Dark Powers.")]
    public bool WarOfTheRingEnabled { get; set; } = true;

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 1 Start Day", 1, 365, Order = 1,
        HintText = "Days after campaign start when Isengard and Dunland attack Rohan.")]
    public int Phase1TriggerDay { get; set; } = 30;

    [SettingPropertyGroup("War of the Ring")]
    [SettingPropertyInteger("Phase 2 Start Day", 1, 365, Order = 2,
        HintText = "Days after campaign start when all hostile kingdoms go to war. Peace is blocked.")]
    public int Phase2TriggerDay { get; set; } = 45;

    [SettingPropertyGroup("War of the Ring/Test Mode")]
    [SettingPropertyBool("Enable Test Mode", Order = 0,
        HintText = "Uses short delays (2/5 days) for rapid testing. Overrides Phase 1/2 days.")]
    public bool TestMode { get; set; }
}

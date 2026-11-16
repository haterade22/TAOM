using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace TAOM.Features.BlowDiagnostics;

// Dedicated MCM page (mirrors BattleLoadDiagnostics). Default OFF: this stamps EVERY damaging
// blow to the durable log, which is a per-hit hot path — only turn it on to reproduce a
// combat/siege crash, then send Logs/taom_debug_*.log. The LAST [BlowDiag] line before the log
// stops names the fatal blow (victim race, blow flags, damage type).
public sealed class BlowDiagnosticsSettings : AttributeGlobalSettings<BlowDiagnosticsSettings>
{
    // Hides MCM's GlobalSettings<T>.Instance: a dedicated server, where MCM is not loaded, reads
    // taom-server-settings.json (ServerSettingsSource); a client gets MCM's own. Name the base explicitly:
    // a bare Instance here would recurse.
    public static new BlowDiagnosticsSettings? Instance =>
        ServerSettings.ServerSettingsSource.Get<BlowDiagnosticsSettings>() ?? GlobalSettings<BlowDiagnosticsSettings>.Instance;

    public override string Id => "TAOM.BlowDiagnostics";
    public override string DisplayName => "TAOM — Blow Diagnostics";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    [SettingPropertyGroup("{=taom_mcm_group_master}Master")]
    [SettingPropertyBool("{=taom_mcm_blow_EnableBlowDiagnostics}Enable Blow Diagnostics", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_blow_EnableBlowDiagnostics_hint}Turn ON only to reproduce a combat or siege crash. Logs every damaging blow, every death, and every siege-engine shot to the TAOM debug log (Logs/taom_debug_*.log) with durable flushing, so the LAST line survives a hard native crash and names the fatal blow — victim race, blow flags, damage type, whether it was a fire pot. Costs a synchronous log write per hit while ON, so leave OFF for normal play. Default OFF.")]
    public bool EnableBlowDiagnostics { get; set; } = false;
}

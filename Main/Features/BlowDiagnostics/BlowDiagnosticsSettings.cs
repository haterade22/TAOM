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
    public override string Id => "TAOM.BlowDiagnostics";
    public override string DisplayName => "TAOM — Blow Diagnostics";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Enable Blow Diagnostics", Order = 0,
        HintText = "Turn ON only to reproduce a combat or siege crash. Logs every damaging blow, every death, and every siege-engine shot to the TAOM debug log (Logs/taom_debug_*.log) with durable flushing, so the LAST line survives a hard native crash and names the fatal blow — victim race, blow flags, damage type, whether it was a fire pot. Costs a synchronous log write per hit while ON, so leave OFF for normal play. Default OFF.")]
    public bool EnableBlowDiagnostics { get; set; } = false;
}

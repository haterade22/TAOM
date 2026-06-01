using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace TAOM.Features.BattleLoadDiagnostics;

// Dedicated MCM page (mirrors CrashReportSettings). Defaults are the "diagnose now"
// posture — everything ON — because we ship this specifically to capture the
// intermittent battle-load hang from user machines. Players who hit perf issues can
// disable it.
public sealed class BattleLoadDiagnosticsSettings : AttributeGlobalSettings<BattleLoadDiagnosticsSettings>
{
    public override string Id => "TAOM.BattleLoadDiagnostics";
    public override string DisplayName => "TAOM — Battle Load Diagnostics";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Enable Battle Load Diagnostics", Order = 0,
        HintText = "Logs the full attack->battle-playable lifecycle (encounter, scene selection, Mission.Initialize, every initial-spawn agent's equipment + collision-mesh names) to the TAOM debug log. Leave ON while diagnosing the intermittent battle-load hang — the LAST line in the log names the stuck phase / agent.")]
    public bool EnableBattleLoadDiagnostics { get; set; } = true;

    [SettingPropertyGroup("Stall Watchdog")]
    [SettingPropertyBool("Enable Stall Watchdog", Order = 0,
        HintText = "A background-thread timer that detects a battle stuck on the loading screen and writes a 'STILL LOADING' marker naming the last phase reached. Runs off the main thread so it still fires when the game is frozen. Default ON.")]
    public bool EnableStallWatchdog { get; set; } = true;

    [SettingPropertyGroup("Stall Watchdog")]
    [SettingPropertyBool("Auto-Write Crash Bundle On Stall", Order = 1,
        HintText = "When the watchdog fires, also write a crash-report ZIP (under Logs/) so you can send it in one action. Requires Crash Report capture enabled. Default ON.")]
    public bool EnableStallWatchdogBundle { get; set; } = true;

    [SettingPropertyGroup("Stall Watchdog")]
    [SettingPropertyInteger("Stall Threshold (seconds)", 10, 300, Order = 2,
        HintText = "How long a battle load may run before the watchdog flags it as stalled. Default 45s.")]
    public int StallWatchdogSeconds { get; set; } = 45;
}

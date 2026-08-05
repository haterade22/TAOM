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
    [SettingPropertyInteger("Stall Threshold (seconds)", 10, 600, Order = 2,
        HintText = "How long a battle load may run before the watchdog flags it as stalled. Default 300s (5 min) — large custom siege scenes (e.g. Minas Tirith) legitimately take minutes to load on first entry.")]
    public int StallWatchdogSeconds { get; set; } = 300;

    [SettingPropertyGroup("Exit Stall Sampler")]
    [SettingPropertyBool("Enable Exit Stall Sampler", Order = 0,
        HintText = "If a mission exit stalls past 15s, briefly suspends the game's main thread (at +15/+30/+60s) to photograph its call stack into the TAOM debug log — this is what root-caused the tournament-exit freeze (#331). Tiny residual risk: a suspension landing mid-GC can freeze the game harder than the stall itself. Turn OFF to keep the other diagnostics without any thread suspension. Default ON.")]
    public bool EnableExitStallSampler { get; set; } = true;

    [SettingPropertyGroup("Memory Sampler")]
    [SettingPropertyBool("Enable Memory Sampler", Order = 0, RequireRestart = false,
        HintText = "Writes a periodic [MemSample] line (process private/working-set MB, managed heap, system commit use/limit, available RAM) to the TAOM debug log, plus a one-shot WARN when system commit headroom runs low — the memory trajectory before an out-of-memory crash. Independent of the master Battle Load Diagnostics toggle: this is session-wide crash forensics, not battle-load phase logging. Default ON.")]
    public bool EnableMemorySampler { get; set; } = true;

    [SettingPropertyGroup("Memory Sampler")]
    [SettingPropertyInteger("Sample Interval (seconds)", 10, 120, Order = 1, RequireRestart = false,
        HintText = "Seconds between [MemSample] lines. Default 30s (~120 lines per hour of play). Takes effect on the next sample — no restart needed.")]
    public int MemorySampleIntervalSeconds { get; set; } = 30;
}

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
    [SettingPropertyBool("Enable Battle Load Diagnostics", Order = 0, RequireRestart = false,
        HintText = "Logs the full attack->battle-playable lifecycle (encounter, scene selection, Mission.Initialize, every initial-spawn agent's equipment + collision-mesh names) to the TAOM debug log. Leave ON while diagnosing the intermittent battle-load hang — the LAST line in the log names the stuck phase / agent.")]
    public bool EnableBattleLoadDiagnostics { get; set; } = true;

    [SettingPropertyGroup("Stall Watchdog")]
    [SettingPropertyBool("Enable Stall Watchdog", Order = 0, RequireRestart = false,
        HintText = "A background-thread timer that detects a battle stuck on the loading screen and writes a 'STILL LOADING' marker naming the last phase reached. Runs off the main thread so it still fires when the game is frozen. It does NOT fire while the engine is still compiling shaders and the count is moving (a cold shader cache is a slow load, not a hang); that hold-off ends after 15 minutes of UNBROKEN compilation, so a queue that never drains still gets reported. Default ON.")]
    public bool EnableStallWatchdog { get; set; } = true;

    [SettingPropertyGroup("Stall Watchdog")]
    [SettingPropertyBool("Auto-Write Crash Bundle On Stall", Order = 1, RequireRestart = false,
        HintText = "When the watchdog fires, also write a crash-report ZIP (under Logs/) so you can send it in one action. Requires Crash Report capture enabled. Default ON.")]
    public bool EnableStallWatchdogBundle { get; set; } = true;

    [SettingPropertyGroup("Stall Watchdog")]
    [SettingPropertyInteger("Stall Threshold (seconds)", 10, 600, Order = 2, RequireRestart = false,
        HintText = "How long a battle load may run before the watchdog flags it as stalled. Default 300s (5 min): large custom siege scenes (e.g. Minas Tirith) legitimately take minutes to load on first entry. Past this threshold the watchdog still holds off while the shader-compilation count keeps moving, until the queue has been busy for 15 unbroken minutes.")]
    public int StallWatchdogSeconds { get; set; } = 300;

    [SettingPropertyGroup("Exit Stall Sampler")]
    [SettingPropertyBool("Enable Exit Stall Sampler", Order = 0, RequireRestart = false,
        HintText = "If a mission exit stalls past 15s, briefly suspends the game's main thread (at +15/+30/+60s) to photograph its call stack into the TAOM debug log — this is what root-caused the tournament-exit freeze (#331). Tiny residual risk: a suspension landing mid-GC can freeze the game harder than the stall itself. Turn OFF to keep the other diagnostics without any thread suspension. Default ON.")]
    public bool EnableExitStallSampler { get; set; } = true;

    [SettingPropertyGroup("Exit Stall Sampler")]
    [SettingPropertyBool("Enable Battle Freeze Sampler", Order = 1, RequireRestart = false,
        HintText = "If a battle frame stops for 10s (the agent AI tick or the main mission frame never finishes: the game freezes with no crash), briefly suspends the stuck thread at +10/+20/+40s to photograph its call stack into the TAOM debug log as [MissionStall]. It never runs while the game is responsive, and never while a mission is ending or exiting (the exit sampler above covers that). Independent of the master Battle Load Diagnostics toggle: this is crash forensics, not load logging. Same tiny residual risk as the exit sampler. Default ON.")]
    public bool EnableMissionTickStallSampler { get; set; } = true;

    [SettingPropertyGroup("Exit Stall Sampler")]
    [SettingPropertyBool("Enable Main Thread Freeze Sampler", Order = 2, RequireRestart = false,
        HintText = "If the game finishes no frame for 10 seconds anywhere (map, menus, loading screens), writes a [Freeze] line to the TAOM debug log at +10/+20/+40/+80 s with the number of shaders the engine compiled meanwhile, and photographs the main thread's call stack when that is safe. It never runs while the game is responsive, stands aside during a mission exit and the shader pre-compilation walk, and skips the stack while the game is busy saving or loading. Default ON.")]
    public bool EnableMainThreadFreezeSampler { get; set; } = true;

    [SettingPropertyGroup("Exit Stall Sampler")]
    [SettingPropertyBool("Write Memory Dump On Freeze", Order = 3, RequireRestart = false,
        HintText = "When the game has finished no frame for 80 seconds and the engine log shows no shader compiling, writes one small memory dump (thread stacks only, a few MB) to the TAOM Logs folder for a bug report. One per game session. Off by default: turn it on when asked to help with a freeze report.")]
    public bool WriteFreezeDump { get; set; } = false;

    [SettingPropertyGroup("Memory Sampler")]
    [SettingPropertyBool("Enable Memory Sampler", Order = 0, RequireRestart = false,
        HintText = "Writes memory telemetry to the TAOM debug log: a periodic [MemSample] line (process private/working-set MB, managed heap, system commit use/limit, available RAM), a one-shot WARN when system commit headroom runs low, and a [MemStation] line each time a screen opens or closes so growth can be attributed to a screen. This ONE switch governs all three. Independent of the master Battle Load Diagnostics toggle: this is session-wide crash forensics, not battle-load phase logging. Default ON.")]
    public bool EnableMemorySampler { get; set; } = true;

    [SettingPropertyGroup("Memory Sampler")]
    [SettingPropertyInteger("Sample Interval (seconds)", 10, 120, Order = 1, RequireRestart = false,
        HintText = "Seconds between [MemSample] lines. Default 30s (~120 lines per hour of play). Takes effect on the next sample — no restart needed.")]
    public int MemorySampleIntervalSeconds { get; set; } = 30;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Cull Hidden Settlement Nameplates", Order = 1, RequireRestart = false,
        HintText = "The campaign map updates every settlement nameplate every frame (about a thousand in TAOM), hidden ones included. When on, a nameplate that is hidden and cannot become visible this frame (out of height range, not tracked, not in range, nothing about its settlement changed) is left alone, which saves frame time on the map, most in fast forward. Everything else updates exactly as in the vanilla game. Takes effect on the next frame. Default ON.")]
    public bool CullHiddenNameplates { get; set; } = true;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Release Map View Memory", Order = 2, RequireRestart = false,
        HintText = "In the measurement this feature is adapted from, every screen or menu that covered the campaign map left three colour targets and one depth target of the map view's old set behind (tens of megabytes of graphics memory per closed screen, more at a high resolution). That memory stays until a battle, or a town, village or arena scene, starts. When on, every Nth cover (see Map View Release Interval) has the map view release its graphics memory as you return to the map. The map then shows a short loading screen until its view is ready again, on one return in N (about 0.4 s instead of 0.08 s in that measurement). Takes effect at the next cover. Default ON.")]
    public bool ReleaseMapViewMemory { get; set; } = true;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyInteger("Map View Release Interval", 1, 1000, Order = 3, RequireRestart = false,
        HintText = "Release the map view's memory on every Nth cover of the map. Default 20. A lower number gives memory back sooner but shows the short loading screen on more returns to the map (about 0.4 s in the measurement this feature is adapted from); 1 releases on every cover. Takes effect at the next cover.")]
    public int MapViewReleaseInterval { get; set; } = 20;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Cheat Mode Lists All Items And Troops", Order = 4, RequireRestart = false,
        HintText = "Vanilla cheat mode (cheat_mode = 1 in engine_config.txt, which TAOM's taom.* console commands need) makes your own inventory list every item in the game and the party screen every troop. With TAOM's thousands of items that list takes gigabytes of memory and can freeze the game for many seconds on a 16 GB machine. Off: with cheat mode on, both screens open as they do without it. On: the vanilla cheat lists. Takes effect at the next screen open. Default OFF.")]
    public bool CheatModeListsAllItemsAndTroops { get; set; } = false;
}

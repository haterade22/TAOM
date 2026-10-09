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

    [SettingPropertyGroup("Memory Sampler")]
    [SettingPropertyBool("Enable Memory Sampler", Order = 0, RequireRestart = false,
        HintText = "Writes memory telemetry to the TAOM debug log: a periodic [MemSample] line (process private/working-set MB, managed heap, system commit use/limit, available RAM), a one-shot WARN when system commit headroom runs low, and a [MemStation] line each time a screen opens or closes so growth can be attributed to a screen. This ONE switch governs all three. Independent of the master Battle Load Diagnostics toggle: this is session-wide crash forensics, not battle-load phase logging. Default ON.")]
    public bool EnableMemorySampler { get; set; } = true;

    [SettingPropertyGroup("Memory Sampler")]
    [SettingPropertyInteger("Sample Interval (seconds)", 10, 120, Order = 1, RequireRestart = false,
        HintText = "Seconds between [MemSample] lines. Default 30s (~120 lines per hour of play). Takes effect on the next sample — no restart needed.")]
    public int MemorySampleIntervalSeconds { get; set; } = 30;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Mission Frame-Time Heartbeat", Order = 0, RequireRestart = false,
        HintText = "Writes a [MissionPerf] line to the TAOM debug log every 5 seconds while a mission runs: frames, fps, average / p95 / max frame time in ms, agent and formation counts, garbage collections. This is the in-mission counterpart of [MemSample] and the measurement an AI or content change is judged against. Cost is one timestamp per frame. Default ON.")]
    public bool EnableMissionPerfHeartbeat { get; set; } = true;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Tick Profiler", Order = 1, RequireRestart = true,
        HintText = "Off by default. Adds per-type attribution to the hitch probe's lines: times every mission behaviour's tick, every behaviour's spawn callback and every scene script component by type, and lists the slowest. Installed once at game start: turning it on takes effect after a restart; turning it off stops it from the next mission.")]
    public bool EnableTickProfiler { get; set; } = false;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyInteger("Tick Profiler Top Behaviours", 1, 20, Order = 2, RequireRestart = false,
        HintText = "How many entries each [TickProfile] and [TickSummary] line lists (mission behaviours) and each [MapProfile] and [MapProfileSummary] line lists (map tick listeners and TAOM map views), slowest first. Default 8. Read at each mission start and each campaign session start.")]
    public int TickProfilerTopN { get; set; } = 8;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyInteger("Hitch Threshold (ms)", 50, 2000, Order = 3, RequireRestart = false,
        HintText = "A frame that takes this many milliseconds or more writes one [Hitch] line naming where the frame went, for the first 100 such frames of a mission; later ones are only counted. Below the battle's usual frame time, nearly every frame is a hitch. Default 250. Read at each mission start.")]
    public int HitchThresholdMs { get; set; } = 250;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Hitch Probe", Order = 4, RequireRestart = true,
        HintText = "On by default. Times the parts of every mission frame TAOM can see (the wait for the agent tick, the mission tick, scene scripts, agent spawns) and checks whether an animation clip is loading from disk, so each frame slower than the hitch threshold writes a hitch line and a detail line saying where the time went. Its measured cost is written to the TAOM debug log. Installed once at game start: turning it on takes effect after a restart; turning it off stops measuring from the next mission while 'Enable Tick Profiler' is off (the profiler measures through the probe's patches).")]
    public bool EnableHitchProbe { get; set; } = true;

    [SettingPropertyGroup("Mission Performance")]
    [SettingPropertyBool("Enable Animation Clip Memory Probe", Order = 10, RequireRestart = false,
        HintText = "Writes an [AnimMem] line to the TAOM debug log every 5 seconds while a mission runs: how much on-demand animation clip data the engine holds against its 12 MiB budget, whether a clip is loading from disk at the moment of a sample, and how often the total fell between one-second samples (a sign that clips were evicted), plus a summary when the mission ends. Read-only: it finds the two engine values once per game session by a signature check, then once a second reads the clip byte total and asks the engine whether a clip is loading; if the check fails on this game version it turns itself off and says why in the log. Takes effect at the next mission start. Default ON.")]
    public bool EnableAnimMemoryProbe { get; set; } = true;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Enable Map Profiler", Order = 0, RequireRestart = true,
        HintText = "Off by default. Times the campaign map's frame: the campaign tick, every per-frame tick-event listener by its owner, the map screen and TAOM's map views, and TAOM's application tick. Writes a [MapProfile] line to the TAOM debug log every 5 seconds while the map runs and a [MapProfileSummary] line when the campaign ends. Turning it on takes effect after a restart (the profiler is installed once, at game start); turning it off stops measuring from the next campaign session.")]
    public bool EnableMapProfiler { get; set; } = false;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Cull Hidden Settlement Nameplates", Order = 1, RequireRestart = false,
        HintText = "The campaign map updates every settlement nameplate every frame (about a thousand in TAOM), hidden ones included. When on, a nameplate that is hidden and cannot become visible this frame (out of height range, not tracked, not in range, nothing about its settlement changed) is left alone, which saves frame time on the map, most in fast forward. Everything else updates exactly as in the vanilla game. Takes effect on the next frame, so it can be switched with the map profiler on to compare the [MapProfile] lines. Default ON.")]
    public bool CullHiddenNameplates { get; set; } = true;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyBool("Release Map View Memory", Order = 2, RequireRestart = false,
        HintText = "In the measurement this feature is adapted from, every screen or menu that covered the campaign map left three colour targets and one depth target of the map view's old set behind (tens of megabytes of graphics memory per closed screen, more at a high resolution). That memory stays until a battle, or a town, village or arena scene, starts. When on, every Nth cover (see Map View Release Interval) has the map view release its graphics memory as you return to the map. The map then shows a short loading screen until its view is ready again, on one return in N (about 0.4 s instead of 0.08 s in that measurement). Takes effect at the next cover. Default ON.")]
    public bool ReleaseMapViewMemory { get; set; } = true;

    [SettingPropertyGroup("Map Performance")]
    [SettingPropertyInteger("Map View Release Interval", 1, 1000, Order = 3, RequireRestart = false,
        HintText = "Release the map view's memory on every Nth cover of the map. Default 20. A lower number gives memory back sooner but shows the short loading screen on more returns to the map (about 0.4 s in the measurement this feature is adapted from); 1 releases on every cover. Takes effect at the next cover.")]
    public int MapViewReleaseInterval { get; set; } = 20;

    [SettingPropertyGroup("Load-Time Stamps")]
    [SettingPropertyBool("Enable Load-Time Stamps", Order = 0, RequireRestart = false,
        HintText = "Writes the detailed load-time lines to the TAOM debug log: one [PatchApply] line per Harmony patch group with its apply time (written at game initialization), [LoadPhase] steps of TAOM's game start and game initialization, and [Lifecycle] lines timing every campaign handler of a new game, a loaded save and the session start (a line for each handler taking 10 ms or more, and a total per event). The per-type [LoadXml] lines, the patch phase totals and one [Lifecycle] dispatch line per campaign dispatch are always written. Costs a few microseconds per handler while a campaign loads and nothing during play. Default OFF.")]
    public bool EnableLoadTimeStamps { get; set; } = false;
}

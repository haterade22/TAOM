using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace TAOM.Features.CrashReport;

// Dedicated CrashReport MCM page. Default values match the "comprehensive capture"
// posture — every signal is on by default; players who hit perf issues can disable
// specific sections.
public sealed class CrashReportSettings : AttributeGlobalSettings<CrashReportSettings>
{
    public override string Id => "TAOM.CrashReport";
    public override string DisplayName => "TAOM — Crash Report";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    // --- Master ---

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Enable Crash Capture", Order = 0, RequireRestart = false,
        HintText = "Master toggle. When off, every TAOM crash finalizer passes exceptions straight through and the AppDomain hook ignores them, so the game's own handler takes over (BUTR's too, unless TAOM already suspended it this session; then restart). Takes effect immediately: the finalizers are always installed at launch and check this toggle only when an exception arrives. Default ON.")]
    public bool EnableCrashCapture { get; set; } = true;

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Suspend BUTR Exception Handler", Order = 1, RequireRestart = false,
        HintText = "On first capture, calls ButterLib.ExceptionHandlerSubSystem.Disable() so TAOM's report wins. Default ON.")]
    public bool SuspendButterLibHandler { get; set; } = true;

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Enable Native-to-Managed Capture", Order = 2, RequireRestart = false,
        HintText = "Logs and survives exceptions thrown inside a short list of native-to-managed callbacks (screen early, late and input ticks, scene-script ticks, thumbnail and tableau rendering, and mission combat callbacks for hits, blocks, missiles, charges, falls and agent removal) instead of crashing. When off, those exceptions pass straight through. Takes effect immediately. Default ON.")]
    public bool EnableNativeToManagedCapture { get; set; } = true;

    // --- Bundle ---

    [SettingPropertyGroup("Bundle")]
    [SettingPropertyBool("Write Crash Bundle ZIP", Order = 0, RequireRestart = false,
        HintText = "Produces Logs/taom_crash_<timestamp>_<sig>.zip containing report.txt, report.json, taom_debug.log, rgl_log.txt, manifest.txt. Default ON.")]
    public bool WriteCrashBundle { get; set; } = true;

    // --- QA ---

    [SettingPropertyGroup("QA — Dev Triggers")]
    [SettingPropertyBool("Throw On Next Mission Tick", Order = 0, RequireRestart = false,
        HintText = "QA only. Throws a tagged TaomDevTriggerException on the next Mission.Tick to exercise the crash capture pipeline. Auto-resets to OFF after firing.")]
    public bool ThrowOnNextMissionTick { get; set; }

    [SettingPropertyGroup("QA — Dev Triggers")]
    [SettingPropertyBool("Throw On Next Application Tick", Order = 1, RequireRestart = false,
        HintText = "QA only. Throws a tagged TaomDevTriggerException on the next Module.OnApplicationTick to exercise the capture pipeline without entering a mission. Auto-resets to OFF after firing.")]
    public bool ThrowOnNextApplicationTick { get; set; }
}

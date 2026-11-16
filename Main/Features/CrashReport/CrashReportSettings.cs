using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace TAOM.Features.CrashReport;

// Dedicated CrashReport MCM page. Default values match the "comprehensive capture"
// posture — every signal is on by default; players who hit perf issues can disable
// specific sections.
public sealed class CrashReportSettings : AttributeGlobalSettings<CrashReportSettings>
{
    // Hides MCM's GlobalSettings<T>.Instance: a dedicated server, where MCM is not loaded, reads
    // taom-server-settings.json (ServerSettingsSource); a client gets MCM's own. Name the base explicitly:
    // a bare Instance here would recurse.
    public static new CrashReportSettings? Instance =>
        ServerSettings.ServerSettingsSource.Get<CrashReportSettings>() ?? GlobalSettings<CrashReportSettings>.Instance;

    public override string Id => "TAOM.CrashReport";
    public override string DisplayName => "TAOM — Crash Report";
    public override string FolderName => "TAOM";
    public override string FormatType => "json2";

    // --- Master ---

    [SettingPropertyGroup("{=taom_mcm_group_master}Master")]
    [SettingPropertyBool("{=taom_mcm_crash_EnableCrashCapture}Enable Crash Capture", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_crash_EnableCrashCapture_hint}Master toggle. When off, every TAOM crash finalizer passes exceptions straight through and the AppDomain hook ignores them, so the game's own handler takes over (BUTR's too, unless TAOM already suspended it this session; then restart). Takes effect immediately: the finalizers are always installed at launch and check this toggle only when an exception arrives. Default ON.")]
    public bool EnableCrashCapture { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_master}Master")]
    [SettingPropertyBool("{=taom_mcm_crash_SuspendButterLibHandler}Suspend BUTR Exception Handler", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_crash_SuspendButterLibHandler_hint}On first capture, calls ButterLib.ExceptionHandlerSubSystem.Disable() so TAOM's report wins. Default ON.")]
    public bool SuspendButterLibHandler { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_master}Master")]
    [SettingPropertyBool("{=taom_mcm_crash_EnableNativeToManagedCapture}Enable Native-to-Managed Capture", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_crash_EnableNativeToManagedCapture_hint}Logs and survives exceptions thrown inside a short list of native-to-managed callbacks (screen early, late and input ticks, scene-script ticks, thumbnail and tableau rendering, and mission combat callbacks for hits, blocks, missiles, charges, falls and agent removal) instead of crashing. When off, those exceptions pass straight through. Takes effect immediately. Default ON.")]
    public bool EnableNativeToManagedCapture { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_master}Master")]
    [SettingPropertyBool("{=taom_mcm_crash_SurviveMissionStartFailures}Survive Mission Start Failures", Order = 3, RequireRestart = false,
        HintText = "{=taom_mcm_crash_SurviveMissionStartFailures_hint}When a mission behavior, a module's mission-start callback or a mission object throws in one of the six start calls of Mission.AfterStart while a mission is loading, log it, show a short message, cut that call short and let the mission start, instead of the game loading the mission again every frame for ever. A throw elsewhere in the load, such as a behavior's OnMissionScreenPreLoad, is not covered. Enable Crash Capture does not affect this setting. When off, the exception passes straight through as in the vanilla game. Takes effect immediately. Default ON.")]
    public bool SurviveMissionStartFailures { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_master}Master")]
    [SettingPropertyBool("{=taom_mcm_crash_SkeletonBufferGuard}Skeleton Buffer Guard", Order = 4, RequireRestart = true,
        HintText = "{=taom_mcm_crash_SkeletonBufferGuard_hint}The engine reserves room for the skeletons drawn in each frame in two per-frame buffers (65,536 and 262,144 entries) and never checks that the next one fits; one skeleton too many (about 2,340 in view for the first buffer) freezes the battle for good. When on, TAOM bounds-checks both buffers by rewriting 13 bytes of the engine at launch at each one, so a skeleton that would not fit is drawn from that buffer's first slots instead (its bones may look wrong for that frame). TAOM installs each check only where the exact engine bytes are found exactly once: a build that only moves the function is still patched, and a changed build gets nothing written. It also writes nothing for a buffer another module already guards, and guards the other one regardless. Takes effect at the next launch. Default ON.")]
    public bool SkeletonBufferGuard { get; set; } = true;

    [SettingPropertyGroup("{=taom_mcm_group_master}Master")]
    [SettingPropertyBool("{=taom_mcm_crash_SkeletonBufferWatch}Skeleton Buffer Watch", Order = 5, RequireRestart = false,
        HintText = "{=taom_mcm_crash_SkeletonBufferWatch_hint}Reads how full those two buffers get during every mission, not only battles, and writes the peaks to the log when the mission ends, with how many times each guard had to step in. If nothing guards the first buffer, also shows one warning at 90 percent full. Takes effect at the start of the next mission. Default ON.")]
    public bool SkeletonBufferWatch { get; set; } = true;

    // --- Bundle ---

    [SettingPropertyGroup("{=taom_mcm_group_bundle}Bundle")]
    [SettingPropertyBool("{=taom_mcm_crash_WriteCrashBundle}Write Crash Bundle ZIP", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_crash_WriteCrashBundle_hint}Produces Logs/taom_crash_<timestamp>_<sig>.zip containing report.txt, report.json, taom_debug.log, rgl_log.txt, manifest.txt. Default ON.")]
    public bool WriteCrashBundle { get; set; } = true;

    // --- Bug report ---

    [SettingPropertyGroup("{=taom_mcm_group_bug_report}Bug Report")]
    [SettingPropertyButton("{=taom_mcm_crash_MakeBugReportAction}Make Bug Report", Order = 0, RequireRestart = false, Content = "{=taom_mcm_crash_MakeBugReportAction_button}Make",
        HintText = "{=taom_mcm_crash_MakeBugReportAction_hint}Writes one zip in the Logs folder with what a bug report needs: this game's and the previous game's TAOM logs, the game's own logs, the game's crash folder from the previous game (files up to 64 MB each, 128 MB in all), the module list, the TAOM settings and, in a campaign, the campaign details. Then shows where the zip is. It carries your PC's name, your hero's name and folder paths of your install.")]
    public System.Action MakeBugReportAction { get; set; } = static () => BugReport.BugReportStartup.MakeAndShow("button");

    [SettingPropertyGroup("{=taom_mcm_group_bug_report}Bug Report")]
    [SettingPropertyBool("{=taom_mcm_crash_OfferBugReportAfterUnexpectedEnd}Offer A Bug Report After An Unexpected End", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_crash_OfferBugReportAfterUnexpectedEnd_hint}On the first main menu after a game that closed without TAOM's normal shutdown (a crash, a freeze you had to close, or a stopped game), ask whether to make a bug report from that game's logs. Default ON.")]
    public bool OfferBugReportAfterUnexpectedEnd { get; set; } = true;

    // --- QA ---

    [SettingPropertyGroup("{=taom_mcm_group_qa_dev_triggers}QA — Dev Triggers")]
    [SettingPropertyBool("{=taom_mcm_crash_ThrowOnNextMissionTick}Throw On Next Mission Tick", Order = 0, RequireRestart = false,
        HintText = "{=taom_mcm_crash_ThrowOnNextMissionTick_hint}QA only. Throws a tagged TaomDevTriggerException on the next Mission.Tick to exercise the crash capture pipeline. Auto-resets to OFF after firing.")]
    public bool ThrowOnNextMissionTick { get; set; }

    [SettingPropertyGroup("{=taom_mcm_group_qa_dev_triggers}QA — Dev Triggers")]
    [SettingPropertyBool("{=taom_mcm_crash_ThrowOnNextApplicationTick}Throw On Next Application Tick", Order = 1, RequireRestart = false,
        HintText = "{=taom_mcm_crash_ThrowOnNextApplicationTick_hint}QA only. Throws a tagged TaomDevTriggerException on the next Module.OnApplicationTick to exercise the capture pipeline without entering a mission. Auto-resets to OFF after firing.")]
    public bool ThrowOnNextApplicationTick { get; set; }

    [SettingPropertyGroup("{=taom_mcm_group_qa_dev_triggers}QA — Dev Triggers")]
    [SettingPropertyBool("{=taom_mcm_crash_ThrowOnNextMissionAfterStart}Throw On Next Mission AfterStart", Order = 2, RequireRestart = false,
        HintText = "{=taom_mcm_crash_ThrowOnNextMissionAfterStart_hint}QA only. Throws a tagged TaomDevTriggerException from a TAOM mission behavior's AfterStart on the next mission start, the #699 shape. Needs Enable Crash Capture on. With Survive Mission Start Failures on, the mission starts with that behavior's AfterStart cut short, and one message shows. With it off, the throw escapes once, TAOM's crash capture shows its crash dialog and writes a bundle, and the engine loads the mission again. Auto-resets to OFF after firing.")]
    public bool ThrowOnNextMissionAfterStart { get; set; }
}

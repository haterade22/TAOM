using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI;
using TAOM.Core.Logging;

namespace TAOM.Features.DevConsole.Hooks;

/// <summary>
/// Probe B of Patch79: once a tooltip is requested, did the engine actually build one, and if not,
/// at which stage did it fail?
///
/// <c>GauntletInformationView.OnShowTooltip</c> has THREE silent exits, all verified in the v1.4.8
/// decompile (lines 103-124), and none throws or logs anywhere a TAOM log can see:
///  1. the requested type is not in <c>RegisteredTypes</c>;
///  2. <c>Activator.CreateInstance(value.TooltipType, ...)</c> throws inside the try;
///  3. <c>LoadMovie</c> throws inside the same try, AFTER the view model was constructed.
/// Exits 2 and 3 share one catch that downgrades to <c>Debug.FailedAssert</c>, which reaches only
/// <c>rgl_log</c>. There is no <c>rgl_log</c> on this install, so all three are otherwise invisible.
///
/// The two private fields tell them apart from outside. <c>OnHideTooltip()</c> nulls both
/// <c>_dataSource</c> and <c>_movie</c> at the top of every call (lines 126-139), then the success
/// path assigns <c>_dataSource</c> at line 113 and <c>_movie</c> at line 114, only after
/// <c>LoadMovie</c> returns. So after the call: both set means built; <c>_dataSource</c> alone means
/// exit 3 (the data is fine, the prefab is not); neither means exit 1 or 2. An earlier revision read
/// only <c>_dataSource</c> and would have reported exit 3 as a success.
///
/// Pairs with Probe A. Probe A silent means the hover never arrived; Probe A firing plus a FAILED line
/// here names the failing stage; both quiet on a missing tooltip points past this layer entirely, at
/// rendering or the widget tree.
///
/// Rate limiting is per process: the sets are never reset, so a second campaign in the same process
/// reports only outcomes it has not already seen. Restart the game between diagnostic runs.
///
/// Diagnostic only, and deliberately scaffolding: remove it once the question is answered.
/// </summary>
[HarmonyPatch(typeof(GauntletInformationView), "OnShowTooltip")]
[HarmonyPatchCategory("Patch79_TooltipDiagnostics")]
public static class Patch79_GauntletInformationView_OnShowTooltip_Probe
{
    private static IModLogger? _logger;
    private static readonly TooltipProbeLog Seen = new TooltipProbeLog();

    // Resolved once at wiring time, never inside the postfix: this sits on a per-hover path and
    // harmony-patches.md bans uncached reflection there.
    private static FieldInfo? _dataSourceField;
    private static FieldInfo? _movieField;

    public static void Initialize(IModLogger? logger)
    {
        _logger = logger;
        _dataSourceField = AccessTools.Field(typeof(GauntletInformationView), "_dataSource");
        _movieField = AccessTools.Field(typeof(GauntletInformationView), "_movie");

        if (_dataSourceField == null || _movieField == null)
        {
            logger?.LogWarning(
                "[TooltipProbe] GauntletInformationView._dataSource or _movie did not resolve, so Probe B "
                + "cannot tell a built tooltip from a silently failed one and will stay silent. A field "
                + "was renamed by an engine update; re-verify against the installed DLLs before trusting "
                + "this probe's silence.");
        }
    }

    [HarmonyPostfix]
    public static void Postfix(GauntletInformationView? __instance, Type? type)
    {
        try
        {
            if (__instance == null || _dataSourceField == null || _movieField == null) return;

            var constructed = _dataSourceField.GetValue(__instance) != null;
            var loaded = _movieField.GetValue(__instance) != null;

            // A movie with no view model (registered type is not a TooltipBaseVM) renders broken, so
            // it is classified with the construction failures rather than as built.
            var outcome = constructed && loaded ? TooltipBuildOutcome.Built
                        : constructed ? TooltipBuildOutcome.ConstructedButMovieFailed
                        : TooltipBuildOutcome.NotConstructed;

            if (Seen.TryRecordBuild(type?.FullName, outcome, out var message))
            {
                if (outcome == TooltipBuildOutcome.Built) _logger?.LogInfo(message);
                else _logger?.LogWarning(message);
            }
        }
        catch
        {
            // A diagnostic must never be the reason a tooltip stops working.
        }
    }
}

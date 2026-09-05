using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TAOM.Core.Logging;

namespace TAOM.Features.DevConsole.Hooks;

/// <summary>
/// Probe A of Patch79: does a map bar hover reach the view model at all?
///
/// In v1.4.8 the bar's tooltips stopped being fixed properties on <c>MapInfoVM</c> and became list
/// items (<c>PrimaryInfoItems</c> / <c>SecondaryInfoItems</c> of <see cref="MapInfoItemVM"/>). The
/// <c>HintWidget</c> in the item template fires <c>ExecuteBeginHint</c> on the list element, and
/// Gauntlet resolves that by walking MapInfo to the collection to [index] to the method, invoking it
/// null-conditionally. A failed path resolution therefore does nothing at all: no exception, no log,
/// the values on the bar keep updating, and only the tooltip is missing. In 1.2.12 these were
/// separate bound properties and this failure mode did not exist.
///
/// So the absence of a line from this probe is itself the finding. Hover an icon; if nothing is
/// logged, the command never arrived and nothing downstream had a chance to run.
///
/// Rate limiting is per process: the set is never reset, so a second campaign in the same process
/// reports only items it has not already seen. Restart the game between diagnostic runs.
///
/// Diagnostic only, and deliberately scaffolding: remove it once the question is answered.
/// </summary>
[HarmonyPatch(typeof(MapInfoItemVM), nameof(MapInfoItemVM.ExecuteBeginHint))]
[HarmonyPatchCategory("Patch79_TooltipDiagnostics")]
public static class Patch79_MapInfoItemVM_ExecuteBeginHint_Probe
{
    private static IModLogger? _logger;
    private static readonly TooltipProbeLog Seen = new TooltipProbeLog();

    public static void Initialize(IModLogger? logger) => _logger = logger;

    [HarmonyPostfix]
    public static void Postfix(MapInfoItemVM? __instance)
    {
        // Runs on every hover of every bar item, so everything here is cheap and rate limited.
        // A throw would take the vanilla hover path down with it, hence the blanket catch.
        try
        {
            if (Seen.TryRecordHover(__instance?.ItemId, out var message))
                _logger?.LogInfo(message);
        }
        catch
        {
            // A diagnostic must never be the reason a tooltip stops working.
        }
    }
}

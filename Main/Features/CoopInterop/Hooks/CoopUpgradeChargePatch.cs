using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.CoopInterop.Hooks;

/// <summary>
/// Patch144, #967 C2: runs after BannerlordCoop's server-side
/// <c>GameInterface.Services.Party.Handlers.PartyDoneLogicHandler.ApplyUpgradedTroopHistory</c>
/// (internal class, private static method, bound by reflection) and charges <c>upgrade_cost</c> to the
/// player whose party screen the server just applied. Coop's handler still applies prisoner recruitment
/// effects and the roster order after this call, so nothing may escape the postfix. Applied only when Coop's type is
/// present. SubModule hands in the campaign's earning service each campaign start. Notes:
/// docs/reference/harmony-patch-registry.md "Patch144_CoopUpgradeCharge".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(PatchCategory)]
public static class CoopUpgradeChargePatch
{
    internal const string PatchCategory = "Patch144_CoopUpgradeCharge";
    internal const string CheckId = "taom.coop.upgrade-charge";
    private const string TargetTypeName = "GameInterface.Services.Party.Handlers.PartyDoneLogicHandler";
    private const string TargetMethodName = "ApplyUpgradedTroopHistory";

    private static ISpecialResourceEventAdapter? _events;
    private static SpecialResourceEarningService? _earning;

    internal static void Initialize(ISpecialResourceEventAdapter? events, SpecialResourceEarningService? earning)
    {
        _events = events;
        _earning = earning;
    }

    [HarmonyPrepare]
    public static bool Prepare()
    {
        if (TargetMethod() != null) return true;

        // Co-op is loaded but the method moved: say so once, since the patch then silently never applies.
        if (CoopPresence.IsActive)
        {
            try
            {
                IoC.Resolve<IModLogger>().LogWarning(
                    $"[CoopUpgradeCharge] Patch144 not applied: '{TargetTypeName}.{TargetMethodName}' was not found; " +
                    "if this is BannerlordCoop, its API may have changed. A player's upgrade_cost will not be charged.");
            }
            catch { /* swallow-ok: logging must never fail patch preparation */ }
        }
        return false;
    }

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
        => ReflectionUtils.FindTypeAcrossLoadedAssemblies(TargetTypeName)?.GetMethod(
            TargetMethodName,
            BindingFlags.NonPublic | BindingFlags.Static);

    [HarmonyPostfix]
    public static void Postfix(Hero __0, List<Tuple<CharacterObject, CharacterObject, int>> __1)
    {
        if (_events == null || _earning == null) return;

        try
        {
            var upgrades = ToUpgradeLines(__1);
            if (upgrades.Count == 0) return;

            var upgrader = _events.DescribeHero(__0?.StringId);
            var charged = _earning.ChargeCoopUpgrades(upgrader, upgrades);
            if (charged > 0f)
                Outcomes.Pass(CheckId, "", FormattableString.Invariant($"hero={upgrader.HeroId} charged={charged:0.##}"));
        }
        catch (Exception ex)
        {
            Outcomes.Fail(CheckId, "", ex.GetType().Name);
            Outcomes.Swallowed(nameof(CoopUpgradeChargePatch), nameof(Postfix), ex);
        }
    }

    // Each (from, to, count) line becomes the target troop's id and the count, the row Patch26 queues.
    internal static List<CoopUpgradeLine> ToUpgradeLines(List<Tuple<CharacterObject, CharacterObject, int>>? history)
    {
        var lines = new List<CoopUpgradeLine>();
        if (history == null) return lines;

        foreach (var upgrade in history)
        {
            var targetId = upgrade?.Item2?.StringId;
            if (targetId != null && upgrade!.Item3 > 0)
                lines.Add(new CoopUpgradeLine(targetId, upgrade.Item3));
        }
        return lines;
    }
}

using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.SmartCavalryAI.Models;

namespace TAOM.Features.SmartCavalryAI.Hooks;

/// <summary>
/// Postfix on <c>Formation.SetTargetFormation(Formation)</c>. The player's targeted charge is
/// <c>OrderController.SetOrderWithFormation(Charge, target)</c> (OrderController.cs:812-817,
/// v1.4.8): a plain Charge first, then the target. By the time the target arrives, Patch31 has
/// already started a cycle at the NEAREST enemy, so this postfix re-points that cycle at the
/// formation the player chose through <see cref="ICavalryChargeService.RetargetCycle"/>.
///
/// <para>Every <c>SetMovementOrder</c> ends with <c>SetTargetFormation(null)</c> (Formation.cs:714)
/// and <c>MovementOrder.OnApply(ChargeToTarget)</c> sets the target under our own writes; nulls are
/// ignored and our writes arrive under <see cref="SmartCavalryRecursionGuard"/>. An AI-controlled
/// formation is left alone (Patch31 has already cancelled its cycle on the order that preceded this
/// call). <c>Advance</c> with a target names its formation the same way, but the Advance itself is a
/// non-charge order that cancelled the cycle first, so the formation is Idle and nothing happens.</para>
/// </summary>
[HarmonyPatch(typeof(Formation), nameof(Formation.SetTargetFormation), new[] { typeof(Formation) })]
// Shares Patch31's deferred category so the pair is applied together, once, from
// OnMissionBehaviorInitialize; this target has no Mission-dependent cctor of its own.
[HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
public static class Patch31b_FormationSetTargetFormation
{
    private static ISmartCavalryAISettingsProvider? _settings;
    private static ICavalryChargeService? _service;
    private static IBattlefieldQueryAdapter? _battlefield;

    private static ISmartCavalryAISettingsProvider Settings => _settings ??= IoC.Resolve<ISmartCavalryAISettingsProvider>();
    private static ICavalryChargeService Service => _service ??= IoC.Resolve<ICavalryChargeService>();
    private static IBattlefieldQueryAdapter Battlefield => _battlefield ??= IoC.Resolve<IBattlefieldQueryAdapter>();

    [HarmonyPostfix]
    public static void Postfix(Formation __instance, Formation targetFormation)
    {
        if (SmartCavalryRecursionGuard.IsSuppressed) return;
        if (__instance == null || targetFormation == null || targetFormation.CountOfUnits == 0) return;

        var team = Mission.Current?.PlayerTeam;
        if (team == null || __instance.Team != team) return;
        if (__instance.IsAIControlled) return;

        var settings = Settings;
        if (!settings.IsEnabled) return;
        if (Service.GetState(__instance) == CavalryState.Idle) return;

        var position = targetFormation.CurrentPosition;
        Service.RetargetCycle(
            new FormationAdapter(__instance),
            new CavalryCommandAdapter(__instance),
            Battlefield,
            targetFormation,
            new Vec3(position.x, position.y, 0f, -1f),
            Mission.Current?.CurrentTime ?? 0f);
    }
}

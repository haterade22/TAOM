using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SmartCavalryAI.Models;

namespace TAOM.Features.SmartCavalryAI.Hooks;

/// <summary>
/// Prefix + postfix on <c>Formation.SetMovementOrder</c>. A player-issued <c>Charge</c> or
/// <c>ChargeToTarget</c> on a player-team cavalry formation hands control to
/// <see cref="ICavalryChargeService.HandleChargeOrder"/>. Any OTHER order on a formation the
/// machine is driving, and any order at all on a formation the team AI commands
/// (<c>Formation.IsAIControlled</c>: F6 delegation, an enlisted battle, a dead player), cancels
/// the machine for that formation so the newer order stands, toggle or no toggle (#586).
///
/// <para>Who calls the target (v1.4.8, 63 call sites): <c>OrderController.SetOrder</c> and
/// <c>SetOrderWithFormation</c> for the player (a targeted charge is a plain Charge followed by
/// <c>SetTargetFormation(target)</c>, OrderController.cs:812-817, which
/// <c>Patch31b_FormationSetTargetFormation</c> picks up); the team AI's behaviours, gated on
/// <c>IsAIControlled</c> in <c>FormationAI.TickOccasionally</c> (FormationAI.cs:284-290); the
/// engine's own resets (an <c>Invalid</c> input becomes Stop, Formation.cs:688-691; <c>Team.Tick</c>
/// issues Retreat to a routed side); identical re-issues (see <see cref="Prefix"/>); and
/// <c>Formation.Tick</c>'s substitute loop (Formation.cs:2291-2295), which issues a plain Charge
/// when a ChargeToTarget's target empties. That last one is not under
/// <see cref="SmartCavalryRecursionGuard"/>, so it re-enters here as a charge order and lands in the
/// service's "charge now" path, the same re-target its own tick performs. Orders this feature issues
/// itself arrive under the guard and are skipped.</para>
///
/// <para>Compares the enum members (<c>MovementOrderEnum.Charge</c> = 2, <c>ChargeToTarget</c> = 3
/// on v1.4.8), never int casts. The values are pinned by <c>SmartCavalryAIBindingTests</c>.</para>
/// </summary>
// Explicit parameter type array matches the Patch35 sibling: Formation.SetMovementOrder has one
// overload today, and a second one would otherwise make the attribute ambiguous.
[HarmonyPatch(typeof(Formation), nameof(Formation.SetMovementOrder), new[] { typeof(MovementOrder) })]
// Shared with Patch35_Formation_SetMovementOrder and Patch31b. The category is applied from
// OnMissionBehaviorInitialize (one-shot guarded) because MovementOrder.cctor reads
// Mission.Current.CurrentTime, which is null during OnSubModuleLoad / OnGameInitializationFinished.
[HarmonyPatchCategory("Patch_MissionTime_SetMovementOrder")]
public static class Patch31_FormationSetMovementOrder
{
    // A re-issue of a Move carries the very same struct; a player click on "the same spot" does not
    // land within a centimetre of it.
    private const float ReissuePositionToleranceSquared = 0.0001f;

    private static ISmartCavalryAISettingsProvider? _settings;
    private static ICavalryChargeService? _service;
    private static IBattlefieldQueryAdapter? _battlefield;
    private static IModLogger? _logger;

    private static ISmartCavalryAISettingsProvider Settings => _settings ??= IoC.Resolve<ISmartCavalryAISettingsProvider>();
    private static ICavalryChargeService Service => _service ??= IoC.Resolve<ICavalryChargeService>();
    private static IBattlefieldQueryAdapter Battlefield => _battlefield ??= IoC.Resolve<IBattlefieldQueryAdapter>();
    private static IModLogger Logger => _logger ??= IoC.Resolve<IModLogger>();

    /// <summary>Captures the order in force BEFORE this call so the postfix can tell a new order
    /// from the engine re-issuing the current one. <c>BannerBearerLogic.FormationBannerController
    /// .RepositionFormation</c> re-applies <c>GetReadonlyMovementOrderReference()</c> whenever a
    /// bearer dies or is reassigned; <c>Formation.CopyOrdersFrom</c> and <c>DeploymentHandler</c>
    /// have the same shape. None of those is player intent, so none may cancel a cycle.</summary>
    [HarmonyPrefix]
    public static void Prefix(Formation __instance, out MovementOrder __state)
    {
        __state = __instance != null ? __instance.GetReadonlyMovementOrderReference() : default;
    }

    [HarmonyPostfix]
    public static void Postfix(Formation __instance, MovementOrder input, MovementOrder __state)
    {
        if (SmartCavalryRecursionGuard.IsSuppressed) return;
        if (__instance == null) return;

        // PlayerTeam null in spectator/custom-battle missions; bail before any IoC resolve.
        var team = Mission.Current?.PlayerTeam;
        if (team == null || __instance.Team != team) return;

        // Ownership and displacement are honoured whether or not the toggle is on: only STARTING
        // a cycle needs the feature enabled.
        if (__instance.IsAIControlled)
        {
            CancelIfActive(__instance);
            return;
        }
        var isChargeOrder = input.OrderEnum == MovementOrder.MovementOrderEnum.Charge
            || input.OrderEnum == MovementOrder.MovementOrderEnum.ChargeToTarget;
        if (!isChargeOrder)
        {
            if (IsReissue(__instance, in __state, in input)) return;
            CancelIfActive(__instance);
            return;
        }

        var settings = Settings;
        if (!settings.IsEnabled) return;

        var cav = new FormationAdapter(__instance);
        if (!cav.RepresentativeIsCavalry) return;

        // ChargeToTarget names its formation; a plain Charge means the nearest enemy formation
        // (a player's targeted charge names its target one call later, see Patch31b).
        object? target;
        Vec2 targetPosition;
        var named = input.OrderEnum == MovementOrder.MovementOrderEnum.ChargeToTarget ? input.TargetFormation : null;
        if (named != null && named.CountOfUnits > 0)
        {
            target = named;
            targetPosition = named.CurrentPosition;
        }
        else if (!Battlefield.TryGetNearestEnemyFormation(__instance, out target, out targetPosition) || target == null)
        {
            return;
        }

        var commands = new CavalryCommandAdapter(__instance);
        var time = Mission.Current?.CurrentTime ?? 0f;
        Service.HandleChargeOrder(cav, commands, Battlefield, target, new Vec3(targetPosition.x, targetPosition.y, 0f, -1f), time);

        if (settings.IsDebugMode)
        {
            Logger.LogInfo($"[SmartCavalryAI] {__instance.FormationIndex} ordered {input.OrderEnum} -> state={Service.GetState(__instance)}");
        }
    }

    /// <summary>Same kind of order as before, and for a Move the very same spot: engine housekeeping,
    /// not a new order from the player. The engine's own test allows a metre for Move, which a player
    /// click could fall inside; a re-issue carries the identical position, so a centimetre is enough.</summary>
    private static bool IsReissue(Formation formation, in MovementOrder previous, in MovementOrder input)
    {
        if (previous.OrderEnum != input.OrderEnum) return false;
        if (input.OrderEnum != MovementOrder.MovementOrderEnum.Move)
        {
            return previous.AreOrdersPracticallySame(previous, input, isAIControlled: true);
        }
        return previous.GetPosition(formation).DistanceSquared(input.GetPosition(formation)) < ReissuePositionToleranceSquared;
    }

    // GetState is one lock and one dictionary lookup; only a formation mid-cycle pays more.
    private static void CancelIfActive(Formation formation)
    {
        if (Service.GetState(formation) != CavalryState.Idle) Service.CancelCharge(formation);
    }
}

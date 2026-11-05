using System;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MarshalsWord.Hooks;

/// <summary>
/// Patch141 (docs/features/marshals-word.md, build-spec 5.2): one prefix on each of the six public virtual methods through which a player
/// order reaches <c>OrderController</c>. While the queue key or the mark key is down with the order panel open, an order of the player's own
/// controller that is on the allow list is stored as a step (queue key), or a charge on an enemy company is stored as a mark (mark key), and
/// vanilla does not run it. Everything else returns true at the first test. The
/// prefixes only read (they run in the screen tick); the step starts later, in <c>OnMissionTick</c>. The registry section holds the callers.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(MarshalsWordModule.PatchCategory)]
public static class MarshalOrderCapturePatch
{
    private static bool Armed(OrderController instance) =>
        MarshalHooks.IsLive && (MarshalHooks.QueueArmed || MarshalHooks.MarkArmed) && instance == MarshalHooks.PlayerController;

    private static bool Offer(MarshalOrderPayload? payload) => payload != null && MarshalHooks.TryCapture(payload);

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SetOrder))]
    [HarmonyPrefix]
    public static bool Prefix(OrderController __instance, OrderType orderType)
    {
        return !Armed(__instance) || !Offer(MarshalOrderPayload.ForOrder(orderType));
    }

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SetOrderWithAgent))]
    [HarmonyPrefix]
    public static bool Prefix(OrderController __instance, OrderType orderType, Agent agent)
    {
        return !Armed(__instance) || !Offer(MarshalOrderPayload.ForAgent(orderType));
    }

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SetOrderWithPosition))]
    [HarmonyPrefix]
    public static bool Prefix(OrderController __instance, OrderType orderType, WorldPosition orderPosition)
    {
        return !Armed(__instance) || !Offer(MarshalOrderPayload.ForPosition(orderType, orderPosition));
    }

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SetOrderWithFormation))]
    [HarmonyPrefix]
    public static bool Prefix(OrderController __instance, OrderType orderType, Formation orderFormation)
    {
        return !Armed(__instance) || !Offer(MarshalOrderPayload.ForFormation(orderType, orderFormation));
    }

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SetOrderWithTwoPositions))]
    [HarmonyPrefix]
    public static bool Prefix(OrderController __instance, OrderType orderType, WorldPosition position1, WorldPosition position2)
    {
        return !Armed(__instance) || !Offer(MarshalOrderPayload.ForTwoPositions(orderType, position1, position2));
    }

    [HarmonyPatch(typeof(OrderController), nameof(OrderController.SetOrderWithOrderableObject))]
    [HarmonyPrefix]
    public static bool Prefix(OrderController __instance, IOrderable target)
    {
        return !Armed(__instance) || !Offer(Orderable(__instance, target));
    }

    private static MarshalOrderPayload? Orderable(OrderController instance, IOrderable target)
    {
        try
        {
            return MarshalOrderPayload.ForOrderable(target, instance.Team.Side);
        }
        catch (Exception exception)
        {
            Outcomes.Swallowed("MarshalsWord", "MarshalOrderCapturePatch.Orderable", exception);
            return null;
        }
    }
}

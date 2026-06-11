using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// The bite actions the spider BT plays, resolved once (ActionIndexCache.Create resolves the index
/// EAGERLY in v1.4.5 — no lazy retry). Shared by SpiderAttackService (play, via the config names) and the
/// engage gate (Index comparison — zero-alloc, no per-eval native GetName() marshal). The names live in
/// the EXTERNAL LOTRLOME_Armory action_types.xml; SpiderMissionBehavior.Initialize logs an error if any
/// resolved to act_none (the bad-name → channel-0 locomotion-kill "slide" class that shipped once on the
/// elephant — see docs/features/elephant.md "Action code correction").
/// </summary>
internal static class SpiderAttackActions
{
    internal static readonly ActionIndexCache BiteStand = ActionIndexCache.Create(SpiderConfig.BiteStandActionName);
    internal static readonly ActionIndexCache BiteCharge = ActionIndexCache.Create(SpiderConfig.BiteChargeActionName);

    /// <summary>True when <paramref name="current"/> is one of the BT's own bite clips (Index compare).</summary>
    internal static bool IsSpiderAttack(ActionIndexCache current)
        => current == BiteStand || current == BiteCharge;

    /// <summary>True when any bite name failed to resolve against the loaded action data (Armory drift).</summary>
    internal static bool AnyUnresolved()
        => BiteStand == ActionIndexCache.act_none
        || BiteCharge == ActionIndexCache.act_none;
}

using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider.BehaviorTreeElements;

/// <summary>
/// The attack clips the spider BT plays, resolved once (ActionIndexCache.Create resolves the index EAGERLY in
/// v1.4.5 — no lazy retry). Shared by SpiderAttackService (play, via <see cref="ForName"/>) and the engage gate
/// (Index comparison — zero-alloc, no per-eval native GetName() marshal). The names live in the EXTERNAL
/// LOTRLOME_Armory action_types.xml; SpiderMissionBehavior.Initialize logs an error if any resolved to act_none
/// (the bad-name → channel-0 locomotion-kill "slide" class that shipped once on the elephant — see
/// docs/features/elephant.md "Action code correction").
/// </summary>
internal static class SpiderAttackActions
{
    internal static readonly ActionIndexCache PounceFront = ActionIndexCache.Create(SpiderConfig.PounceFrontActionName);
    internal static readonly ActionIndexCache PounceCharge = ActionIndexCache.Create(SpiderConfig.PounceChargeActionName);
    internal static readonly ActionIndexCache SwingLeft = ActionIndexCache.Create(SpiderConfig.SwingLeftActionName);
    internal static readonly ActionIndexCache SwingRight = ActionIndexCache.Create(SpiderConfig.SwingRightActionName);

    /// <summary>The eager cache for a clip name (single source of truth: the service's SelectActionName picks the name).</summary>
    internal static ActionIndexCache ForName(string name)
        => name == SpiderConfig.PounceFrontActionName ? PounceFront
        : name == SpiderConfig.PounceChargeActionName ? PounceCharge
        : name == SpiderConfig.SwingLeftActionName ? SwingLeft
        : name == SpiderConfig.SwingRightActionName ? SwingRight
        : ActionIndexCache.act_none;

    /// <summary>True when <paramref name="current"/> is one of the BT's own attack clips (Index compare — anti-chain gate).</summary>
    internal static bool IsSpiderAttack(ActionIndexCache current)
        => current == PounceFront || current == PounceCharge || current == SwingLeft || current == SwingRight;

    /// <summary>True when any attack name failed to resolve against the loaded action data (Armory drift).</summary>
    internal static bool AnyUnresolved()
        => PounceFront == ActionIndexCache.act_none
        || PounceCharge == ActionIndexCache.act_none
        || SwingLeft == ActionIndexCache.act_none
        || SwingRight == ActionIndexCache.act_none;
}

using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Mumakil.BehaviorTreeElements;

/// <summary>
/// The attack actions the Mûmakil BT plays, resolved once (ActionIndexCache.Create resolves the index EAGERLY in
/// v1.4.5 — no lazy retry). These are the ELEPHANT's clips (act_elephant_attack_*): the Mûmakil shares the
/// as_elephant action set, so its attacks reuse the elephant clips. Shared by the tasks (play) and the engage
/// gate (Index comparison — zero-alloc, no per-eval native GetName() marshal). The names live in the EXTERNAL
/// LOTRLOME_Armory action_types.xml; <c>MumakilMissionBehavior.Initialize</c> logs an error if any resolved to
/// act_none (the bad-name → channel-0 locomotion-kill "slide" failure class).
/// </summary>
internal static class MumakilAttackActions
{
    internal static readonly ActionIndexCache Trample = ActionIndexCache.Create(MumakilConfig.TrampleActionName);
    internal static readonly ActionIndexCache TrampleAlt = ActionIndexCache.Create(MumakilConfig.TrampleAltActionName);
    internal static readonly ActionIndexCache SwingLeft = ActionIndexCache.Create(MumakilConfig.SideAttackLeftActionName);
    internal static readonly ActionIndexCache SwingRight = ActionIndexCache.Create(MumakilConfig.SideAttackRightActionName);

    /// <summary>True when <paramref name="current"/> is one of the BT's own attack clips (Index compare).</summary>
    internal static bool IsMumakilAttack(ActionIndexCache current)
        => current == Trample || current == TrampleAlt || current == SwingLeft || current == SwingRight;

    /// <summary>True when any attack name failed to resolve against the loaded action data (Armory drift).</summary>
    internal static bool AnyUnresolved()
        => Trample == ActionIndexCache.act_none
        || TrampleAlt == ActionIndexCache.act_none
        || SwingLeft == ActionIndexCache.act_none
        || SwingRight == ActionIndexCache.act_none;
}

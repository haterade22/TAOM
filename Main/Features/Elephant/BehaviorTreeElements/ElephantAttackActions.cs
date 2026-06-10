using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Elephant.BehaviorTreeElements;

/// <summary>
/// The three attack actions the elephant BT plays, resolved once (ActionIndexCache.Create resolves the index
/// EAGERLY in v1.4.5 — no lazy retry). Shared by the tasks (play) and the engage gate (Index comparison —
/// zero-alloc, no per-eval native GetName() marshal, immune to other action names containing "attack").
/// The names live in the EXTERNAL LOTRLOME_Armory action_types.xml; ElephantMissionBehavior.Initialize
/// logs an error if any resolved to act_none (the bad-name → channel-0 locomotion-kill "slide" failure
/// shipped once in this feature — see docs/features/elephant.md "Action code correction").
/// </summary>
internal static class ElephantAttackActions
{
    internal static readonly ActionIndexCache Trample = ActionIndexCache.Create(ElephantConfig.TrampleActionName);
    internal static readonly ActionIndexCache TrampleAlt = ActionIndexCache.Create(ElephantConfig.TrampleAltActionName);
    internal static readonly ActionIndexCache SwingLeft = ActionIndexCache.Create(ElephantConfig.SideAttackLeftActionName);
    internal static readonly ActionIndexCache SwingRight = ActionIndexCache.Create(ElephantConfig.SideAttackRightActionName);

    /// <summary>True when <paramref name="current"/> is one of the BT's own attack clips (Index compare).</summary>
    internal static bool IsElephantAttack(ActionIndexCache current)
        => current == Trample || current == TrampleAlt || current == SwingLeft || current == SwingRight;

    /// <summary>True when any attack name failed to resolve against the loaded action data (Armory drift).</summary>
    internal static bool AnyUnresolved()
        => Trample == ActionIndexCache.act_none
        || TrampleAlt == ActionIndexCache.act_none
        || SwingLeft == ActionIndexCache.act_none
        || SwingRight == ActionIndexCache.act_none;
}

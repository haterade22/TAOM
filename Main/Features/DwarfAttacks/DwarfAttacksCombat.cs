using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DwarfAttacks;

/// <summary>
/// The attacks' engine handles, resolved on first touch (mission code only; a test never touches this type, because
/// resolving an action name is a native call).
/// </summary>
internal static class DwarfAttacksCombat
{
    /// <summary>act_none when LOTRLOME_Armory's action_types.xml lost the action (checked at mission start).</summary>
    internal static readonly ActionIndexCache HeadButt = ActionIndexCache.Create(DwarfAttacksConfig.HeadButtActionName);

    internal static readonly ActionIndexCache DropKick = ActionIndexCache.Create(DwarfAttacksConfig.DropKickActionName);

    /// <summary>
    /// The troll smash's start flags (TrollBruteForceCombat): priority 60 outranks the AI's own attack (10), kick (33)
    /// and parry (15) starts while a hit reaction (80) or death (95) still cuts in; enforce-all and lock-movement match
    /// the clips' own flags, so the AI's upper-body and movement channels cannot blend over the attack.
    /// </summary>
    internal const AnimFlags StartFlags =
        AnimFlags.amf_priority_reload | AnimFlags.anf_enforce_all | AnimFlags.anf_lock_movement;

    internal static ActionIndexCache For(DwarfAttack attack) => attack == DwarfAttack.HeadButt ? HeadButt : DropKick;

    /// <summary>False when the Armory lost the action: playing act_none would clear the dwarf's channel 0.</summary>
    internal static bool IsResolved(DwarfAttack attack) => For(attack) != ActionIndexCache.act_none;

    /// <summary>An unresolved action never counts, or an empty channel 0 would read as "already attacking".</summary>
    internal static bool IsOurs(ActionIndexCache action) =>
        action != ActionIndexCache.act_none && (action == HeadButt || action == DropKick);

    /// <summary>
    /// The enemies either attack may target or hit, shared by the ready decorator and the impact so an enemy the impact
    /// would skip never starts an attack: active, not fading or invulnerable, on foot (neither clip reaches a rider) and
    /// hostile. The height test is the service's.
    /// </summary>
    internal static bool IsCandidate(Agent dwarf, Agent? other) =>
        other != null
        && !ReferenceEquals(other, dwarf)
        && other.IsActive()
        && !other.IsFadingOut()
        && other.CurrentMortalityState != Agent.MortalityState.Invulnerable
        && !other.IsMount
        && !other.HasMount
        && other.IsEnemyOf(dwarf);
}

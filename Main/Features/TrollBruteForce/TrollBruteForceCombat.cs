using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// The smash's engine handles, resolved on first touch (mission code only; a test never touches this type,
/// because resolving an action name is a native call).
/// </summary>
internal static class TrollBruteForceCombat
{
    /// <summary>act_none when LOTRLOME_Armory's action_types.xml lost the action (checked at mission start).</summary>
    internal static readonly ActionIndexCache Action = ActionIndexCache.Create(TrollBruteForceConfig.ActionName);

    /// <summary>
    /// Priority 60 (the reload band) outranks the AI's own attack (10), kick (33) and parry (15) starts, while a
    /// hit reaction (80) or death (95) still cuts in (v1.5.3 AnimFlags.cs:14-31). Enforce-all and lock-movement
    /// match the clip's own metadata, so the AI's upper-body and movement channels cannot blend over the smash.
    /// </summary>
    internal const AnimFlags StartFlags =
        AnimFlags.amf_priority_reload | AnimFlags.anf_enforce_all | AnimFlags.anf_lock_movement;
}

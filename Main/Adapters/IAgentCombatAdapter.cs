using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Read-only view of an Agent's mission-time combat state, used by the BattleActionBar
/// composition analyzer. Wraps <c>Agent</c>'s sealed type per ADR-007. This is the
/// mission-time complement to <see cref="IHeroCombatAdapter"/> (which is campaign-time).
/// </summary>
public interface IAgentCombatAdapter
{
    /// <summary>Stable agent index for the mission (per <c>Agent.Index</c>).</summary>
    int AgentIndex { get; }

    bool HasMount { get; }
    bool HasShield { get; }

    /// <summary>True when the agent's <c>CharacterObject.IsRanged</c> is true.</summary>
    bool IsRanged { get; }

    /// <summary>Returns the weapon class for weapon slot 0..3, or null if the slot is empty.</summary>
    WeaponClass? GetPrimaryWeaponClass(int weaponSlot);
}

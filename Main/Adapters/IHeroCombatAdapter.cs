namespace TAOM.Adapters;

/// <summary>
/// Read-only view of a Hero's combat-relevant state for CompanionTactics role detection.
/// Used by <c>ICompanionRoleService</c> to classify heroes by equipment without leaking
/// the sealed <c>Hero</c> / <c>Equipment</c> types past the service boundary (ADR-007).
/// </summary>
public interface IHeroCombatAdapter
{
    /// <summary>Stable hero identifier (e.g., <c>Hero.StringId</c>) — used as a cache key.</summary>
    string StringId { get; }

    bool HasMount { get; }
    bool HasShield { get; }

    /// <summary>Snapshot of the equipment the adapter was built from (BattleEquipment by default).</summary>
    IBattleEquipmentSnapshot Equipment { get; }
}

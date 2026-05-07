using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Read-only snapshot of a hero's battle Equipment captured at a point in time.
/// Equipment is a sealed indexer-shaped struct in TaleWorlds; this snapshot
/// extracts the four weapon-slot WeaponClass values + has-shield + has-mount
/// flags so role detection logic can run against an opaque, mockable surface
/// (no sealed types cross the service boundary — ADR-007).
/// </summary>
public interface IBattleEquipmentSnapshot
{
    /// <summary>Returns the weapon class for slot 0..3, or null if the slot is empty / not a weapon.</summary>
    WeaponClass? GetWeaponClass(int slot);

    bool HasShield { get; }
    bool HasMount { get; }
}

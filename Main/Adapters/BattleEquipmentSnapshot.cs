using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Concrete implementation of <see cref="IBattleEquipmentSnapshot"/>. Captures the four
/// weapon-slot WeaponClass values + has-shield + has-mount at construction from a sealed
/// <c>Equipment</c> struct so the sealed type never leaves the adapter (ADR-007).
/// </summary>
public sealed class BattleEquipmentSnapshot : IBattleEquipmentSnapshot
{
    private readonly WeaponClass?[] _weaponClasses;

    public bool HasShield { get; }
    public bool HasMount { get; }

    public BattleEquipmentSnapshot(Equipment equipment)
    {
        _weaponClasses = new WeaponClass?[4];
        if (equipment == null)
        {
            HasShield = false;
            HasMount = false;
            return;
        }

        var hasShield = false;
        for (var i = 0; i < 4; i++)
        {
            var slot = equipment[(EquipmentIndex)i];
            if (slot.IsEmpty) continue;
            var item = slot.Item;
            if (item == null) continue;

            if (item.PrimaryWeapon != null)
                _weaponClasses[i] = item.PrimaryWeapon.WeaponClass;

            if (item.ItemType == ItemObject.ItemTypeEnum.Shield)
                hasShield = true;
        }

        HasShield = hasShield;

        var horse = equipment[EquipmentIndex.Horse];
        HasMount = !horse.IsEmpty;
    }

    public WeaponClass? GetWeaponClass(int slot)
    {
        if (slot < 0 || slot >= _weaponClasses.Length) return null;
        return _weaponClasses[slot];
    }
}

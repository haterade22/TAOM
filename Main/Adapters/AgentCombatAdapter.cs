using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// Concrete implementation of <see cref="IAgentCombatAdapter"/>. Wraps a sealed
/// <see cref="Agent"/> for the BattleActionBar's per-agent inspection. Reads from
/// <c>Agent.SpawnEquipment</c> (the deterministic equipment snapshot) rather than
/// the per-frame mutable <c>Agent.Equipment</c> (MissionEquipment) — composition
/// detection only cares about the stable spawn loadout (ADR-007).
/// </summary>
public sealed class AgentCombatAdapter : IAgentCombatAdapter
{
    private readonly Agent _agent;

    public AgentCombatAdapter(Agent agent)
    {
        _agent = agent;
    }

    public int AgentIndex => _agent?.Index ?? -1;
    public bool HasMount => _agent != null && _agent.HasMount;

    public bool IsRanged
    {
        get
        {
            var character = _agent?.Character as CharacterObject;
            return character?.IsRanged ?? false;
        }
    }

    public bool HasShield
    {
        get
        {
            var spawn = _agent?.SpawnEquipment;
            if (spawn == null) return false;
            for (var i = 0; i < 4; i++)
            {
                var slot = spawn[(EquipmentIndex)i];
                if (slot.IsEmpty) continue;
                var item = slot.Item;
                if (item == null) continue;
                if (item.ItemType == ItemObject.ItemTypeEnum.Shield) return true;
            }
            return false;
        }
    }

    public WeaponClass? GetPrimaryWeaponClass(int weaponSlot)
    {
        if (weaponSlot < 0 || weaponSlot > 3) return null;
        var spawn = _agent?.SpawnEquipment;
        if (spawn == null) return null;
        var slot = spawn[(EquipmentIndex)weaponSlot];
        if (slot.IsEmpty) return null;
        var item = slot.Item;
        if (item?.PrimaryWeapon == null) return null;
        return item.PrimaryWeapon.WeaponClass;
    }
}

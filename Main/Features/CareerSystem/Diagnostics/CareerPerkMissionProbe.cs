using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;

namespace TAOM.Features.CareerSystem.Diagnostics;

/// <summary>
/// The mission block of <c>taom.career_perks</c> (#613): the player agent's driven properties after base, career
/// and buffs, its mount's, the consumable slots and the live buff. Boundary code that reads the engine's
/// <c>Agent</c>, split out of the console command to keep that entry point thin (ADR-002).
/// </summary>
public static class CareerPerkMissionProbe
{
    public static CareerPerkMissionSnapshot? Gather(Hero hero)
    {
        var mission = Mission.Current;
        if (mission == null)
            return null;

        var block = new CareerPerkMissionSnapshot();
        var agent = mission.MainAgent;
        if (agent == null || !agent.IsActive() || !CareerHeroIdentityGate.IsCareerHeroAgent(agent, hero))
            return block;

        block.PlayerAgentAlive = true;
        var props = agent.AgentDrivenProperties;
        if (props != null)
        {
            block.SwingSpeedMultiplier = props.SwingSpeedMultiplier;
            block.MaxSpeedMultiplier = props.MaxSpeedMultiplier;
            block.DamageMultiplierBonus = props.DamageMultiplierBonus;
            block.ReadySpeedMultiplier = props.ThrustOrRangedReadySpeedMultiplier;
            block.ArmorEncumbrance = props.ArmorEncumbrance;
        }

        var mount = agent.MountAgent;
        if (mount != null && mount.AgentDrivenProperties != null)
        {
            block.HasMount = true;
            block.MountChargeDamage = mount.AgentDrivenProperties.MountChargeDamage;
            block.MountSpeed = mount.AgentDrivenProperties.MountSpeed;
            block.MountHealthLimit = mount.HealthLimit;
        }

        for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
        {
            var weapon = agent.Equipment[slot];
            if (weapon.IsEmpty || !weapon.IsAnyConsumable()) continue;
            block.AmmoSlots.Add($"slot {(int)slot} {weapon.Item?.StringId} {weapon.Amount}/{weapon.ModifiedMaxAmount}");
        }

        var buff = CareerAbilityBuffTracker.GetBuff(hero.StringId);
        block.LiveBuff = buff == null ? null : ActiveBuffsFormat.Describe(buff);
        return block;
    }
}

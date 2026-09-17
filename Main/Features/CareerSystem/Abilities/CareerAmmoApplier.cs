using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>
/// The Ammo career passive on a hero's consumable stacks (#613), applied the way vanilla applies
/// Deep Quivers, Fletcher and Well Prepared: from <c>AgentStatCalculateModel.InitializeMissionEquipment</c>,
/// before the agent is built, through <c>MissionEquipment.SetAmountOfSlot(slot, amount,
/// addOverflowToMaxAmount: true)</c>, which raises the stack's max with the amount
/// (<c>SandboxAgentStatCalculateModel.cs:209</c>). The engine treats <c>ModifiedMaxAmount</c> as the
/// cap everywhere else (a pickup merges only up to it, <c>Agent.cs:3496</c>), so the old refill,
/// <c>Agent.SetWeaponAmountInSlot</c> above an unchanged max after build, could not be trusted for a
/// full stack. <c>IsAnyConsumable</c>, not <c>IsAnyAmmo</c>: <c>IsAmmo</c> is "consumable and not a
/// weapon", so a javelin stack was skipped and nine Ammo pips did nothing. Boundary helper over the
/// sealed <c>Agent</c>; the arithmetic is <see cref="CareerPassiveMath.BoostAmmo"/>.
/// </summary>
public static class CareerAmmoApplier
{
    public static void Apply(Agent? agent, string? heroId, float bonus, IModLogger? logger)
    {
        if (agent == null || !(bonus > 0f))
            return;
        var equipment = agent.Equipment;
        if (equipment == null)
            return;

        for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
        {
            var weapon = equipment[slot];
            if (weapon.IsEmpty || !weapon.IsAnyConsumable())
                continue;

            int boosted = CareerPassiveMath.BoostAmmo(weapon.ModifiedMaxAmount, weapon.Amount, bonus);
            if (boosted <= weapon.Amount)
                continue;

            equipment.SetAmountOfSlot(slot, (short)boosted, addOverflowToMaxAmount: true);
            logger?.LogInfo($"[CareerPerks] Ammo +{bonus:P0} for '{heroId}': slot {(int)slot} {weapon.Item?.StringId} {weapon.Amount}/{weapon.ModifiedMaxAmount} -> {boosted}/{equipment[slot].ModifiedMaxAmount}");
        }
    }
}

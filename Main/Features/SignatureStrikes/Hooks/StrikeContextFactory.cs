using TAOM.Features.SignatureStrikes.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// The one boundary that turns an engine melee collision into a <see cref="StrikeContext"/>.
/// Shared by <c>TaomCombatMechanicsModel</c> (the primary-victim verdicts, called from inside
/// <c>CreateMeleeBlow</c>) and <see cref="SignatureStrikesMissionLogic"/> (the ring, called from
/// <c>OnMeleeHit</c>) so the two paths cannot describe the same hit differently: both see the
/// same direction, the same weapon gate and the same cooldown stamps.
/// </summary>
public static class StrikeContextFactory
{
    public static StrikeContext FromMeleeCollision(
        Agent attacker,
        Agent? victim,
        in AttackCollisionData collisionData,
        bool isCanceled,
        BlowFlags blowFlags,
        SignatureAgentEntry entry,
        float missionTime)
    {
        return new StrikeContext(
            IsSignatureAttacker: true,
            SignatureIndex: entry.SignatureIndex,
            Direction: MapDirection(collisionData.AttackDirection),
            Collision: MapCollision(collisionData.CollisionResult),
            IsCanceled: isCanceled,
            IsColliderAgent: collisionData.IsColliderAgent,
            IsAlternativeAttack: collisionData.IsAlternativeAttack,
            IsMissile: collisionData.IsMissile,
            IsHorseCharge: collisionData.IsHorseCharge,
            AttackBlockedWithShield: collisionData.AttackBlockedWithShield,
            HasMeleeWeapon: HasMeleeWeapon(attacker, collisionData.AffectorWeaponSlotOrMissileIndex),
            VictimIsHuman: victim != null && victim.IsHuman,
            VictimIsMounted: victim != null && victim.HasMount,
            HasShrugOff: (blowFlags & BlowFlags.ShrugOff) != 0,
            InflictedDamage: collisionData.InflictedDamage,
            MissionTime: missionTime,
            LastStrikeTimes: entry.Times);
    }

    /// <summary>
    /// The attacker holds a weapon, and its ITEM (not its current usage) is not a ranged or thrown
    /// type. A javelin swung in melee mode has a melee <c>CurrentUsageItem</c> but a Thrown item;
    /// the item is what Mike's "any weapon except thrown and bow" rule is about.
    /// </summary>
    public static bool HasMeleeWeapon(Agent attacker, int weaponSlot)
    {
        if (weaponSlot < 0 || weaponSlot >= (int)EquipmentIndex.NumAllWeaponSlots)
            return false;

        var weapon = attacker.Equipment[weaponSlot];
        if (weapon.IsEmpty || weapon.Item == null)
            return false;

        return !IsRangedOrThrown(weapon.Item.ItemType);
    }

    public static bool IsRangedOrThrown(ItemObject.ItemTypeEnum type)
    {
        switch (type)
        {
            case ItemObject.ItemTypeEnum.Bow:
            case ItemObject.ItemTypeEnum.Crossbow:
            case ItemObject.ItemTypeEnum.Sling:
            case ItemObject.ItemTypeEnum.Thrown:
            case ItemObject.ItemTypeEnum.Pistol:
            case ItemObject.ItemTypeEnum.Musket:
            case ItemObject.ItemTypeEnum.Arrows:
            case ItemObject.ItemTypeEnum.Bolts:
            case ItemObject.ItemTypeEnum.Bullets:
            case ItemObject.ItemTypeEnum.SlingStones:
                return true;
            default:
                return false;
        }
    }

    public static StrikeDirection MapDirection(Agent.UsageDirection direction)
    {
        switch (direction)
        {
            case Agent.UsageDirection.AttackUp: return StrikeDirection.Overhead;
            case Agent.UsageDirection.AttackDown: return StrikeDirection.Thrust;
            case Agent.UsageDirection.AttackLeft: return StrikeDirection.Left;
            case Agent.UsageDirection.AttackRight: return StrikeDirection.Right;
            default: return StrikeDirection.None;
        }
    }

    // By member, not by cast: SignatureStrikesBindingTests pins the two enums' NAMES against each
    // other, and a renamed member must surface there rather than as a silently shifted value.
    public static StrikeCollision MapCollision(CombatCollisionResult result)
    {
        switch (result)
        {
            case CombatCollisionResult.StrikeAgent: return StrikeCollision.StrikeAgent;
            case CombatCollisionResult.HitWorld: return StrikeCollision.HitWorld;
            case CombatCollisionResult.Blocked: return StrikeCollision.Blocked;
            case CombatCollisionResult.Parried: return StrikeCollision.Parried;
            case CombatCollisionResult.ChamberBlocked: return StrikeCollision.ChamberBlocked;
            default: return StrikeCollision.None;
        }
    }
}

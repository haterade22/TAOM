using TAOM.Features.AdvancedCombat;
using TAOM.Features.ElephantLike;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.DwarfAttacks.Hooks;

/// <param name="Victim">The victim's name, or null when no enemy was in reach.</param>
internal readonly record struct DwarfAttackHitResult(string? Victim, float Distance, int Damage, bool KnockedDown);

/// <summary>
/// Delivers one attack's blow to ONE enemy: of the enemies on foot within the attack's reach of the dwarf and in front
/// of the facing the attack started with, the one faced most squarely, the nearer one breaking a tie
/// (<see cref="SingleVictimPick"/>, the war ram's rule). Blunt and knock-back through
/// <see cref="CustomAttacksUtils.TakeDamage"/>, the dwarf owning it; the service decides the damage and the knockdown.
/// The victim filter is <see cref="DwarfAttacksCombat.IsCandidate"/> (the troll ring's, plus riders) and the service's
/// height test. A raised shield counts only when it faces the dwarf. Main thread only.
/// </summary>
internal static class DwarfAttackHit
{
    private static readonly MBList<Agent> Buffer = new();

    internal static DwarfAttackHitResult Deliver(Mission mission, Agent dwarf, DwarfAttack attack, Vec2 forward,
        DwarfAttackService service, int damagePercent)
    {
        // A recycled slot keeps answering for its new tenant (#592): only the current occupant deals the blow.
        if (!AgentSlotIdentity.IsCurrentOccupant(dwarf) || !dwarf.IsActive() || dwarf.Team == null)
            return default;

        Vec3 position3 = dwarf.Position;
        Vec2 position = position3.AsVec2;
        Buffer.Clear();
        mission.GetNearbyEnemyAgents(position, service.Reach(attack), dwarf.Team, Buffer);

        var pick = new SingleVictimPick();
        Agent? victim = null;
        float victimDistance = 0f;
        foreach (Agent other in Buffer)
        {
            if (!DwarfAttacksCombat.IsCandidate(dwarf, other)) continue;
            Vec3 otherPosition = other.Position;
            // The engine's query is flat: a man on a ladder below a wall-top dwarf is not in reach.
            if (!service.IsWithinHeight(otherPosition.z - position3.z)) continue;

            Vec2 offset = otherPosition.AsVec2 - position;
            float distance = offset.Length;
            if (!(distance > 0f)) continue;
            float facing = (offset.x * forward.x + offset.y * forward.y) / distance;
            if (!service.CanHit(attack, distance, facing)) continue;
            if (pick.Offer(facing, distance))
            {
                victim = other;
                victimDistance = distance;
            }
        }
        Buffer.Clear();    // a static buffer must not keep the battle's agents alive after the blow
        if (victim == null) return new DwarfAttackHitResult(null, 0f, 0, false);

        // A raised shield covers only its bearer's front: one held toward another enemy does not stop a blow from behind.
        bool shieldRaised = victim.GetCurrentActionType(1) == Agent.ActionCodeType.DefendShield;
        float victimFacing = -1f;
        if (shieldRaised)
        {
            Vec2 look = victim.LookDirection.AsVec2;
            float lookLength = look.Length;
            Vec2 toDwarf = position - victim.Position.AsVec2;
            float toDwarfLength = toDwarf.Length;
            if (lookLength > 1e-4f && toDwarfLength > 1e-4f)
                victimFacing = (look.x * toDwarf.x + look.y * toDwarf.y) / (lookLength * toDwarfLength);
        }
        bool shieldBlocked = service.ShieldBlocks(shieldRaised, victimFacing);
        if (service.DecideBlow(attack, shieldBlocked, damagePercent) is not DwarfAttackBlow blow)
            return new DwarfAttackHitResult(victim.Name, victimDistance, 0, false);

        float magnitude = attack == DwarfAttack.HeadButt
            ? DwarfAttacksConfig.HeadButtBlowMagnitude
            : DwarfAttacksConfig.DropKickBlowMagnitude;
        CustomAttacksUtils.TakeDamage(victim, dwarf, blow.Damage, magnitude, knockDown: blow.KnockDown,
            extraFlags: BlowFlags.KnockBack, damageType: DamageTypes.Blunt, chargeImpactSound: true);
        return new DwarfAttackHitResult(victim.Name, victimDistance, blow.Damage, blow.KnockDown);
    }
}

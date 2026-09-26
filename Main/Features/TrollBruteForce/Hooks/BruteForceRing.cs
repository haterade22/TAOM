using System.Collections.Generic;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce.Hooks;

/// <param name="Cap">The smash's rolled target cap (<see cref="TrollBruteForceConfig.RingMinTargets"/> to <see cref="TrollBruteForceConfig.RingMaxTargets"/>); 0 when no ring was cast.</param>
/// <param name="Spared">Eligible enemies the cap left untouched.</param>
internal readonly record struct BruteForceRingResult(int Hit, int KnockedDown, int Skipped, float BodySize = 0f, int Cap = 0, int Spared = 0);

/// <summary>
/// Delivers the smash's ring. Every enemy human within the scaled outer radius of the impact centre that the service
/// gives a blow is eligible; the smash rolls a cap of <see cref="TrollBruteForceConfig.RingMinTargets"/> to
/// <see cref="TrollBruteForceConfig.RingMaxTargets"/>, and only that many of the eligible, nearest the centre first,
/// take the blow (Blunt, knock-back, knockdown unless shield-blocking) through
/// <see cref="CustomAttacksUtils.TakeDamage"/>, the troll owning it. The rest are untouched. The victim filter is the
/// signature strikes' (SignatureStrikeRunner). Main thread only.
/// </summary>
internal static class BruteForceRing
{
    private static readonly MBList<Agent> Buffer = new();

    internal static BruteForceRingResult Deliver(Mission mission, Agent troll, ITrollBruteForceService service)
    {
        // A recycled slot keeps answering for its new tenant (#592): only the current occupant deals the ring.
        if (!troll.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(troll) || troll.Team == null)
            return default;

        float scale = service.BodySize(troll.AgentScale, troll.Monster?.StandingEyeHeight ?? 0f);
        // the scale the distances actually use (capped, a bad value read as 1), for the log line
        float effectiveScale = service.OuterRadius(scale) / TrollBruteForceConfig.OuterRadius;
        Vec3 position = troll.Position;
        Vec3 look = troll.LookDirection;
        if (!service.TryGetImpactCentre(position.x, position.y, look.x, look.y, scale, out float cx, out float cy))
            return new BruteForceRingResult(0, 0, 0, effectiveScale);

        var centre = new Vec2(cx, cy);
        Buffer.Clear();
        mission.GetNearbyEnemyAgents(centre, service.OuterRadius(scale), troll.Team, Buffer);

        // Every enemy the ring could hit, with its blow, and its distance from the centre for the cap's nearest-first pick.
        var eligible = new List<(Agent Victim, BruteForceBlow Blow)>(Buffer.Count);
        var distances = new List<float>(Buffer.Count);
        int skipped = 0;
        foreach (Agent victim in Buffer)
        {
            if (victim == null
                || ReferenceEquals(victim, troll)
                || !victim.IsActive()
                || victim.IsFadingOut()
                || victim.CurrentMortalityState == Agent.MortalityState.Invulnerable
                || victim.IsMount
                || !victim.IsEnemyOf(troll))
            {
                skipped++;
                continue;
            }

            float distance = (victim.Position.AsVec2 - centre).Length;
            bool shieldBlocked = victim.GetCurrentActionType(1) == Agent.ActionCodeType.DefendShield;
            BruteForceBlow? blow = service.DecideRingBlow(distance, scale, shieldBlocked);
            if (blow is not BruteForceBlow b || b.Damage <= 0)
            {
                skipped++;
                continue;
            }

            eligible.Add((victim, b));
            distances.Add(distance);
        }

        // The roll is taken here, at the boundary, so the service stays deterministic (Mike, 2026-09-26: 1 to 5 a smash).
        // MBRandom.RandomInt(min, max) is MBFastRandom.Next: uniform over [min, max), max exclusive, hence the + 1.
        int cap = MBRandom.RandomInt(TrollBruteForceConfig.RingMinTargets, TrollBruteForceConfig.RingMaxTargets + 1);
        int hit = 0, knockedDown = 0;
        foreach (int i in service.NearestRingVictims(distances, cap))
        {
            (Agent victim, BruteForceBlow blow) = eligible[i];
            CustomAttacksUtils.TakeDamage(victim, troll, blow.Damage, TrollBruteForceConfig.BlowMagnitude,
                knockDown: blow.KnockDown, extraFlags: BlowFlags.KnockBack, damageType: DamageTypes.Blunt,
                chargeImpactSound: true);
            hit++;
            if (blow.KnockDown) knockedDown++;
        }

        return new BruteForceRingResult(hit, knockedDown, skipped, effectiveScale, cap, eligible.Count - hit);
    }
}

using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce.Hooks;

internal readonly record struct BruteForceRingResult(int Hit, int KnockedDown, int Skipped);

/// <summary>
/// Delivers the smash's ring: every enemy human within the scaled outer radius of the impact centre takes the
/// service's blow (Blunt, knock-back, knockdown unless shield-blocking) through <see cref="CustomAttacksUtils.TakeDamage"/>,
/// the troll owning it. The victim filter is the signature strikes' (SignatureStrikeRunner). Main thread only.
/// </summary>
internal static class BruteForceRing
{
    private static readonly MBList<Agent> Buffer = new();

    internal static BruteForceRingResult Deliver(Mission mission, Agent troll, ITrollBruteForceService service)
    {
        // A recycled slot keeps answering for its new tenant (#592): only the current occupant deals the ring.
        if (!troll.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(troll) || troll.Team == null)
            return default;

        float scale = troll.AgentScale;
        Vec3 position = troll.Position;
        Vec3 look = troll.LookDirection;
        if (!service.TryGetImpactCentre(position.x, position.y, look.x, look.y, scale, out float cx, out float cy))
            return default;

        var centre = new Vec2(cx, cy);
        Buffer.Clear();
        mission.GetNearbyEnemyAgents(centre, service.OuterRadius(scale), troll.Team, Buffer);

        int hit = 0, knockedDown = 0, skipped = 0;
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

            CustomAttacksUtils.TakeDamage(victim, troll, b.Damage, TrollBruteForceConfig.BlowMagnitude,
                knockDown: b.KnockDown, extraFlags: BlowFlags.KnockBack, damageType: DamageTypes.Blunt,
                chargeImpactSound: true);
            hit++;
            if (b.KnockDown) knockedDown++;
        }

        return new BruteForceRingResult(hit, knockedDown, skipped);
    }
}

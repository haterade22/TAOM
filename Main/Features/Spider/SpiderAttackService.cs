using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Spider.BehaviorTreeElements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Spider;

public class SpiderAttackService : ISpiderAttackService
{
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;

    public SpiderAttackService(IMissionAdapterFactory adapterFactory, IModLogger logger)
    {
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public bool IsSpiderMonster(string? monsterId) => monsterId == SpiderConfig.SpiderMonsterId;

    public int CalculateSpiderBiteDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent, float critRoll)
    {
        float fromSpeed = Math.Min(SpiderConfig.MaxSpeedDamage, velocity * SpiderConfig.MaxSpeedDamage / SpiderConfig.SpeedForMaxDamage);
        float allDamage = fromSpeed + SpiderConfig.MaxBaseDamage;
        float damageAbsorption = MathF.Clamp((100f - armorEffectivenessPercent * SpiderConfig.ArmorMitigationFactor) / 100f,
                                             SpiderConfig.MinArmorPassthrough, 1f);
        float damage = allDamage * damageAbsorption;
        if (critRoll < SpiderConfig.CritChance) damage *= SpiderConfig.CritMultiplier;
        return (int)damage;
    }

    public bool HandleSpiderTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId, SpiderStrikeProfile strike)
    {
        if (!IsStrikeable(attacker, target)) return false;

        try
        {
            float velocity = attacker.MovementVelocity.Y;
            int armor = target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest);
            // Crit roll at hit time (the spider's hit happens in this callback, not a BT task — so the roll is
            // generated here, the boundary, like the elephant's BT supplies its damage roll). Pure scaling lives
            // in CalculateSpiderBiteDamage.
            float critRoll = MBRandom.RandomFloat;
            bool isCrit = critRoll < SpiderConfig.CritChance;
            int damage = SpiderStrikes.Scale(CalculateSpiderBiteDamage(target, velocity, armor, critRoll), strike);

            // Damager attribution (warg pattern): prefer the spider's rider; fall back to the spider
            // itself when riderless. If both are absent/dead, vanilla self-damage fallback at 20.
            IAgentAdapter damager = attacker.RiderAgent ?? attacker;
            if (damager == null || damager.Health <= 0)
            {
                damager = target;
                damage = 20;
                isCrit = false;   // fallback overrides the crit-scaled value — keep the diag log honest
            }

            if (target.IsHorse() || target.IsCamel()) damage *= 2;

            if (!target.HasMount)
                target.ProjectAgent(damager.Position, SpiderStrikes.Reaction(damage, isCrit, strike));

            // Underlying-agent extraction at the boundary — required because
            // CustomAttacksUtils.TakeDamage operates on sealed Agent types.
            var damagerAgent = (damager as AgentAdapter)?.GetUnderlyingAgent();
            var targetAgent = (target as AgentAdapter)?.GetUnderlyingAgent();
            // A capped strike is a creature bandit's (#692), logged by its budgeted lines instead; this one has no cap.
            if (!strike.IsCapped)
                _logger.LogInfo($"[Spider][diag] HIT: bite connected on '{targetAgent?.Name ?? "?"}' bone={boneId} " +
                                $"damage={damage}{(isCrit ? " CRIT" : "")} vel={velocity:0.0} armor={armor} targetMount={target.HasMount} " +
                                $"damager={(attacker.RiderAgent != null ? "rider" : "spider-self")}.");
            if (damagerAgent == null || targetAgent == null) return false;
            CustomAttacksUtils.TakeDamage(targetAgent, damagerAgent, damage);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError($"[Spider] HandleSpiderTargetHit error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    public SpiderStrikeOutcome SpiderAttack(IAgentAdapter spider, SpiderAttackKind kind, float bearing, SpiderStrikeSet strikes)
    {
        if (spider == null || !spider.IsActive()) return default;

        float velocityY = spider.MovementVelocity.Y;
        string clipName = SelectActionName(kind, velocityY, bearing);
        ActionIndexCache action = SpiderAttackActions.ForName(clipName);
        (float arcCenterDeg, float arcHalfAngleDeg) = SelectArc(kind, bearing);
        var strike = strikes.For(clipName);

        // [Spider][diag] fire log — attack-gated (the BT cooldown decorators limit this to ~once/2-5s per spider).
        // Single prefix for one-grep removal after sign-off. A capped strike is a creature bandit's (#692), whose attack
        // task logs the returned outcome in its budgeted lines instead.
        if (!strike.IsCapped)
            _logger.LogInfo($"[Spider][diag] ATTACK fire: kind={kind} clip={clipName} " +
                            $"arc=center{arcCenterDeg:0}±{arcHalfAngleDeg:0}deg radius={SpiderConfig.StrikeRadius:0.0}m " +
                            $"bearing={bearing:0.00}({(bearing >= 0f ? "LEFT" : "RIGHT")}) vel.Y={velocityY:0.0}.");

        // Radial damage in the front arc (the elephant's reliable model — replaces the bone-collision that
        // connected ~6%). Each in-arc enemy runs through HandleSpiderTargetHit (team check, crit, armor, rider
        // attribution, fall/flinch). The front-leg clip still plays for the visual. An uncapped strike (the ridden
        // spider) hits each enemy as the strike reports it; a capped one (a creature bandit, #692) holds the
        // strikeable enemies and hits the nearest few once the strike returns (it is synchronous), so the arc's outcome
        // is known here. Allies in the arc are counted apart for a capped strike: the team rule skips them by design,
        // so they are not a miss.
        var (inArc, alliesInArc, struck) = StrikeArc(spider, strike, countAllies: strike.IsCapped,
            hit => spider.RadialStrike(action, SpiderConfig.StrikeRadius, arcHalfAngleDeg, arcCenterDeg, hit));
        return new SpiderStrikeOutcome(clipName, inArc, alliesInArc, struck, strike.MaxTargets);
    }

    /// <summary>
    /// Handles one strike's arc: <paramref name="radialStrike"/> plays the clip and reports each agent in the arc. Returns
    /// the agents reported, the allies among them (counted only for the creature diagnostics) and the enemies struck. A
    /// seam apart from <see cref="SpiderAttack"/>, whose clip
    /// lookup needs the engine, so the cap is testable with fakes.
    /// </summary>
    internal (int inArc, int allies, int struck) StrikeArc(IAgentAdapter spider, SpiderStrikeProfile strike, bool countAllies,
        Action<Action<IAgentAdapter, IAgentAdapter, sbyte>> radialStrike)
    {
        List<(IAgentAdapter attacker, IAgentAdapter target, sbyte bone)>? held = strike.IsCapped ? new() : null;
        int inArc = 0, allies = 0, struck = 0;
        radialStrike((attackerAdapter, targetAdapter, boneId) =>
        {
            inArc++;
            if (countAllies && attackerAdapter != null && targetAdapter != null && IsSameSide(attackerAdapter, targetAdapter))
                allies++;
            if (held == null)
            {
                if (HandleSpiderTargetHit(attackerAdapter, targetAdapter, boneId, strike)) struck++;
            }
            // A capped strike (a creature bandit's) holds soldiers only: a riderless mount in the arc is on nobody's side and
            // would take the slot of the soldier who opened the engage gate, which skips mounts (Codex 2026-09-28 F2).
            else if (!IsLooseMount(targetAdapter) && IsStrikeable(attackerAdapter, targetAdapter))
                held.Add((attackerAdapter, targetAdapter, boneId));
        });
        if (held != null)
            struck = StrikeNearest(spider, held, strike);
        return (inArc, allies, struck);
    }

    /// <summary>
    /// A capped strike's hits: the nearest <see cref="SpiderStrikeProfile.MaxTargets"/> of the held enemies, a cavalryman and
    /// his horse counted once. The list lives only for this call, so no adapter outlives the frame (a slot can be reused).
    /// The rider is matched by reference: MissionAdapterFactory caches one adapter per agent, and both the arc's targets
    /// and <c>RiderAgent</c> come from that cache.
    /// </summary>
    private int StrikeNearest(IAgentAdapter spider, List<(IAgentAdapter attacker, IAgentAdapter target, sbyte bone)> held,
        SpiderStrikeProfile strike)
    {
        var origin = spider.Position;
        var candidates = new SpiderStrikeCandidate[held.Count];
        for (int i = 0; i < held.Count; i++)
        {
            var target = held[i].target;
            int rider = -1;
            if (target.IsMount && target.RiderAgent != null)
                for (int j = 0; j < held.Count; j++)
                    if (ReferenceEquals(held[j].target, target.RiderAgent)) { rider = j; break; }
            candidates[i] = new SpiderStrikeCandidate(i, target.Position.DistanceSquared(origin), rider);
        }

        int struck = 0;
        foreach (int i in SpiderStrikeTargets.Pick(candidates, strike.MaxTargets))
            if (HandleSpiderTargetHit(held[i].attacker, held[i].target, held[i].bone, strike)) struck++;
        return struck;
    }

    /// <summary>
    /// Whether a strike may damage this target: present, active, not fading, not on the spider's side, and standing or
    /// routed. One predicate for both the uncapped hit and the capped strike's candidates, so the two cannot drift.
    /// </summary>
    private bool IsStrikeable(IAgentAdapter attacker, IAgentAdapter target)
    {
        if (target == null || !target.IsActive() || target.IsFadingOut()) return false;
        if (attacker == null || IsSameSide(attacker, target)) return false;
        try
        {
            return target.State == AgentState.Active || target.State == AgentState.Routed;
        }
        catch (Exception e)
        {
            _logger.LogError($"[Spider] IsStrikeable error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    private static bool IsLooseMount(IAgentAdapter target) => target != null && target.IsMount && target.RiderAgent == null;

    // RIDDEN-mount team rule (warg pattern): if the victim is a mount, attribute its team to its rider; the
    // spider's own side is decided by ITS rider when present (the spider is the mount).
    private static bool IsSameSide(IAgentAdapter attacker, IAgentAdapter target)
    {
        var victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target;
        var attackerTeamSource = attacker.RiderAgent ?? attacker;
        return attackerTeamSource.IsSameTeam(victimTeamSource);
    }

    // --- Pure decision helpers (no TaleWorlds types — unit-tested; elephant-parity) ---

    public bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds)
        => lastFired == null || (now - lastFired.Value).TotalSeconds >= cooldownSeconds;

    public string SelectActionName(SpiderAttackKind kind, float velocityY, float bearing)
        => kind == SpiderAttackKind.Pounce
            ? (velocityY >= SpiderConfig.ChargeVelocityThreshold ? SpiderConfig.PounceChargeActionName : SpiderConfig.PounceFrontActionName)
            : (bearing >= 0f ? SpiderConfig.SwingLeftActionName : SpiderConfig.SwingRightActionName);

    public (float centerDeg, float halfAngleDeg) SelectArc(SpiderAttackKind kind, float bearing)
    {
        if (kind == SpiderAttackKind.Pounce)
            return (0f, SpiderConfig.PounceArcHalfAngleDeg);   // forward cone
        // Side swipe: the matching front flank (+ center = LEFT, the bearing sign convention).
        float center = bearing >= 0f ? SpiderConfig.SideArcCenterDeg : -SpiderConfig.SideArcCenterDeg;
        return (center, SpiderConfig.SideArcHalfAngleDeg);
    }
}

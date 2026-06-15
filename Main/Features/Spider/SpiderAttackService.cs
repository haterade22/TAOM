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

    public void HandleSpiderTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId)
    {
        if (target == null || !target.IsActive() || target.IsFadingOut()) return;
        if (attacker == null) return;

        // RIDDEN-mount team rule (warg pattern): if the victim is a mount, attribute its team to its
        // rider; the spider's own side is decided by ITS rider when present (the spider is the mount).
        var victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target;
        var attackerTeamSource = attacker.RiderAgent ?? attacker;
        if (attackerTeamSource.IsSameTeam(victimTeamSource)) return;

        try
        {
            if (target.State != AgentState.Active && target.State != AgentState.Routed) return;

            float velocity = attacker.MovementVelocity.Y;
            int armor = target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest);
            // Crit roll at hit time (the spider's hit happens in this callback, not a BT task — so the roll is
            // generated here, the boundary, like the elephant's BT supplies its damage roll). Pure scaling lives
            // in CalculateSpiderBiteDamage.
            float critRoll = MBRandom.RandomFloat;
            bool isCrit = critRoll < SpiderConfig.CritChance;
            int damage = CalculateSpiderBiteDamage(target, velocity, armor, critRoll);

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
            {
                DamageAnimation anim;
                if (damage < SpiderConfig.DamageToFlinch) anim = DamageAnimation.Nothing;
                else if (damage < SpiderConfig.DamageToFall) anim = DamageAnimation.Flinch;
                else anim = DamageAnimation.Fall;
                target.ProjectAgent(damager.Position, anim);
            }

            // Underlying-agent extraction at the boundary — required because
            // CustomAttacksUtils.TakeDamage operates on sealed Agent types.
            var damagerAgent = (damager as AgentAdapter)?.GetUnderlyingAgent();
            var targetAgent = (target as AgentAdapter)?.GetUnderlyingAgent();
            _logger.LogInfo($"[Spider][diag] HIT: bite connected on '{targetAgent?.Name ?? "?"}' bone={boneId} " +
                            $"damage={damage}{(isCrit ? " CRIT" : "")} vel={velocity:0.0} armor={armor} targetMount={target.HasMount} " +
                            $"damager={(attacker.RiderAgent != null ? "rider" : "spider-self")}.");
            if (damagerAgent != null && targetAgent != null)
                CustomAttacksUtils.TakeDamage(targetAgent, damagerAgent, damage);
        }
        catch (Exception e)
        {
            _logger.LogError($"[Spider] HandleSpiderTargetHit error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    public void SpiderAttack(IAgentAdapter spider, SpiderAttackKind kind, float bearing)
    {
        if (spider == null || !spider.IsActive()) return;

        float velocityY = spider.MovementVelocity.Y;
        string clipName = SelectActionName(kind, velocityY, bearing);
        ActionIndexCache action = SpiderAttackActions.ForName(clipName);
        List<sbyte> boneIds = SelectBones(kind, velocityY, bearing);

        // Charge pounce gets a wider/longer collision window (the lunge sweeps through); the front bite and the
        // side swipes use the tighter standing window. Per-clip, mirroring the warg's standing-vs-running split.
        bool isChargePounce = kind == SpiderAttackKind.Pounce && velocityY >= SpiderConfig.ChargeVelocityThreshold;
        float actionProgressMin = 0.1f;
        float actionProgressMax = isChargePounce ? 0.7f : 0.5f;
        float boneCollisionRadius = kind == SpiderAttackKind.Pounce ? SpiderConfig.PounceCollisionRadius : SpiderConfig.SideCollisionRadius;
        float targetDetectionRange = SpiderConfig.TargetDetectionRange;

        // [Spider][diag] fire log — attack-gated (fires only when an attack actually fires; the BT cooldown
        // decorators limit this to ~once/2-5s per spider, never per-tick). Single prefix for one-grep removal
        // after in-game sign-off. bearing is informational for a pounce (clip is chosen by speed there).
        _logger.LogInfo($"[Spider][diag] ATTACK fire: kind={kind} clip={clipName} " +
                        $"bearing={bearing:0.000}({(bearing >= 0f ? "LEFT" : "RIGHT")}) vel.Y={velocityY:0.0} " +
                        $"bones=[{string.Join(",", boneIds)}] range={targetDetectionRange:0.0}m radius={boneCollisionRadius:0.0} " +
                        $"window=[{actionProgressMin:0.0}-{actionProgressMax:0.0}].");

        spider.CustomAttack(action, boneIds, actionProgressMin, actionProgressMax, targetDetectionRange, boneCollisionRadius, true,
            (attackerAdapter, targetAdapter, boneId) => HandleSpiderTargetHit(attackerAdapter, targetAdapter, boneId));
    }

    // --- Pure decision helpers (no TaleWorlds types — unit-tested; elephant-parity) ---

    public bool IsOffCooldown(DateTime? lastFired, DateTime now, double cooldownSeconds)
        => lastFired == null || (now - lastFired.Value).TotalSeconds >= cooldownSeconds;

    public string SelectActionName(SpiderAttackKind kind, float velocityY, float bearing)
        => kind == SpiderAttackKind.Pounce
            ? (velocityY >= SpiderConfig.ChargeVelocityThreshold ? SpiderConfig.PounceChargeActionName : SpiderConfig.PounceFrontActionName)
            : (bearing >= 0f ? SpiderConfig.SwingLeftActionName : SpiderConfig.SwingRightActionName);

    public List<sbyte> SelectBones(SpiderAttackKind kind, float velocityY, float bearing)
        => kind == SpiderAttackKind.Pounce
            ? (velocityY >= SpiderConfig.ChargeVelocityThreshold ? SpiderConfig.PounceChargeBones : SpiderConfig.PounceFrontBones)
            : (bearing >= 0f ? SpiderConfig.LeftSwingBones : SpiderConfig.RightSwingBones);
}

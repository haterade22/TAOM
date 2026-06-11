using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
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

    public int CalculateSpiderBiteDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent)
    {
        float fromSpeed = Math.Min(SpiderConfig.MaxSpeedDamage, velocity * SpiderConfig.MaxSpeedDamage / SpiderConfig.SpeedForMaxDamage);
        float allDamage = fromSpeed + SpiderConfig.MaxBaseDamage;
        float damageAbsorption = (100f - armorEffectivenessPercent) / 100f;
        return (int)(allDamage * MathF.Clamp(damageAbsorption, 0f, 1f));
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
            int damage = CalculateSpiderBiteDamage(target, velocity, armor);

            // Damager attribution (warg pattern): prefer the spider's rider; fall back to the spider
            // itself when riderless. If both are absent/dead, vanilla self-damage fallback at 20.
            IAgentAdapter damager = attacker.RiderAgent ?? attacker;
            if (damager == null || damager.Health <= 0)
            {
                damager = target;
                damage = 20;
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
                            $"damage={damage} vel={velocity:0.0} armor={armor} targetMount={target.HasMount} " +
                            $"damager={(attacker.RiderAgent != null ? "rider" : "spider-self")}.");
            if (damagerAgent != null && targetAgent != null)
                CustomAttacksUtils.TakeDamage(targetAgent, damagerAgent, damage);
        }
        catch (Exception e)
        {
            _logger.LogError($"[Spider] HandleSpiderTargetHit error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    public void SpiderAttack(IAgentAdapter spider)
    {
        if (spider == null || !spider.IsActive()) return;

        // Bite uses act_spider_attack_front for standing/short-range, act_spider_attack_charge for fast charge.
        // Bone collision uses fang/jaw bones (placeholder indices in SpiderConfig — refine after runtime probe).
        List<sbyte> boneIds;
        ActionIndexCache action;
        float actionProgressMin;
        float actionProgressMax;
        float boneCollisionRadius = 0.3f;
        float targetDetectionRange = SpiderConfig.TargetDetectionRange;

        if (spider.MovementVelocity.Y >= 4f)
        {
            boneIds = SpiderConfig.ChargeAttackBones;
            action = ActionIndexCache.Create("act_spider_attack_charge");
            actionProgressMin = 0.1f;
            actionProgressMax = 0.7f;
            boneCollisionRadius = 0.4f;
        }
        else
        {
            boneIds = SpiderConfig.StandAttackBones;
            action = ActionIndexCache.Create("act_spider_attack_front");
            actionProgressMin = 0.1f;
            actionProgressMax = 0.5f;
        }

        _logger.LogInfo($"[Spider][diag] bite: CustomAttack vel.Y={spider.MovementVelocity.Y:0.0} " +
                        $"reach={targetDetectionRange:0.0}m radius={boneCollisionRadius:0.0} bones={boneIds.Count} " +
                        $"action={(spider.MovementVelocity.Y >= 4f ? "charge" : "front")}.");
        spider.CustomAttack(action, boneIds, actionProgressMin, actionProgressMax, targetDetectionRange, boneCollisionRadius, true,
            (attackerAdapter, targetAdapter, boneId) => HandleSpiderTargetHit(attackerAdapter, targetAdapter, boneId));
    }
}

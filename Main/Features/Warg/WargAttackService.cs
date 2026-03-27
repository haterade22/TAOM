using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Warg;

public class WargAttackService : IWargAttackService
{
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;

    public WargAttackService(IMissionAdapterFactory adapterFactory, IModLogger logger)
    {
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public int CalculateWargAttackDamage(Agent target, float velocity)
    {
        float fromSpeed = Math.Min(WargConfig.MaxSpeedDamage, velocity * WargConfig.MaxSpeedDamage / WargConfig.SpeedForMaxDamage);
        float allDamage = fromSpeed + WargConfig.MaxBaseDamage;
        float damageAbsorption = (100 - target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest)) / 100;
        int damage = (int)(allDamage * MathF.Clamp(damageAbsorption, 0, 1));
        return damage;
    }

    public void HandleWargTargetHit(Agent attacker, Agent target, sbyte boneId)
    {
        if (target == null || !target.IsActive()) return;

        Team victimTeam;
        if (target.IsMount && target.RiderAgent != null)
            victimTeam = target.RiderAgent.Team;
        else
            victimTeam = target.Team;

        if (attacker.RiderAgent != null && attacker.RiderAgent.Team == victimTeam) return;

        try
        {
            if (target.State == AgentState.Active || target.State == AgentState.Routed)
            {
                if (target.IsFadingOut()) return;

                int damage = CalculateWargAttackDamage(target, attacker.MovementVelocity.Y);

                Agent damagerAgent = attacker?.RiderAgent ?? attacker;
                if (damagerAgent == null || damagerAgent.Health <= 0)
                {
                    damagerAgent = target;
                    damage = 20;
                }

                var targetAdapter = _adapterFactory.GetAgentAdapter(target);
                if (targetAdapter.IsHorse() || targetAdapter.IsCamel()) damage *= 2;

                if (!target.HasMount)
                {
                    DamageAnimation anim;
                    if (damage < WargConfig.DamageToFlinch) anim = DamageAnimation.Nothing;
                    else if (damage < WargConfig.DamageToFall) anim = DamageAnimation.Flinch;
                    else anim = DamageAnimation.Fall;
                    targetAdapter.ProjectAgent(damagerAgent.Position, anim);
                }
                CustomAttacksUtils.TakeDamage(target, damagerAgent, damage);
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"[Warg] HandleWargTargetHit error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    public void WargAttack(Agent warg)
    {
        List<sbyte> boneIds;
        float targetDetectionRange = 20f;
        float boneCollisionRadius = 0.3f;
        float actionProgressMax;
        float actionProgressMin;
        ActionIndexCache action;

        if (warg.MovementVelocity.Y >= 4)
        {
            boneIds = new List<sbyte> { 23 };
            action = ActionIndexCache.Create("act_warg_attack_running");
            actionProgressMin = 0.1f;
            actionProgressMax = 0.7f;
            boneCollisionRadius = 0.4f;
        }
        else
        {
            boneIds = new List<sbyte> { 23, 37, 43 };
            action = ActionIndexCache.Create("act_warg_attack_stand");
            actionProgressMin = 0.1f;
            actionProgressMax = 0.5f;
        }

        var wargAdapter = _adapterFactory.GetAgentAdapter(warg);
        wargAdapter.CustomAttack(action, boneIds, actionProgressMin, actionProgressMax, targetDetectionRange, boneCollisionRadius, true, (attackerAdapter, targetAdapter, boneId) =>
        {
            var attackerAgent = (attackerAdapter as AgentAdapter)?.GetUnderlyingAgent();
            var targetAgent = (targetAdapter as AgentAdapter)?.GetUnderlyingAgent();
            if (attackerAgent != null && targetAgent != null)
                HandleWargTargetHit(attackerAgent, targetAgent, boneId);
        });
    }
}

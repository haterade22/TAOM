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
    // Boundary conversion (Agent → IAgentAdapter) happens in WargAttackTask. The service
    // itself is adapter-pure post-#178. The factory field is retained — mirrors the
    // Spider service pattern — and remains available for future per-callback adapter
    // resolution if needed.
    private readonly IMissionAdapterFactory _adapterFactory;
    private readonly IModLogger _logger;

    public WargAttackService(IMissionAdapterFactory adapterFactory, IModLogger logger)
    {
        _adapterFactory = adapterFactory;
        _logger = logger;
    }

    public int CalculateWargAttackDamage(IAgentAdapter target, float velocity, float armorEffectivenessPercent)
    {
        float fromSpeed = Math.Min(WargConfig.MaxSpeedDamage, velocity * WargConfig.MaxSpeedDamage / WargConfig.SpeedForMaxDamage);
        float allDamage = fromSpeed + WargConfig.MaxBaseDamage;
        float damageAbsorption = (100f - armorEffectivenessPercent) / 100f;
        return (int)(allDamage * MathF.Clamp(damageAbsorption, 0f, 1f));
    }

    public void HandleWargTargetHit(IAgentAdapter attacker, IAgentAdapter target, sbyte boneId)
    {
        if (target == null || !target.IsActive() || target.IsFadingOut())
            return;
        if (attacker == null)
            return;

        // Warg-specific victim-team rule: if the victim is a mount, attribute the team to its rider.
        var victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target;
        if (attacker.RiderAgent != null && attacker.RiderAgent.IsSameTeam(victimTeamSource))
            return;

        try
        {
            if (target.State != AgentState.Active && target.State != AgentState.Routed)
                return;

            float velocity = attacker.MovementVelocity.Y;
            int armor = target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest);
            int damage = CalculateWargAttackDamage(target, velocity, armor);

            // Damager attribution: prefer the warg's rider; fall back to the warg itself.
            // If both are absent/dead, vanilla self-damage fallback at 20 damage.
            IAgentAdapter damagerAdapter = attacker.RiderAgent ?? attacker;
            if (damagerAdapter == null || damagerAdapter.Health <= 0)
            {
                damagerAdapter = target;
                damage = 20;
            }

            if (target.IsHorse() || target.IsCamel())
                damage *= 2;

            if (!target.HasMount)
            {
                DamageAnimation anim;
                if (damage < WargConfig.DamageToFlinch) anim = DamageAnimation.Nothing;
                else if (damage < WargConfig.DamageToFall) anim = DamageAnimation.Flinch;
                else anim = DamageAnimation.Fall;
                target.ProjectAgent(damagerAdapter.Position, anim);
            }

            // Underlying-agent extraction at the boundary — required because
            // CustomAttacksUtils.TakeDamage operates on sealed Agent types.
            // Mirrors the established Spider pattern.
            var damagerAgent = (damagerAdapter as AgentAdapter)?.GetUnderlyingAgent();
            var targetAgent = (target as AgentAdapter)?.GetUnderlyingAgent();
            if (damagerAgent != null && targetAgent != null)
            {
                CustomAttacksUtils.TakeDamage(targetAgent, damagerAgent, damage);
            }
            else
            {
                _logger.LogWarning($"[Warg] HandleWargTargetHit: skipped TakeDamage — damagerAgent={damagerAgent?.Name ?? "null"} targetAgent={targetAgent?.Name ?? "null"} (adapter extraction failed)");
            }
        }
        catch (Exception e)
        {
            _logger.LogError($"[Warg] HandleWargTargetHit error: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    public void WargAttack(IAgentAdapter warg)
    {
        if (warg == null || !warg.IsActive())
            return;

        List<sbyte> boneIds;
        float targetDetectionRange = WargConfig.TargetDetectionRange;
        float boneCollisionRadius = 0.3f;
        float actionProgressMax;
        float actionProgressMin;
        ActionIndexCache action;
        string actionName;

        // skeleton_warg bone map (49 bones, verified 2026-05-24 #219):
        //   17 Chest_M        — body slam impact
        //   20 Head_M         — head impact
        //   22 Jaw_M          — jaw root
        //   23 JawEnd_M       — bite tip
        //   32 Scapula_R      — right shoulder
        //   36 Fingers1_R     — right front paw
        //   37 Fingers2_R     — right front paw tip
        //   38 Scapula_L      — left shoulder
        //   42 Fingers1_L     — left front paw
        //   43 Fingers2_L     — left front paw tip
        // These 10 bones form the "impact cone" presented during a charge —
        // anything in front of the warg's chest is likely to overlap at least
        // one of them as the warg passes through.
        if (warg.MovementVelocity.Y >= 4)
        {
            // Running attack — full impact cone for fast charges. Prior single-bone
            // [23] / 3-bone [23,37,43] / 1.5m-radius variants all failed because
            // even a wide sphere can't compensate for sampling 3 bones across a
            // single point on the warg. With 10 bones spread across the warg's
            // entire front, a 1.0m sphere on each gives a continuous detection
            // volume around the charging body.
            boneIds = new List<sbyte> { 17, 20, 22, 23, 32, 36, 37, 38, 42, 43 };
            actionName = "act_warg_attack_running";
            action = ActionIndexCache.Create(actionName);
            actionProgressMin = 0.0f;
            actionProgressMax = 0.9f;
            boneCollisionRadius = 1.0f;
        }
        else
        {
            // Stand attack — same impact cone but tighter radius since warg
            // is nearly stationary; the bones are positioned precisely each frame.
            boneIds = new List<sbyte> { 17, 20, 22, 23, 32, 36, 37, 38, 42, 43 };
            actionName = "act_warg_attack_stand";
            action = ActionIndexCache.Create(actionName);
            actionProgressMin = 0.1f;
            actionProgressMax = 0.5f;
            boneCollisionRadius = 0.5f;
        }

        warg.CustomAttack(action, boneIds, actionProgressMin, actionProgressMax, targetDetectionRange, boneCollisionRadius, true,
            (attackerAdapter, targetAdapter, boneId) => HandleWargTargetHit(attackerAdapter, targetAdapter, boneId));
    }
}

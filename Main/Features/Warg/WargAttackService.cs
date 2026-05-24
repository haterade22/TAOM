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
        // Diagnostic 2026-05-24: trace why "Mount charged for 1-3 Blunt" appears
        // instead of (or alongside) our 40+ Pierce bite. Logs every gate so the
        // user's next taom_debug log answers the question definitively.
        if (target == null || !target.IsActive() || target.IsFadingOut())
        {
            _logger.LogDebug($"[Warg] HandleWargTargetHit: skipped — target null/inactive/fading bone={boneId}");
            return;
        }
        if (attacker == null)
        {
            _logger.LogDebug("[Warg] HandleWargTargetHit: skipped — attacker null");
            return;
        }

        // Warg-specific victim-team rule: if the victim is a mount, attribute the team to its rider.
        var victimTeamSource = target.IsMount && target.RiderAgent != null ? target.RiderAgent : target;
        if (attacker.RiderAgent != null && attacker.RiderAgent.IsSameTeam(victimTeamSource))
        {
            _logger.LogDebug($"[Warg] HandleWargTargetHit: skipped — same team (rider='{attacker.RiderAgent.Name}', victim='{target.Name}')");
            return;
        }

        try
        {
            if (target.State != AgentState.Active && target.State != AgentState.Routed)
            {
                _logger.LogDebug($"[Warg] HandleWargTargetHit: skipped — target state={target.State} bone={boneId}");
                return;
            }

            float velocity = attacker.MovementVelocity.Y;
            int armor = target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Chest);
            int damage = CalculateWargAttackDamage(target, velocity, armor);

            // Damager attribution: prefer the warg's rider; fall back to the warg itself.
            // If both are absent/dead, vanilla self-damage fallback at 20 damage.
            IAgentAdapter damagerAdapter = attacker.RiderAgent ?? attacker;
            string damagerKind = attacker.RiderAgent != null ? "rider" : "warg-self";
            if (damagerAdapter == null || damagerAdapter.Health <= 0)
            {
                damagerAdapter = target;
                damage = 20;
                damagerKind = "self-fallback";
            }

            if (target.IsHorse() || target.IsCamel())
            {
                damage *= 2;
                damagerKind += "+mount2x";
            }

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
                _logger.LogInfo($"[Warg] BITE HIT: attacker='{attacker.Name}' target='{target.Name}' bone={boneId} vel={velocity:F2} armor={armor} damage={damage}p (Pierce, damager={damagerKind})");
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
        {
            _logger.LogDebug("[Warg] WargAttack: skipped — warg null/inactive");
            return;
        }

        List<sbyte> boneIds;
        float targetDetectionRange = WargConfig.TargetDetectionRange;
        float boneCollisionRadius = 0.3f;
        float actionProgressMax;
        float actionProgressMin;
        ActionIndexCache action;
        string actionName;

        if (warg.MovementVelocity.Y >= 4)
        {
            boneIds = new List<sbyte> { 23 };
            actionName = "act_warg_attack_running";
            action = ActionIndexCache.Create(actionName);
            actionProgressMin = 0.1f;
            actionProgressMax = 0.7f;
            boneCollisionRadius = 0.4f;
        }
        else
        {
            boneIds = new List<sbyte> { 23, 37, 43 };
            actionName = "act_warg_attack_stand";
            action = ActionIndexCache.Create(actionName);
            actionProgressMin = 0.1f;
            actionProgressMax = 0.5f;
        }

        // Diagnostic 2026-05-24: log every WargAttack invocation so we can correlate
        // BT eval rate against actual hit reports in HandleWargTargetHit.
        _logger.LogInfo($"[Warg] WargAttack: attacker='{warg.Name}' action={actionName} vel={warg.MovementVelocity.Y:F2} boneIds=[{string.Join(",", boneIds)}] range={targetDetectionRange:F1}m collisionR={boneCollisionRadius:F2}m");

        warg.CustomAttack(action, boneIds, actionProgressMin, actionProgressMax, targetDetectionRange, boneCollisionRadius, true,
            (attackerAdapter, targetAdapter, boneId) => HandleWargTargetHit(attackerAdapter, targetAdapter, boneId));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

public class AgentAdapter : IAgentAdapter
{
    private readonly Agent _agent;
    private readonly IMissionAdapterFactory _factory;
    private readonly IModLogger _logger;
    private readonly Func<IBoneCollisionService> _boneCollisionServiceFactory;
    // Reusable scan buffer for RadialStrike (the attack is cooldown-gated, but the field buffer keeps the
    // zero-alloc SpatialGrid overload's benefit and matches the engage decorator's pattern).
    private readonly List<Agent> _radialScratch = new();

    public AgentAdapter(Agent agent, IMissionAdapterFactory factory, IModLogger logger, Func<IBoneCollisionService> boneCollisionServiceFactory)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _boneCollisionServiceFactory = boneCollisionServiceFactory ?? throw new ArgumentNullException(nameof(boneCollisionServiceFactory));
    }

    public int Index => _agent.Index;

    public IAgentVisualsAdapter AgentVisuals =>
        _agent != null && _agent.IsActive() && !_agent.IsFadingOut() && _agent.AgentVisuals != null
            ? new AgentVisualsAdapter(_agent.AgentVisuals)
            : null;

    public string Name => _agent.Name;
    public bool IsHuman => _agent.IsHuman;
    public bool HasMount => _agent.HasMount;
    public Vec3 Position => _agent.Position;
    public Vec2 MovementVelocity => _agent.MovementVelocity;
    public MatrixFrame Frame => _agent.Frame;
    public bool IsMount => _agent.IsMount;
    public bool IsAIControlled => _agent?.IsAIControlled ?? false;

    public IAgentAdapter RiderAgent =>
        _agent.RiderAgent != null ? _factory.GetAgentAdapter(_agent.RiderAgent) : null;

    public IAgentAdapter MountAgent =>
        _agent.MountAgent != null ? _factory.GetAgentAdapter(_agent.MountAgent) : null;

    public ActionIndexCache GetCurrentAction(int actionChannelNo) => _agent.GetCurrentAction(actionChannelNo);
    public float GetCurrentActionProgress(int actionChannelNo) => _agent.GetCurrentActionProgress(actionChannelNo);

    internal Agent GetUnderlyingAgent() => _agent;

    public bool IsWarg() => _agent.Monster.StringId == "warg";
    public bool IsHorse() => _agent.Monster.StringId == "horse";
    public bool IsCamel() => _agent.Monster.StringId == "camel";
    public bool IsActive() => _agent?.IsActive() ?? false;
    public bool IsFadingOut() => _agent?.IsFadingOut() ?? false;
    public int Health => (int)(_agent?.Health ?? 0f);
    public AgentState State => _agent?.State ?? AgentState.Killed;

    public bool IsSameTeam(IAgentAdapter other)
    {
        if (other is not AgentAdapter otherImpl) return false;
        var otherAgent = otherImpl.GetUnderlyingAgent();
        if (_agent == null || otherAgent == null) return false;
        return _agent.Team != null && otherAgent.Team != null && _agent.Team == otherAgent.Team;
    }

    public int GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType bodyPart) =>
        (int)(_agent?.GetBaseArmorEffectivenessForBodyPart(bodyPart) ?? 0f);

    public void ProjectAgent(Vec3 position, DamageAnimation animation)
    {
        if (animation == DamageAnimation.Nothing) return;
        if (!_agent.IsHuman || _agent.HasMount) return;

        var blowDirection = CustomAttacksUtils.GetDirectionOfBlow(_agent, position);

        Random random = new();
        string projectionAnimation;
        switch (blowDirection)
        {
            case BlowDirection.Back:
                projectionAnimation = animation == DamageAnimation.Flinch
                    ? HumanAnimationConstants.BackFlinchAnimations[random.Next(0, HumanAnimationConstants.BackFlinchAnimations.Count - 1)]
                    : HumanAnimationConstants.BackFallAnimations[random.Next(0, HumanAnimationConstants.BackFallAnimations.Count - 1)];
                break;
            case BlowDirection.Front:
                projectionAnimation = animation == DamageAnimation.Flinch
                    ? HumanAnimationConstants.FrontFlinchAnimations[random.Next(0, HumanAnimationConstants.FrontFlinchAnimations.Count - 1)]
                    : HumanAnimationConstants.FrontFallAnimations[random.Next(0, HumanAnimationConstants.FrontFallAnimations.Count - 1)];
                break;
            case BlowDirection.Right:
                projectionAnimation = animation == DamageAnimation.Flinch
                    ? HumanAnimationConstants.RightFlinchAnimations[random.Next(0, HumanAnimationConstants.RightFlinchAnimations.Count - 1)]
                    : HumanAnimationConstants.RightFallAnimations[random.Next(0, HumanAnimationConstants.RightFallAnimations.Count - 1)];
                break;
            case BlowDirection.Left:
            default:
                projectionAnimation = animation == DamageAnimation.Flinch
                    ? HumanAnimationConstants.LeftFlinchAnimations[random.Next(0, HumanAnimationConstants.LeftFlinchAnimations.Count - 1)]
                    : HumanAnimationConstants.LeftFallAnimations[random.Next(0, HumanAnimationConstants.LeftFallAnimations.Count - 1)];
                break;
        }
        var action = ActionIndexCache.Create(projectionAnimation);
        _agent.SetActionChannel(0, action, true, 0UL, 0f, 1, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
    }

    public bool IsAttackLikelyToHit(IAgentAdapter attacker, float coneAngle, float attackDistance)
    {
        if (attacker == null) return false;

        var attackerAgent = (attacker as AgentAdapter)?.GetUnderlyingAgent();
        if (attackerAgent == null) return false;

        var distanceToTarget = (_agent.Position - attackerAgent.Position).Length;
        var distanceForAttack = attackDistance + Math.Max(attackerAgent.MovementVelocity.Y, 0);
        if (distanceToTarget > distanceForAttack) return false;

        var halfConeAngleRadians = coneAngle * 0.5f * (float)(Math.PI / 180);
        var attackerLookDirection = attackerAgent.Frame.rotation.f.NormalizedCopy();
        var attackerToVictim = (_agent.Position - attackerAgent.Position).NormalizedCopy();
        var dotProduct = Vec3.DotProduct(attackerLookDirection, attackerToVictim);
        var angleBetween = (float)Math.Acos(dotProduct);

        return angleBetween <= halfConeAngleRadians;
    }

    public void CustomAttack(
        ActionIndexCache action,
        List<sbyte> bonesIdsForCollision,
        float actionProgressMin,
        float actionProgressMax,
        float targetDetectionRange,
        float boneCollisionRadius,
        bool stopOnFirstHit,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onHitCallback,
        Action onExpirationCallback = null)
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            _logger.LogWarning("AgentAdapter:CustomAttack: attempt to use on a null or dead agent.");
            return;
        }

        _agent.SetActionChannel(0, action, true);

        if (SpatialGrid.Instance == null)
        {
            _logger.LogWarning("AgentAdapter:CustomAttack: SpatialGrid not initialized.");
            return;
        }

        // Filter rationale (2026-05-24 fix per #219 log evidence):
        //   agt != _agent            — exclude the warg itself
        //   agt != _agent.RiderAgent — exclude the warg's own rider. The original
        //       check `agt.RiderAgent != _agent` was a no-op for humans because
        //       Agent.RiderAgent is only populated on mounts (returns the rider).
        //       On a human rider it returns null, so the filter NEVER excluded
        //       the rider — the warg's bone collision was detecting its own
        //       rider as a target ~50% of the time, consuming the attack
        //       opportunity via stopOnFirstHit before reaching an enemy.
        //   agt.IsActive()           — alive check
        var targets = SpatialGrid.Instance.GetNearAliveAgentsInRange(targetDetectionRange, _agent)
            .FindAll(agt => agt != _agent && agt != _agent.RiderAgent && agt.IsActive())
            .Select(x => _factory.GetAgentAdapter(x))
            .ToList();

        if (targets.Count == 0) return;

        var boneCollisionService = _boneCollisionServiceFactory();
        boneCollisionService.AddBoneCheckComponent(new BoneCheckDuringAnimation(
            action,
            this,
            targets,
            bonesIdsForCollision,
            actionProgressMin,
            actionProgressMax,
            boneCollisionRadius,
            stopOnFirstHit,
            onHitCallback,
            onExpirationCallback
        ));
    }

    public void RadialStrike(
        ActionIndexCache action,
        float strikeRadius,
        float arcHalfAngleDeg,
        float arcCenterBearingDeg,
        Action<IAgentAdapter, IAgentAdapter, sbyte> onHitCallback)
    {
        if (_agent == null || !_agent.IsActive() || _agent.IsFadingOut())
        {
            _logger.LogWarning("AgentAdapter:RadialStrike: attempt to use on a null or dead agent.");
            return;
        }

        _agent.SetActionChannel(0, action, true);

        if (SpatialGrid.Instance == null)
        {
            _logger.LogWarning("AgentAdapter:RadialStrike: SpatialGrid not initialized.");
            return;
        }

        // Radial-in-arc: fire onHitCallback for every enemy within strikeRadius whose horizontal bearing from our
        // look-direction is inside [arcCenter ± arcHalfAngle] (signed bearing, + = LEFT). Instant at strike time —
        // the elephant's reliable radial model, replacing the bone-collision that connected only ~6% on this big
        // fast mount (the team check + damage live in the callback, HandleSpiderTargetHit). Excludes self + rider.
        SpatialGrid.Instance.GetNearAliveAgentsInRange(strikeRadius, _agent, _radialScratch);
        if (_radialScratch.Count == 0) return;

        Vec3 lookDir = _agent.LookDirection;
        float lookLenSq = lookDir.x * lookDir.x + lookDir.y * lookDir.y;
        if (lookLenSq < 1e-6f) return;                       // degenerate facing — skip this strike
        float invLook = 1f / (float)Math.Sqrt(lookLenSq);
        float lx = lookDir.x * invLook, ly = lookDir.y * invLook;

        for (int i = 0; i < _radialScratch.Count; i++)
        {
            Agent target = _radialScratch[i];
            if (target == _agent || target == _agent.RiderAgent || !target.IsActive()) continue;

            float dx = target.Position.x - _agent.Position.x;
            float dy = target.Position.y - _agent.Position.y;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-4f) continue;                     // on top of us — no meaningful bearing
            float inv = 1f / (float)Math.Sqrt(lenSq);
            float tx = dx * inv, ty = dy * inv;

            float dot = lx * tx + ly * ty;                   // forward-ness
            float cross = lx * ty - ly * tx;                 // side (+ = left)
            float signedAngleDeg = (float)(Math.Atan2(cross, dot) * (180.0 / Math.PI));
            if (Math.Abs(signedAngleDeg - arcCenterBearingDeg) > arcHalfAngleDeg) continue;

            onHitCallback(this, _factory.GetAgentAdapter(target), 0);
        }
    }
}

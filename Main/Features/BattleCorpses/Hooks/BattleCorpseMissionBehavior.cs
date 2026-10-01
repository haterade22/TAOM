using System;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MountDespawn.Hooks;

namespace TAOM.Features.BattleCorpses.Hooks;

/// <summary>
/// Sets the battle's corpse fade time and corpse cap (#701), and logs one <c>[BattleSettings]</c>
/// line naming the player's ragdoll, corpse and battle-size options beside them, so a freeze report's
/// log says what the player was running.
///
/// Applied from <c>OnMissionTick</c>: the gate admits only Battle mode, so the first apply lands within a
/// second of deployment ending. Re-checked once a second so an MCM change mid-battle takes effect, and
/// re-applied only when the limits change. Both setters only write a mission field (native, v1.5.3);
/// the mission tick and the corpse limiter read it, and native's mission reset restores the defaults,
/// so nothing carries into the next mission. A fresh instance is built for every mission.
///
/// Shares <see cref="MountDespawnMissionGate"/>: field battles, sieges and sally-outs only, so stealth
/// missions that set <c>DisableCorpseFadeOut</c> and drag corpses, and towns, arenas and hideouts, are
/// never touched.
/// </summary>
public sealed class BattleCorpseMissionBehavior : MissionBehavior
{
    private const float CheckIntervalSeconds = 1f;

    private readonly BattleCorpsePolicy _policy;
    private readonly IGraphicsOptionsAdapter _options;
    private readonly IModLogger _logger;

    private float _accumulator = CheckIntervalSeconds;
    private BattleCorpseLimits? _last;
    private bool _disabled;

    // Other, not Logic: this class derives from MissionBehavior, and Logic makes vanilla add a null to MissionLogics.
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public BattleCorpseMissionBehavior(BattleCorpsePolicy policy, IGraphicsOptionsAdapter options, IModLogger logger)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_disabled) return;

        _accumulator += dt;
        if (_accumulator < CheckIntervalSeconds) return;
        _accumulator = 0f;

        try
        {
            var mission = Mission;
            if (!MountDespawnMissionGate.IsEligible(mission)) return;

            var limits = _policy.Resolve(_options.CorpseOption);
            if (limits == _last) return;

            MissionThreadGuard.NoteCall("BattleCorpseMissionBehavior.Apply", _logger.LogWarning);
            mission.SetMissionCorpseFadeOutTimeInSeconds(limits.FadeSeconds);
            mission.SetOverrideCorpseCount(limits.CorpseCap);
            _last = limits;

            _logger.LogInfo(BattleCorpsePolicy.DescribeLine(
                _options.RagdollOption, _options.CorpseOption, _options.BattleSizeOption, limits));
        }
        catch (Exception ex)
        {
            // Never take a battle down over a cleanup setting. Whatever the setters already wrote stays
            // for this mission; native's reset clears it for the next.
            _disabled = true;
            _logger.LogError($"[BattleCorpses] disabled for this mission after {ex.GetType().Name}: {ex.Message}");
        }
    }
}

using System;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.DreadAura;
using TAOM.Features.SignatureStrikes.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// Entry point for signature strikes (#605): roster, enqueue a ring on a signature hero's melee
/// hit, drain it one tick later, stand down on failure. Design: <c>docs/features/signature-strikes.md</c>.
/// Inherits <see cref="MissionLogic"/>, NOT <see cref="MissionBehavior"/> (<c>rca-looter-battle-nre-2026-05-24.md</c>;
/// pinned by SignatureStrikesBindingTests). <c>OnMeleeHit</c> only ENQUEUES: it runs inside the
/// engine's <c>MeleeHitCallback</c> with the swing's momentum a live <c>ref</c>, so a blow
/// registered there would re-enter the hit pipeline. The ring lands on the next <c>OnMissionTick</c>.
/// </summary>
public sealed class SignatureStrikesMissionLogic : MissionLogic
{
    private readonly ISignatureStrikeService _service;
    private readonly ISignatureAgentRoster _roster;
    private readonly IModLogger _logger;
    private readonly SignatureStrikeRunner _runner;
    private readonly StrikeRequestBuffer _buffer = new StrikeRequestBuffer();

    private bool _eligible;
    private bool _scanned;
    private bool _disabledForThisMission;

    public SignatureStrikesMissionLogic()
    {
        _service = IoC.Resolve<ISignatureStrikeService>();
        _roster = IoC.Resolve<ISignatureAgentRoster>();
        _logger = IoC.Resolve<IModLogger>();
        _runner = new SignatureStrikeRunner(_service, IoC.Resolve<IDreadRegistry>(), _logger);
    }

    public override void OnBehaviorInitialize()
    {
        base.OnBehaviorInitialize();
        Reset();
        _eligible = SignatureMissionGate.IsEligible(Mission);
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);
        if (!_eligible || _disabledForThisMission)
            return;

        try
        {
            _roster.TryRegister(agent);
        }
        catch (Exception ex)
        {
            StandDown($"registration failed, {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override void OnAgentDeleted(Agent affectedAgent)
    {
        _roster.Remove(affectedAgent);
    }

    public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
    {
        if (!_eligible || _disabledForThisMission || !_roster.TryGet(attacker, out var entry))
            return;
        // Tripwire, not a lock: this callback shares the engine chain OnAgentHit runs on.
        MissionThreadGuard.NoteCall("SignatureStrikes.OnMeleeHit",
            m => TaleWorlds.Library.Debug.Print(m, 0, TaleWorlds.Library.Debug.DebugColor.Red));

        try
        {
            var now = Mission.CurrentTime;
            var context = StrikeContextFactory.FromMeleeCollision(
                attacker, victim, in collisionData, isCanceled, BlowFlags.None, entry, now);
            var effect = _service.Evaluate(in context);
            if (!effect.HasValue)
                return;
            // Stamp at enqueue: the later bodies of one cleaving swing read as inside the cooldown.
            if (effect.Value.Kind == StrikeKind.Slam)
                entry.LastSlamTime = now;
            else
                entry.LastSweepTime = now;
            _buffer.Enqueue(new StrikeRequest(
                attacker, victim, collisionData.CollisionGlobalPosition, effect.Value, attacker.Name));
        }
        catch (Exception ex)
        {
            StandDown($"hit evaluation failed, {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (!_eligible || _disabledForThisMission)
            return;
        // Patch37_CrashReport turns a per-frame throw into a crash-report flood: fail once, stand down.
        try
        {
            // One-shot safety net for agents built before OnAgentBuild reached this behavior
            // (the DreadAura and warg trackers carry the same scan). Idempotent.
            if (!_scanned)
            {
                _scanned = true;
                foreach (var agent in Mission.AllAgents)
                    _roster.TryRegister(agent);
            }
            if (_buffer.PendingCount == 0)
                return;
            var due = _buffer.Swap();
            for (var i = 0; i < due.Count; i++)
                _runner.Run(Mission, due[i]);
        }
        catch (Exception ex)
        {
            StandDown($"ring failed, {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        Reset();
    }

    // Agent references must not outlive the mission (the roster is a process singleton).
    private void Reset()
    {
        _roster.Clear();
        _buffer.Clear();
        _runner.Clear();
        _eligible = false;
        _scanned = false;
        _disabledForThisMission = false;
    }

    private void StandDown(string reason)
    {
        _disabledForThisMission = true;
        _buffer.Clear();
        _logger.LogError($"[SignatureStrikes] disabled for this mission after {reason}");
    }
}

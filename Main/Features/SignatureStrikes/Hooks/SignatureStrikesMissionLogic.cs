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
/// Inherits <see cref="MissionLogic"/>, NOT <see cref="MissionBehavior"/> (<c>rca-looter-battle-nre-2026-05-24.md</c>).
/// <c>OnMeleeHit</c> only ENQUEUES: it runs inside the engine's <c>MeleeHitCallback</c> with the
/// swing's momentum a live <c>ref</c>; the ring lands on the next <c>OnMissionTick</c>.
/// No <c>OnBehaviorInitialize</c>: the engine dispatches it before TAOM's behaviors are added, so
/// it never runs for them (#606); the gate is read on first use, state resets in <c>OnCreated</c>.
/// </summary>
public sealed class SignatureStrikesMissionLogic : MissionLogic
{
    private readonly ISignatureStrikeService _service;
    private readonly ISignatureAgentRoster _roster;
    private readonly IModLogger _logger;
    private readonly SignatureStrikeRunner _runner;
    private readonly StrikeRequestBuffer _buffer = new StrikeRequestBuffer();
    private bool? _eligible;
    private bool _scanned;
    private bool _disabledForThisMission;

    public SignatureStrikesMissionLogic()
    {
        _service = IoC.Resolve<ISignatureStrikeService>();
        _roster = IoC.Resolve<ISignatureAgentRoster>();
        _logger = IoC.Resolve<IModLogger>();
        _runner = new SignatureStrikeRunner(_service, IoC.Resolve<IDreadRegistry>(), _logger);
    }

    public override void OnCreated() => Reset();

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (!Active())
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

    public override void OnAgentDeleted(Agent affectedAgent) => _roster.Remove(affectedAgent);

    public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
    {
        if (!Active() || !_roster.TryGet(attacker, out var entry))
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
            _logger.LogInfo($"[SignatureStrikes] hit by {attacker.Name}: {context.Direction} {context.Collision} dmg={context.InflictedDamage} weapon={context.HasMeleeWeapon} canceled={isCanceled} victim={(victim == null ? "ground" : victim.Name)} -> {(effect.HasValue ? effect.Value.Kind.ToString() : "no effect")}");
            if (!effect.HasValue)
                return;
            // Stamp at enqueue: the later bodies of one cleaving swing read as inside the cooldown.
            entry.Times = entry.Times.With(effect.Value.Kind, now);
            // A scream rings the wraith where it stands; a slam rings the point the weapon hit.
            var center = effect.Value.Origin == StrikeOrigin.Self ? attacker.Position : collisionData.CollisionGlobalPosition;
            _buffer.Enqueue(new StrikeRequest(attacker, victim, center, effect.Value, attacker.Name));
        }
        catch (Exception ex)
        {
            StandDown($"hit evaluation failed, {ex.GetType().Name}: {ex.Message}");
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (!Active())
            return;
        try
        {
            // One-shot safety net for agents built before this behavior saw them. Idempotent.
            if (!_scanned)
            {
                _scanned = true;
                _roster.RegisterAll(Mission.AllAgents);
                _logger.LogInfo($"[SignatureStrikes] first-tick scan: {_roster.Count} signature agent(s) on the field");
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

    protected override void OnEndMission() => Reset();

    // Read once per mission on first use; the combat type is set by native InitializeMission by then.
    private bool Active()
    {
        if (_disabledForThisMission)
            return false;
        _eligible ??= SignatureMissionGate.IsEligible(Mission, _logger);
        return _eligible.Value;
    }

    // Agent references must not outlive the mission (the roster is a process singleton).
    private void Reset()
    {
        _roster.Clear();
        _buffer.Clear();
        _runner.Clear();
        _eligible = null;
        _scanned = false;
        _disabledForThisMission = false;
    }

    // Clears the roster too: the model probes it and would grant free knockdowns (Codex 114, F1).
    private void StandDown(string reason)
    {
        _disabledForThisMission = true;
        _buffer.Clear();
        _roster.Clear();
        _logger.LogError($"[SignatureStrikes] disabled for this mission after {reason}");
    }
}

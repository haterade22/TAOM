using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.DreadAura;
using TAOM.Features.DreadAura.Hooks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// Applies one queued strike: its sound, then the enemies near the ring's centre, each one's
/// falloff, a synthetic blow through <see cref="CustomAttacksUtils.TakeDamage"/> and a one-shot
/// morale drain. The struck foe takes the drain but never the ring's blow: it already took the
/// real one (#645; before that it was skipped outright, so a slam's fear missed the agent Sauron
/// actually hit). Split from <see cref="SignatureStrikesMissionLogic"/> so the entry point owns only
/// the gate and the queue (ADR-002). Boundary class: it touches <c>Agent</c> directly and delegates
/// every number to <see cref="ISignatureStrikeService"/>.
///
/// The fear burst reuses DreadAura's policy-free pieces (<see cref="DreadAgentGate.CanAffect"/>,
/// <see cref="IDreadRegistry.ResolveResist"/>, and the CALL to the registered
/// <c>BattleMoraleModel</c> for tier and hero resistance) but not its drain service, which is
/// gated on the Dread Aura toggle and clamped to Dread's own morale floor.
/// </summary>
public sealed class SignatureStrikeRunner
{
    private readonly ISignatureStrikeService _service;
    private readonly IDreadRegistry _dreadRegistry;
    private readonly IModLogger _logger;
    private readonly StrikeSoundPlayer _sound;

    // Reused across rings. Every Mission.GetNearby* overload Clear()s the list it is handed.
    private readonly MBList<Agent> _nearbyBuffer = new MBList<Agent>();

    public SignatureStrikeRunner(ISignatureStrikeService service, IDreadRegistry dreadRegistry, IModLogger logger)
    {
        _service = service;
        _dreadRegistry = dreadRegistry;
        _logger = logger;
        _sound = new StrikeSoundPlayer(logger);
    }

    public void Clear()
    {
        _nearbyBuffer.Clear();
        _sound.Clear();
    }

    public void Run(Mission mission, in StrikeRequest request)
    {
        var attacker = request.Attacker;

        // One frame has passed since the hit. The engine recycles a deleted agent's index and the
        // managed handle keeps answering for the new tenant (#592): only the slot's current
        // occupant may deal the ring.
        if (attacker == null || !attacker.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(attacker))
            return;

        var team = attacker.Team;
        if (team == null)
            return;

        var effect = request.Effect;
        var center = request.Center.AsVec2;

        // The strike happened, so its voice plays even when the ring finds nobody.
        var sound = _sound.Play(mission, attacker, effect.Sound);

        // Nothing to apply (a profile with no damage share and no fear): no query, no log line.
        if (!(effect.DamageFraction > 0f) && !(effect.FearMorale > 0f))
            return;

        // The centre is an engine float handed straight to a native query; gate it here, before
        // the boundary, not after (csharp-architecture.md "Engine-Float Decision Gates").
        if (!FiniteFloatValidator.IsFinite(center.x) || !FiniteFloatValidator.IsFinite(center.y))
            return;

        // Enemy filtering happens native-side, so allies never enter the loop at all.
        _nearbyBuffer.Clear();
        mission.GetNearbyEnemyAgents(center, effect.OuterRadius, team, _nearbyBuffer);

        var moraleModel = MissionGameModels.Current?.BattleMoraleModel;
        var hit = 0;
        var feared = 0;
        var skipped = 0;
        var moraleTaken = 0f;

        foreach (var victim in _nearbyBuffer)
        {
            // GetNearbyAgentsAux adds its `GetManagedObjectWithId(...) as Agent` result
            // UNCONDITIONALLY, so a reclaimed id lands here as null.
            if (victim == null
                || ReferenceEquals(victim, attacker)
                || !victim.IsActive()
                || victim.IsFadingOut()
                || victim.CurrentMortalityState == Agent.MortalityState.Invulnerable
                || victim.IsMount
                || !victim.IsEnemyOf(attacker))
            {
                skipped++;
                continue;
            }

            var distance = (victim.Position.AsVec2 - center).Length;
            var falloff = SignatureStrikeFalloff.Compute(distance, effect.InnerRadius, effect.OuterRadius);
            if (!(falloff > 0f))
            {
                skipped++;
                continue;
            }

            // Fear before damage: the blow may kill the victim, and morale on a corpse is harmless
            // while a blow on a fading agent is the crash class TakeDamage guards against.
            if (effect.FearMorale > 0f && moraleModel != null && DreadAgentGate.CanAffect(victim))
            {
                var scaled = moraleModel.CalculateMoraleChangeToCharacter(victim, effect.FearMorale);
                var drain = _service.ComputeFearDrain(
                    scaled, falloff, _dreadRegistry.ResolveResist(victim.Character?.Race), victim.GetMorale());
                if (drain > 0f)
                {
                    victim.ChangeMorale(-drain);
                    feared++;
                    moraleTaken += drain;
                }
            }

            // The struck foe already took the real blow and the model's knockdown or knock-back
            // verdict; the ring adds its fear, never a second hit.
            if (ReferenceEquals(victim, request.PrimaryVictim))
                continue;

            var blocking = victim.GetCurrentActionType(1) == Agent.ActionCodeType.DefendShield;
            var damage = _service.ComputeRingDamage(effect.DamageBasis, effect.DamageFraction, falloff, blocking);
            if (damage <= 0)
            {
                skipped++;
                continue;
            }

            CustomAttacksUtils.TakeDamage(
                victim, attacker, damage, effect.Magnitude,
                knockDown: effect.KnockDown && !blocking,
                extraFlags: effect.KnockBack ? BlowFlags.KnockBack : BlowFlags.None);
            hit++;
        }

        _logger.LogInfo(
            $"[SignatureStrikes] {effect.Kind} ('{effect.SignatureId}') by {request.AttackerName}: basis {effect.DamageBasis}, ring={hit} hit, {feared} feared (morale -{moraleTaken:0.#}), {skipped} skipped, sound={sound}");
    }
}

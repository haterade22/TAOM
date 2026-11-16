using System.Globalization;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SignatureStrikes.Hooks;

/// <summary>
/// The one instant-kill executor (#1359). A strike's kill chance is rolled here, on the main
/// tick, never in the hit callback: the callback holds a live momentum ref, and a blow registered
/// inside it re-enters the hit pipeline. The roll and every victim fact the pure decision needs
/// are read here; the decision and the damage number come from <see cref="ISignatureStrikeService"/>.
/// The Phase 5 ranged kit calls the same method. Main thread only.
/// </summary>
public sealed class SignatureInstantKill
{
    internal const string CheckId = "taom.signature-strikes.instant-kill";
    private const string RollsCounterId = "taom.signature-strikes.instant-kill-rolls";

    private readonly ISignatureStrikeService _service;
    private readonly ISignatureAgentRoster _roster;
    private readonly ISignatureStrikeRegistry _registry;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly OutcomeCounter _rolls;

    public SignatureInstantKill(
        ISignatureStrikeService service,
        ISignatureAgentRoster roster,
        ISignatureStrikeRegistry registry,
        IOutcomeLog outcomes,
        IModLogger logger)
    {
        _service = service;
        _roster = roster;
        _registry = registry;
        _outcomes = outcomes;
        _logger = logger;
        _rolls = outcomes.Counter(RollsCounterId);
    }

    /// <summary>Rolls the chance and, on a win against an eligible victim, writes a lethal blow.
    /// True when the victim died of it.</summary>
    public bool TryKill(Agent attacker, Agent? victim, float killChance, float magnitude, string signatureId)
    {
        if (!(killChance > 0f) || attacker == null)
            return false;

        // One frame has passed since the hit: the victim may be fading or its slot reused (#592).
        // CustomAttacksUtils.TakeDamage re-checks too, but the roll must not be spent on a corpse.
        if (victim == null
            || !victim.IsActive()
            || victim.IsFadingOut()
            || !AgentSlotIdentity.IsCurrentOccupant(victim)
            || !(victim.Health > 0f))
            return false;

        var health = victim.Health;
        var roll = MBRandom.RandomFloat;
        var isPlayer = victim.IsMainAgent || victim.IsPlayerControlled || !victim.IsAIControlled;
        var isGeneral = ReferenceEquals(victim, victim.Team?.GeneralAgent);
        var isBearer = _roster.TryGet(victim, out _);
        var race = victim.Character?.Race;

        if (!_service.DecideInstantKill(
                killChance, roll, victim.IsHuman, victim.IsHero, isPlayer, isGeneral, isBearer,
                _registry.IsKillImmuneRace(race), RaceAbilityHooks.IsShruggingOff(victim), health))
        {
            _rolls.Fail();
            return false;
        }

        var damage = _service.InstantKillDamage(health);
        if (damage <= 0)
        {
            _rolls.Fail();
            return false;
        }

        CustomAttacksUtils.TakeDamage(victim, attacker, damage, magnitude, knockDown: false);

        var died = !victim.IsActive() || !(victim.Health > 0f);
        if (died)
            _rolls.Pass();
        else
            _rolls.Fail();

        var troop = victim.Character?.StringId ?? "unknown";
        var detail = string.Format(CultureInfo.InvariantCulture,
            "signature={0} victim={1} chance={2:F2} roll={3:F3} health={4:F0} damage={5} died={6}",
            signatureId, troop, killChance, roll, health, damage, died ? 1 : 0);
        LongDefeatChecks.Record(_outcomes, CheckId, signatureId,
            new CheckVerdict(died ? CheckResult.Pass : CheckResult.Fail, detail));
        _logger.LogInfo($"[SignatureStrikes] instant kill ('{signatureId}'): {detail}");
        return died;
    }
}

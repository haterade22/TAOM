using System;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.DreadAura.Hooks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// A death, after the engine's removal callback (deferred to the main thread when it arrives off it):
/// forgets the dead soldier, remembers him as fallen kin, and credits his killer, whose live ability may
/// lengthen, heal him and frighten the enemies around the body. The values are the ones captured at the
/// callback, because by now the dead agent's slot may hold a new agent whose native state the old handle
/// would read (#592); the killer is a held handle, so it is re-checked before anything touches it. The
/// service decides who earns credit and how much a heal may give. Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class RaceAbilityDeaths
{
    private readonly RaceAbilityRuntime _runtime;
    private readonly MBList<Agent> _scratch = new MBList<Agent>();

    public RaceAbilityDeaths(RaceAbilityRuntime runtime) => _runtime = runtime;

    // On the main thread this runs inline, inside the engine's removal loop, which guards no behavior: a throw
    // here would skip the engine's own removal bookkeeping, so it is caught and reported once per battle.
    public void OnAgentRemoved(Agent affected, bool victimIsSoldier, Agent? killer, bool died, Vec2 position, Team? team,
        int? race, string? culture, float now)
    {
        try
        {
            Handle(affected, victimIsSoldier, killer, died, position, team, race, culture, now);
        }
        catch (Exception ex)
        {
            _runtime.ReportFailure(nameof(RaceAbilityDeaths), ex);
        }
    }

    // Mission end: the buffer would otherwise keep the last fear's victims, and the mission, alive.
    internal void Clear() => _scratch.Clear();

    private void Handle(Agent affected, bool victimIsSoldier, Agent? killer, bool died, Vec2 position, Team? team,
        int? race, string? culture, float now)
    {
        MissionThreadGuard.NoteCall("RaceAbilityDeaths.OnAgentRemoved", _runtime.Warn);
        var store = _runtime.Store;
        var service = _runtime.Service;
        store.Remove(affected);
        if (died && team != null && race.HasValue)
            _runtime.Fallen.Remember(position.x, position.y, team, race.Value, _runtime.Resolver.Resolve(race, culture), now);

        if (killer == null)
            return;
        var killerAlive = killer.IsActive() && AgentSlotIdentity.IsCurrentOccupant(killer);
        var killerProfile = killerAlive ? _runtime.ProfileOf(killer) : null;
        if (!service.CreditsKill(died, victimIsSoldier, killer == affected, killerAlive, killer.Team == team, killerProfile != null))
            return;
        store.RecordKill(killer, now);

        var state = store.Get(killer);
        var effects = state?.CurrentEffects;
        if (state == null || effects == null)
            return;
        var abilityId = state.Profile.AbilityId;
        var telemetry = _runtime.Telemetry;
        telemetry.Add(abilityId, RaceAbilityStat.Kills);
        if (state.Phase == RaceAbilityPhase.Active)
        {
            var extended = service.ExtendOnKill(state.PhaseEndsAt, state.ActivatedAt, state.Profile);
            if (extended > state.PhaseEndsAt)
            {
                store.ExtendActive(killer, extended);
                telemetry.Add(abilityId, RaceAbilityStat.Extensions);
            }
        }
        var health = killer.Health;
        var healed = service.HealOnKill(health, killer.HealthLimit, effects.HealPerKill);
        if (healed > health)
        {
            killer.Health = healed;
            telemetry.Add(abilityId, RaceAbilityStat.HealthHealed, (long)Math.Round(healed - health));
        }
        if (effects.FearOnKillMorale > 0f && effects.FearOnKillRadius > 0f && killer.Team != null)
            Frighten(killer, position, effects.FearOnKillRadius, effects.FearOnKillMorale, abilityId);
    }

    // Scaled through the registered morale model, as the Dread Aura and the signature strikes do, so in a
    // campaign the victim's tier and hero resistance apply (Custom Battle's characters all resist alike);
    // gated like them to live AI humans with morale to lose.
    private void Frighten(Agent killer, Vec2 at, float radius, float morale, string abilityId)
    {
        var moraleModel = MissionGameModels.Current?.BattleMoraleModel;
        if (moraleModel == null)
            return;
        _scratch.Clear();
        Mission.Current.GetNearbyEnemyAgents(at, radius, killer.Team, _scratch);
        foreach (var victim in _scratch)
        {
            if (!DreadAgentGate.CanAffect(victim))
                continue;
            var drain = moraleModel.CalculateMoraleChangeToCharacter(victim, morale);
            if (!(drain > 0f))
                continue;
            victim.ChangeMorale(-drain);
            _runtime.Telemetry.Add(abilityId, RaceAbilityStat.Frightened);
        }
    }
}

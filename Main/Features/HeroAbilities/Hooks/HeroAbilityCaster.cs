using System;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.DreadAura.Hooks;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.RaceAbilities.Domain;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

internal readonly record struct CastResult(int Allies, int Shocked);

/// <summary>
/// Casts one hero ability (#1359): the hero's own grant, the grants on his allies in reach, the morale shock on the
/// enemies in reach, the cry, the sparks, the outline and the message. Main thread only (the mission tick). A grant
/// is published before the stats refresh that reads it, as HeroAuraPulse does. Holds agents only inside one call.
/// Boundary code, game-tested (ADR-008).
/// </summary>
internal sealed class HeroAbilityCaster
{
    // Mission.GetNearbyAllyAgents searches the proximity grid up to 15 m and walks every agent past it.
    private const float MaxRadius = 15f;

    private readonly HeroAbilityRuntime _runtime;
    private readonly string _missionKey;
    private readonly MBList<Agent> _scratch = new MBList<Agent>();
    private bool _sampled;

    internal HeroAbilityCaster(HeroAbilityRuntime runtime, string missionKey)
    {
        _runtime = runtime;
        _missionKey = missionKey;
    }

    // Grants made this mission, and whether one of them gave the stats-refreshed sample.
    internal int Grants { get; private set; }

    internal bool Sampled => _sampled;

    internal CastResult Cast(Mission mission, Agent hero, HeroAbilityConfig ability, uint? glow, float now)
    {
        MissionThreadGuard.NoteCall("HeroAbilityCaster.Cast", _runtime.RaceAbilities.Warn);
        var until = now + ability.DurationSeconds;
        Grant(hero, ability.Id + ":self", _runtime.Service.SelfEffects(ability), until, ability.Id);
        if (ability.ExtraArrows > 0)
            _runtime.Bursts[hero] = new HeroBurst(until, ability.ExtraArrows);
        var team = hero.Team;
        var allies = team == null ? 0 : Allies(mission, hero, team, ability, until);
        var shocked = team == null ? 0 : Shock(mission, hero, team, ability);

        var visuals = _runtime.RaceAbilities.Visuals;
        // The race abilities' own switch (RaceAbilityActivator.Activate): the MCM option mutes the cry.
        if (_runtime.RaceAbilities.Settings.WarCries)
            RaceAbilityActivator.WarCry(hero, ability.WarCry);
        visuals.Burst(hero);
        if (glow.HasValue)
            visuals.Outline(hero, glow);

        var playerSide = team != null && (team.IsPlayerTeam || team.IsPlayerAlly);
        var line = HeroAbilityNames.Cast(hero.NameTextObject, ability.Id, playerSide).ToString();
        InformationManager.DisplayMessage(playerSide ? new InformationMessage(line) : new InformationMessage(line, Colors.Red));
        _runtime.Logger.LogInfo($"[HeroAbilities] {ability.Id} by {hero.Character?.StringId} at {now:0.0} s: allies={allies} shocked={shocked}");
        return new CastResult(allies, shocked);
    }

    // Mission end: the buffer would otherwise keep the last cast's soldiers, and the mission, alive.
    internal void Clear()
    {
        _scratch.Clear();
        _sampled = false;
        Grants = 0;
    }

    // The engine's own guard order: a deleted agent's handle answers for the slot's next tenant (#592).
    internal static void Refresh(Agent agent)
    {
        if (agent == null || !agent.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(agent))
            return;
        agent.UpdateAgentProperties();
        // A rider's mount speed rides its own stats, through its rider (#611).
        agent.MountAgent?.UpdateAgentProperties();
    }

    // The query's "ally" is every team not hostile to the hero's, and it returns the hero himself: the buff is for his
    // own team only, and in co-op for this peer's own soldiers.
    private int Allies(Mission mission, Agent hero, Team team, HeroAbilityConfig ability, float until)
    {
        if (!(ability.Radius > 0f))
            return 0;
        _scratch.Clear();
        mission.GetNearbyAllyAgents(hero.Position.AsVec2, Math.Min(ability.Radius, MaxRadius), team, _scratch);
        var count = 0;
        foreach (var ally in _scratch)
        {
            if (ally == null || ReferenceEquals(ally, hero) || !ally.IsHuman || !ally.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(ally)
                || ally.Team != team || !CoopMissionOwnership.IsOwnedHere(ally))
                continue;
            // A bow or crossbow with ammunition (Agent.cs:917).
            if (ability.AlliesRangedOnly && !ally.IsRangedCached)
                continue;
            Grant(ally, ability.Id, ability.AllyEffects, until, ability.Id);
            count++;
            // The engine gives the player's own agent no morale component (GetMorale answers -1).
            if (ally.IsAIControlled)
            {
                var gain = _runtime.Service.AllyMoraleGain(ally.GetMorale(), ability.AllyMorale, ability.AllyEffects.MoraleFloor);
                if (gain > 0f)
                    ally.ChangeMorale(gain);
            }
        }
        _scratch.Clear();
        return count;
    }

    private int Shock(Mission mission, Agent hero, Team team, HeroAbilityConfig ability)
    {
        if (!(ability.EnemyShockRadius > 0f) || !(ability.EnemyShockMorale > 0f))
            return 0;
        var model = MissionGameModels.Current?.BattleMoraleModel;
        _scratch.Clear();
        mission.GetNearbyEnemyAgents(hero.Position.AsVec2, Math.Min(ability.EnemyShockRadius, MaxRadius), team, _scratch);
        var count = 0;
        foreach (var enemy in _scratch)
        {
            if (enemy == null || !AgentSlotIdentity.IsCurrentOccupant(enemy))
                continue;
            if (MoraleShock.Apply(enemy, ability.EnemyShockMorale, 1f, model, _runtime.CareerStats, _runtime.DreadRegistry) > 0f)
                count++;
        }
        _scratch.Clear();
        return count;
    }

    private void Grant(Agent agent, string source, RaceAbilityEffects effects, float until, string abilityId)
    {
        Grants++;
        if (!_sampled && effects.SwingSpeedPercent > 0f)
        {
            Sample(agent, source, effects, until, abilityId);
            return;
        }
        _runtime.Ledger.Grant(agent, source, effects, until);
        Refresh(agent);
    }

    // Proves the refresh reaches the models: the swing speed before and after the grant, against what the merge says it
    // should be. The wanted ratio is computed here from the effects read before the grant, never from the hooks after
    // it: a ledger that stopped reaching the hooks would then want 1 and sample nothing. Taken once per mission, on the first grant that moves the swing speed.
    private void Sample(Agent agent, string source, RaceAbilityEffects effects, float until, string abilityId)
    {
        Refresh(agent);
        var swingBefore = agent.AgentDrivenProperties?.SwingSpeedMultiplier ?? float.NaN;
        var before = RaceAbilityHooks.MergedEffectsOf(agent);
        _runtime.Ledger.Grant(agent, source, effects, until);
        Refresh(agent);
        var swingAfter = agent.AgentDrivenProperties?.SwingSpeedMultiplier ?? float.NaN;
        var want = HeroAbilityChecks.SwingRatio(before, RaceAbilityEffectsMerge.Combine(before, effects));
        if (!HeroAbilityChecks.IsStatsSample(want))
            return;
        _sampled = true;
        LongDefeatChecks.Record(_runtime.Outcomes, HeroAbilityChecks.StatsId, _missionKey,
            HeroAbilityChecks.Stats(abilityId, agent.Character?.StringId ?? "none", swingAfter / swingBefore, want));
    }
}

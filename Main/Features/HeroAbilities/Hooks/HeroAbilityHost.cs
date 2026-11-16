using System;
using System.Collections.Generic;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.RaceAbilities;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The heroes of one mission and their ability clocks (#1359), run once a second from the mission tick as DwarfTreeHost
/// runs the dwarves' trees: every hero soldier's one tree slot holds the race-ability tree (csharp-architecture.md item 3).
/// Each pass skips a body that is no longer its slot's occupant (#592), a co-op puppet (only the owner casts), and a hero
/// the AI does not drive. A dropped hero keeps his record for the end-of-mission check. Main thread only.
/// Boundary code, game-tested (ADR-008).
/// </summary>
internal sealed class HeroAbilityHost
{
    private sealed class HeroRecord
    {
        internal HeroRecord(Agent agent, HeroKitConfig kit, int serial, bool hasContactRule, int contactCount, float contactRange)
        {
            Agent = agent;
            Kit = kit;
            Serial = serial;
            HasContactRule = hasContactRule;
            ContactCount = contactCount;
            ContactRange = contactRange;
            LastFired = new float?[kit.Abilities.Count];
        }

        internal Agent Agent { get; }
        internal HeroKitConfig Kit { get; }
        internal int Serial { get; }
        // The kit's easiest enemy trigger, resolved once; a kit with none has no contact.
        internal bool HasContactRule { get; }
        internal int ContactCount { get; }
        internal float ContactRange { get; }
        internal float?[] LastFired { get; }
        internal bool Dropped { get; set; }
        internal bool Owned { get; set; }
        internal bool PassiveGiven { get; set; }
        internal bool Outlined { get; set; }
        // No sample yet: the first pass cannot tell a wound.
        internal float LastHealth { get; set; } = -1f;
        internal int Fired { get; set; }
        internal int ContactSeconds { get; set; }
        internal int Allies { get; set; }
        internal int Shocked { get; set; }
    }

    private readonly HeroAbilityRuntime _runtime;
    private readonly HeroAbilityCaster _caster;
    private readonly List<HeroRecord> _heroes = new List<HeroRecord>();
    private readonly MBList<Agent> _scratch = new MBList<Agent>();
    private readonly List<Agent> _changed = new List<Agent>();

    internal HeroAbilityHost(HeroAbilityRuntime runtime, HeroAbilityCaster caster)
    {
        _runtime = runtime;
        _caster = caster;
    }

    internal int Count => _heroes.Count;

    // Character.StringId names the mission hero in a Custom Battle and in a campaign alike.
    internal bool TryRegister(Agent agent)
    {
        if (agent == null || !agent.IsHuman || agent.IsMount || agent.Character == null)
            return false;
        var kit = _runtime.Service.KitFor(agent.Character.StringId);
        if (kit == null)
            return false;
        var serial = 1;
        foreach (var record in _heroes)
        {
            if (ReferenceEquals(record.Agent, agent))
                return false;
            if (record.Kit == kit)
                serial++;
        }
        var hasRule = _runtime.Service.TryContactRule(kit, out var ruleCount, out var ruleRange);
        _heroes.Add(new HeroRecord(agent, kit, serial, hasRule, ruleCount, ruleRange));
        _runtime.Logger.LogInfo($"[HeroAbilities] {kit.CharacterId} #{serial} registered, {kit.Abilities.Count} ability(ies)");
        return true;
    }

    internal void RunAll(Mission mission, float now)
    {
        var service = _runtime.Service;
        foreach (var record in _heroes)
        {
            var hero = record.Agent;
            if (record.Dropped)
                continue;
            if (!AgentSlotIdentity.IsCurrentOccupant(hero) || !hero.IsActive())
            {
                Drop(record);
                continue;
            }
            if (!CoopMissionOwnership.IsOwnedHere(hero))
                continue;
            record.Owned = true;
            if (!record.PassiveGiven)
            {
                // Granted on the first owned pass rather than in OnAgentBuild, so the refresh runs on a built agent.
                record.PassiveGiven = true;
                _runtime.Ledger.Grant(hero, "passive:" + record.Kit.CharacterId, record.Kit.Passive, float.PositiveInfinity);
                if (record.Kit.Ranged != null)
                    _runtime.Ranged.Add(hero, record.Kit.Ranged);
                HeroAbilityCaster.Refresh(hero);
            }

            var health = hero.Health;
            var tookDamage = RaceAbilityService.TookDamage(record.LastHealth, health);
            record.LastHealth = health;
            if (service.ShouldDecide(hero.IsAIControlled, hero.IsPlayerControlled, hero.IsMainAgent, hero.IsRunningAway))
            {
                if (InContact(mission, hero, record))
                    record.ContactSeconds++;
                var abilities = record.Kit.Abilities;
                var pick = service.PickAbility(abilities, record.LastFired, now,
                    i => service.Fires(service.ProfileOf(abilities[i]),
                        _runtime.RaceAbilities.Sensor.Sense(hero, service.ProfileOf(abilities[i]), now, tookDamage)));
                if (pick >= 0)
                {
                    record.LastFired[pick] = now;
                    var result = _caster.Cast(mission, hero, abilities[pick], record.Kit.GlowColor, now);
                    record.Fired++;
                    record.Allies += result.Allies;
                    record.Shocked += result.Shocked;
                }
            }
            Outline(record, now);
        }
    }

    internal void Expire(float now)
    {
        _changed.Clear();
        _runtime.Ledger.Expire(now, _changed);
        foreach (var agent in _changed)
            HeroAbilityCaster.Refresh(agent);
        _changed.Clear();
    }

    // The engine removed the body (deferred to the main tick): its grants go, and a hero's record stays for the check.
    internal void Forget(Agent agent)
    {
        _runtime.Ledger.Remove(agent);
        foreach (var record in _heroes)
            if (ReferenceEquals(record.Agent, agent))
                Drop(record);
    }

    internal void WriteChecks(string missionKey)
    {
        var outcomes = _runtime.Outcomes;
        foreach (var record in _heroes)
            LongDefeatChecks.Record(outcomes, HeroAbilityChecks.FiredId, $"{missionKey}-{record.Kit.CharacterId}-{record.Serial}",
                HeroAbilityChecks.Fired(record.Kit.CharacterId, record.Fired, record.ContactSeconds, record.Allies, record.Shocked, record.Owned));
        if (!_caster.Sampled && _caster.Grants > 0)
            LongDefeatChecks.Record(outcomes, HeroAbilityChecks.StatsId, missionKey, HeroAbilityChecks.NoStatsSample(_caster.Grants));
    }

    // No pass runs after a stand-down, so nothing would end the buffs already given: end them all now. A passive's end
    // is infinity, which an infinite clock reaches. The roster goes first, so that expiry's refresh drops the zero aim
    // error and the shield pierce too.
    internal void StandDown()
    {
        _runtime.Ranged.Clear();
        _runtime.Bursts.Clear();
        try
        {
            Expire(float.PositiveInfinity);
            foreach (var record in _heroes)
                if (record.Outlined)
                {
                    record.Outlined = false;
                    _runtime.RaceAbilities.Visuals.Outline(record.Agent, null);
                }
        }
        catch (Exception ex)
        {
            _runtime.Ledger.Clear();
            _runtime.Logger.LogError($"[HeroAbilities] stand-down refresh failed, buffs cleared: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Mission end: the agents are being torn down, so no native call.
    internal void Clear()
    {
        _runtime.Ledger.Clear();
        _runtime.Ranged.Clear();
        _runtime.Bursts.Clear();
        _heroes.Clear();
        _scratch.Clear();
        _changed.Clear();
        _caster.Clear();
    }

    private void Drop(HeroRecord record)
    {
        record.Dropped = true;
        if (record.Outlined)
        {
            record.Outlined = false;
            _runtime.RaceAbilities.Visuals.Outline(record.Agent, null);
        }
        _runtime.Ledger.Remove(record.Agent);
        _runtime.Ranged.Remove(record.Agent);
        _runtime.Bursts.Remove(record.Agent);
    }

    // Painted again on every pass while an ability runs: an equipment rebuild replaces the meshes the outline was on,
    // and the race visuals may clear it.
    private void Outline(HeroRecord record, float now)
    {
        var glow = record.Kit.GlowColor;
        var active = false;
        for (var i = 0; i < record.LastFired.Length; i++)
            active |= _runtime.Service.IsActive(record.LastFired[i], now, record.Kit.Abilities[i].DurationSeconds);
        if (active && glow.HasValue)
        {
            record.Outlined = true;
            _runtime.RaceAbilities.Visuals.Outline(record.Agent, glow);
        }
        else if (record.Outlined)
        {
            record.Outlined = false;
            _runtime.RaceAbilities.Visuals.Outline(record.Agent, null);
        }
    }

    // Contact is the kit's easiest enemy trigger held by a count of its own, not the sensor's: the query is flat (a Vec2),
    // the trigger is 3D (RaceAbilitySensor reads enemy.Position.Distance), so a wall-walk hero over attackers at the wall
    // foot is in contact for the sensor only when they are close in 3D. The query takes the trigger's full range, as the
    // sensor's does (RaceAbilitySensor.cs:46): a range clamped to 15 m would count no contact for a 40 m trigger (#1361).
    // Past 15 m the native query walks every agent (traps/mission.md "Native branches"); it runs once a second per hero.
    private bool InContact(Mission mission, Agent hero, HeroRecord record)
    {
        var team = hero.Team;
        if (team == null || !record.HasContactRule)
            return false;
        _scratch.Clear();
        mission.GetNearbyEnemyAgents(hero.Position.AsVec2, record.ContactRange, team, _scratch);
        var count = 0;
        foreach (var enemy in _scratch)
            if (enemy != null && enemy.IsActive() && enemy.IsHuman && enemy.Position.Distance(hero.Position) <= record.ContactRange)
                count++;
        _scratch.Clear();
        return count >= record.ContactCount;
    }
}

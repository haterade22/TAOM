using System.Collections.Generic;
using System.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.DreadAura.Hooks;
using TAOM.Features.HeroAuras.Domain;
using TAOM.Features.RaceAbilities.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAuras.Hooks;

/// <summary>
/// One battle's aura sources and the pulse that stamps their allies (#1127). A source is a hero the config names, found
/// by DreadSourceTracker.HeroAxisId. Each pulse stamps every allied soldier in a hero's reach with the strongest auras
/// on him; the same auras at the same strength only move the end, a change refreshes his stats and his horse's. Main
/// thread only. Holds agent references for one mission: <see cref="Clear"/> at its end. Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class HeroAuraPulse
{
    private readonly HeroAuraRuntime _runtime;
    private readonly List<KeyValuePair<Agent, HeroAura>> _sources = new List<KeyValuePair<Agent, HeroAura>>();
    private readonly MBList<Agent> _scratch = new MBList<Agent>();
    private readonly Dictionary<Agent, List<HeroAura>> _reached = new Dictionary<Agent, List<HeroAura>>();
    private readonly List<RaceAbilityEffects> _effects = new List<RaceAbilityEffects>();
    private readonly List<Agent> _expired = new List<Agent>();

    public HeroAuraPulse(HeroAuraRuntime runtime) => _runtime = runtime;

    public int SourceCount => _sources.Count;

    public string DescribeSources() => string.Join(", ", _sources.Select(s => $"{s.Key.Name} ({s.Value.Id})"));

    public void TryRegister(Agent agent)
    {
        if (agent == null || !agent.IsHuman || agent.IsMount || agent.Character == null)
            return;
        var campaignCharacter = agent.Character as CharacterObject;
        var heroId = DreadSourceTracker.HeroAxisId(campaignCharacter != null, campaignCharacter?.HeroObject?.StringId, agent.Character.StringId);
        var aura = _runtime.Service.AuraFor(heroId);
        if (aura != null && _sources.All(s => !ReferenceEquals(s.Key, agent)))
            _sources.Add(new KeyValuePair<Agent, HeroAura>(agent, aura));
    }

    // A fallen hero leaves the list; his soldiers keep the aura until it lingers out.
    public void Prune() =>
        _sources.RemoveAll(s => s.Key == null || !s.Key.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(s.Key));

    public void Run(Mission mission, float now)
    {
        _reached.Clear();
        foreach (var source in _sources)
        {
            var hero = source.Key;
            var team = hero.Team;
            if (team == null)
                continue;
            _scratch.Clear();
            mission.GetNearbyAllyAgents(hero.Position.AsVec2, source.Value.Radius, team, _scratch);
            // The engine's "ally" is every team not hostile to the hero's, so an allied army's troops are in, and
            // so is the hero himself (the query never excludes its centre): skip him. In co-op only this peer's own
            // soldiers take the aura, as for the career area buffs (MissionAbilityExecutionContext).
            foreach (var ally in _scratch)
            {
                if (ally == null || ReferenceEquals(ally, hero) || !ally.IsHuman || !ally.IsActive()
                    || !source.Value.Reaches(ally.Character?.Race) || !CoopMissionOwnership.IsOwnedHere(ally))
                    continue;
                if (!_reached.TryGetValue(ally, out var auras))
                    _reached[ally] = auras = new List<HeroAura>();
                if (!auras.Contains(source.Value))
                    auras.Add(source.Value);
            }
        }

        var strength = _runtime.Settings.Strength;
        var suffix = HeroAura.StrengthSuffix(strength);
        var until = now + _runtime.Service.LingerSeconds;
        foreach (var pair in _reached)
            Stamp(pair.Key, pair.Value, strength, suffix, until);
    }

    public void Expire(float now)
    {
        _runtime.Ledger.Expire(now, _expired);
        foreach (var soldier in _expired)
            Refresh(soldier);
    }

    public void Clear()
    {
        _sources.Clear();
        _reached.Clear();
        _scratch.Clear();
        _effects.Clear();
        _expired.Clear();
    }

    private void Stamp(Agent soldier, List<HeroAura> auras, float strength, string strengthSuffix, float until)
    {
        foreach (var aura in auras)
            _runtime.Ledger.RecordReach(aura.Id, soldier);
        var auraKey = HeroAura.KeyFor(auras, strengthSuffix);
        var effects = _runtime.Ledger.TryExtend(soldier, auraKey, until);
        if (effects == null)
        {
            _effects.Clear();
            foreach (var aura in auras)
                _effects.Add(_runtime.Service.EffectsFor(aura, strength));
            effects = _runtime.Service.Merge(_effects);
            _runtime.Ledger.Stamp(soldier, auraKey, effects, until);
            Refresh(soldier);
        }
        TopUpMorale(soldier, effects);
    }

    // The engine gives the player's own agent no morale component.
    private void TopUpMorale(Agent soldier, RaceAbilityEffects? effects)
    {
        if (effects == null || !(effects.MoraleFloor > 0f) || !soldier.IsAIControlled)
            return;
        var topUp = _runtime.RaceAbilities.MoraleTopUp(soldier.GetMorale(), effects.MoraleFloor);
        if (topUp > 0f)
            soldier.ChangeMorale(topUp);
    }

    private static void Refresh(Agent soldier)
    {
        if (soldier == null || !soldier.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(soldier))
            return;
        soldier.UpdateAgentProperties();
        // A rider's mount speed rides its own stats, through its rider (#611).
        soldier.MountAgent?.UpdateAgentProperties();
    }
}

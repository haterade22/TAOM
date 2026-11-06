using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.LongDefeat.Hooks;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>The Long Defeat mission's engine writes and reads beyond the field (#1079). Main thread.</summary>
internal sealed class LongDefeatEffectsAdapter : ILongDefeatEffectsAdapter
{
    private readonly Mission _mission;
    private readonly LongDefeatAgentRegistry? _registry;

    /// <param name="registry">The roles of the attackers the field spawned; null (the adapter's tests) finds no commander.</param>
    public LongDefeatEffectsAdapter(Mission mission, LongDefeatAgentRegistry? registry = null)
    {
        _mission = mission;
        _registry = registry;
    }

    public void PublishStats(LdStatTable table)
    {
        LongDefeatHookState.Publish(_mission, table);
    }

    // BaseHealthLimit is MaxHitPoints() until something writes it (Agent.cs:1438-1439), so the limit over MaxHitPoints() is the factor SpawnBatch wrote.
    public LdScaleSample ReadScale()
    {
        var blows = LongDefeatHookState.Raised;
        IEnumerable<Agent>? standing = _mission.AttackerTeam?.ActiveAgents;
        if (standing == null)
            return new LdScaleSample("", 0f, 0f, blows);

        var sampled = FirstRoster(standing, _registry, agent => agent.IsHuman && agent.IsActive() && !agent.IsRunningAway && agent.Character != null);
        return sampled?.Character is { } character
            ? new LdScaleSample(character.StringId, sampled.HealthLimit, character.MaxHitPoints(), blows)
            : new LdScaleSample("", 0f, 0f, blows);
    }

    // The baseline is the stat model's own answer for the same agent with the stat hook inert on this thread. The model reads the agent, and it also reads one
    // value from the property object it fills: "float num2 = agentDrivenProperties.ArmorEncumbrance + num;" (CustomBattleAgentStatCalculateModel.cs:190), which
    // only InitializeAgentStats writes (:22). The scratch therefore starts with the agent's own ArmorEncumbrance, or the drums baselines would be those of an
    // unarmoured soldier. The agent is refreshed first, as the engine does when a weapon changes (OnWeaponUsageIndexChange, Agent.cs:2380-2383, calls
    // UpdateAgentProperties), so the value read and the baseline are both the model's answer for the agent as it stands now, not for the weapon it held when
    // it spawned. The four armour values are written once at spawn and not by UpdateAgentStats, so their baseline is what the spawn equipment sums to.
    public LdStatSample ReadStats(IReadOnlyList<string> stats)
    {
        var agent = FirstPlainAttacker();
        return agent == null ? LdStatSample.None : new LdStatSample(agent.Character.StringId, Readings(agent, stats));
    }

    // The seed below is read by a source pin: the baseline starts from the agent's own encumbrance, then the stat model runs once with the hooks inert on this thread.
    private static List<LdStatReading> Readings(Agent agent, IReadOnlyList<string> stats)
    {
        agent.UpdateAgentProperties();
        var scratch = new AgentDrivenProperties { ArmorEncumbrance = agent.AgentDrivenProperties.ArmorEncumbrance };
        var model = MissionGameModels.Current.AgentStatCalculateModel;
        LongDefeatStatHooks.Baseline(() => model.UpdateAgentStats(agent, scratch));
        var readings = new List<LdStatReading>();
        foreach (var name in stats)
        {
            if (name == nameof(DrivenProperty.MountSpeed))
            {
                // The horse's speed is the horse's own property, written through its rider's record; a soldier on foot has none to read.
                if (agent.MountAgent is { } mount)
                    readings.Add(MountReading(mount));
                continue;
            }

            if (Enum.TryParse(name, out DrivenProperty property))
                readings.Add(new LdStatReading(name, ArmourBase(agent, name) ?? scratch.GetStat(property), agent.GetAgentDrivenPropertyValue(property)));
        }

        return readings;
    }

    // The horse as the engine holds it, against the stat model's answer for the same horse with the hook inert. MountSpeed is assigned afresh on every UpdateHorseStats
    // ("agentDrivenProperties.MountSpeed = environmentSpeedFactor * 0.22f * (1f + num);", CustomBattleAgentStatCalculateModel.cs:391), so a scratch object needs no seed.
    private static LdStatReading MountReading(Agent mount)
    {
        mount.UpdateAgentProperties();
        var scratch = new AgentDrivenProperties();
        var model = MissionGameModels.Current.AgentStatCalculateModel;
        LongDefeatStatHooks.Baseline(() => model.UpdateAgentStats(mount, scratch));
        return new LdStatReading(nameof(DrivenProperty.MountSpeed), scratch.MountSpeed, mount.GetAgentDrivenPropertyValue(DrivenProperty.MountSpeed));
    }

    // Each live ram and tower, as the battlefield adapter keeps them after deployment: not disabled, and visible (an undeployed engine of the prefab stays in the scene hidden). The maximum is the
    // type's vanilla value times the factor, the same line that sets it at deployment, "component.MaxHitPoint = weapon!.GetSiegeEngineType().BaseHitPoints;" (LongDefeatBattlefieldAdapter.SetVanillaHitPoints),
    // and the current hit points keep their share of it. The setter clamps at 0 ("_hitPoint = TaleWorlds.Library.MathF.Max(value, 0f);", DestructableComponent.cs:117), so a destroyed engine
    // stays destroyed, and no Reset() runs: it would restore a destroyed engine. A visible gate is only read, for the check's log, and from every mission object, not the active list: a gate that
    // falls is disabled in the same call ("destructionComponent.GameEntity.GetFirstScriptOfType<UsableMachine>()?.Disable();", Mission.cs:3060, which ends in SetDisabled, and that in
    // "Mission.Current.DeactivateMissionObject(this);", MissionObject.cs:280, which runs "_activeMissionObjects.Remove(missionObject);", Mission.cs:1595), so only the full list can show it destroyed.
    // Rams and towers stay on the active list: leaving it when destroyed is what "live" means for them.
    public LdEngineReport SetEngineHitPoints(float factor)
    {
        var readings = new List<LdEngineReading>();
        foreach (var ram in LiveEngines<BatteringRam>())
            Harden(ram, "ram", factor, readings);
        foreach (var tower in LiveEngines<SiegeTower>())
            Harden(tower, "tower", factor, readings);

        var gates = _mission.MissionObjects.FindAllWithType<CastleGate>().Where(g => g.GameEntity.IsVisibleIncludeParents()).ToList();
        return new LdEngineReport(readings, gates.Count, gates.Count(g => g.IsDestroyed));
    }

    private IEnumerable<T> LiveEngines<T>() where T : SiegeWeapon =>
        _mission.ActiveMissionObjects.FindAllWithType<T>().Where(w => !w.IsDisabled && w.GameEntity.IsVisibleIncludeParents()).ToList();

    private static void Harden(SiegeWeapon weapon, string kind, float factor, List<LdEngineReading> readings)
    {
        var component = weapon.GameEntity.GetFirstScriptOfType<DestructableComponent>();
        if (component == null)
            return;

        var vanilla = weapon.GetSiegeEngineType().BaseHitPoints;
        var before = component.MaxHitPoint;
        component.MaxHitPoint = vanilla * factor;
        if (before > 0f)
            component.HitPoint *= component.MaxHitPoint / before;
        readings.Add(new LdEngineReading(kind, vanilla, component.MaxHitPoint, component.HitPoint));
    }

    // The attackers' positions are read once into an array, then each standing human defender is looked at once: the morale read, the change and the read back are the pulse's own arithmetic
    // (LdMoralePulse.Apply). The change is "commonAIComponent.Morale += delta;" (AgentComponentExtensions.cs:21-27) and the component clamps to 0..100 (CommonAIComponent.cs:47). A defender
    // with no component answers -1 and is skipped. The lord is never a target: a human-played lord has no component, but an autoplayed one is handed to the AI (AdoptMain), and the controller
    // setter tells every behaviour (Agent.cs:1268-1271), so AgentCommonAILogic gives him one (AgentCommonAILogic.cs:16-24) and the drain would count him and lower his margin against casualty panic.
    // Attackers are not targeted: the pulse looks at them only for their positions.
    public LdPulseResult PulseMorale(LdMoralePulse pulse)
    {
        var watch = Stopwatch.StartNew();
        var attackers = StandingAttackers();
        var spots = new LdPulseSpot[attackers.Count];
        for (var i = 0; i < spots.Length; i++)
            spots[i] = SpotOf(attackers[i]);

        IEnumerable<Agent>? team = _mission.DefenderTeam?.ActiveAgents;
        var defenders = team == null ? new List<Agent>() : team.Where(a => a.IsHuman && a.IsActive() && !a.IsMainAgent).ToList();
        var result = pulse.Apply(spots, defenders, SpotOf, agent => agent.GetMorale(), (agent, drain) => agent.ChangeMorale(-drain));
        return result with { Milliseconds = watch.Elapsed.TotalMilliseconds };
    }

    private static LdPulseSpot SpotOf(Agent agent)
    {
        var at = agent.Position;
        return new LdPulseSpot(at.x, at.y);
    }

    // The victim loses the damage with no blow while his health stays above it, the way Fords' poison does ("agent.Health -= damage;"). A tick that would kill needs a real owner: AgentMoraleInteractionLogic hands
    // the killing blow's affector to CustomBattleMoraleModel, which reads "affectorAgent.Formation" with no null test (CustomBattleMoraleModel.cs:42), and Agent.Die swaps the victim's last hitter in for an owner of
    // -1 or himself ("(b.OwnerId == -1 || b.OwnerId == Index) && IsHuman && _lastHitInfo.CanOverrideBlow", Agent.cs:4689-4693), an index that may since belong to another agent. So Fords' owner -1 is not copied:
    // the owner is chosen from the registry (the poisoner, else the commander, else any attacker standing), read for IsLive right here, and with none the tick is withheld and the victim keeps 1 health.
    public LdPoisonResult ApplyPoison(AgentKey victim, AgentKey poisoner, int damage)
    {
        if (_registry == null || damage <= 0 || !_registry.TryGetAgent(victim, out var found) || !(found is Agent agent) || !IsLive(agent) || agent.CurrentMortalityState != Agent.MortalityState.Mortal)
            return default;

        if (agent.Health > damage)
        {
            agent.Health -= damage;
            return new LdPoisonResult(damage, Killed: false, Withheld: false, OwnerMissing: false, Substituted: false);
        }

        // Tested positive before the cast: Math.Max(0f, NaN) is NaN and (int)NaN is int.MinValue, which the withheld branch below would wrap into a huge damage.
        var health = agent.Health;
        var taken = health > 0f ? (int)health : 0;
        var choice = PoisonOwner.Choose(StandingAttackerKey(poisoner), StandingAttackerKey(CommanderKey()), StandingAttackerKey(AnyAttackerKey()));
        if (choice is not { } ownerKey)
        {
            // No attacker stands to own the kill: the victim keeps 1 health until a later tick or the clear.
            var held = Math.Max(0, taken - 1);
            if (agent.Health > 1f)
                agent.Health = 1f;
            return new LdPoisonResult(held, Killed: false, Withheld: true, OwnerMissing: false, Substituted: false);
        }

        var substituted = ownerKey != poisoner;
        if (!_registry.TryGetAgent(ownerKey, out var chosen) || !(chosen is Agent owner) || !IsLive(owner))
            return new LdPoisonResult(0, Killed: false, Withheld: false, OwnerMissing: true, Substituted: substituted);

        var blow = new Blow(owner.Index)
        {
            DamageType = DamageTypes.Pierce,
            BoneIndex = agent.Monster.HeadLookDirectionBoneIndex,
            GlobalPosition = agent.Position,
            BaseMagnitude = damage,
            InflictedDamage = damage,
            SwingDirection = agent.LookDirection,
            DamageCalculated = true,
        };
        blow.GlobalPosition.z += agent.GetEyeGlobalHeight();
        blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
        blow.Direction = blow.SwingDirection;
        agent.Die(blow);
        return new LdPoisonResult(taken, Killed: true, Withheld: false, OwnerMissing: false, Substituted: substituted);
    }

    // A candidate owner counts only while the registry still holds him as a human attacker and he is active at this call. A key that is not an agent answers null.
    private AgentKey? StandingAttackerKey(AgentKey? key)
    {
        if (key is not { } k || _registry == null || !_registry.TryGetAgent(k, out var found))
            return null;

        return found is Agent a && a.IsHuman && IsLive(a) && a.Team?.Side == BattleSideEnum.Attacker ? k : null;
    }

    // A handle the registry holds across frames passes only while it is still the occupant of its slot: a deleted Agent answers IsActive() for the slot's next tenant (csharp-architecture.md,
    // "Mission-scope agent handles"), and Die on it would strike whoever now holds the index. The Fords adapter gates its handles the same way.
    private static bool IsLive(Agent agent) => agent.IsActive() && AgentSlotIdentity.IsCurrentOccupant(agent);

    private AgentKey? CommanderKey() => OwnerCandidate(role => role == AttackerRole.Commander);

    private AgentKey? AnyAttackerKey() => OwnerCandidate(_ => true);

    private AgentKey? OwnerCandidate(Func<AttackerRole, bool> wanted)
    {
        if (_registry == null)
            return null;

        foreach (var entry in _registry.Snapshot())
        {
            if (wanted(entry.Role) && entry.Agent is Agent a && a.IsHuman && IsLive(a) && a.Team?.Side == BattleSideEnum.Attacker)
                return entry.Key;
        }

        return null;
    }

    public LdTouched RefreshStats(BoonTarget target)
    {
        var watch = Stopwatch.StartNew();
        var standing = Standing(target);
        foreach (var agent in standing)
        {
            agent.UpdateAgentProperties();
            agent.MountAgent?.UpdateAgentProperties();
        }

        return new LdTouched(standing.Count, watch.Elapsed.TotalMilliseconds);
    }

    public LdTouched RefreshAttackers()
    {
        var watch = Stopwatch.StartNew();
        var standing = StandingAttackers();
        foreach (var agent in standing)
        {
            agent.UpdateAgentProperties();
            agent.MountAgent?.UpdateAgentProperties();
        }

        return new LdTouched(standing.Count, watch.Elapsed.TotalMilliseconds);
    }

    // BaseHealthLimit is MaxHitPoints() until something writes it (Agent.cs:1438-1439), so the limit over MaxHitPoints() is the factor SpawnBatch wrote.
    public LdScaleSample ReadCommander()
    {
        var agent = StandingCommander();
        return agent?.Character == null ? new LdScaleSample("", 0f, 0f, 0) : new LdScaleSample(agent.Character.StringId, agent.HealthLimit, agent.Character.MaxHitPoints(), 0);
    }

    public string DescribeCommander()
    {
        var agent = StandingCommander();
        if (agent == null)
            return "";

        var at = agent.Position;
        var formation = agent.Formation;
        return $"troop={agent.Character?.StringId} at=({at.x:F1}, {at.y:F1}, {at.z:F1}) health={agent.Health:F0}/{agent.HealthLimit:F0} formation={formation?.RepresentativeClass} "
            + $"behavior={formation?.AI?.ActiveBehavior?.GetType().Name} speed={agent.MovementVelocity.Length:F1} unique={agent.GetAgentFlags().HasAnyFlag(AgentFlag.IsUnique)}";
    }

    private List<Agent> StandingAttackers()
    {
        IEnumerable<Agent>? attackers = _mission.AttackerTeam?.ActiveAgents;
        return attackers == null ? new List<Agent>() : attackers.Where(a => a.IsHuman && a.IsActive()).ToList();
    }

    private Agent? StandingCommander() =>
        _registry == null ? null : StandingAttackers().FirstOrDefault(a => _registry.RoleOf(a) == AttackerRole.Commander);

    public LdTouched ScaleHealth(BoonTarget target, float factor)
    {
        var watch = Stopwatch.StartNew();
        var standing = Standing(target);
        foreach (var agent in standing)
            Rescale(agent, agent.BaseHealthLimit * factor);

        return new LdTouched(standing.Count, watch.Elapsed.TotalMilliseconds);
    }

    public void SetPlayerHealth(float factor)
    {
        var main = Standing(BoonTarget.Player).FirstOrDefault();
        if (main?.Character != null)
            Rescale(main, main.Character.MaxHitPoints() * factor);
    }

    public LdTargetSample ReadTarget(BoonTarget target, IReadOnlyList<string> stats, bool newest)
    {
        var plain = Standing(target).Where(a => !a.IsRunningAway && !RaceAbilityHooks.HasLiveAbility(a)).ToList();
        var agent = newest ? plain.LastOrDefault() : plain.FirstOrDefault();
        return agent == null
            ? LdTargetSample.None
            : new LdTargetSample(agent.Character.StringId, agent.HealthLimit, agent.Character.MaxHitPoints(), Readings(agent, stats));
    }

    // The standing humans of one group of the player's side, in spawn order (Team.cs:658 appends). The main agent belongs to the player target alone, as the hooks decide.
    private List<Agent> Standing(BoonTarget target)
    {
        IEnumerable<Agent>? defenders = _mission.DefenderTeam?.ActiveAgents;
        if (defenders == null)
            return new List<Agent>();

        return defenders.Where(a => a.IsHuman && a.IsActive() && LongDefeatTargets.Of(a, _mission) == target).ToList();
    }

    // A new limit on the three writes the engine's horse spawn makes (Mission.cs:4607-4609), the health following at the share it had.
    private static void Rescale(Agent agent, float limit)
    {
        var share = agent.HealthLimit > 0f ? agent.Health / agent.HealthLimit : 1f;
        agent.BaseHealthLimit = limit;
        agent.HealthLimit = limit;
        agent.Health = share * limit;
    }

    /// <summary>
    /// The first of <paramref name="agents"/> that <paramref name="fits"/> and is a roster soldier. The team keeps its agents in spawn order (Team.cs:655-665), so this is the oldest roster
    /// soldier standing: a commander, queued once the first batch closes, would otherwise be the oldest after the opening batch is gone, and a summon would be read as a roster troop.
    /// </summary>
    internal static T? FirstRoster<T>(IEnumerable<T> agents, LongDefeatAgentRegistry? registry, Func<T, bool> fits) where T : class
    {
        foreach (var agent in agents)
        {
            if (fits(agent) && (registry == null || registry.RoleOf(agent) == AttackerRole.Roster))
                return agent;
        }

        return null;
    }

    private Agent? FirstPlainAttacker()
    {
        IEnumerable<Agent>? standing = _mission.AttackerTeam?.ActiveAgents;
        if (standing == null)
            return null;

        foreach (var agent in standing)
        {
            if (agent.IsHuman && agent.IsActive() && !agent.IsRunningAway && agent.Character != null && !RaceAbilityHooks.HasLiveAbility(agent))
                return agent;
        }

        return null;
    }

    private static float? ArmourBase(Agent agent, string name)
    {
        switch (name)
        {
            case "ArmorHead":
                return agent.SpawnEquipment.GetHeadArmorSum();
            case "ArmorTorso":
                return agent.SpawnEquipment.GetHumanBodyArmorSum();
            case "ArmorLegs":
                return agent.SpawnEquipment.GetLegArmorSum();
            case "ArmorArms":
                return agent.SpawnEquipment.GetArmArmorSum();
            default:
                return null;
        }
    }
}

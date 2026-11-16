using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// The shop's heroes and a story mission's free heroes (#1325, #1337, #1366): one spawn into the heroes' own formation, a standing Move order at
/// a wall lane for that formation, a repair that keeps its membership to the held heroes, and the bodies held by reference so a removal is
/// reported by the hero id.
/// The held bodies are a copy-on-write array: the writers (the spawn, <see cref="TakeHero"/> on any thread, <see cref="ClearHeroes"/>)
/// replace it under <see cref="_heroGate"/>, and the main-thread readers and <see cref="IsHeldHero"/> read it without the lock, so a
/// per-tick read allocates nothing.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    private const FormationClass HeroClass = (FormationClass)HeroGroup.FormationIndex;

    private readonly object _heroGate = new();
    private volatile (string Id, Agent Agent)[] _heroes = Array.Empty<(string Id, Agent Agent)>();

    // The heroes' place, resolved on the first order of the mission. The spawn place of the last hero is its last fallback.
    private SplitPlace? _heroPlace;
    private bool _heroPlaceRead;
    private Vec3 _heroPlaceAt;
    private LdPlace? _heroHome;

    // The mission time of the field's last wall order, so the hunters line does not judge heroes who are still on their way.
    private float _heroPostedAt;

    // Between the stalemate charge and the tactic's return (OrderCharge, RestoreDefenderTactic) the heroes charge with every other defender.
    private bool _defendersCharge;

    // The formation is positioned at the place before its first spawn, as the mounted lord's is (SpawnMounted): the spawn then finds it
    // positioned and never asks the deployment plan for a plan nobody added troops to. A hero's character has no horse and a battle set
    // of his own (ShippedMissionHeroesTests), and that first battle set goes in as the kit. In a Custom Game is_hero makes IsHero true and the
    // engine fixes the equipment to a clone of that set (Mission.cs:4284-4287, :4108), so the kit changes nothing there. In a campaign
    // (#1366) the character has no Hero, IsHero is false (CharacterObject.cs:300), and with no kit the engine would roll the set through
    // Equipment.GetRandomEquipmentElements with random item modifiers on (Mission.cs:4108, Equipment.cs:604-614). A kit is cloned before that.
    // The engine wields the first weapon slot at the spawn (Mission.SpawnTroopWithAgentBuildData runs agent.WieldInitialWeapons, which takes the first
    // slot), and Legolas's bow is in the first slot, so this adapter draws no weapon.
    // An empty formation 7 is a split target of the siege tactic (TacticComponent.cs:229-235), so strangers leave it before the spawn.
    public LdHeroReport SpawnHero(HeroSpawn hero, LdPlace place)
    {
        var troop = MBObjectManager.Instance.GetObject<BasicCharacterObject>(hero.TroopId);
        if (troop == null)
            return new LdHeroReport(false, $"no character '{hero.TroopId}'");

        var team = Side(false);
        var hunt = HeroesHunt;
        var cls = hunt ? HeroClass : troop.DefaultFormationClass == FormationClass.Ranged ? FormationClass.Ranged : FormationClass.Infantry;
        var formation = team.GetFormation(cls);
        if (hunt)
            MoveHeroStrangersOut(formation, _heroes);
        _heroHome = place;
        if (!formation.HasBeenPositioned)
            formation.SetPositioning(new WorldPosition(_mission.Scene, new Vec3(place.X, place.Y, place.Z)), FacingOf(place));

        Agent agent;
        formation.BeginSpawn(1, false);
        try
        {
            agent = Spawn(troop, team, true, cls, true, 1, 0, false, kit: troop.FirstBattleEquipment, place: place, ownBody: true);
        }
        finally
        {
            formation.EndSpawn();
        }

        if (agent == null)
            return new LdHeroReport(false, "the engine returned no agent");

        var before = agent.HealthLimit;
        ScaleHealth(agent, hero.HealthFactor);
        HoldHero(hero.Id, agent);
        var held = _heroes;
        if (hunt)
        {
            PutHeroesBack(formation, held);
            if (formation.IsAIControlled)
                Post(formation);
        }

        return new LdHeroReport(true, null, agent.Character?.IsHero ?? false, agent.HasMount, agent.Formation?.Index ?? -1,
            before > 0f ? agent.HealthLimit / before : 0f, agent.WieldedWeapon.Item?.StringId ?? "", !formation.IsAIControlled,
            Strangers(formation, held), formation.GetReadonlyMovementOrderReference().OrderEnum.ToString(), agent.Character is CharacterObject, Enemy: !hunt);
    }

    // The heroes hunt for the player only when he defends. In a story attack (#1409) they are the enemy: each stands in the garrison's formation of his class
    // (default_group sets DefaultFormationClass, BasicCharacterObject.cs:499) under the defenders' AI, never held off AI in formation 7. A formation off AI is
    // the target the siege tactic consolidates into (TacticComponent.cs:191), the #1370 churn.
    private bool HeroesHunt => !PlayerAttacks;

    // SiegeLanes is the static list TeamAISiegeComponent builds for the mission; each lane stores one formation per team index.
    public bool DefenderLanesAssigned()
    {
        var defenders = _mission.DefenderTeam;
        var lanes = TeamAISiegeComponent.SiegeLanes;
        if (defenders == null || lanes == null)
            return false;

        foreach (var lane in lanes)
        {
            if (lane.GetLastAssignedFormation(defenders.TeamIndex) != null)
                return true;
        }

        return false;
    }

    // Main thread, every tick while a hero stands. Only reads when the group is whole and off AI control.
    public LdHuntersHold HoldHunters()
    {
        var formation = _mission.DefenderTeam?.GetFormation(HeroClass);
        if (formation == null)
            return LdHuntersHold.Nothing;

        var held = _heroes;
        var live = 0;
        var whole = true;
        foreach (var pair in held)
        {
            if (!OnDuty(pair.Agent))
                continue;

            live++;
            if (!ReferenceEquals(pair.Agent.Formation, formation))
                whole = false;
        }

        if (live == 0)
            return LdHuntersHold.Nothing;
        if (!formation.IsAIControlled && whole && formation.CountOfUnits == live)
            return LdHuntersHold.Nothing;

        return RepairHunters(formation, held);
    }

    // The siege tactic can still add soldiers to a formation off AI control: consolidation ranks it first as a target (TacticComponent.cs:191,
    // "+ 10000" when not AI controlled), and CarryOutDefense moves units into it (TacticDefendCastle.cs:588-611, :823-824). The engine also hands
    // the formation back at the lord's fall (Mission.cs:3039-3041), and the tactic can move the heroes out before the next tick.
    private LdHuntersHold RepairHunters(Formation formation, (string Id, Agent Agent)[] held)
    {
        var regained = formation.IsAIControlled;
        var movedOut = MoveHeroStrangersOut(formation, held);
        var movedBack = PutHeroesBack(formation, held);
        if (formation.IsAIControlled)
            Post(formation);

        return new LdHuntersHold(regained, movedOut, movedBack);
    }

    // A ranged stranger goes where the defenders' archers belong, any other to the foot formation. The units are collected first, since a
    // formation change inside ApplyActionOnEachUnit would edit the list it walks; detached units are covered by it.
    private int MoveHeroStrangersOut(Formation formation, (string Id, Agent Agent)[] held)
    {
        if (formation.CountOfUnits == 0)
            return 0;

        var strangers = new List<Agent>();
        formation.ApplyActionOnEachUnit(agent =>
        {
            if (!IsLiveHeld(agent, held))
                strangers.Add(agent);
        });

        var team = formation.Team;
        foreach (var agent in strangers)
            agent.Formation = team.GetFormation(agent.IsRangedCached ? FormationClass.Ranged : FormationClass.Infantry);
        return strangers.Count;
    }

    private static int PutHeroesBack(Formation formation, (string Id, Agent Agent)[] held)
    {
        var moved = 0;
        foreach (var pair in held)
        {
            if (OnDuty(pair.Agent) && !ReferenceEquals(pair.Agent.Formation, formation))
            {
                pair.Agent.Formation = formation;
                moved++;
            }
        }

        return moved;
    }

    private static int Strangers(Formation formation, (string Id, Agent Agent)[] held)
    {
        var strangers = 0;
        formation.ApplyActionOnEachUnit(agent =>
        {
            if (!IsLiveHeld(agent, held))
                strangers++;
        });
        return strangers;
    }

    // A routing hero has no formation (Agent.OnFleeing clears it) and is not pulled back, as the split leaves a routing soldier alone.
    private static bool OnDuty(Agent agent) => Live(agent) && !agent.IsRunningAway;

    private static bool IsLiveHeld(Agent agent, (string Id, Agent Agent)[] held)
    {
        foreach (var pair in held)
        {
            if (ReferenceEquals(pair.Agent, agent))
                return Live(agent);
        }

        return false;
    }

    // The heroes' standing order. A charge with no enemy standing is not applicable (MovementOrder.cs:804-815), and Formation.Tick replaces it
    // with its substitute (Formation.cs:2466-2470), which is Stop (MovementOrder.cs:1107-1112): heroes are sold between waves only. A charge that
    // stays live inside the walls sends the group to a gate to get out (MovementOrder.cs:885-907). A Move is always applicable (MovementOrder.cs:829-830).
    // Off AI control first: an AI formation's behaviour orders it again at its next tick (Formation.cs:2458-2461), and with the player as general a
    // formation off AI control is not AI-owned (Formation.cs:326-348), so the tactic's transfers out of it are refused (Formation.cs:2155-2158).
    // The line, the fire at will and the wide form are set each time: the formation keeps what a siege behaviour gave it while the AI held it
    // (BehaviorDefendCastleKeyPosition sets a shield wall and a lane's width), and Formation.ResetAux restores the defaults only at a mission reset.
    // No side is set: FormationAI.Side steers only the AI's behaviours, and the tactic names a lane's defender by it (TacticDefendCastle.cs:665-710).
    // In the stalemate charge every defender charges, and so do the heroes: attackers stand then, so the charge stays applicable.
    private void Post(Formation formation)
    {
        formation.SetControlledByAI(false);
        if (_defendersCharge)
        {
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
            return;
        }

        if (HeroesPlace() is not { } place)
        {
            formation.SetMovementOrder(MovementOrder.MovementOrderStop);
            return;
        }

        formation.SetMovementOrder(MovementOrder.MovementOrderMove(place.Position));
        formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(place.Direction));
        formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
        formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        formation.SetFormOrder(FormOrder.FormOrderWide);
        _heroPostedAt = _mission.CurrentTime;
    }

    // The tactic comes back after a stalemate charge: the heroes take the wall order before the new tactic's first tick (TeamAIComponent.ResetTactic
    // ticks it at once), and a charge with no attacker left would turn to Stop where they stand.
    private void PostHunters()
    {
        if (!HeroesHunt)
            return;

        var formation = _mission.DefenderTeam?.GetFormation(HeroClass);
        if (formation == null)
            return;

        var held = _heroes;
        foreach (var pair in held)
        {
            if (!OnDuty(pair.Agent))
                continue;

            Post(formation);
            return;
        }
    }

    // One wall lane's own place, the lane nearest the heroes' spawn place: there the lane's defenders stand and the attackers come over the wall.
    // The middle between two lanes is where the split's wall line stands before contact, and a melee soldier does not seek an enemy 20 m away
    // (HumanAIComponent.cs:752-755). With no wall lane, the outer gate, else the last hero's spawn place.
    private SplitPlace? HeroesPlace()
    {
        if (_heroPlaceRead)
            return _heroPlace;

        _heroPlaceRead = true;
        var gate = GatePlace();
        _heroPlace = NearestLane(WallLanes(gate?.Side), _heroHome) ?? gate ?? OwnPlace(_heroHome);
        if (_heroPlace is { } place)
            _heroPlaceAt = place.Position.GetGroundVec3();
        return _heroPlace;
    }

    private static SplitPlace? NearestLane(List<SplitPlace> lanes, LdPlace? home)
    {
        if (lanes.Count == 0)
            return null;
        if (home is not { } from)
            return lanes[0];

        var at = new Vec2(from.X, from.Y);
        var best = lanes[0];
        foreach (var lane in lanes)
        {
            if (lane.Position.AsVec2.DistanceSquared(at) < best.Position.AsVec2.DistanceSquared(at))
                best = lane;
        }

        return best;
    }

    // FormationAI starts on the middle side (FormationAI.cs:45), so the fallback keeps that side.
    private SplitPlace? OwnPlace(LdPlace? home)
    {
        if (home is not { } place)
            return null;

        var position = new WorldPosition(_mission.Scene, new Vec3(place.X, place.Y, place.Z));
        return Usable(position) ? new SplitPlace(position, FacingOf(place), DefaultWidth, FormationAI.BehaviorSide.Middle) : null;
    }

    // Main thread. The group is read from the formation: an agent's IsAIControlled is true for every AI soldier, so it says nothing about the
    // formation. The group is read also when no hero stands.
    public LdHeroesRead ReadHeroes()
    {
        var held = _heroes;
        var place = _heroPlaceRead ? _heroPlace : null;
        var at = _heroPlaceAt;
        var facts = new List<LdHeroFacts>();
        foreach (var pair in held.OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var agent = pair.Agent;
            if (!OnDuty(agent))
                continue;

            var limit = agent.HealthLimit;
            var position = agent.Position;
            var distance = place == null ? float.NaN : position.AsVec2.Distance(at.AsVec2);
            var height = place == null ? float.NaN : Math.Abs(position.z - at.z);
            facts.Add(new LdHeroFacts(pair.Id, agent.KillCount, agent.Formation?.Index ?? -1, limit > 0f ? agent.Health / limit : 0f, distance, height, agent.IsPlayerControlled,
                agent.WieldedWeapon.CurrentUsageItem?.WeaponClass.ToString() ?? "none"));
        }

        var formation = _mission.DefenderTeam?.GetFormation(HeroClass);
        if (formation == null)
            return new LdHeroesRead(facts, false, "none", false, 0, 0f);

        ref readonly var order = ref formation.GetReadonlyMovementOrderReference();
        var own = place != null && order.OrderEnum == MovementOrder.MovementOrderEnum.Move && order.GetPosition(formation).Distance(at.AsVec2) <= OwnOrderTolerance;
        return new LdHeroesRead(facts, !formation.IsAIControlled, order.OrderEnum.ToString(), own, formation.CountOfUnits, own ? _mission.CurrentTime - _heroPostedAt : 0f);
    }

    // Main thread. A hero already held under the same id is replaced.
    private void HoldHero(string heroId, Agent agent)
    {
        lock (_heroGate)
        {
            var kept = _heroes.Where(p => !string.Equals(p.Id, heroId, StringComparison.Ordinal));
            _heroes = kept.Concat(new[] { (heroId, agent) }).ToArray();
        }
    }

    // Any thread. The hero leaves the held set with his removal, so a later body under the same id is a new hero.
    private bool TakeHero(Agent agent, out string heroId)
    {
        lock (_heroGate)
        {
            var held = _heroes;
            for (var i = 0; i < held.Length; i++)
            {
                if (!ReferenceEquals(held[i].Agent, agent))
                    continue;

                heroId = held[i].Id;
                var rest = new (string Id, Agent Agent)[held.Length - 1];
                Array.Copy(held, 0, rest, 0, i);
                Array.Copy(held, i + 1, rest, i, held.Length - i - 1);
                _heroes = rest;
                return true;
            }
        }

        heroId = "";
        return false;
    }

    private bool IsHeldHero(Agent agent)
    {
        foreach (var pair in _heroes)
        {
            if (ReferenceEquals(pair.Agent, agent))
                return true;
        }

        return false;
    }

    private void ClearHeroes()
    {
        lock (_heroGate)
            _heroes = Array.Empty<(string Id, Agent Agent)>();
        _heroPlace = null;
        _heroPlaceRead = false;
        _heroPlaceAt = default;
        _heroHome = null;
        _heroPostedAt = 0f;
        _defendersCharge = false;
    }
}

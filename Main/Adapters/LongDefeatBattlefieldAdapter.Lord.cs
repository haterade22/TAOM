using System;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The engine half of the Long Defeat death rule (phase4-plan.md Slice 4, spike S12), main thread only, called from the director's tick and never from a removal or hit callback. Every agent is held by
/// reference behind <c>Live</c> and nothing here is keyed by <c>Agent.Index</c>, which the engine hands to the next agent it builds (csharp-architecture.md "Mission-scope agent handles"). The engine members read for it, in
/// the installed v1.5.7 source: <c>Mission.CanTakeControlOfAgent</c> (Mission.cs:6393-6400) needs the take-over armed (<c>SetPlayerCanTakeControlOfAnotherAgentWhenDead</c>, which every launcher of this run calls), a
/// human, active, non-hero agent of <c>PlayerTeam</c> that uses no game object, at 25% health or more, and no main agent;
/// <c>Mission.TakeControlOfAgent</c> (:6407-6414) clears fast forward and sets <c>Controller = Player</c>; the <c>Agent.Controller</c> setter (Agent.cs:1233-1275) returns when the value is unchanged, sets
/// <c>Mission.MainAgent</c> only when the new value is <c>Player</c> and calls <c>Formation.OnAgentControllerChanged</c>, so handing a body to the AI never clears the main agent; <c>Team.GeneralAgent</c> has a public
/// setter (Team.cs:128) and nothing in the engine clears it when its body is removed.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter : ILongDefeatLordAdapter
{
    private bool _lordLastKnown;
    private float _lordLastX;
    private float _lordLastY;
    private float _lordLastZ;

    /// <summary>
    /// Keeps where the main agent stands as three plain numbers, read while he is live (from <c>ReadLord</c>). The bot's take-over picks the soldier nearest to where the lord's last body stood, and a removed
    /// agent's handle answers for the slot's next tenant, so it is never read from a held dead <c>Agent</c>.
    /// </summary>
    private void NoteLordAt(Vec3 at)
    {
        _lordLastX = at.x;
        _lordLastY = at.y;
        _lordLastZ = at.z;
        _lordLastKnown = true;
    }

    /// <summary>
    /// The rise (D40). The soldier the lord took is found first, by reference: the live main agent that is not the body the field spawned for the lord. He is handed to the AI, then the lord is spawned at the place in
    /// his kit, in the order vanilla's own body swap uses (<c>MainAgent.Controller = AI</c>, then the new body to <c>Player</c>, Mission.cs:4736-4738). That order keeps the formation's books right when both stand in
    /// one formation: the hand-back clears <c>Formation.HasPlayerControlledTroop</c> (Formation.cs:1928-1930) and the lord's own <c>AddUnit</c> sets it again (:2288-2290); the other way round the flag would stay
    /// false with the player in the formation. Handing a body to the AI never clears <c>Mission.MainAgent</c>, so the soldier stays main until the new body, built with the <c>Player</c> controller (Mission.cs:4506),
    /// takes it. A refused spawn gives the soldier back to the player, so a person is never left in a body he cannot leave. A soldier already AI-driven (every body under autoplay, after its adoption) is a no-op. One
    /// who is using a machine or a ladder is handed back all the same and the answer says so (R23: stopping his use first is not decided).
    /// </summary>
    public LdRise RiseLord(LdPlace? place, LdLoadout? loadout)
    {
        var main = _mission.MainAgent;
        var taken = main != null && Live(main) && !ReferenceEquals(main, _lordWatch.Lord) ? main : null;
        var wasUsing = taken != null && taken.IsUsingGameObject;
        var troop = taken?.Character?.StringId ?? "";
        var handBack = HandBackSoldier(taken);
        var spawned = SpawnPlayer(true, loadout, place);
        if (!spawned || !(_lordWatch.Lord is Agent fresh) || !Live(fresh))
        {
            if (handBack == LdHandBack.Done && taken != null && Live(taken))
                taken.Controller = AgentControllerType.Player;
            _logger.LogWarning($"[LongDefeat][Lord] rise: the lord could not be spawned at {(place == null ? "the engine's frame" : At(new Vec3(place.X, place.Y, place.Z)))}; the soldier stays the player's");
            return LdRise.Refused;
        }

        _knownMain = fresh;
        _logger.LogInfo($"[LongDefeat][Lord] rise: spawned, soldier={(troop.Length == 0 ? "none" : troop)} handed_back={handBack} was_using={(wasUsing ? 1 : 0)}"
            + $" player_flag={(fresh.Formation == null ? "none" : fresh.Formation.HasPlayerControlledTroop ? "1" : "0")}{FormationTail(taken, handBack)}");
        return new LdRise(true, handBack, wasUsing);
    }

    private static LdHandBack HandBackSoldier(Agent? taken)
    {
        if (taken == null)
            return LdHandBack.None;
        if (!Live(taken))
            return LdHandBack.Dead;
        if (taken.Controller == AgentControllerType.AI)
            return LdHandBack.Noop;

        taken.Controller = AgentControllerType.AI;
        return LdHandBack.Done;
    }

    /// <summary>The formation and order of a soldier just handed back, so a game run can settle whether he returns to his formation's order (A30). Empty for a soldier no longer standing.</summary>
    private static string FormationTail(Agent? taken, LdHandBack handBack)
    {
        if (taken == null || handBack == LdHandBack.None || handBack == LdHandBack.Dead || !Live(taken))
            return "";

        var formation = taken.Formation;
        return formation == null
            ? " formation=none"
            : $" formation={formation.FormationIndex} order={formation.GetReadonlyMovementOrderReference().OrderEnum} controller={taken.Controller}";
    }

    /// <summary>
    /// The bot's take-over (D44a). Among the player side team's agents <c>Mission.CanTakeControlOfAgent</c> accepts, the one nearest to the last position the lord's live body stood at is taken through
    /// <c>Mission.TakeControlOfAgent</c>. A posted tower archer is never taken: the engine's test does not look at a scripted position, and the posts' hold would pin the bot's body to its tower for the wave.
    /// His troop, formation and order are logged before the take (the director logs them again after the adoption). With no soldier it takes nobody and logs it once per ask.
    /// </summary>
    public LdTake TakeOverForBot()
    {
        var team = PlayerSideTeam;
        if (team == null || _mission.MainAgent != null)
        {
            _logger.LogInfo("[LongDefeat][Lord] take-over: nobody taken, a main agent stands or the mission has no player side team");
            return default;
        }

        Agent? best = null;
        var bestDistance = float.MaxValue;
        var candidates = 0;
        foreach (var agent in team.ActiveAgents)
        {
            if (!_mission.CanTakeControlOfAgent(agent) || !Live(agent) || _postedByAgent.ContainsKey(agent))
                continue;

            candidates++;
            var distance = DistanceFromLastLord(agent.Position);
            if (best == null || distance < bestDistance)
            {
                best = agent;
                bestDistance = distance;
            }
        }

        if (best == null)
        {
            _logger.LogInfo("[LongDefeat][Lord] take-over: no soldier qualifies (candidates=0)");
            return default;
        }

        var troop = best.Character?.StringId ?? "";
        var formation = best.Formation;
        _logger.LogInfo($"[LongDefeat][Lord] take-over before: troop={troop} formation={(formation == null ? "none" : formation.FormationIndex.ToString())} "
            + $"order={(formation == null ? "none" : formation.GetReadonlyMovementOrderReference().OrderEnum.ToString())} controller={best.Controller} candidates={candidates} "
            + $"distance={(_lordLastKnown ? Num(bestDistance) : "unknown")} last={(_lordLastKnown ? At(new Vec3(_lordLastX, _lordLastY, _lordLastZ)) : "unknown")}");
        _mission.TakeControlOfAgent(best);
        return new LdTake(true, troop);
    }

    /// <summary>The distance on the ground from the lord's last live position to a point. With no position ever read every soldier is at 0, so the first one the engine accepts is taken; a distance that is not a number is <c>float.MaxValue</c>, so that soldier loses to any measured one.</summary>
    private float DistanceFromLastLord(Vec3 at)
    {
        if (!_lordLastKnown)
            return 0f;

        var dx = at.x - _lordLastX;
        var dy = at.y - _lordLastY;
        var distance = (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
        return distance < float.MaxValue ? distance : float.MaxValue;
    }

    /// <summary>
    /// The general's seat (D41): the live main agent for the lord, the live held hero with that id for a hero, and null for nobody or for a hero who does not stand. A hero is seated only on his own side: in a
    /// story mission where the player attacks, the free heroes stand with the defenders, and an enemy never leads the player's team. Never a dead body: the engine does not clear
    /// <c>Team.GeneralAgent</c> when its body is removed (Mission.cs:3015-3041), so a seat nobody writes stays the dead lord.
    /// </summary>
    public void SetGeneral(LdGeneralSeat seat)
    {
        var team = PlayerSideTeam;
        if (team == null)
            return;

        Agent? general = null;
        if (seat.Kind == GeneralSeatKind.Lord)
        {
            var main = _mission.MainAgent;
            general = main != null && Live(main) ? main : null;
        }
        else if (seat.Kind == GeneralSeatKind.Hero)
        {
            foreach (var pair in _heroes)
            {
                if (string.Equals(pair.Id, seat.HeroId, StringComparison.Ordinal) && Live(pair.Agent) && ReferenceEquals(pair.Agent.Team, team))
                    general = pair.Agent;
            }
        }

        team.GeneralAgent = general;
        _logger.LogInfo($"[LongDefeat][Lord] general written: seat={SeatName(seat)} live={(general == null ? 0 : 1)}");
    }

    private static string SeatName(LdGeneralSeat seat) => seat.Kind == GeneralSeatKind.Hero ? "hero:" + seat.HeroId : seat.Kind == GeneralSeatKind.Lord ? "lord" : "none";

    /// <summary>Who the engine names as the player's team general now: nobody, the live main agent, a live held hero by his id, or any other body (the dead lord's).</summary>
    public LdSeatRead ReadSeat()
    {
        var general = PlayerSideTeam?.GeneralAgent;
        if (general == null)
            return new LdSeatRead(LdSeatKind.None);

        var main = _mission.MainAgent;
        if (main != null && ReferenceEquals(general, main))
            return Live(main) ? new LdSeatRead(LdSeatKind.Main) : new LdSeatRead(LdSeatKind.Stale);

        foreach (var pair in _heroes)
        {
            if (ReferenceEquals(pair.Agent, general))
                return Live(general) && ReferenceEquals(general.Team, PlayerSideTeam) ? new LdSeatRead(LdSeatKind.Hero, pair.Id) : new LdSeatRead(LdSeatKind.Stale);
        }

        return new LdSeatRead(LdSeatKind.Stale);
    }

    /// <summary>The controller of the main agent (<c>none</c> with no live one) and how many other live humans of the player's side are <c>Player</c>-controlled.</summary>
    public LdControlRead ReadControl()
    {
        var main = _mission.MainAgent;
        var controller = main != null && Live(main) ? main.Controller.ToString() : "none";
        var others = 0;
        var team = PlayerSideTeam;
        if (team != null)
        {
            foreach (var agent in team.ActiveAgents)
            {
                if (agent.IsHuman && !ReferenceEquals(agent, main) && Live(agent) && agent.Controller == AgentControllerType.Player)
                    others++;
            }
        }

        return new LdControlRead(controller, others);
    }

    /// <summary>
    /// The forced fall (D47): fells the lord's own live body, which must be the main agent, with the blow <c>Fell</c> builds (<see cref="StrikeFelled"/>), owned by the nearest standing agent of the other side. The
    /// body is marked first, so the removal the blow raises reports itself forced (<c>LdRemoval.Forced</c>), and the mark is withdrawn when the blow leaves him standing. Standing is read from his health, which
    /// managed code writes before <c>RegisterBlow</c> returns (<c>HandleBlow</c> sets it and calls <c>Die</c> under 1, Agent.cs:5503-5530); whether the native side has removed him by then is not needed. An immortal
    /// body or a mission that disables dying keeps its health, so the mark is withdrawn. <c>KillAgentCheat</c> is never used here:
    /// it credits <c>MainAgent ?? agent</c> (Mission.cs:6220), which is the lord himself. With no such owner nobody is felled.
    /// </summary>
    public bool FellLord()
    {
        var lord = _lordWatch.Lord as Agent;
        if (lord == null || !Live(lord) || !ReferenceEquals(lord, _mission.MainAgent))
        {
            _logger.LogWarning("[LongDefeat][Lord] forced fall: the lord's own body is not the standing main agent; nobody was felled");
            return false;
        }

        var owner = NearestStandingFoe(lord);
        if (owner == null)
        {
            _logger.LogWarning("[LongDefeat][Lord] forced fall: no attacker stands to own the blow; nobody was felled");
            return false;
        }

        _logger.LogInfo($"[LongDefeat][Lord] forced fall: felling the lord's body, blow owned by {owner.Character?.StringId} at {Num(lord.Position.AsVec2.Distance(owner.Position.AsVec2))} m");
        _lordWatch.MarkForced(lord);
        lord.Health = FellHealth(lord.Health);
        StrikeFelled(lord, owner);
        if (lord.Health >= 1f)
        {
            _lordWatch.ClearForced();
            _logger.LogWarning("[LongDefeat][Lord] forced fall: the blow left the lord standing; the mark is withdrawn");
            return false;
        }

        return true;
    }

    /// <summary>The standing human of the side opposite the player's, other than the victim, nearest to him; null when none stands. A distance that is not a number never wins.</summary>
    private Agent? NearestStandingFoe(Agent victim)
    {
        var from = victim.Position.AsVec2;
        Agent? nearest = null;
        var best = float.MaxValue;
        foreach (var agent in Side(!PlayerAttacks).ActiveAgents)
        {
            if (!agent.IsHuman || !Live(agent) || agent.IsRunningAway || ReferenceEquals(agent, victim))
                continue;

            var distance = from.Distance(agent.Position.AsVec2);
            if (!(distance < best))
                continue;

            best = distance;
            nearest = agent;
        }

        return nearest;
    }

    /// <summary>Drops the last position the lord stood at and the body marked forced. The run's end calls it, and so does <c>Clear</c>.</summary>
    public void ReleaseLord()
    {
        _lordLastKnown = false;
        _lordWatch.ClearForced();
    }
}

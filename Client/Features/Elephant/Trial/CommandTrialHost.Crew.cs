using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Elephant;
using TAOM.Features.Mumakil;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The crew steps' window onto the engine (#1503): the crew machines, their seats and the Charge order.</summary>
internal sealed partial class CommandTrialHost
{
    // Keyed by the agent object, never by its index: the engine gives a deleted agent's index to the next agent it builds.
    private readonly Dictionary<Agent, Vec3> _beastFirstSeenAt = new();
    private UsableMachine? _crewMachine;
    private Agent? _crewBeast;

    public int ChooseCrew(CrewBeast beast)
    {
        _crewMachine = null;
        _crewBeast = null;
        // The engine lists a mission object made at run time here too: MissionObject.OnPreInit calls Mission.AddActiveMissionObject.
        foreach (var missionObject in _mission.ActiveMissionObjects)
        {
            if (missionObject is not UsableMachine machine)
                continue;
            var agent = BeastOf(machine);
            if (agent == null || !IsLive(agent))
                continue;

            if (!_beastFirstSeenAt.ContainsKey(agent))
                _beastFirstSeenAt[agent] = agent.Position;
            if (_crewMachine == null && KindOf(machine) == beast && HasFullCrew(machine))
            {
                _crewMachine = machine;
                _crewBeast = agent;
            }
        }

        if (_crewMachine == null)
            return 0;
        return ChooseCrewArcher() ? SeatCount(_crewMachine) : 0;
    }

    public bool ChooseCrewArcher()
    {
        _chosenAgent = null;
        _chosenFormation = null;
        _adapter.TrialKeepTarget = null;
        if (_crewMachine == null)
            return false;

        foreach (var seat in _crewMachine.StandingPoints)
        {
            if (ProbeOf(seat) == null)
                continue;
            var archer = ArcherOf(seat);
            if (archer == null)
                continue;

            _chosenAgent = archer;
            _adapter.TrialKeepTarget = archer;
            return true;
        }

        return false;
    }

    public CrewReading ReadCrew()
    {
        var machine = _crewMachine;
        var beast = _crewBeast;
        if (machine == null || beast == null || !IsLive(beast))
            return new CrewReading(false, Array.Empty<CrewSeatReading>(), float.NaN);

        var seats = new List<CrewSeatReading>();
        foreach (var seat in machine.StandingPoints)
        {
            var probe = ProbeOf(seat);
            if (probe == null)
                continue;
            var archer = ArcherOf(seat);
            seats.Add(new CrewSeatReading(archer != null ? ShotsOf(archer) : 0, probe.Rescues));
        }

        var moved = _beastFirstSeenAt.TryGetValue(beast, out var start) ? beast.Position.Distance(start) : float.NaN;
        return new CrewReading(true, seats, moved);
    }

    public void ChargeAll()
    {
        NoteWrite("CommandTrialHost.ChargeAll");
        var team = _mission.PlayerTeam;
        if (team == null)
            return;

        foreach (var formation in team.FormationsIncludingEmpty)
        {
            if (formation.CountOfUnits > 0)
                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }
    }

    private static Agent? BeastOf(UsableMachine machine) => machine switch
    {
        TaomMumakilPlatform platform => platform.mumakilAgent,
        TaomHowdahMachine howdah => howdah.elephantAgent,
        _ => null,
    };

    private static CrewBeast? KindOf(UsableMachine machine) => machine switch
    {
        TaomMumakilPlatform => CrewBeast.Mumakil,
        TaomHowdahMachine => CrewBeast.Howdah,
        _ => null,
    };

    private static HowdahSeatProbe? ProbeOf(StandingPoint seat) => seat switch
    {
        TaomMumakilStandingPoint mumakilSeat => mumakilSeat.Probe,
        TaomHowdahStandingPoint howdahSeat => howdahSeat.Probe,
        _ => null,
    };

    private static int ShotsOf(Agent archer) => archer.Origin switch
    {
        MumakilCrewAgentOrigin origin => origin.ShotsFired,
        HowdahCrewAgentOrigin origin => origin.ShotsFired,
        _ => 0,
    };

    // A held Agent answers for its slot's next tenant after the engine deletes it, so the slot check comes before every native read.
    private static bool IsLive(Agent agent) => AgentSlotIdentity.IsCurrentOccupant(agent) && agent.IsActive();

    // Vanilla UsableMissionObject.OnUse moves the agent from MovingAgent to UserAgent; TAOM's crew seats override OnUse and keep it as MovingAgent.
    private static Agent? ArcherOf(StandingPoint seat)
    {
        var archer = seat.MovingAgent;
        return archer != null && IsLive(archer) ? archer : null;
    }

    private static int SeatCount(UsableMachine machine)
    {
        var count = 0;
        foreach (var seat in machine.StandingPoints)
        {
            if (ProbeOf(seat) != null)
                count++;
        }

        return count;
    }

    private static bool HasFullCrew(UsableMachine machine)
    {
        var seats = 0;
        foreach (var seat in machine.StandingPoints)
        {
            if (ProbeOf(seat) == null)
                continue;
            seats++;
            if (ArcherOf(seat) == null)
                return false;
        }

        return seats > 0;
    }
}

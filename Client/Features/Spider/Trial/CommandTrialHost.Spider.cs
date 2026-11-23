using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TAOM.Features.Spider;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The spider step's window onto the engine (#1541): the riders on a spider, their mounts' speed and the Stop order.</summary>
internal sealed partial class CommandTrialHost
{
    /// <summary>Keyed by the agent object, never by its index: the engine gives a deleted agent's index to the next agent it builds.</summary>
    private readonly HashSet<Agent> _spiderRiders = new();

    /// <summary>The speed of a mount is the length of <c>Agent.MovementVelocity</c>, its planar velocity in its own frame; the stamina trial host reads a soldier's speed the same way.</summary>
    public SpiderReading ReadSpiders()
    {
        var team = _mission.PlayerTeam;
        if (team == null)
            return new SpiderReading(0, 0, 0f);

        foreach (var agent in team.ActiveAgents)
        {
            if (agent.IsHuman && SpiderMountOf(agent) != null)
                _spiderRiders.Add(agent);
        }

        var alive = 0;
        var mounted = 0;
        var top = 0f;
        foreach (var rider in _spiderRiders)
        {
            if (!IsLive(rider))
                continue;
            alive++;
            var mount = SpiderMountOf(rider);
            if (mount == null)
                continue;
            mounted++;
            var speed = mount.MovementVelocity.Length;
            if (speed > top || float.IsNaN(speed))
                top = speed;
        }

        return new SpiderReading(alive, mounted, top);
    }

    public bool ChooseSpiderRider()
    {
        _chosenAgent = null;
        _chosenFormation = null;
        _adapter.TrialKeepTarget = null;
        var team = _mission.PlayerTeam;
        if (team == null)
            return false;

        foreach (var agent in team.ActiveAgents)
        {
            if (!agent.IsHuman || SpiderMountOf(agent) == null)
                continue;

            _chosenAgent = agent;
            _adapter.TrialKeepTarget = agent;
            return true;
        }

        return false;
    }

    public void HoldAll()
    {
        NoteWrite("CommandTrialHost.HoldAll");
        var team = _mission.PlayerTeam;
        if (team == null)
            return;

        foreach (var formation in team.FormationsIncludingEmpty)
        {
            if (formation.CountOfUnits > 0)
                formation.SetMovementOrder(MovementOrder.MovementOrderStop);
        }
    }

    /// <summary>The rider's living spider, or null when he is not live, rides nothing or rides another beast. <c>IsLive</c> guards a held Agent before each native read.</summary>
    private static Agent? SpiderMountOf(Agent rider)
    {
        if (!IsLive(rider))
            return null;
        var mount = rider.MountAgent;
        return mount != null && IsLive(mount) && mount.Monster?.StringId == SpiderConfig.SpiderMonsterId ? mount : null;
    }
}

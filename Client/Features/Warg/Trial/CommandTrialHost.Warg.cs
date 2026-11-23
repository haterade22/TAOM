using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.Warg;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The warg step's window onto the engine (#1563): the riders on a warg, their mounts' speed, limit and scale, and the horses beside them.</summary>
internal sealed partial class CommandTrialHost
{
    /// <summary>Keyed by the agent object, never by its index: the engine gives a deleted agent's index to the next agent it builds.</summary>
    private readonly HashSet<Agent> _wargRiders = new();

    /// <summary>
    /// The speed of a mount is the length of <c>Agent.MovementVelocity</c>, as the spider step reads it. <c>Math.Max</c> returns NaN when
    /// either value is NaN, so a speed that is no number reaches the pass rule and fails it.
    /// </summary>
    public WargReading ReadWargs()
    {
        var team = _mission.PlayerTeam;
        if (team == null)
            return new WargReading(0, 0, 0f, 0f, 0f, 0f);

        var horseTop = 0f;
        foreach (var agent in team.ActiveAgents)
        {
            if (!agent.IsHuman || !IsLive(agent))
                continue;
            var mount = agent.MountAgent;
            if (mount == null || !IsLive(mount))
                continue;
            if (IsWarg(mount))
                _wargRiders.Add(agent);
            else
                horseTop = Math.Max(horseTop, mount.MovementVelocity.Length);
        }

        var alive = 0;
        var mounted = 0;
        var top = 0f;
        var limit = 0f;
        var scale = 0f;
        foreach (var rider in _wargRiders)
        {
            if (!IsLive(rider))
                continue;
            alive++;
            var mount = rider.MountAgent;
            if (mount == null || !IsLive(mount) || !IsWarg(mount))
                continue;
            mounted++;
            top = Math.Max(top, mount.MovementVelocity.Length);
            limit = Math.Max(limit, mount.GetAgentDrivenPropertyValue(DrivenProperty.MountSpeed));
            scale = mount.AgentScale;
        }

        return new WargReading(alive, mounted, top, horseTop, limit, scale);
    }

    private static bool IsWarg(Agent mount) => WargConfig.IsWargMonster(mount.Monster?.StringId!);
}

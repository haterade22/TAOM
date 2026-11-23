using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.Spider;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

/// <summary>The spider step's window onto the engine (#1541): the riders on a spider, their mounts' speed and the Stop order.</summary>
internal sealed partial class CommandTrialHost
{
    /// <summary>Keyed by the agent object, never by its index: the engine gives a deleted agent's index to the next agent it builds.</summary>
    private readonly HashSet<Agent> _spiderRiders = new();

    private readonly SpiderSlideMeter _spiderSlide = new();
    private readonly Dictionary<Agent, Vec3[]> _spiderTipsAt = new();
    private sbyte[]? _spiderTipBones;

    /// <summary>
    /// The speed of a mount is the length of <c>Agent.MovementVelocity</c>, its planar velocity in its own frame; the stamina trial host reads a soldier's speed the same way.
    /// A mount whose leg tips cannot be read is not judged, and a run that judged too little fails (<c>JudgedAtLeastSecondsPerRider</c>).
    /// </summary>
    public SpiderReading ReadSpiders()
    {
        var team = _mission.PlayerTeam;
        if (team == null)
            return new SpiderReading(0, 0, 0f, _spiderSlide.SlideSeconds, _spiderSlide.JudgedSeconds);

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
            _spiderSlide.Add(mount, _mission.CurrentTime, speed, LegTravel(mount));
            if (speed > top || float.IsNaN(speed))
                top = speed;
        }

        return new SpiderReading(alive, mounted, top, _spiderSlide.SlideSeconds, _spiderSlide.JudgedSeconds);
    }

    /// <summary>
    /// The mean metres the mount's eight leg tips moved against its body since its last reading, from the bones' frames in the
    /// mount's own space. No number on a mount's first reading, with no skeleton yet, and when a tip bone is not on the skeleton.
    /// </summary>
    private float LegTravel(Agent mount)
    {
        var skeleton = mount.AgentVisuals?.GetSkeleton();
        if (skeleton == null)
            return float.NaN;

        _spiderTipBones ??= SpiderTrialRules.LegTipBones
            .Select(bone => Skeleton.GetBoneIndexFromName(mount.ActionSet.GetSkeletonName(), bone)).ToArray();
        if (_spiderTipBones.Any(bone => bone < 0))
            return float.NaN;

        var at = new Vec3[_spiderTipBones.Length];
        for (var i = 0; i < at.Length; i++)
            at[i] = skeleton.GetBoneEntitialFrameWithIndex(_spiderTipBones[i]).origin;

        var travel = float.NaN;
        if (_spiderTipsAt.TryGetValue(mount, out var was))
        {
            travel = 0f;
            for (var i = 0; i < at.Length; i++)
                travel += (at[i] - was[i]).Length;
            travel /= at.Length;
        }

        _spiderTipsAt[mount] = at;
        return travel;
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

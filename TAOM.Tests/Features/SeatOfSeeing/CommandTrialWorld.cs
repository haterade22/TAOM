using System;
using System.Collections.Generic;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>A stand-in for the engine and the Seat, so the field trial's script runs a whole battle in a test; the knobs break one fact at a time.</summary>
internal sealed partial class CommandTrialWorld : ICommandTrialHost
{
    private const float GroundZ = 100f;

    internal readonly Vec3f Hero = new(100f, 100f, GroundZ);
    internal Vec3f View = new(100f, 92f, GroundZ + 3f);
    internal float Time;
    internal bool Deployment = true;

    internal bool HeroPresent = true;
    internal bool HeroPlayerControlled = true;
    internal float HeroDriftPerSecond;
    internal bool ViewFrozen;
    internal bool HeroGone;
    internal float? ClockFailsFrom;

    internal readonly List<string> Screenshots = new();
    internal readonly List<float> ScreenshotTimes = new();
    internal int DeploymentFinishes;
    internal float? DeploymentFinishedAt;

    private float _heroDrift;

    private Vec3f HeroCamera => new(Hero.X + _heroDrift, Hero.Y - 3f, Hero.Z + 2f);

    public float MissionTime => ClockFailsFrom.HasValue && Time >= ClockFailsFrom.Value ? throw new InvalidOperationException("the clock failed") : _timeOn ? MissionClock : Time;

    public bool InDeployment => Deployment;

    public bool InBattle => !Deployment;

    public bool HeroAlive => HeroPresent;

    public bool HeroIsPlayerControlled => HeroPlayerControlled && !_heroOnAi;

    public Vec3f? HeroPosition() => HeroGone ? null : new Vec3f(Hero.X + _heroDrift, Hero.Y, Hero.Z);

    public Vec3f? ViewPosition() => new Vec3f(View.X, View.Y, View.Z + LiftOfTheCamera);

    public float GroundHeight(float x, float y) => GroundZ;

    public void FinishDeployment()
    {
        DeploymentFinishes++;
        DeploymentFinishedAt ??= Time;
        Deployment = false;
    }

    public void Screenshot(string fileName)
    {
        Screenshots.Add(fileName);
        ScreenshotTimes.Add(Time);
    }

    /// <summary>One screen tick: the key is taken first (as the view does), then the script runs.</summary>
    internal void ScreenTick(CommandTrialScript script, float dt = 1f / 60f)
    {
        TakeThePress();
        TakeTheReopen();
        script.OnScreenTick(dt);
    }

    /// <summary>One mission tick: the camera frame (it runs first in Mission.OnTick), the hero, then the script's mission tick.</summary>
    internal void MissionTick(CommandTrialScript script, float dt)
    {
        Frame(dt);
        _heroDrift += (HeroDriftPerSecond + FightDrift + AfterHandBackDrift) * dt;
        InMissionTick = true;
        try
        {
            script.OnMissionTick();
        }
        finally
        {
            InMissionTick = false;
        }
    }

    internal void Run(CommandTrialScript script, float seconds, float dt = 1f / 60f) => Run(script, seconds, new[] { dt });

    /// <summary>Runs frames whose lengths repeat the cycle in order: the uneven frames of a machine that stutters.</summary>
    internal void Run(CommandTrialScript script, float seconds, float[] frameCycle)
    {
        var end = Time + seconds;
        for (var frame = 0; Time < end && !script.IsDone; frame++)
        {
            var dt = frameCycle[frame % frameCycle.Length];
            Time += dt;
            AdvanceMissionClock(dt);
            ScreenTick(script, dt);
            MissionTick(script, dt);
        }
    }
}

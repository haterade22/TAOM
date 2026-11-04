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

    private Vec3f HeroCamera => new(Hero.X, Hero.Y - 3f, Hero.Z + 2f);

    public float MissionTime => ClockFailsFrom.HasValue && Time >= ClockFailsFrom.Value ? throw new InvalidOperationException("the clock failed") : Time;

    public bool InDeployment => Deployment;

    public bool InBattle => !Deployment;

    public bool HeroAlive => HeroPresent;

    public bool HeroIsPlayerControlled => HeroPlayerControlled;

    public Vec3f? HeroPosition() => HeroGone ? null : new Vec3f(Hero.X + _heroDrift, Hero.Y, Hero.Z);

    public Vec3f? ViewPosition() => View;

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
        _heroDrift += HeroDriftPerSecond * dt;
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

    internal void Run(CommandTrialScript script, float seconds, float dt = 1f / 60f)
    {
        var end = Time + seconds;
        while (Time < end && !script.IsDone)
        {
            Time += dt;
            ScreenTick(script, dt);
            MissionTick(script, dt);
        }
    }
}

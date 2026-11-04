using System;
using System.Collections.Generic;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The Seat half of the stand-in: the key (taken at the next screen tick), the blend, the flight and the return glide, by the Seat's own arithmetic in <see cref="SeatCameraMath"/>.</summary>
internal sealed partial class CommandTrialWorld
{
    internal float FlightHeight = 22f;
    internal float ReenterHeight = 22f;
    internal float BlendSeconds = 0.7f;
    internal SeatInput? Input;

    internal bool PressesAreIgnored;
    internal bool GlideArms = true;
    internal bool FaultOnEntry;
    internal bool FaultInDeployment;
    internal bool FaultOnLeave;
    internal float SpeedFactor = 1f;
    internal float FramesPerOwnedTick = 1f;
    internal float GlideSeconds = 1f;
    internal bool GlideStuck;
    internal bool HoldsTheSeat;
    internal bool SeatIgnoresDeployment;

    internal readonly List<float> PressTimes = new();
    internal int PressesInDeployment;
    internal float? ForwardHeldAt;
    internal float SeatedAt = float.NaN;

    private bool _pending;
    private float _blend;
    private float _framesOwned;
    private float _glideLeft;
    private bool _everEntered;

    public SeatState State { get; private set; }

    public bool Faulted { get; private set; }

    public bool GlideBegan { get; private set; }

    public bool PressPending => _pending;

    public int Frames => (int)_framesOwned;

    internal int Presses => PressTimes.Count;

    public void PressSeatKey()
    {
        _pending = true;
        PressTimes.Add(Time);
        if (Deployment)
            PressesInDeployment++;
    }

    public void SetInput(SeatInput? input)
    {
        Input = input;
        if (input is { Forward: > 0f })
            ForwardHeldAt ??= Time;
    }

    private void TakeThePress()
    {
        if (_pending && !PressesAreIgnored)
        {
            _pending = false;
            Take();
        }
    }

    private void Take()
    {
        if (Deployment && !SeatIgnoresDeployment)
        {
            Faulted |= FaultInDeployment;
            return;
        }

        if (State != SeatState.Off && HoldsTheSeat)
            return;
        if (State == SeatState.Off)
        {
            if (FaultOnEntry)
            {
                Faulted = true;
                return;
            }

            State = BlendSeconds <= 0f ? SeatState.Seated : SeatState.Rising;
            SeatedAt = State == SeatState.Seated ? Time : SeatedAt;
            _blend = 0f;
            _framesOwned = 0f;
            FlightHeight = _everEntered ? ReenterHeight : FlightHeight;
            _everEntered = true;
            View = new Vec3f(Hero.X, Hero.Y - 9f, GroundZ + FlightHeight);
            return;
        }

        State = SeatState.Off;
        GlideBegan = GlideArms;
        Faulted |= FaultOnLeave;
        if (GlideSeconds <= 0f)
            View = HeroCamera;
        else
            _glideLeft = GlideSeconds;
    }

    private void Frame(float dt)
    {
        if (State == SeatState.Rising)
        {
            _blend += dt;
            _framesOwned += FramesPerOwnedTick;
            if (_blend >= BlendSeconds)
            {
                State = SeatState.Seated;
                SeatedAt = Time;
            }
        }
        else if (State == SeatState.Seated)
        {
            _framesOwned += FramesPerOwnedTick;
            if (!ViewFrozen && Input.HasValue)
            {
                var speed = SeatCameraMath.PanSpeed(FlightHeight) * SpeedFactor;
                View = new Vec3f(View.X, View.Y + Input.Value.Forward * speed * dt, GroundZ + FlightHeight);
            }
        }
        else if (_glideLeft > 0f && !GlideStuck)
        {
            _glideLeft = Math.Max(0f, _glideLeft - dt);
            var t = 1f - _glideLeft / GlideSeconds;
            View = Vec3f.Lerp(View, HeroCamera, Math.Min(1f, t * 3f));
        }
    }
}

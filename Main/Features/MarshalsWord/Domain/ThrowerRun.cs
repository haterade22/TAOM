namespace TAOM.Features.MarshalsWord.Domain;

internal enum ThrowerState
{
    Approach,
    Stand,
    Released,
}

internal enum ThrowerReleaseKind
{
    None,
    Rule,
    Gate,
    Lost,
}

/// <summary>What one slow pass tells a run: the census, the gate answer, the gap, the company's place, the approach answer and the time.</summary>
internal readonly record struct ThrowerStep(ThrowerCensus Census, string Why, float Gap, GroundPoint Position, ApproachAnswer Approach, double Now, bool HasTarget = true);

/// <summary>One company's run for one Advance (build-spec 9.4): approach, stand, then release to vanilla's Advance.</summary>
internal sealed class ThrowerRun
{
    private const double SpentTolerance = 1e-9;

    private double _lastNow;
    private int _lastArmed;
    private int _lastAmmo;
    private bool _seen;
    private bool _here;

    internal ThrowerRun(ThrowerCensus first, float range, double now)
    {
        Armed0 = first.Armed;
        Ammo0 = first.Ammo;
        Range = range;
        StandGap = MarshalThrowerRules.StandGap(range);
        StartedAt = now;
        _lastNow = now;
        _lastArmed = first.Armed;
        _lastAmmo = first.Ammo;
    }

    internal ThrowerState State { get; private set; } = ThrowerState.Approach;

    internal GroundPoint? Published { get; private set; }

    internal int Armed0 { get; }

    internal int Ammo0 { get; }

    internal float Range { get; }

    internal float StandGap { get; }

    internal double StartedAt { get; }

    internal bool EverStood { get; private set; }

    internal bool ClosedBeforeStand { get; private set; }

    internal float GapAtStand { get; private set; } = float.NaN;

    internal float MinGap { get; private set; } = float.PositiveInfinity;

    internal float MaxOvershoot { get; private set; }

    internal double StoodSeconds { get; private set; }

    internal ThrowerReleaseKind ReleaseKind { get; private set; }

    internal string ReleaseReason { get; private set; } = "";

    internal double ReleasedAt { get; private set; } = double.NaN;

    internal int ArmedAtRelease { get; private set; }

    /// <summary>Metres a second between the company's positions at the last two passes; not a number until there are two.</summary>
    internal float Speed { get; private set; } = float.NaN;

    /// <summary>The company's position at the last pass.</summary>
    internal GroundPoint Position { get; private set; }

    /// <summary>The unit direction from the company to its target at the last pass that had a point.</summary>
    internal float DirX { get; private set; }

    internal float DirY { get; private set; }

    internal int AmmoAtRelease { get; private set; }

    internal void Step(ThrowerStep step)
    {
        if (State == ThrowerState.Released)
            return;

        var elapsed = step.Now - _lastNow;
        if (State == ThrowerState.Stand)
            StoodSeconds += elapsed;
        Speed = _seen && elapsed > 0.0 ? (float)(Distance(step.Position, Position) / elapsed) : float.NaN;
        Position = step.Position;
        _seen = true;
        _lastNow = step.Now;
        _lastArmed = step.Census.Armed;
        _lastAmmo = step.Census.Ammo;

        if (step.HasTarget)
        {
            if (step.Approach.Kind != ApproachKind.NoPoint)
            {
                DirX = step.Approach.DirX;
                DirY = step.Approach.DirY;
            }

            NoteFacts(step);
            Move(step);
        }

        ReleaseIfDue(step);
    }

    internal void Lose(string reason, double now)
    {
        if (State == ThrowerState.Released)
            return;
        Release(ThrowerReleaseKind.Lost, reason, now);
    }

    private void NoteFacts(ThrowerStep step)
    {
        if (step.Gap < MinGap)
            MinGap = step.Gap;
        if (!EverStood && step.Gap <= MarshalTuning.MeleeGapMetres)
            ClosedBeforeStand = true;
        if (Published is { } point && step.Approach.Kind != ApproachKind.NoPoint)
        {
            var past = (step.Position.X - point.X) * step.Approach.DirX + (step.Position.Y - point.Y) * step.Approach.DirY;
            if (past > MaxOvershoot)
                MaxOvershoot = past;
        }
    }

    private void Move(ThrowerStep step)
    {
        if (State == ThrowerState.Stand && step.Gap > StandGap + MarshalTuning.FollowMetres)
            State = ThrowerState.Approach;
        if (State != ThrowerState.Approach)
            return;

        if (step.Approach.Kind == ApproachKind.StandHere)
        {
            if (!_here)
                Published = step.Approach.Point;
            _here = true;
        }
        else if (step.Approach.Kind == ApproachKind.Walk && MovedPastRepublish(step.Approach.Point))
        {
            Published = step.Approach.Point;
            _here = false;
        }

        if (Published is { } anchor && Speed <= MarshalTuning.StandStillSpeed && Distance(step.Position, anchor) <= MarshalTuning.StandArriveMetres)
            Stand(step.Gap);
    }

    private void Stand(float gap)
    {
        State = ThrowerState.Stand;
        if (EverStood)
            return;
        EverStood = true;
        GapAtStand = gap;
    }

    private void ReleaseIfDue(ThrowerStep step)
    {
        var reason = DueReason(step);
        if (reason.Length == 0)
            return;
        var kind = reason == step.Why ? ThrowerReleaseKind.Gate : ThrowerReleaseKind.Rule;
        Release(kind, reason, step.Now);
    }

    private string DueReason(ThrowerStep step)
    {
        if (step.Census.Armed <= MarshalTuning.ReleaseShare * Armed0 + SpentTolerance)
            return "spent";
        if (step.HasTarget && !(step.Gap > MarshalTuning.CloseMetres))
            return "enemy-close";
        if (StoodSeconds >= MarshalTuning.StandLimitSeconds)
            return "stand-time";
        if (step.Now - StartedAt >= MarshalTuning.RunLimitSeconds)
            return "run-time";
        return step.Why == MarshalThrowerRules.TooFew ? "" : step.Why;
    }

    private void Release(ThrowerReleaseKind kind, string reason, double now)
    {
        State = ThrowerState.Released;
        ReleaseKind = kind;
        ReleaseReason = reason;
        ReleasedAt = now;
        ArmedAtRelease = _lastArmed;
        AmmoAtRelease = _lastAmmo;
        Published = null;
    }

    private bool MovedPastRepublish(GroundPoint point) =>
        Published is not { } last || Distance(point, last) > MarshalTuning.RepublishMetres;

    private static float Distance(GroundPoint a, GroundPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (float)System.Math.Sqrt(dx * dx + dy * dy);
    }
}

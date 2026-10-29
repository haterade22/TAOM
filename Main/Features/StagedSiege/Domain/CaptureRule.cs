using System;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>What the capture point is doing right now.</summary>
public enum CaptureStatus
{
    Idle,
    Rising,
    Falling,
    Contested,
    Captured,
}

/// <summary>The progress after one reconcile pass, and why it moved (or did not).</summary>
public readonly struct CaptureStep
{
    public CaptureStep(float progress, CaptureStatus status)
    {
        Progress = progress;
        Status = status;
    }

    public float Progress { get; }
    public CaptureStatus Status { get; }
}

/// <summary>
/// The capture arithmetic. Attackers fill the bar when there are enough of them and they outnumber the defenders by the
/// dominance ratio; defenders drain it the same way; anything in between holds it. Pure and total: a NaN or a negative step
/// changes nothing.
/// </summary>
public static class CaptureRule
{
    public static CaptureStep Step(float progress, int attackers, int defenders, float dt, StagedSiegeTunables t)
    {
        if (!(dt > 0f))
            return new CaptureStep(progress, CaptureStatus.Idle);

        dt = Math.Min(dt, 2f * t.ReconcileSeconds);
        if (float.IsNaN(progress) || float.IsInfinity(progress))
            progress = 0f;

        if (attackers >= t.MinAttackers && attackers >= t.DominanceRatio * defenders)
        {
            progress += dt / t.CaptureSeconds;
            return progress >= 1f
                ? new CaptureStep(1f, CaptureStatus.Captured)
                : new CaptureStep(progress, CaptureStatus.Rising);
        }

        if (defenders >= 1 && defenders >= t.DominanceRatio * attackers)
            return new CaptureStep(Math.Max(0f, progress - dt / t.DecaySeconds), CaptureStatus.Falling);

        return new CaptureStep(progress, attackers > 0 && defenders > 0 ? CaptureStatus.Contested : CaptureStatus.Idle);
    }
}

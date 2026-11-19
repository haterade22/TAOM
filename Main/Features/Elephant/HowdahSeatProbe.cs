using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.Elephant;

/// <summary>
/// One crew seat's numbers for the status line (#1482). Pure: no engine types, so the arithmetic is tested.
///
/// It exists because totals misled two diagnoses. A teleport count beside a restart count cannot say whether a
/// teleport fell inside the reload that restarted, and one sample of an archer's distance from its seat cannot say
/// how far behind it got between samples. So a restart is counted against a teleport only when one happened since
/// the previous restart, and the lag is a range over the whole period.
///
/// Totals (teleports, rescues, restarts, the highest progress) belong to the occupant and survive a period. The lag
/// range, the seat's own view of its distance and the tick-order count belong to the period.
/// </summary>
internal sealed class HowdahSeatProbe
{
    private const float RestartDrop = 0.01f;

    private float _lastProgress;
    private int _teleportsSinceRestart;

    public int Teleports { get; private set; }

    /// <summary>Placements from far away, by the safety net and not by the seat's ordinary correction.</summary>
    public int Rescues { get; private set; }

    public int Restarts { get; private set; }

    /// <summary>Restarts with at least one teleport or rescue since the restart before.</summary>
    public int RestartsAfterTeleport { get; private set; }

    public float MaxProgress { get; private set; }

    /// <summary>Platform-local forward offset of the archer from its seat, metres. Negative is behind the seat.</summary>
    public float LagForwardMin { get; private set; } = float.NaN;
    public float LagForwardMax { get; private set; } = float.NaN;
    public float LagDistanceMax { get; private set; } = float.NaN;

    /// <summary>The largest distance the SEAT measured in its own tick, which is the number it corrects on.</summary>
    public float SeenDistanceMax { get; private set; } = float.NaN;

    public int SeatTicks { get; private set; }

    /// <summary>Seat ticks that ran before the platform was re-framed that frame, so they saw last frame's seat.</summary>
    public int SeatFirstTicks { get; private set; }

    public void Reset()
    {
        Teleports = 0;
        Rescues = 0;
        Restarts = 0;
        RestartsAfterTeleport = 0;
        MaxProgress = 0f;
        _lastProgress = 0f;
        _teleportsSinceRestart = 0;
        EndPeriod();
    }

    public void NoteTeleport()
    {
        Teleports++;
        _teleportsSinceRestart++;
    }

    public void NoteRescue()
    {
        Rescues++;
        _teleportsSinceRestart++;
    }

    public void NoteProgress(float progress)
    {
        if (!FiniteFloatValidator.IsFinite(progress)) return;
        if (progress < _lastProgress - RestartDrop)
        {
            Restarts++;
            if (_teleportsSinceRestart > 0) RestartsAfterTeleport++;
            _teleportsSinceRestart = 0;
        }
        if (progress > MaxProgress) MaxProgress = progress;
        _lastProgress = progress;
    }

    public void NoteLag(float forward, float distance)
    {
        if (!FiniteFloatValidator.IsFinite(forward) || !FiniteFloatValidator.IsFinite(distance)) return;
        if (float.IsNaN(LagForwardMin) || forward < LagForwardMin) LagForwardMin = forward;
        if (float.IsNaN(LagForwardMax) || forward > LagForwardMax) LagForwardMax = forward;
        if (float.IsNaN(LagDistanceMax) || distance > LagDistanceMax) LagDistanceMax = distance;
    }

    public void NoteSeatTick(float seenDistance, bool platformTickedFirst)
    {
        SeatTicks++;
        if (!platformTickedFirst) SeatFirstTicks++;
        if (!FiniteFloatValidator.IsFinite(seenDistance)) return;
        if (float.IsNaN(SeenDistanceMax) || seenDistance > SeenDistanceMax) SeenDistanceMax = seenDistance;
    }

    /// <summary>The seat's fields for one status line, then a fresh period.</summary>
    public string FormatAndEndPeriod()
    {
        string line = string.Format(CultureInfo.InvariantCulture,
            "tp{0}/rs{1}/re{2}(tp{3})/max{4}/lag{5}..{6}/off{7}/seen{8}/seatFirst{9}of{10}",
            Teleports, Rescues, Restarts, RestartsAfterTeleport, HowdahDiagnostics.Format(MaxProgress, 2),
            HowdahDiagnostics.Format(LagForwardMin, 2), HowdahDiagnostics.Format(LagForwardMax, 2),
            HowdahDiagnostics.Format(LagDistanceMax, 2), HowdahDiagnostics.Format(SeenDistanceMax, 2),
            SeatFirstTicks, SeatTicks);
        EndPeriod();
        return line;
    }

    private void EndPeriod()
    {
        LagForwardMin = float.NaN;
        LagForwardMax = float.NaN;
        LagDistanceMax = float.NaN;
        SeenDistanceMax = float.NaN;
        SeatTicks = 0;
        SeatFirstTicks = 0;
    }
}

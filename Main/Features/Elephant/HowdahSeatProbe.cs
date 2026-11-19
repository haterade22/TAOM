using System.Globalization;
using TAOM.Core.Validation;

namespace TAOM.Features.Elephant;

/// <summary>
/// One crew seat's numbers for the status line (#1482). Pure: no engine types, so the arithmetic is tested.
///
/// It exists because totals misled two diagnoses. A teleport count beside a restart count cannot say whether a
/// teleport fell inside the reload that restarted, and one sample of an archer's distance from its seat cannot say
/// how far behind it got between samples. So a restart is counted against a rescue only when one happened since the
/// previous restart, and the lag is a range over the whole period.
///
/// Totals (rescues, restarts, the highest progress) belong to the occupant and survive a period. The lag range
/// belongs to the period.
/// </summary>
internal sealed class HowdahSeatProbe
{
    private const float RestartDrop = 0.01f;

    private float _lastProgress;
    private int _rescuesSinceRestart;

    /// <summary>Teleports back to the seat by the safety net, for an archer far off its deck. A healthy seat
    /// reads zero: the position lock and the attachment hold the archer.</summary>
    public int Rescues { get; private set; }

    public int Restarts { get; private set; }

    /// <summary>Restarts with at least one rescue since the restart before.</summary>
    public int RestartsAfterRescue { get; private set; }

    public float MaxProgress { get; private set; }

    /// <summary>Platform-local forward offset of the archer from its seat, metres. Negative is behind the seat.</summary>
    public float LagForwardMin { get; private set; } = float.NaN;
    public float LagForwardMax { get; private set; } = float.NaN;
    public float LagDistanceMax { get; private set; } = float.NaN;

    public void Reset()
    {
        Rescues = 0;
        Restarts = 0;
        RestartsAfterRescue = 0;
        MaxProgress = 0f;
        _lastProgress = 0f;
        _rescuesSinceRestart = 0;
        EndPeriod();
    }

    public void NoteRescue()
    {
        Rescues++;
        _rescuesSinceRestart++;
    }

    public void NoteProgress(float progress)
    {
        if (!FiniteFloatValidator.IsFinite(progress)) return;
        if (progress < _lastProgress - RestartDrop)
        {
            Restarts++;
            if (_rescuesSinceRestart > 0) RestartsAfterRescue++;
            _rescuesSinceRestart = 0;
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

    /// <summary>The seat's fields for one status line, then a fresh period.</summary>
    public string FormatAndEndPeriod()
    {
        string line = string.Format(CultureInfo.InvariantCulture,
            "rs{0}/re{1}(rs{2})/max{3}/lag{4}..{5}/off{6}",
            Rescues, Restarts, RestartsAfterRescue, HowdahDiagnostics.Format(MaxProgress, 2),
            HowdahDiagnostics.Format(LagForwardMin, 2), HowdahDiagnostics.Format(LagForwardMax, 2),
            HowdahDiagnostics.Format(LagDistanceMax, 2));
        EndPeriod();
        return line;
    }

    private void EndPeriod()
    {
        LagForwardMin = float.NaN;
        LagForwardMax = float.NaN;
        LagDistanceMax = float.NaN;
    }
}

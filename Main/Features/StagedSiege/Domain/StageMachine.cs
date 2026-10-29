namespace TAOM.Features.StagedSiege.Domain;

/// <summary>How a staged siege ended, when it did.</summary>
public enum StagedEnd
{
    None,
    DefenderPushedBack,
    PlayerDefeated,
}

/// <summary>What a capture (or a forced jump) changed.</summary>
public readonly struct StageTransition
{
    public StageTransition(StageId from, StageId? to, bool openGates, StagedEnd end, bool changed)
    {
        From = from;
        To = to;
        OpenGates = openGates;
        End = end;
        Changed = changed;
    }

    public StageId From { get; }

    /// <summary>The stage now current; null when the capture ended the siege.</summary>
    public StageId? To { get; }

    public bool OpenGates { get; }
    public StagedEnd End { get; }
    public bool Changed { get; }
}

/// <summary>
/// The A to D progression of one siege. Capturing the current point moves to the next; capturing B opens the gates; capturing
/// D ends the siege, in the player's favour when the player attacks and against them when the player defends.
/// </summary>
public sealed class StageMachine
{
    public StageMachine(StagedSide playerSide) => PlayerSide = playerSide;

    public StagedSide PlayerSide { get; }
    public StageId Current { get; private set; } = StageId.A;
    public int CapturedMask { get; private set; }
    public bool GatesOpened { get; private set; }
    public StagedEnd End { get; private set; }

    /// <summary>The current point is taken.</summary>
    public StageTransition Capture()
    {
        var from = Current;
        if (End != StagedEnd.None)
            return new StageTransition(from, null, false, End, false);

        CapturedMask |= 1 << (int)from;
        var openGates = from == StageId.B;
        GatesOpened |= openGates;

        if (from == StageId.D)
        {
            End = PlayerSide == StagedSide.Attacker ? StagedEnd.DefenderPushedBack : StagedEnd.PlayerDefeated;
            return new StageTransition(from, null, openGates, End, true);
        }

        Current = from + 1;
        return new StageTransition(from, Current, openGates, StagedEnd.None, true);
    }

    /// <summary>
    /// Jumps forward by capturing every point up to <paramref name="target"/> (null: through D, which ends the siege). A target
    /// before the current point is refused, and the current point is a no-op.
    /// </summary>
    public StageTransition Force(StageId? target)
    {
        var from = Current;
        if (End != StagedEnd.None || (target.HasValue && target.Value <= from))
            return new StageTransition(from, null, false, End, false);

        var openGates = false;
        StageTransition last = default;
        while (End == StagedEnd.None && (!target.HasValue || Current < target.Value))
        {
            last = Capture();
            openGates |= last.OpenGates;
        }

        return new StageTransition(from, last.To, openGates, End, true);
    }
}

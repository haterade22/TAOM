namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>The window of one hold-the-line proof: a quiet control takes a new baseline and the window stays open, and any other verdict, Pending included, closes it.</summary>
internal sealed class MarshalHoldWindow
{
    private double _begun;
    private double _baseAt = double.NaN;
    private int _baseHeld;
    private int _baseControl;
    private bool _open;

    /// <summary>The baseline of the blow counts has been taken, so the window now compares against it.</summary>
    internal bool BaselineTaken => !double.IsNaN(_baseAt);

    internal void Begin(double now)
    {
        _begun = now;
        _baseAt = double.NaN;
        _open = true;
    }

    internal HoldVerdict? Observe(double now, int flagOff, int heldBlows, int controlBlows)
    {
        if (!_open || now - _begun < MarshalTuning.HoldSettleSeconds)
            return null;
        if (double.IsNaN(_baseAt))
        {
            if (flagOff < MarshalTuning.HoldMinFlagOff)
                return null;
            _baseAt = now;
            _baseHeld = heldBlows;
            _baseControl = controlBlows;
            return null;
        }

        if (now - _baseAt < MarshalTuning.HoldProofSeconds)
            return null;

        var held = heldBlows - _baseHeld;
        var control = controlBlows - _baseControl;
        var verdict = new HoldVerdict(MarshalHoldRules.Judge(flagOff, held, control), flagOff, held, control, now - _baseAt);
        if (verdict.Proof == HoldProof.NoControl)
            _baseAt = double.NaN;
        else
            _open = false;
        return verdict;
    }
}

namespace TAOM.Features.MarshalsWord.Domain;

internal enum AttackFlagAction
{
    None,
    Clear,
    Restore,
    Forget,
}

internal enum HoldProof
{
    Pending,
    NoControl,
    Pass,
    Fail,
}

/// <summary>One judged window of the hold-the-line proof: the soldiers whose flag read off, the blows each company dealt in the window, and its length in mission seconds.</summary>
internal readonly record struct HoldVerdict(HoldProof Proof, int FlagOff, int HeldBlows, int ControlBlows, double Seconds);

internal static class MarshalHoldRules
{
    /// <summary>What to do to one soldier's attack flag. Only a flag this feature cleared is ever restored, and not one the engine's own clear has taken since.</summary>
    internal static AttackFlagAction FlagAction(bool holdOn, bool flagSet, bool weCleared, bool engineCleared)
    {
        if (holdOn)
            return flagSet ? AttackFlagAction.Clear : AttackFlagAction.None;
        if (!weCleared)
            return AttackFlagAction.None;
        return flagSet || engineCleared ? AttackFlagAction.Forget : AttackFlagAction.Restore;
    }

    /// <summary>The held company passes when it dealt at most one blow in <c>HoldShareDenominator</c> of the control's, and the control dealt enough to prove the enemy was in reach.</summary>
    internal static HoldProof Judge(int flagOff, int heldBlows, int controlBlows)
    {
        if (flagOff < MarshalTuning.HoldMinFlagOff)
            return HoldProof.Pending;
        if (controlBlows < MarshalTuning.HoldControlMinBlows)
            return HoldProof.NoControl;
        return heldBlows * MarshalTuning.HoldShareDenominator <= controlBlows ? HoldProof.Pass : HoldProof.Fail;
    }
}

/// <summary>
/// The window of one hold-the-line proof. It counts blows from a baseline taken once the settle seconds have passed and enough soldiers read
/// flag off, and it judges once the proof seconds have passed since that baseline. A control too quiet to prove anything takes a new baseline and the window stays open;
/// any other verdict closes it.
/// </summary>
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

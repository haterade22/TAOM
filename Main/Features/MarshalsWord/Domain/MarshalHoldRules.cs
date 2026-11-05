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

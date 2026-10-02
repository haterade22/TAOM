namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>The one reading of a <see cref="SwitchOutcome"/> that the handover's followers share.</summary>
public static class SwitchOutcomeExtensions
{
    /// <summary>The player is now the hero: the handover finished, or failed only after the swap.</summary>
    public static bool TookEffect(this SwitchOutcome outcome)
        => outcome == SwitchOutcome.Switched || outcome == SwitchOutcome.SwitchedWithErrors;
}

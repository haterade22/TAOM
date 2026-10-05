namespace TAOM.Features.SiegeForces;

/// <summary>The MCM "Battle Tactics/Siege Forces" group. The switch gates only the OFFER of the picker, never a state transition.</summary>
public interface ISiegeForcesSettingsProvider
{
    /// <summary>True when the troop selection screen is offered before a wall battle. Read live at each assault.</summary>
    bool PickerEnabled { get; }
}

public sealed class SiegeForcesSettingsProvider : ISiegeForcesSettingsProvider
{
    public bool PickerEnabled => From(TaomSettings.Instance);

    /// <summary>
    /// The setting, or on when MCM has not built its settings yet (its <c>Instance</c> is null until its own provider is
    /// up). The fallback is the compiled default; a test pins both.
    /// </summary>
    public static bool From(TaomSettings? settings) => settings?.EnableSiegeTroopPicker ?? true;
}

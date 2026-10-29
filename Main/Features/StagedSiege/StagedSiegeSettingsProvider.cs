namespace TAOM.Features.StagedSiege;

/// <summary>
/// The MCM "Battle Tactics/Staged Siege" group (docs/features/staged-siege.md). The switch is read ONCE per battle, when the
/// mission behavior decides whether to arm, so a change applies from the next battle and nothing is half-applied mid-battle.
/// No interface: nothing fakes it (ADR-002), and the one consumer is a mission behavior.
/// </summary>
public sealed class StagedSiegeSettingsProvider
{
    public bool IsEnabled => From(TaomSettings.Instance);

    /// <summary>
    /// The setting, or off when MCM has not built its settings yet (its <c>Instance</c> is null until its own provider is up).
    /// The fallback is the compiled default; a test pins both.
    /// </summary>
    public static bool From(TaomSettings? settings) => settings?.EnableStagedSiege ?? false;
}

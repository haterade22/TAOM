namespace TAOM.Features.CreatureSiegeRole;

/// <summary>
/// The MCM "Battle Tactics/Creature Siege Role" group (docs/features/creature-siege-role.md). The switch is read ONCE per
/// battle, when the mission behavior decides whether to act, so a change applies from the next battle and nothing is
/// half-applied mid-battle. No interface: nothing fakes it (ADR-002), and the one consumer is a mission behavior.
/// </summary>
public sealed class CreatureSiegeRoleSettingsProvider
{
    public bool IsEnabled => From(TaomSettings.Instance);

    /// <summary>
    /// The setting, or on when MCM has not built its settings yet (its <c>Instance</c> is null until its own provider is
    /// up). The fallback is the compiled default; a test pins both.
    /// </summary>
    public static bool From(TaomSettings? settings) => settings?.EnableCreatureSiegeRole ?? true;
}

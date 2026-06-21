namespace TAOM.Features.CompanionTactics;

/// <summary>
/// Reads CompanionTactics-related properties off <see cref="TaomSettings"/>. Falls back to
/// the documented defaults when the global instance is unavailable (e.g., headless tests
/// without MCM bootstrapped).
/// </summary>
public sealed class CompanionTacticsSettingsProvider : ICompanionTacticsSettingsProvider
{
    public bool EnableCompanionRoleTooltips => TaomSettings.Instance?.EnableCompanionRoleTooltips ?? true;
    public bool EnableOOBRoleDisplay => TaomSettings.Instance?.EnableOOBRoleDisplay ?? true;
    public bool CompanionRolesDebug => TaomSettings.Instance?.CompanionRolesDebug ?? false;

    public bool EnableFormationPresets => TaomSettings.Instance?.EnableFormationPresets ?? false;
    public int MaxFormationPresets => TaomSettings.Instance?.MaxFormationPresets ?? 10;
    public bool FormationPresetsDebug => TaomSettings.Instance?.FormationPresetsDebug ?? false;

    public bool EnableBattleActionBar => TaomSettings.Instance?.EnableBattleActionBar ?? true;
    public bool CancelStanceOnMove => TaomSettings.Instance?.CancelStanceOnMove ?? true;
    public bool EnableVolleyFire => TaomSettings.Instance?.EnableVolleyFire ?? true;
    public bool BattleActionBarDebug => TaomSettings.Instance?.BattleActionBarDebug ?? false;
}

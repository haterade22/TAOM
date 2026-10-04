namespace TAOM.Features.CompanionTactics;

/// <summary>
/// Reads CompanionTactics-related properties off <see cref="TaomSettings"/>. Falls back to
/// the documented defaults when the global instance is unavailable (e.g., headless tests
/// without MCM bootstrapped).
/// </summary>
public sealed class CompanionTacticsSettingsProvider : ICompanionTacticsSettingsProvider
{
    // Read every frame by BattleActionBarMissionView and OOBOverlayService, and per formation order by
    // Patch35: cached on the first non-null read, read through (BattleBalanceSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CompanionTacticsSettingsProvider() { }
    internal CompanionTacticsSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool EnableCompanionRoleTooltips => Settings?.EnableCompanionRoleTooltips ?? true;
    public bool EnableOOBRoleDisplay => Settings?.EnableOOBRoleDisplay ?? true;
    public bool CompanionRolesDebug => Settings?.CompanionRolesDebug ?? false;

    public bool EnableFormationPresets => Settings?.EnableFormationPresets ?? false;
    public int MaxFormationPresets => Settings?.MaxFormationPresets ?? 10;
    public bool FormationPresetsDebug => Settings?.FormationPresetsDebug ?? false;

    public bool EnableBattleActionBar => Settings?.EnableBattleActionBar ?? true;
    public bool CancelStanceOnMove => Settings?.CancelStanceOnMove ?? true;
    public bool EnableVolleyFire => Settings?.EnableVolleyFire ?? true;
    public bool BattleActionBarDebug => Settings?.BattleActionBarDebug ?? false;
}

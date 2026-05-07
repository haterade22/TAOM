namespace TAOM.Features.CompanionTactics;

/// <summary>
/// Typed read of the CompanionTactics MCM settings. Single seam so services can be
/// tested with NSubstitute without booting MCM. Mirrors the MixedFormations pattern.
/// </summary>
public interface ICompanionTacticsSettingsProvider
{
    bool EnableCompanionRoleTooltips { get; }
    bool EnableOOBRoleDisplay { get; }
    bool CompanionRolesDebug { get; }

    bool EnableFormationPresets { get; }
    int MaxFormationPresets { get; }
    bool FormationPresetsDebug { get; }

    bool EnableBattleActionBar { get; }
    bool CancelStanceOnMove { get; }
    bool EnableVolleyFire { get; }
    bool BattleActionBarDebug { get; }
}

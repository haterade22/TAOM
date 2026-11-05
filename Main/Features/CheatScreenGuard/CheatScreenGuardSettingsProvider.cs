using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>
/// Reads the Battle Load Diagnostics MCM page. The reference is cached on its first non-null read and read THROUGH, never
/// snapshotted: MCM edits its one registered instance in place, so a live edit still applies. Lazy, not in the constructor,
/// so a resolve before MCM is up cannot pin the fallback. Falls back to the shipped default (off) while MCM has no instance.
/// </summary>
public sealed class CheatScreenGuardSettingsProvider : ICheatScreenGuardSettingsProvider
{
    private BattleLoadDiagnosticsSettings? _settings;

    public CheatScreenGuardSettingsProvider()
    {
    }

    internal CheatScreenGuardSettingsProvider(BattleLoadDiagnosticsSettings settings) => _settings = settings;

    private BattleLoadDiagnosticsSettings? Settings => _settings ??= BattleLoadDiagnosticsSettings.Instance;

    public bool CheatListsEnabled => Settings?.CheatModeListsAllItemsAndTroops ?? false;
}

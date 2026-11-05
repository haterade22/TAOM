// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Features.NameplateCull;

/// <summary>
/// Reads the Battle Load Diagnostics MCM page once per frame on the campaign map. HOT PATH: resolving
/// <c>BattleLoadDiagnosticsSettings.Instance</c> walks MCM's settings containers, so the reference is cached on its first
/// non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in place, so a live edit still
/// applies. Lazy, not in the constructor, so a resolve before MCM is up cannot pin the fallback (the same contract as
/// <c>CombatMechanicsSettingsProvider</c>, plan 031). Falls back to the shipped default (on) while MCM has no instance.
/// </summary>
public sealed class NameplateCullSettingsProvider : INameplateCullSettingsProvider
{
    private BattleLoadDiagnosticsSettings? _settings;

    public NameplateCullSettingsProvider()
    {
    }

    internal NameplateCullSettingsProvider(BattleLoadDiagnosticsSettings settings) => _settings = settings;

    private BattleLoadDiagnosticsSettings? Settings => _settings ??= BattleLoadDiagnosticsSettings.Instance;

    public bool CullEnabled => Settings?.CullHiddenNameplates ?? true;
}

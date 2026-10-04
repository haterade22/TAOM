using TAOM.Features.QuickActions.Models;

namespace TAOM.Features.QuickActions;

public sealed class QuickActionsSettingsProvider : IQuickActionsSettingsProvider
{
    // Read on a campaign hot path (per party, per score, per day or every map frame). Resolving
    // TaomSettings.Instance walks MCM's settings containers, so the reference is cached on its first
    // non-null read and read THROUGH, never snapshotted: MCM edits its one registered instance in place
    // (reset and presets copy values into it), so live MCM edits still apply. Lazy, not in the
    // constructor, so a resolve before MCM is up cannot pin the fallbacks. Same contract as
    // BattleBalanceSettingsProvider.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public QuickActionsSettingsProvider() { }
    internal QuickActionsSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool EnableQuickActions => Settings?.EnableQuickActions ?? true;
    public bool EnableInventorySearch => Settings?.EnableInventorySearch ?? true;

    public DamagedQualityPreset DamagedPreset =>
        DamagedQualityPresetExtensions.FromDropdownIndex(
            Settings?.DamagedQualityDropdown?.SelectedIndex ?? 2);

    public float CustomDamagedThreshold => Settings?.DamagedThreshold ?? -0.20f;
    public bool UseCustomThreshold => Settings?.UseCustomThreshold ?? false;
    public bool SellDamagedEquipped => Settings?.SellDamagedEquipped ?? false;
    public bool ExcludeDamagedHorses => Settings?.ExcludeDamagedHorses ?? true;

    public int LowValueThreshold => Settings?.LowValueThreshold ?? 100;
    public bool SellLowValueEquipped => Settings?.SellLowValueEquipped ?? false;
    public bool ExcludeLowValueFood => Settings?.ExcludeLowValueFood ?? true;
    public bool ExcludeLowValueHorses => Settings?.ExcludeLowValueHorses ?? true;
    public bool ExcludeLowValueTradeGoods => Settings?.ExcludeLowValueTradeGoods ?? false;

    public bool ShowConfirmation => Settings?.QuickActionsShowConfirmation ?? true;
    public bool PlaySounds => Settings?.QuickActionsPlaySounds ?? true;
    public bool IsDebugMode => Settings?.QuickActionsDebug ?? false;

    public float ResolveDamagedThreshold() =>
        UseCustomThreshold ? CustomDamagedThreshold : DamagedPreset.ToThreshold();
}

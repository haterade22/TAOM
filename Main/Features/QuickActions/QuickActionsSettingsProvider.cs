using TAOM.Features.QuickActions.Models;

namespace TAOM.Features.QuickActions;

public sealed class QuickActionsSettingsProvider : IQuickActionsSettingsProvider
{
    public bool EnableQuickActions => TaomSettings.Instance?.EnableQuickActions ?? true;
    public bool EnableInventorySearch => TaomSettings.Instance?.EnableInventorySearch ?? true;

    public DamagedQualityPreset DamagedPreset =>
        DamagedQualityPresetExtensions.FromDropdownIndex(
            TaomSettings.Instance?.DamagedQualityDropdown?.SelectedIndex ?? 2);

    public float CustomDamagedThreshold => TaomSettings.Instance?.DamagedThreshold ?? -0.20f;
    public bool UseCustomThreshold => TaomSettings.Instance?.UseCustomThreshold ?? false;
    public bool SellDamagedEquipped => TaomSettings.Instance?.SellDamagedEquipped ?? false;
    public bool ExcludeDamagedHorses => TaomSettings.Instance?.ExcludeDamagedHorses ?? true;

    public int LowValueThreshold => TaomSettings.Instance?.LowValueThreshold ?? 100;
    public bool SellLowValueEquipped => TaomSettings.Instance?.SellLowValueEquipped ?? false;
    public bool ExcludeLowValueFood => TaomSettings.Instance?.ExcludeLowValueFood ?? true;
    public bool ExcludeLowValueHorses => TaomSettings.Instance?.ExcludeLowValueHorses ?? true;
    public bool ExcludeLowValueTradeGoods => TaomSettings.Instance?.ExcludeLowValueTradeGoods ?? false;

    public bool ShowConfirmation => TaomSettings.Instance?.QuickActionsShowConfirmation ?? true;
    public bool PlaySounds => TaomSettings.Instance?.QuickActionsPlaySounds ?? true;
    public bool IsDebugMode => TaomSettings.Instance?.QuickActionsDebug ?? false;

    public float ResolveDamagedThreshold() =>
        UseCustomThreshold ? CustomDamagedThreshold : DamagedPreset.ToThreshold();
}

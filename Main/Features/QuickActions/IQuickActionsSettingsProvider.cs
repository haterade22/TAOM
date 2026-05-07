using TAOM.Features.QuickActions.Models;

namespace TAOM.Features.QuickActions;

public interface IQuickActionsSettingsProvider
{
    bool EnableQuickActions { get; }
    bool EnableInventorySearch { get; }

    DamagedQualityPreset DamagedPreset { get; }
    float CustomDamagedThreshold { get; }
    bool UseCustomThreshold { get; }
    bool SellDamagedEquipped { get; }
    bool ExcludeDamagedHorses { get; }

    int LowValueThreshold { get; }
    bool SellLowValueEquipped { get; }
    bool ExcludeLowValueFood { get; }
    bool ExcludeLowValueHorses { get; }
    bool ExcludeLowValueTradeGoods { get; }

    bool ShowConfirmation { get; }
    bool PlaySounds { get; }
    bool IsDebugMode { get; }

    /// <summary>
    /// Returns the resolved damage threshold (negative float, e.g. -0.20f) honoring
    /// <see cref="UseCustomThreshold"/>. The service compares
    /// <c>(modifier.PriceMultiplier - 1f) &lt;= threshold</c>.
    /// </summary>
    float ResolveDamagedThreshold();
}

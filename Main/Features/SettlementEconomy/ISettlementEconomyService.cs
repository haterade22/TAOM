namespace TAOM.Features.SettlementEconomy;

public interface ISettlementEconomyService
{
    /// <summary>
    /// Pure: the daily market-gold change for a town — <c>round(rate × (base + prosperity ×
    /// perProsperity − currentGold))</c>, the vanilla <c>GetTownGoldChange</c> shape with config
    /// constants. Banker's rounding (parity with TaleWorlds <c>MathF.Round</c>). NaN/Infinity
    /// prosperity is treated as 0; a null config computes with the vanilla constants.
    /// </summary>
    int ComputeTownGoldChange(float prosperity, int currentGold, SettlementEconomyConfig config);
}

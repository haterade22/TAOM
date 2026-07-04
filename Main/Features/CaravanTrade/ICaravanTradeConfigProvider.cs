namespace TAOM.Features.CaravanTrade;

/// <summary>Loads + validates <c>caravan_trade/caravan_trade_config.json</c>. Cached for the process (Reuse.Singleton).</summary>
public interface ICaravanTradeConfigProvider
{
    CaravanTradeConfig GetConfig();
}

namespace TAOM.Adapters;

public interface ISettlementProductionAdapter
{
    string SettlementId { get; }
    string OwnerKingdomId { get; }
    int ProsperityLevel { get; }
    bool IsPlayerOwned { get; }
}

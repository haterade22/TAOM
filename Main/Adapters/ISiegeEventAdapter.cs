namespace TAOM.Adapters;

public interface ISiegeEventAdapter
{
    string SettlementId { get; }
    string SettlementName { get; }
    string DefenderFactionId { get; }
    string AttackerFactionId { get; }
    string AttackerName { get; }
    bool IsTown { get; }
}

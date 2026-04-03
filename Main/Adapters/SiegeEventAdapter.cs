using TaleWorlds.CampaignSystem.Siege;

namespace TAOM.Adapters;

public class SiegeEventAdapter : ISiegeEventAdapter
{
    private readonly SiegeEvent _siege;

    public SiegeEventAdapter(SiegeEvent siege)
    {
        _siege = siege;
    }

    public string SettlementId => _siege.BesiegedSettlement?.StringId ?? "";
    public string SettlementName => _siege.BesiegedSettlement?.Name?.ToString() ?? "";
    public string DefenderFactionId => _siege.BesiegedSettlement?.MapFaction?.StringId ?? "";
    public string AttackerFactionId => _siege.BesiegerCamp?.LeaderParty?.MapFaction?.StringId ?? "";
    public string AttackerName => _siege.BesiegerCamp?.LeaderParty?.Name?.ToString() ?? "";
    public bool IsTown => _siege.BesiegedSettlement?.IsTown ?? false;
}

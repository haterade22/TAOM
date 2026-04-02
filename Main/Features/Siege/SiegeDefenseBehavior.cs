using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Siege;

namespace TAOM.Features.Siege;

public class SiegeDefenseBehavior : CampaignBehaviorBase
{
    private readonly ISiegeDefenseService _service;
    private readonly IModLogger _logger;

    public SiegeDefenseBehavior(ISiegeDefenseService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        _logger.LogInfo("[SiegeDefense] SiegeDefenseBehavior registering events");
        CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeEventStarted);
        CampaignEvents.OnSiegeEventEndedEvent.AddNonSerializedListener(this, OnSiegeEventEnded);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSiegeEventStarted(SiegeEvent siegeEvent)
    {
        var adapter = new SiegeEventAdapter(siegeEvent);
        _service.OnSiegeStarted(adapter);
    }

    private void OnSiegeEventEnded(SiegeEvent siegeEvent)
    {
        _service.OnSiegeEnded(siegeEvent.BesiegedSettlement?.StringId ?? "");
    }

    private void OnSettlementOwnerChanged(
        TaleWorlds.CampaignSystem.Settlements.Settlement settlement,
        bool opened,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        _service.OnSiegeEnded(settlement.StringId);
    }

    private void OnHourlyTick() => _service.OnHourlyTick();
}

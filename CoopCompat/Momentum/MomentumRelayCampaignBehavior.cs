using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace TAOM.CoopCompat.Momentum;

/// <summary>Feeds the campaign's hourly tick to <see cref="MomentumRelayService"/>. Holds no state of its own, so nothing is saved.</summary>
public sealed class MomentumRelayCampaignBehavior : CampaignBehaviorBase
{
    private readonly MomentumRelayService _service;

    public MomentumRelayCampaignBehavior(MomentumRelayService service)
    {
        _service = service;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, _service.OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }
}

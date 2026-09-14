using TaleWorlds.CampaignSystem;

namespace TAOM.Features.MapLoadDiagnostics;

/// <summary>
/// Gives the process-wide diagnostics a per-campaign baseline.
///
/// <para>
/// The heartbeat service is a DryIoc singleton and the tracer is static, so a second campaign
/// started in the same process would otherwise report its first heartbeat against the previous
/// campaign's clock, party count and sequence numbers: exactly the misleading line this feature
/// exists to prevent (csharp-architecture.md, "Singleton Services Holding Per-Campaign State").
/// OnSessionLaunched fires once per new campaign and once per loaded save, so each load starts its
/// own timeline.
/// </para>
/// </summary>
public class MapLoadDiagnosticsBehavior : CampaignBehaviorBase
{
    private readonly IMapLoadHeartbeatService _heartbeat;

    public MapLoadDiagnosticsBehavior(IMapLoadHeartbeatService heartbeat)
    {
        _heartbeat = heartbeat;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // Nothing persists: the timeline is a per-process diagnostic, not campaign state.
    }

    internal void OnSessionLaunched(CampaignGameStarter starter)
    {
        _heartbeat.ResetForNewSession();
        MapLoadTracer.ResetForNewSession();
    }
}

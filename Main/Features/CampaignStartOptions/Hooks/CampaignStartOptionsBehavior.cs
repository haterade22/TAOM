using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.CampaignStartOptions.Hooks;

/// <summary>
/// Thin boundary: once per game session (the module builds one behavior per campaign start), after the session
/// launches, asks <see cref="CampaignStartMismatchNotice"/> whether to show its message. Saves nothing.
/// The body is inside a try: an event invokes its listeners with no try, and a notice must never fault the load.
/// </summary>
public sealed class CampaignStartOptionsBehavior : CampaignBehaviorBase
{
    private readonly CampaignStartMismatchNotice _notice;
    private readonly IModLogger _logger;
    private bool _done;

    public CampaignStartOptionsBehavior(CampaignStartMismatchNotice notice, IModLogger logger)
    {
        _notice = notice;
        _logger = logger;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the event cannot be raised outside a campaign.
    internal void OnSessionLaunched(CampaignGameStarter? starter)
    {
        if (_done) return;
        _done = true;
        try
        {
            _notice.ShowIfDifferent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"CampaignStartOptions: the start-choices notice failed: {ex.Message}");
        }
    }
}

using TaleWorlds.CampaignSystem;

namespace TAOM.Features.NamedCompanions;

public class NamedCompanionBehavior : CampaignBehaviorBase
{
    private readonly INamedCompanionService _service;

    public NamedCompanionBehavior(INamedCompanionService service)
    {
        _service = service;
    }

    public override void RegisterEvents()
    {
        // #127 R1 — clear the singleton `_spawned` flag at the START of new-game creation so
        // campaign 2 in the same Bannerlord process re-runs SpawnCompanions. Per
        // TaleWorlds.CampaignSystem.CampaignEvents.OnNewGameCreated (Campaign.cs:2078), the
        // dispatch order is OnNewGameCreatedEvent FIRST (line 2080) then
        // OnNewGameCreatedPartialFollowUpEvent (line 2083) — so the reset lands before the
        // partial-follow-up calls SpawnCompanions and leaves the latch correctly set.
        //
        // Codex review caught the earlier OnSessionLaunchedEvent subscription — that event fires
        // AFTER OnNewGameCreated for new-game flow, so it would have cleared `_spawned` AFTER
        // the spawn ran (defeating the latch). Loaded games don't dispatch OnNewGameCreatedEvent,
        // but SpawnCompanions is never called on load — only EnsureCompanionsPlaced, which
        // doesn't consult `_spawned`. So the reset doesn't need to run on load.
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(
            this, _ => _service.ResetSession());
        CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(
            this, OnNewGamePartialFollowUp);
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
            this, _ => _service.EnsureCompanionsPlaced());
    }

    public void OnNewGamePartialFollowUp(CampaignGameStarter starter, int index)
    {
        if (index != 1) return;
        _service.SpawnCompanions();
    }

    public override void SyncData(IDataStore dataStore) { }
}

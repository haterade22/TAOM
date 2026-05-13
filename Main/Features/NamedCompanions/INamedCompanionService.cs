namespace TAOM.Features.NamedCompanions;

public interface INamedCompanionService
{
    void SpawnCompanions();
    void EnsureCompanionsPlaced();

    // #127 — `_spawned` is a singleton field. Campaign 2 in the same Bannerlord process would have
    // `_spawned=true` already and SpawnCompanions would early-exit. ResetSession is called by the
    // CampaignBehavior on OnSessionLaunched so a new campaign re-spawns its companions.
    void ResetSession();
}

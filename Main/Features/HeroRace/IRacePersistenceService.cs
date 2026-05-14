using TaleWorlds.CampaignSystem;

namespace TAOM.Features.HeroRace;

public interface IRacePersistenceService
{
    void CaptureHeroRaces();
    void RestoreHeroRaces();
    void SyncRaceData(IDataStore dataStore);
    // Phase 9b #130 R1 — singleton reset on new campaign
    void ResetForNewCampaign();
    // Phase 9b #130 P3 — exposed on interface for testability (was concrete-only)
    int CapturedRaceCount { get; }
}

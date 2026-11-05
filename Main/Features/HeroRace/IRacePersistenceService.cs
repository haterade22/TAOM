using TaleWorlds.CampaignSystem;

namespace TAOM.Features.HeroRace;

public interface IRacePersistenceService
{
    void CaptureHeroRaces();
    // #1011: the OnBeforeSave capture; it also writes the taom.race-persist.capture check line
    void CaptureHeroRacesForSave();
    void RestoreHeroRaces();
    void SyncRaceData(IDataStore dataStore);
    // Phase 9b #130 R1 — singleton reset on new campaign
    void ResetForNewCampaign();
    // #1035: clears every per-campaign field; the behavior's constructor calls it for each campaign start
    void ResetForNewSession();
    // Phase 9b #130 P3 — exposed on interface for testability (was concrete-only)
    int CapturedRaceCount { get; }
}

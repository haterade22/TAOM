using TaleWorlds.CampaignSystem;

namespace TAOM.Features.HeroRace;

public interface IRacePersistenceService
{
    void CaptureHeroRaces();
    void RestoreHeroRaces();
    void SyncRaceData(IDataStore dataStore);
}

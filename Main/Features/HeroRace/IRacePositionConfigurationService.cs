using TAOM.Features.HeroRace.Configuration;

namespace TAOM.Features.HeroRace;

public interface IRacePositionConfigurationService
{
    RacePositionConfig.RacePositionConfigItem GetRaceConfiguration(string raceName);

    RacePositionConfig.RacePositionConfigItem GetMountConfiguration(string raceName);
}

using TAOM.Features.HeroRace.Configuration;
using System.Linq;

namespace TAOM.Features.HeroRace;

public class RacePositionConfigurationService : IRacePositionConfigurationService
{
    private readonly RacePositionConfig _avatarConfig;
    private readonly RacePositionConfig _imageConfig;

    public RacePositionConfigurationService()
    {
        _avatarConfig = RacePositionConfig.LoadConfig("CharacterAvatarPatch");
        _imageConfig = RacePositionConfig.LoadConfig("CharacterImagePatch");
    }

    public RacePositionConfig.RacePositionConfigItem GetRaceConfiguration(string raceName)
    {
        if (string.IsNullOrEmpty(raceName))
        {
            return null;
        }

        var lowerRace = raceName.ToLower();
        return _avatarConfig.Items.FirstOrDefault(item => item.Race == lowerRace)
               ?? _imageConfig.Items.FirstOrDefault(item => item.Race == lowerRace);
    }

    public RacePositionConfig.RacePositionConfigItem GetMountConfiguration(string raceName)
    {
        if (string.IsNullOrEmpty(raceName))
        {
            return null;
        }

        var mountRace = $"mount_{raceName.ToLower()}";
        return _avatarConfig.Items.FirstOrDefault(item => item.Race == mountRace)
               ?? _imageConfig.Items.FirstOrDefault(item => item.Race == mountRace);
    }
}

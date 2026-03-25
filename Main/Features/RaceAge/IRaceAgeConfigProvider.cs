using TAOM.Features.RaceAge.Models;

namespace TAOM.Features.RaceAge;

public interface IRaceAgeConfigProvider
{
    RaceAgeConfig LoadConfig();
}

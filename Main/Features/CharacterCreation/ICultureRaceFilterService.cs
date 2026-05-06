using System.Collections.Generic;

namespace TAOM.Features.CharacterCreation;

public interface ICultureRaceFilterService
{
    bool HasFilter(string cultureId);
    IReadOnlyList<string> GetAllowedRaces(string cultureId);
    bool IsRaceAllowed(string cultureId, string raceName);
}

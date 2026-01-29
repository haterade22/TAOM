using System.Collections.Generic;

namespace TAOM.Core.Domain;

public interface IRaceManager
{
    List<int> GetAllRaceIds();

    List<string> GetAllRaceNames();

    bool IsValidRaceName(string name);

    bool IsValidRaceId(int id);

    int GetRaceIdFromName(string name);

    string GetRaceNameFromId(int id);
}

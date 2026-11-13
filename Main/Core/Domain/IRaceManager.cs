using System.Collections.Generic;

namespace TAOM.Core.Domain;

public interface IRaceManager
{
    bool IsValidRaceName(string name);

    bool IsValidRaceId(int id);

    int GetRaceIdFromName(string name);

    string GetRaceNameFromId(int id);

    /// <summary>
    /// Race names in FaceGen index order (index == race int for THIS process's module set).
    /// Backs the RacePersistence save legend (#330). The
    /// ordering is guaranteed — never rely on Dictionary.Values order for index math.
    /// </summary>
    IReadOnlyList<string> GetOrderedRaceNames();
}

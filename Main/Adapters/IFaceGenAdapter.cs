using TaleWorlds.Core;

namespace TAOM.Adapters;

public interface IFaceGenAdapter
{
    string[] GetRaceNames();
    Monster GetBaseMonsterFromRace(int race);
}

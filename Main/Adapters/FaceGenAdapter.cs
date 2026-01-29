using TaleWorlds.Core;

namespace TAOM.Adapters;

public class FaceGenAdapter : IFaceGenAdapter
{
    public string[] GetRaceNames()
    {
        return FaceGen.GetRaceNames();
    }

    public Monster GetBaseMonsterFromRace(int race)
    {
        return FaceGen.GetBaseMonsterFromRace(race);
    }
}

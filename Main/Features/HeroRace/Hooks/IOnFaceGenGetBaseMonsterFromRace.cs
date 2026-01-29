using TaleWorlds.Core;

namespace TAOM.Features.HeroRace.Hooks;

public interface IOnFaceGenGetBaseMonsterFromRace
{
    void OnGetBaseMonsterFromRace(ref Monster result, int race);
}

using System.Collections.Generic;
using TaleWorlds.Core;

namespace TAOM.Features.CustomBattles.Hooks;

public interface IOnGetCustomBattleFactions
{
    void OnGetCustomBattleFactions(ref IEnumerable<BasicCultureObject> factions);
}

using System.Collections.Generic;
using TaleWorlds.Core;

namespace TAOM.Features.CustomBattles.Hooks;

public interface IOnGetCustomBattleCommanders
{
    void OnGetCustomBattleCommanders(ref IEnumerable<BasicCharacterObject> commanders);
}

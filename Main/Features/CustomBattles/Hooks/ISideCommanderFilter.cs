using System.Collections.Generic;
using TaleWorlds.Core;

namespace TAOM.Features.CustomBattles.Hooks;

public interface ISideCommanderFilter
{
    IReadOnlyList<BasicCharacterObject> ResolveCommandersForCulture(string cultureId);
}

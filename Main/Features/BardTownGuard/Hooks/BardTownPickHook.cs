using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TAOM.Features.BardTownGuard.Hooks;

/// <summary>
/// Stands in for the engine's <c>GetRandomElementWithPredicate</c> at the one call in the musician's condition.
/// Vanilla's pick runs first, so a town with a same-culture neighbour behaves exactly as before. Only on null does the
/// same engine pick run again over every other town, and with no other town the answer is the current settlement, so
/// <c>.Name</c> never reads null.
/// </summary>
public static class BardTownPickHook
{
    public static Settlement PickTown(MBReadOnlyList<Settlement> all, Func<Settlement, bool> predicate)
        => TaleWorlds.Core.Extensions.GetRandomElementWithPredicate(all, predicate)
            ?? TaleWorlds.Core.Extensions.GetRandomElementWithPredicate(all, s => s.IsTown && s != Settlement.CurrentSettlement)
            ?? Settlement.CurrentSettlement;
}

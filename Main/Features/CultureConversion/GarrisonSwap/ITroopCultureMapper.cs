using System.Collections.Generic;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Features.CultureConversion.GarrisonSwap;

/// <summary>
/// Pure, engine-free decision for "which troop of culture X replaces this one". Holds no state and
/// touches no TaleWorlds type, so every rung of the matching ladder is unit-testable against a
/// hand-built <see cref="CultureTroopIndex"/>.
///
/// The engine offers nothing to build on here: a grep of the whole v1.5.3 campaign tree finds no
/// API that maps a troop to its counterpart in another culture, and TAOM ships no garrison party
/// template to copy a composition from. Tier and formation role are the only two signals available,
/// so they are what this matches on.
/// </summary>
public interface ITroopCultureMapper
{
    /// <summary>
    /// Plans a garrison roster's replacement in <paramref name="targetCultureId"/>. Heroes, empty
    /// stacks, troops already of the target culture, and troops whose template carries no culture at
    /// all are left alone. Anything the ladder cannot place lands in
    /// <see cref="TroopSwapPlan.UnmappedTroopIds"/> and keeps its original stack.
    /// </summary>
    /// <param name="settlementId">Seeds the deterministic candidate pick, so one town's choices are stable and reproducible.</param>
    TroopSwapPlan MapGarrison(
        string settlementId,
        string targetCultureId,
        IReadOnlyList<GarrisonTroopInfo> roster,
        CultureTroopIndex targetIndex);

    /// <summary>
    /// Plans a militia roster's replacement. Tries slot-for-slot first (a culture's melee-basic
    /// militia becomes the new culture's melee-basic militia), and only falls back to the garrison
    /// ladder for a stack that is not one of <paramref name="fromMilitia"/>'s four slots.
    /// </summary>
    /// <param name="targetIndex">Optional — only consulted for the fallback. A culture with militia slots but no regular troops still maps.</param>
    TroopSwapPlan MapMilitia(
        string settlementId,
        string targetCultureId,
        IReadOnlyList<GarrisonTroopInfo> roster,
        CultureMilitiaTroops? fromMilitia,
        CultureMilitiaTroops? toMilitia,
        CultureTroopIndex? targetIndex);
}

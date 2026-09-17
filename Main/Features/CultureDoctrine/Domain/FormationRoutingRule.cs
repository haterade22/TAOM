using TaleWorlds.Core;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// The whole of <c>Mission.GetAgentTroopClass</c> (`Mission.cs:2555-2567`) with the doctrine's
/// routing in front of it: a subscriber to <c>GetAgentTroopClass_Override</c> REPLACES the
/// engine's body rather than filtering its result, so the dismount rule for sieges, naval
/// battles and a sally-out's attackers is reproduced here and applied to a routed class too.
/// Pure: the engine's class and flags in, a class out.
/// </summary>
public static class FormationRoutingRule
{
    public static FormationClass Apply(FormationClass engineClass, bool dismount, FormationRouting routing, string? troopId)
    {
        var formationClass = routing.TryRoute(troopId, out var routed) ? routed : engineClass;
        return dismount ? formationClass.DismountedClass() : formationClass;
    }

    /// <summary>Vanilla's dismount condition, from the mission flags.</summary>
    public static bool Dismounts(bool isSiege, bool isNaval, bool isNavalRaid, bool isSallyOut, BattleSideEnum side) =>
        isSiege || isNaval || isNavalRaid || (isSallyOut && side == BattleSideEnum.Attacker);
}

using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CharacterCreation;

internal static class CareerEquipmentRosterIds
{
    public static string Build(string cultureId, CareerArchetype archetype, bool isFemale)
    {
        var archetypeToken = archetype switch
        {
            CareerArchetype.Infantry => "infantry",
            CareerArchetype.Ranged   => "ranged",
            CareerArchetype.Cavalry  => "cavalry",
            _                        => "infantry",
        };
        return $"player_career_{cultureId}_{archetypeToken}_{(isFemale ? "f" : "m")}";
    }
}

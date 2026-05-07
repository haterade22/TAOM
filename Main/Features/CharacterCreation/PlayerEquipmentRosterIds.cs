namespace TAOM.Features.CharacterCreation;

internal static class PlayerEquipmentRosterIds
{
    public static string Build(string cultureId, string titleType, bool isFemale)
    {
        return $"player_char_creation_{cultureId}_{titleType}_{(isFemale ? "f" : "m")}";
    }
}

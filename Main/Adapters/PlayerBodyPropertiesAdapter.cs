using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

public class PlayerBodyPropertiesAdapter : IPlayerBodyPropertiesAdapter
{
    public bool TryApplyFromXml(string bodyPropertiesXml)
    {
        if (string.IsNullOrEmpty(bodyPropertiesXml))
            return false;

        if (!BodyProperties.FromString(bodyPropertiesXml, out var parsed))
            return false;

        var playerChar = CharacterObject.PlayerCharacter;
        if (playerChar == null)
            return false;

        playerChar.UpdatePlayerCharacterBodyProperties(parsed, playerChar.Race, playerChar.IsFemale);

        return true;
    }
}

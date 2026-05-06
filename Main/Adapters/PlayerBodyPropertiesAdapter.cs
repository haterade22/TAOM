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
        var hero = Hero.MainHero;
        if (playerChar == null || hero == null)
            return false;

        // Two-step write. CharacterObject.UpdatePlayerCharacterBodyProperties is gated by
        // `if (IsPlayerCharacter && IsHero)` (verified via ilspycmd on installed v1.3.15
        // TaleWorlds.CampaignSystem.dll). When the guard does not pass during early CC
        // lifecycle, the override no-ops silently — leaving the body unchanged. Always write
        // Hero.MainHero scalars directly so Hero.BodyProperties (computed from those scalars)
        // returns the configured key regardless of guard state. Calling the override second
        // gives us the OnPlayerBodyPropertiesChanged event when the guard does pass.
        hero.StaticBodyProperties = parsed.StaticProperties;
        hero.Weight = parsed.Weight;
        hero.Build = parsed.Build;

        playerChar.UpdatePlayerCharacterBodyProperties(parsed, playerChar.Race, playerChar.IsFemale);

        return true;
    }
}

using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// The culture a character fights for: a hero's own culture, else the character's (#1157).
/// </summary>
/// <remarks>
/// <c>CharacterObject.Culture</c> is a <c>new</c> property that answers with <c>HeroObject.Culture</c> for a hero
/// (v1.5.7 <c>CharacterObject.cs:264-278</c>). Code that holds the character as a <c>BasicCharacterObject</c>, such
/// as an agent's <c>Character</c>, binds to the base property and reads the raw field
/// (<c>BasicCharacterObject.cs:124-134</c>). For the player's hero that field holds <c>main_hero</c>'s ModuleData
/// culture whatever the player picked, because <c>CharacterCreationContent.ApplyCulture</c> sets only
/// <c>Hero.MainHero.Culture</c> and <c>Clan.PlayerClan.Culture</c> (<c>CharacterCreationContent.cs:137-138</c>). A
/// troop, a hero whose <c>Hero.Culture</c> is null and a custom battle character keep the field's answer.
/// <c>CharacterCultureGateTests</c> fails on a read of the base property outside its list of raw readers.
/// </remarks>
public static class CharacterCulture
{
    public static BasicCultureObject? Of(BasicCharacterObject? character) =>
        (character as CharacterObject)?.Culture ?? character?.Culture;

    public static string? IdOf(BasicCharacterObject? character) => Of(character)?.StringId;
}

using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// An agent's <c>Character</c> is a <c>BasicCharacterObject</c>, so its <c>Culture</c> is the raw field, and the
/// player's hero holds <c>main_hero</c>'s ModuleData culture there (#1157). Built the way a save restores them: no
/// constructor runs.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CharacterCultureTests
{
    private static readonly CultureObject ModuleDataCulture = Culture("battania");
    private static readonly CultureObject PickedCulture = Culture("gondor");

    [TestMethod]
    public void IdOf_HeroWhoseFieldHoldsAnotherCulture_ReturnsTheHerosCulture()
    {
        var character = CharacterWith(ModuleDataCulture);
        HeroOf(character, PickedCulture);

        Assert.AreEqual("gondor", CharacterCulture.IdOf(character));
    }

    [TestMethod]
    public void Of_HeroWhoseFieldHoldsAnotherCulture_ReturnsTheHerosCulture()
    {
        var character = CharacterWith(ModuleDataCulture);
        HeroOf(character, PickedCulture);

        Assert.AreSame(PickedCulture, CharacterCulture.Of(character));
    }

    [TestMethod]
    public void IdOf_Troop_ReturnsItsOwnCulture()
    {
        Assert.AreEqual("battania", CharacterCulture.IdOf(CharacterWith(ModuleDataCulture)));
    }

    [TestMethod]
    public void IdOf_HeroWithoutACulture_ReturnsTheCharactersCulture()
    {
        var character = CharacterWith(ModuleDataCulture);
        HeroOf(character, null);

        Assert.AreEqual("battania", CharacterCulture.IdOf(character));
    }

    [TestMethod]
    public void IdOf_CustomBattleCharacter_ReturnsItsCulture()
    {
        var character = Bare<BasicCharacterObject>();
        character.Culture = ModuleDataCulture;

        Assert.AreEqual("battania", CharacterCulture.IdOf(character));
    }

    [TestMethod]
    public void IdOf_NoCharacter_ReturnsNull()
    {
        Assert.IsNull(CharacterCulture.IdOf(null));
    }

    private static CharacterObject CharacterWith(CultureObject culture)
    {
        var character = Bare<CharacterObject>();
        ((BasicCharacterObject)character).Culture = culture;
        return character;
    }

    private static void HeroOf(CharacterObject character, CultureObject? culture)
    {
        var hero = Bare<Hero>();
        hero.Culture = culture!;
        AccessTools.Field(typeof(Hero), "_characterObject").SetValue(hero, character);
        AccessTools.Field(typeof(CharacterObject), "_heroObject").SetValue(character, hero);
    }

    private static CultureObject Culture(string id)
    {
        var culture = Bare<CultureObject>();
        culture.StringId = id;
        return culture;
    }

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
}

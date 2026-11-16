using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HeroAbilities;

// TextObject needs the engine's text manager to resolve, so these pin the source: every shipped ability has a
// lowercase taom_ key row, and the cast line has an ally and an enemy key.
[TestClass]
public class HeroAbilityNamesTests
{
    private static string Source => RepoPaths.ReadSource("Main/Features/HeroAbilities/HeroAbilityNames.cs", stripComments: true);

    [DataTestMethod]
    [DataRow("aragorn_for_frodo", "For Frodo!")]
    [DataRow("aragorn_isildurs_heir", "Heir of Isildur")]
    [DataRow("gimli_baruk_khazad", "Baruk Khazâd!")]
    [DataRow("gimli_still_standing", "Still Standing")]
    [DataRow("legolas_rapid_fire", "Rapid Fire")]
    [DataRow("legolas_many_arrows", "Many Arrows")]
    [DataRow("legolas_archers_eye", "Archer's Eye")]
    public void Name_EveryShippedAbility_HasItsOwnLowercaseKeyRow(string id, string english) =>
        StringAssert.Contains(Source, $"\"{id}\" => new TextObject(\"{{=taom_hero_ability_{id}}}{english}\")");

    [TestMethod]
    public void Cast_HasAnAllyAndAnEnemyKey()
    {
        StringAssert.Contains(Source, "{=taom_hero_ability_cast_ally}{HERO}: {ABILITY}");
        StringAssert.Contains(Source, "{=taom_hero_ability_cast_enemy}Enemy {HERO}: {ABILITY}");
        Assert.IsFalse(Regex.IsMatch(Source, "\\{=[^}]*[A-Z][^}]*\\}"), "every key is lowercase");
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class CharacterSpawnerActionSetTests
{
    // The arena stand crowd is built through CharacterSpawnerService; for custom races it must
    // resolve "as_<race>_<suffix>" (race skeleton) instead of the engine's human fallback, or the
    // dwarf-rigged clothing renders invisible. These pin the race-prefixed name builder.
    [DataTestMethod]
    [DataRow("dwarf", false, "_villager", "as_dwarf_villager", "as_dwarf_warrior")]
    [DataRow("dwarf", true, "_villager", "as_dwarf_female_villager", "as_dwarf_female_warrior")]
    [DataRow("dwarf", false, "_warrior", "as_dwarf_warrior", "as_dwarf_warrior")]
    [DataRow("dwarf", false, "", "as_dwarf", "as_dwarf_warrior")]
    [DataRow("uruk", true, "_warrior", "as_uruk_female_warrior", "as_uruk_female_warrior")]
    [DataRow("orc", false, "_villager_2", "as_orc_villager_2", "as_orc_warrior")]
    public void BuildRaceActionSetNames_BuildsRacePrefixedNamesAndWarriorFallback(
        string race, bool female, string suffix, string expectedPrimary, string expectedFallback)
    {
        var (primary, fallback) = CharacterSpawnerService.BuildRaceActionSetNames(race, female, suffix);

        Assert.AreEqual(expectedPrimary, primary, "primary action-set name");
        Assert.AreEqual(expectedFallback, fallback, "fallback action-set name");
    }

    [TestMethod]
    public void BuildRaceActionSetNames_NullSuffix_TreatedAsEmpty()
    {
        var (primary, fallback) = CharacterSpawnerService.BuildRaceActionSetNames("dwarf", false, null);

        Assert.AreEqual("as_dwarf", primary);
        Assert.AreEqual("as_dwarf_warrior", fallback);
    }
}

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using TAOM.Features.BattleBalance;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.BattleBalance;

/// <summary>
/// Pins the battle_balance_config.json we ship. The provider deserializes with Json.NET's default
/// merge, so every key the file omits silently keeps its compiled value; these tests read the file
/// with <see cref="ObjectCreationHandling.Replace"/> so they see only the rows the file carries.
/// </summary>
[TestClass]
public class ShippedBattleBalanceConfigTests
{
    private static BattleBalanceConfig LoadShippedFileOnly()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "configs", "battle_balance_config.json");
        var settings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
        var config = JsonConvert.DeserializeObject<BattleBalanceConfig>(File.ReadAllText(path), settings);
        Assert.IsNotNull(config, "battle_balance_config.json did not deserialize");
        return config;
    }

    /// <summary>
    /// Lindon's troop tree is a clone of Rivendell's, so its parties survive like Rivendell's.
    /// </summary>
    [TestMethod]
    public void ShippedConfig_Lindon_MatchesRivendellSurvivalBonus()
    {
        var bonuses = LoadShippedFileOnly().CasualtyRatios.CulturalSurvivalBonuses;

        Assert.IsTrue(bonuses.TryGetValue("lindon", out var lindon), "battle_balance_config.json has no lindon row");
        Assert.AreEqual(bonuses["rivendell"], lindon, 0.001f);
    }

    /// <summary>
    /// Arthedain's realm is cloned from Gondor's, so its parties survive like Gondor's.
    /// </summary>
    [TestMethod]
    public void ShippedConfig_Arthedain_MatchesGondorSurvivalBonus()
    {
        var bonuses = LoadShippedFileOnly().CasualtyRatios.CulturalSurvivalBonuses;

        Assert.IsTrue(bonuses.TryGetValue("arthedain", out var arthedain), "battle_balance_config.json has no arthedain row");
        Assert.AreEqual(bonuses["gondor"], arthedain, 0.001f);
    }

    /// <summary>
    /// Mirkwood is an elf realm like Rivendell and Lindon; it had no row at all until 2026-11-14 (#1283).
    /// </summary>
    [TestMethod]
    public void ShippedConfig_Mirkwood_MatchesRivendellSurvivalBonus()
    {
        var bonuses = LoadShippedFileOnly().CasualtyRatios.CulturalSurvivalBonuses;

        Assert.IsTrue(bonuses.TryGetValue("mirkwood", out var mirkwood), "battle_balance_config.json has no mirkwood row");
        Assert.AreEqual(bonuses["rivendell"], mirkwood, 0.001f);
    }

    /// <summary>
    /// Mordor carries no survival penalty (#1283). The -0.2 it shipped with multiplied the death chance of a
    /// wounded regular by 1.2. With no Medicine bonus on either side (no surgeon skill in the party, no
    /// Doctor's Oath in the enemy's) that chance is 1 / (1 + level x 0.02), so a troop of level 10 or below
    /// then never survived a cut or pierce blow.
    /// </summary>
    [TestMethod]
    public void ShippedConfig_Mordor_HasNoSurvivalPenalty()
    {
        var bonuses = LoadShippedFileOnly().CasualtyRatios.CulturalSurvivalBonuses;

        Assert.IsFalse(bonuses.TryGetValue("mordor", out var mordor) && mordor < 0f,
            $"battle_balance_config.json gives mordor a survival penalty again ({mordor})");
    }

    /// <summary>
    /// The table is keyed on culture StringIds, and a key no culture carries is silent: the lookup finds
    /// no row and returns 0, so that realm's parties keep the engine's roll.
    /// </summary>
    [TestMethod]
    public void ShippedConfig_EverySurvivalBonusKey_IsAMainCultureId()
    {
        var cultures = CultureDataFixture.MainCultureIds();

        var unknown = LoadShippedFileOnly().CasualtyRatios.CulturalSurvivalBonuses.Keys
            .Where(key => !cultures.Contains(key)).OrderBy(key => key).ToList();

        Assert.AreEqual(0, unknown.Count, $"survival bonus keys that are no playable culture: {string.Join(", ", unknown)}");
    }

    /// <summary>
    /// The compiled table is what a missing or unreadable file falls back to, so it carries every
    /// culture row the shipped file does (the #749 fix mirrored Lindon by hand; nothing checked it).
    /// </summary>
    [TestMethod]
    public void CompiledDefaults_SurvivalBonuses_MatchTheShippedFile()
    {
        var shipped = LoadShippedFileOnly().CasualtyRatios.CulturalSurvivalBonuses;
        var compiled = new BattleBalanceConfig().CasualtyRatios.CulturalSurvivalBonuses;

        CollectionAssert.AreEquivalent(shipped.Keys.ToList(), compiled.Keys.ToList());
        foreach (var pair in shipped)
            Assert.AreEqual(pair.Value, compiled[pair.Key], 0.001f, pair.Key);
    }
}

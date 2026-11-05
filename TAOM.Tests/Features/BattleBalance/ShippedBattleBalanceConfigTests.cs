using System.IO;
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
}

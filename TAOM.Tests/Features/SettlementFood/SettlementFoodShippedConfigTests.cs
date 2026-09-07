using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SettlementFood;

namespace TAOM.Tests.Features.SettlementFood;

/// <summary>
/// Pins the SHIPPED settlement_food_config.json.
///
/// Why this suite exists: the tuning in that file is the entire fix for chronic town starvation, and
/// the one way to break it silently is to raise <c>hinterlandFoodPerProsperity</c> to or past
/// <c>1 / prosperityFoodDivisor</c>. At that point net food stops falling as prosperity rises, every
/// surplus town overflows its store forever, vanilla converts the overflow into prosperity, and
/// prosperity / town gold / garrison caps inflate map-wide. The provider reverts such a value at
/// runtime with a warning, but a warning in a log nobody reads would leave the shipped mod quietly
/// running vanilla-with-extra-steps. This suite fails the build instead.
/// </summary>
[TestClass]
public class SettlementFoodShippedConfigTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ShippedConfigPath() =>
        Path.Combine(FindRepoRoot(), "Main", "_Module", "ModuleData", "settlement_food", "settlement_food_config.json");

    private static SettlementFoodConfig LoadShipped()
    {
        var path = ShippedConfigPath();
        Assert.IsTrue(File.Exists(path), $"settlement_food_config.json not found at {path}");
        var config = JsonConvert.DeserializeObject<SettlementFoodConfig>(File.ReadAllText(path));
        Assert.IsNotNull(config, "settlement_food_config.json did not deserialize");
        return config;
    }

    [TestMethod]
    public void ShippedConfig_HinterlandRate_IsStrictlyBelowInverseProsperityDivisor()
    {
        var c = LoadShipped();

        var bound = 1f / c.ProsperityFoodDivisor;
        Assert.IsTrue(
            c.HinterlandFoodPerProsperity < bound,
            $"hinterlandFoodPerProsperity={c.HinterlandFoodPerProsperity} must be strictly below " +
            $"1/prosperityFoodDivisor={bound} (divisor {c.ProsperityFoodDivisor}). At or above it the " +
            "food balance stops responding to prosperity and prosperity inflates without limit.");
    }

    [TestMethod]
    public void ShippedConfig_SurvivesItsOwnValidatorUnchanged()
    {
        // The strongest form of the gate: feed the real file through the real provider and assert
        // nothing was reverted. Catches any shipped value that trips validation, not just the ratio.
        var tempDir = Path.Combine(Path.GetTempPath(), "TAOM_SettlementFoodShipped_" + Path.GetRandomFileName());
        var configDir = Path.Combine(tempDir, "settlement_food");
        Directory.CreateDirectory(configDir);
        try
        {
            File.Copy(ShippedConfigPath(), Path.Combine(configDir, "settlement_food_config.json"));

            var pathService = Substitute.For<IPathService>();
            pathService.ModuleDataPath.Returns(tempDir);
            var logger = Substitute.For<IModLogger>();

            var loaded = new SettlementFoodConfigProvider(pathService, logger).GetConfig();
            var onDisk = LoadShipped();

            logger.DidNotReceive().LogWarning(Arg.Any<string>());
            Assert.AreEqual(onDisk.GarrisonFoodDivisor, loaded.GarrisonFoodDivisor);
            Assert.AreEqual(onDisk.ProsperityFoodDivisor, loaded.ProsperityFoodDivisor);
            Assert.AreEqual(onDisk.TownBaseFood, loaded.TownBaseFood, 0.001f);
            Assert.AreEqual(onDisk.CastleBaseFood, loaded.CastleBaseFood, 0.001f);
            Assert.AreEqual(onDisk.VillageFoodMultiplier, loaded.VillageFoodMultiplier, 0.001f);
            Assert.AreEqual(onDisk.FlatFoodBonus, loaded.FlatFoodBonus, 0.001f);
            Assert.AreEqual(onDisk.HinterlandFoodPerProsperity, loaded.HinterlandFoodPerProsperity, 0.000001f);
            Assert.AreEqual(onDisk.FoodStocksUpperLimit, loaded.FoodStocksUpperLimit);
            Assert.AreEqual(onDisk.CastleFoodStockUpperLimitBonus, loaded.CastleFoodStockUpperLimitBonus);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [TestMethod]
    public void ShippedConfig_ClearsTheWorstTownOnTheMap()
    {
        // town_EW10 is the worst case in TAOM_Map: prosperity 3500 and ZERO bound villages, so it has
        // no village production at all. If the shipped tuning cannot carry that town to a positive
        // balance, it cannot carry the map. Garrison is excluded here (it is not map data); the
        // margin this leaves is what absorbs it.
        var c = LoadShipped();

        const float prosperity = 3500f;
        var production = c.TownBaseFood + prosperity * c.HinterlandFoodPerProsperity + c.FlatFoodBonus;
        var consumption = prosperity / c.ProsperityFoodDivisor;

        Assert.IsTrue(
            production - consumption > 0f,
            $"worst-case town (prosperity {prosperity}, no villages) nets " +
            $"{production - consumption:F1}/day, which must be positive before garrison");
    }

    [TestMethod]
    public void ShippedConfig_NetFoodStillFallsAsProsperityRises()
    {
        // The self-limiter must survive: a fief that grows must get food-tighter, not looser, or
        // there is no ceiling on growth at all. This is the behavioural form of the ratio invariant.
        var c = LoadShipped();

        float Net(float prosperity) =>
            c.TownBaseFood + prosperity * c.HinterlandFoodPerProsperity + c.FlatFoodBonus
            - prosperity / c.ProsperityFoodDivisor;

        Assert.IsTrue(Net(12000f) < Net(4000f),
            $"net at prosperity 12000 ({Net(12000f):F1}) must be below net at 4000 ({Net(4000f):F1})");
    }
}

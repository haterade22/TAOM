using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroAbilities;
using TAOM.Features.HeroAbilities.Domain;

namespace TAOM.Tests.Features.HeroAbilities;

// The ranged block and the two ability fields that ride on it (#1361).
[TestClass]
public class HeroAbilitiesRangedConfigTests
{
    private const string Trigger = "{\"kind\":\"EnemiesWithin\",\"range\":10,\"count\":3}";
    private const string GoodRanged =
        "\"ranged\":{\"zeroAimError\":true,\"pierceShields\":true,\"headshotKillChance\":1.0,\"bodyKillChance\":0.1,"
        + "\"extraArrowItem\":\"taom_legolas_arrow\",\"extraArrowSpreadMeters\":10,\"extraArrowRangeMeters\":120}";

    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private IPathService _paths = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_HeroRanged_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "hero_abilities"));
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private HeroKitConfig LoadKit(string kitExtra, string abilityExtra = "")
    {
        var ability = "{\"id\":\"a_one\",\"cooldownSeconds\":90,\"durationSeconds\":12,\"anyOf\":[" + Trigger + "]"
            + (abilityExtra.Length > 0 ? "," + abilityExtra : "") + "}";
        var kit = "{\"characterId\":\"hero_x\"" + (kitExtra.Length > 0 ? "," + kitExtra : "") + ",\"abilities\":[" + ability + "]}";
        File.WriteAllText(Path.Combine(_tempDir, "hero_abilities", "hero_abilities.json"), "{\"heroes\":[" + kit + "]}");
        return new HeroAbilitiesConfigProvider(_paths, _logger).GetConfig().Heroes.Single();
    }

    private static string Ranged(string members) => "\"ranged\":{" + members + "}";

    private void AssertWarned(string text) =>
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(text)));

    [TestMethod]
    public void Load_RangedBlock_KeepsEveryField()
    {
        var kit = LoadKit(GoodRanged, "\"extraArrows\":2");

        var ranged = kit.Ranged!;
        Assert.IsTrue(ranged.ZeroAimError);
        Assert.IsTrue(ranged.PierceShields);
        Assert.AreEqual(1f, ranged.HeadshotKillChance);
        Assert.AreEqual(0.1f, ranged.BodyKillChance);
        Assert.AreEqual("taom_legolas_arrow", ranged.ExtraArrowItem);
        Assert.AreEqual(10f, ranged.ExtraArrowSpreadMeters);
        Assert.AreEqual(120f, ranged.ExtraArrowRangeMeters);
        Assert.AreEqual(2, kit.Abilities.Single().ExtraArrows);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Load_NoRangedBlock_RangedIsNull() => Assert.IsNull(LoadKit("").Ranged);

    [DataTestMethod]
    [DataRow("\"headshotKillChance\":1.5", "headshotKillChance")]
    [DataRow("\"headshotKillChance\":-0.1", "headshotKillChance")]
    [DataRow("\"headshotKillChance\":NaN", "headshotKillChance")]
    [DataRow("\"bodyKillChance\":2", "bodyKillChance")]
    [DataRow("\"bodyKillChance\":NaN", "bodyKillChance")]
    public void Load_KillChanceOutside0To1OrNaN_ZeroedWithWarning(string members, string name)
    {
        var ranged = LoadKit(Ranged(members)).Ranged!;

        Assert.AreEqual(0f, ranged.HeadshotKillChance);
        Assert.AreEqual(0f, ranged.BodyKillChance);
        AssertWarned(name);
    }

    [DataTestMethod]
    [DataRow("21")]
    [DataRow("-1")]
    [DataRow("NaN")]
    public void Load_SpreadOutside0To20_ZeroedWithWarning(string value)
    {
        var ranged = LoadKit(Ranged("\"extraArrowSpreadMeters\":" + value)).Ranged!;

        Assert.AreEqual(0f, ranged.ExtraArrowSpreadMeters);
        AssertWarned("extraArrowSpreadMeters");
    }

    [DataTestMethod]
    [DataRow("151")]
    [DataRow("-1")]
    [DataRow("NaN")]
    public void Load_RangeOutside0To150_ZeroedWithWarning(string value)
    {
        var ranged = LoadKit(Ranged("\"extraArrowRangeMeters\":" + value)).Ranged!;

        Assert.AreEqual(0f, ranged.ExtraArrowRangeMeters);
        AssertWarned("extraArrowRangeMeters");
    }

    [DataTestMethod]
    [DataRow("Taom_Arrow")]
    [DataRow("has space")]
    [DataRow("a-b")]
    public void Load_ExtraArrowItemBadShape_Cleared(string item)
    {
        var ranged = LoadKit(Ranged("\"extraArrowItem\":\"" + item + "\"")).Ranged!;

        Assert.AreEqual("", ranged.ExtraArrowItem);
        AssertWarned("extraArrowItem");
    }

    [DataTestMethod]
    [DataRow("4")]
    [DataRow("-1")]
    public void Load_ExtraArrowsOutside0To3_Zeroed(string value)
    {
        var ability = LoadKit(GoodRanged, "\"extraArrows\":" + value).Abilities.Single();

        Assert.AreEqual(0, ability.ExtraArrows);
        AssertWarned("extraArrows");
    }

    [TestMethod]
    public void Load_ExtraArrowsWithoutUsableRangedBlock_Zeroed()
    {
        Assert.AreEqual(0, LoadKit("", "\"extraArrows\":2").Abilities.Single().ExtraArrows, "no ranged block");
        AssertWarned("extraArrows");

        var noItem = Ranged("\"extraArrowSpreadMeters\":10,\"extraArrowRangeMeters\":120");
        Assert.AreEqual(0, LoadKit(noItem, "\"extraArrows\":2").Abilities.Single().ExtraArrows, "no item");

        var noSpread = Ranged("\"extraArrowItem\":\"a_b\",\"extraArrowRangeMeters\":120");
        Assert.AreEqual(0, LoadKit(noSpread, "\"extraArrows\":2").Abilities.Single().ExtraArrows, "no spread");

        var noRange = Ranged("\"extraArrowItem\":\"a_b\",\"extraArrowSpreadMeters\":10");
        Assert.AreEqual(0, LoadKit(noRange, "\"extraArrows\":2").Abilities.Single().ExtraArrows, "no range");
    }

    [TestMethod]
    public void Load_AlliesRangedOnly_Kept()
    {
        Assert.IsTrue(LoadKit("", "\"alliesRangedOnly\":true").Abilities.Single().AlliesRangedOnly);
        Assert.IsFalse(LoadKit("").Abilities.Single().AlliesRangedOnly);
    }
}

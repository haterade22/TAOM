using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroAuras;

namespace TAOM.Tests.Features.HeroAuras;

[TestClass]
public class HeroAurasConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private HeroAurasConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_HeroAuras_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "hero_auras");
        Directory.CreateDirectory(_featureDir);
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new HeroAurasConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) => File.WriteAllText(Path.Combine(_featureDir, "hero_auras.json"), json);

    private static string OneAura(string effects) =>
        "{ \"auras\": [ { \"id\": \"dain\", \"heroIds\": [\"lord_E1_1\"], \"effects\": " + effects + " } ] }";

    [TestMethod]
    public void GetConfig_MissingFile_DefaultsWithNoAurasAndWarns()
    {
        var config = _sut.GetConfig();

        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(0, config.Auras.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetConfig_MalformedJson_DefaultsAndLogsAnError()
    {
        WriteConfig("{ not json");

        Assert.AreEqual(0, _sut.GetConfig().Auras.Count);
        _logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_ValidAura_Loads()
    {
        WriteConfig(OneAura("{ \"knockdownResistancePercent\": 30 }"));

        var aura = _sut.GetConfig().Auras[0];

        Assert.AreEqual("dain", aura.Id);
        Assert.AreEqual(30f, aura.Effects.KnockdownResistancePercent, 0.001f);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetConfig_RepeatedAuraId_SkipsTheLaterAndWarns()
    {
        // The aura key is built from ids, so two auras with one id would share a key and keep stale effects.
        WriteConfig("{ \"auras\": [ { \"id\": \"dain\", \"heroIds\": [\"lord_E1_1\"], \"effects\": {} }, " +
                    "{ \"id\": \"DAIN\", \"heroIds\": [\"lord_R1_1\"], \"effects\": {} } ] }");

        var auras = _sut.GetConfig().Auras;

        Assert.AreEqual(1, auras.Count);
        Assert.AreEqual("lord_E1_1", auras[0].HeroIds[0]);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("DAIN")));
    }

    [TestMethod]
    public void GetConfig_AuraIdWithAKeySeparator_SkipsAndWarns()
    {
        WriteConfig("{ \"auras\": [ { \"id\": \"dain+elrond\", \"heroIds\": [\"lord_E1_1\"], \"effects\": {} } ] }");

        Assert.AreEqual(0, _sut.GetConfig().Auras.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain+elrond")));
    }

    [TestMethod]
    public void GetConfig_ZeroLinger_RevertsAndWarns()
    {
        // The pulse expires in the same tick it stamps, so a linger of 0 would remove every stamp at once.
        WriteConfig("{ \"lingerSeconds\": 0, \"auras\": [] }");

        Assert.AreEqual(3f, _sut.GetConfig().LingerSeconds, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("lingerSeconds")));
    }

    [TestMethod]
    public void GetConfig_RadiusAboveTheEngineCeiling_RevertsAndWarns()
    {
        WriteConfig("{ \"radius\": 40, \"auras\": [] }");

        Assert.AreEqual(15f, _sut.GetConfig().Radius, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("radius")));
    }

    [TestMethod]
    public void GetConfig_AuraRadiusAboveTheCeiling_FallsBackToTheDefault()
    {
        WriteConfig("{ \"auras\": [ { \"id\": \"dain\", \"heroIds\": [\"lord_E1_1\"], \"radius\": 30, \"effects\": {} } ] }");

        Assert.IsNull(_sut.GetConfig().Auras[0].Radius);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain")));
    }

    [TestMethod]
    public void GetConfig_AuraWithoutHeroes_IsSkipped()
    {
        WriteConfig("{ \"auras\": [ { \"id\": \"nobody\", \"heroIds\": [], \"effects\": {} } ] }");

        Assert.AreEqual(0, _sut.GetConfig().Auras.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("nobody")));
    }

    [TestMethod]
    public void GetConfig_HeroInTwoAuras_SecondIsDropped()
    {
        WriteConfig("{ \"auras\": [ { \"id\": \"a\", \"heroIds\": [\"lord_E1_1\"], \"effects\": {} }, " +
                    "{ \"id\": \"b\", \"heroIds\": [\"lord_E1_1\", \"lord_R1_1\"], \"effects\": {} } ] }");

        var auras = _sut.GetConfig().Auras;

        CollectionAssert.AreEqual(new[] { "lord_R1_1" }, auras[1].HeroIds);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("lord_E1_1")));
    }

    [TestMethod]
    public void GetConfig_HarmfulValue_IsZeroedWithAWarning()
    {
        // An aura only strengthens: a negative bonus, or a positive aim error, is an authoring slip.
        WriteConfig(OneAura("{ \"blockAbilityPercent\": -20, \"aimErrorPercent\": 10, \"meleeDamagePercent\": 5 }"));

        var effects = _sut.GetConfig().Auras[0].Effects;

        Assert.AreEqual(0f, effects.BlockAbilityPercent, 0.001f);
        Assert.AreEqual(0f, effects.AimErrorPercent, 0.001f);
        Assert.AreEqual(5f, effects.MeleeDamagePercent, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("blockAbilityPercent")));
    }

    [TestMethod]
    public void GetConfig_AbilityOnlyField_IsZeroedWithAWarning()
    {
        WriteConfig(OneAura("{ \"healPerKill\": 10, \"moraleOnEnd\": 5 }"));

        var effects = _sut.GetConfig().Auras[0].Effects;

        Assert.AreEqual(0f, effects.HealPerKill, 0.001f);
        Assert.AreEqual(0f, effects.MoraleOnEnd, 0.001f);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("healPerKill")));
    }

    [TestMethod]
    public void GetConfig_MoraleFloorAbove50_IsCapped()
    {
        WriteConfig(OneAura("{ \"moraleFloor\": 80 }"));

        Assert.AreEqual(50f, _sut.GetConfig().Auras[0].Effects.MoraleFloor, 0.001f);
    }
}

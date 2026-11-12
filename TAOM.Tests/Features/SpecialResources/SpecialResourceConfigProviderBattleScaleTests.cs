using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The optional battle_reference_size, battle_min_scale and battle_max_scale attributes of a
/// <c>&lt;Resource&gt;</c>: defaults when absent, and a fallback with one warning when the set is invalid.
/// </summary>
[TestClass]
public class SpecialResourceConfigProviderBattleScaleTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_battle_scale_tests_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "special_resources"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private SpecialResource Load(string extraAttributes)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "special_resources", "special_resources_config.xml"),
            $@"<?xml version=""1.0"" encoding=""utf-8""?>
<SpecialResources>
  <Resource id=""gems"" display_name=""Gems"" icon_sprite=""i"" cap=""600"" per_battle_victory_base=""50"" {extraAttributes}>
    <Kingdom id=""erebor"" />
  </Resource>
</SpecialResources>");
        return new SpecialResourceConfigProvider(_pathService, _logger).GetByKingdomId("erebor");
    }

    [TestMethod]
    public void Load_NoBattleScaleAttributes_UsesTheDefaults()
    {
        var resource = Load("");

        Assert.AreEqual(SpecialResource.DefaultBattleReferenceSize, resource.BattleReferenceSize);
        Assert.AreEqual(SpecialResource.DefaultBattleMinScale, resource.BattleMinScale);
        Assert.AreEqual(SpecialResource.DefaultBattleMaxScale, resource.BattleMaxScale);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("battle scale")));
    }

    [TestMethod]
    public void Load_ValidBattleScale_IsUsed()
    {
        var resource = Load(@"battle_reference_size=""40"" battle_min_scale=""0.1"" battle_max_scale=""5""");

        Assert.AreEqual(40f, resource.BattleReferenceSize);
        Assert.AreEqual(0.1f, resource.BattleMinScale);
        Assert.AreEqual(5f, resource.BattleMaxScale);
    }

    [DataTestMethod]
    [DataRow(@"battle_reference_size=""0""")]
    [DataRow(@"battle_reference_size=""-10""")]
    [DataRow(@"battle_min_scale=""-0.5""")]
    [DataRow(@"battle_min_scale=""4"" battle_max_scale=""2""")]
    public void Load_InvalidBattleScale_FallsBackToDefaultsAndWarnsOnce(string attributes)
    {
        var resource = Load(attributes);

        Assert.AreEqual(SpecialResource.DefaultBattleReferenceSize, resource.BattleReferenceSize);
        Assert.AreEqual(SpecialResource.DefaultBattleMinScale, resource.BattleMinScale);
        Assert.AreEqual(SpecialResource.DefaultBattleMaxScale, resource.BattleMaxScale);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("battle scale")));
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    public void Load_NonFiniteReference_FallsBackToTheDefault(string value)
    {
        var resource = Load($@"battle_reference_size=""{value}""");

        Assert.AreEqual(SpecialResource.DefaultBattleReferenceSize, resource.BattleReferenceSize);
    }
}

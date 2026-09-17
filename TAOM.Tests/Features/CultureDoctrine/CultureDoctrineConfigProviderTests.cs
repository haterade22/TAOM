using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CultureDoctrine;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The
/// provider is fail-soft: a bad row reverts or drops with a warning and the battle still starts.
/// The values that matter are the ones an author types: a multiplier of 20 meaning "twice", a
/// tactic spelled with the engine's <c>Tactic</c> prefix, a side called "defence", a NaN.
/// </summary>
[TestClass]
public class CultureDoctrineConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private CultureDoctrineConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CultureDoctrine_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, "culture_doctrine");
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CultureDoctrineConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteConfig(string json) =>
        File.WriteAllText(Path.Combine(_featureDir, "culture_doctrines.json"), json);

    private const string ValidDefault = @"""default"": { ""tactics"": [ { ""id"": ""Charge"" }, { ""id"": ""FullScaleAttack"", ""minTactics"": 20 } ] }";

    private void WriteConfigWithCulture(string cultureJson) =>
        WriteConfig("{ \"enabled\": true, \"doctrines\": { " + ValidDefault + ", " + cultureJson + " } }");

    [TestMethod]
    public void GetCatalog_FileMissing_ReturnsVanillaEquivalentAndWarns()
    {
        var catalog = _sut.GetCatalog();

        Assert.IsTrue(catalog.Enabled);
        Assert.AreEqual(0, catalog.CultureIds.Count);
        Assert.IsTrue(catalog.Default.IsDefault);
        CollectionAssert.AreEqual(
            DoctrineCatalog.VanillaEquivalent().Default.Tactics.Select(t => t.Tactic).ToArray(),
            catalog.Default.Tactics.Select(t => t.Tactic).ToArray());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetCatalog_InvalidJson_ReturnsVanillaEquivalentAndLogsError()
    {
        WriteConfig("{ this is not json");

        var catalog = _sut.GetCatalog();

        Assert.AreEqual(0, catalog.CultureIds.Count);
        Assert.IsTrue(catalog.Default.Tactics.Count > 0);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetCatalog_ValidFile_ResolvesCultureAndFallsBackToDefault()
    {
        WriteConfigWithCulture(@"""erebor"": { ""tactics"": [ { ""id"": ""Charge"", ""multiplier"": 0.3 }, { ""id"": ""DefensiveEngagement"", ""multiplier"": 2.0, ""side"": ""defender"" } ] }");

        var catalog = _sut.GetCatalog();

        var erebor = catalog.Resolve("erebor");
        Assert.IsFalse(erebor.IsDefault);
        Assert.AreEqual("erebor", erebor.CultureId);
        Assert.AreEqual(2, erebor.Tactics.Count);
        Assert.AreEqual(0.3f, erebor.Tactics[0].Multiplier);
        Assert.AreEqual(DoctrineSide.Defender, erebor.Tactics[1].Side);

        var unknown = catalog.Resolve("khuzait");
        Assert.IsTrue(unknown.IsDefault);
        Assert.AreEqual(2, unknown.Tactics.Count, "the file's own default, not the compiled one");
        Assert.AreEqual(20, unknown.Tactics[1].MinTactics);

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCatalog_CultureKey_ResolvesCaseInsensitively()
    {
        WriteConfigWithCulture(@"""Erebor"": { ""tactics"": [ { ""id"": ""Charge"" } ] }");

        var catalog = _sut.GetCatalog();

        Assert.IsFalse(catalog.Resolve("erebor").IsDefault);
        Assert.IsFalse(catalog.Resolve("EREBOR").IsDefault);
    }

    [TestMethod]
    public void GetCatalog_NullCultureId_ResolvesToDefault()
    {
        WriteConfigWithCulture(@"""erebor"": { ""tactics"": [ { ""id"": ""Charge"" } ] }");

        Assert.IsTrue(_sut.GetCatalog().Resolve(null).IsDefault);
    }

    [TestMethod]
    public void GetCatalog_EnabledFalse_IsCarriedThrough()
    {
        WriteConfig("{ \"enabled\": false, \"doctrines\": { " + ValidDefault + " } }");

        Assert.IsFalse(_sut.GetCatalog().Enabled);
    }

    [TestMethod]
    public void GetCatalog_MissingDefault_InjectsVanillaDefaultAndWarns()
    {
        WriteConfig(@"{ ""doctrines"": { ""erebor"": { ""tactics"": [ { ""id"": ""Charge"" } ] } } }");

        var catalog = _sut.GetCatalog();

        Assert.IsTrue(catalog.Default.IsDefault);
        Assert.AreEqual(DoctrineCatalog.VanillaEquivalent().Default.Tactics.Count, catalog.Default.Tactics.Count);
        Assert.IsFalse(catalog.Resolve("erebor").IsDefault);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("default")));
    }

    [TestMethod]
    public void GetCatalog_DoctrinesNull_ReturnsVanillaEquivalentAndWarns()
    {
        WriteConfig(@"{ ""enabled"": true, ""doctrines"": null }");

        var catalog = _sut.GetCatalog();

        Assert.AreEqual(0, catalog.CultureIds.Count);
        Assert.IsTrue(catalog.Default.Tactics.Count > 0);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("doctrines")));
    }

    [TestMethod]
    public void GetCatalog_UnknownTacticId_DropsTheRowAndWarns()
    {
        WriteConfigWithCulture(@"""erebor"": { ""tactics"": [ { ""id"": ""TacticCharge"" }, { ""id"": ""ShieldWall"" } ] }");

        var erebor = _sut.GetCatalog().Resolve("erebor");

        Assert.AreEqual(1, erebor.Tactics.Count);
        Assert.AreEqual(DoctrineTactic.ShieldWall, erebor.Tactics[0].Tactic);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("TacticCharge")));
    }

    [TestMethod]
    public void GetCatalog_NullRow_IsDroppedAndWarns()
    {
        WriteConfigWithCulture(@"""erebor"": { ""tactics"": [ null, { ""id"": ""Charge"" } ] }");

        var erebor = _sut.GetCatalog().Resolve("erebor");

        Assert.AreEqual(1, erebor.Tactics.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("null")));
    }

    [TestMethod]
    public void GetCatalog_MultiplierAboveFive_RevertsToOneAndWarns()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"", ""multiplier"": 20 } ] }");

        var mordor = _sut.GetCatalog().Resolve("mordor");

        Assert.AreEqual(1f, mordor.Tactics[0].Multiplier);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("multiplier")));
    }

    [TestMethod]
    public void GetCatalog_MultiplierNegative_RevertsToOne()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"", ""multiplier"": -1 } ] }");

        Assert.AreEqual(1f, _sut.GetCatalog().Resolve("mordor").Tactics[0].Multiplier);
    }

    [TestMethod]
    public void GetCatalog_MultiplierNaN_RevertsToOne()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"", ""multiplier"": NaN } ] }");

        Assert.AreEqual(1f, _sut.GetCatalog().Resolve("mordor").Tactics[0].Multiplier);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("multiplier")));
    }

    [TestMethod]
    public void GetCatalog_MultiplierZero_IsKept()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""DefensiveRing"", ""multiplier"": 0 } ] }");

        Assert.AreEqual(0f, _sut.GetCatalog().Resolve("mordor").Tactics[0].Multiplier, "zero is the authored way to park a tactic while keeping the row");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCatalog_MinTacticsOutOfRange_RevertsToZeroAndWarns()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"", ""minTactics"": -5 }, { ""id"": ""FullScaleAttack"", ""minTactics"": 999 } ] }");

        var mordor = _sut.GetCatalog().Resolve("mordor");

        Assert.AreEqual(0, mordor.Tactics[0].MinTactics);
        Assert.AreEqual(0, mordor.Tactics[1].MinTactics);
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("minTactics")));
    }

    [TestMethod]
    public void GetCatalog_UnknownSide_RevertsToAnyAndWarns()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"", ""side"": ""defence"" } ] }");

        Assert.AreEqual(DoctrineSide.Any, _sut.GetCatalog().Resolve("mordor").Tactics[0].Side);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("side")));
    }

    [TestMethod]
    public void GetCatalog_SideNames_ParseCaseInsensitively()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"", ""side"": ""Attacker"" }, { ""id"": ""FullScaleAttack"", ""side"": ""DEFENDER"" }, { ""id"": ""DefensiveLine"" } ] }");

        var mordor = _sut.GetCatalog().Resolve("mordor");

        Assert.AreEqual(DoctrineSide.Attacker, mordor.Tactics[0].Side);
        Assert.AreEqual(DoctrineSide.Defender, mordor.Tactics[1].Side);
        Assert.AreEqual(DoctrineSide.Any, mordor.Tactics[2].Side, "side is optional and defaults to any");
    }

    [TestMethod]
    public void GetCatalog_NullTacticsList_DropsTheDoctrineAndWarns()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": null }");

        var catalog = _sut.GetCatalog();

        Assert.IsTrue(catalog.Resolve("mordor").IsDefault);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("mordor")));
    }

    [TestMethod]
    public void GetCatalog_EmptyTacticsList_IsKept()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [] }");

        var mordor = _sut.GetCatalog().Resolve("mordor");

        Assert.IsFalse(mordor.IsDefault);
        Assert.AreEqual(0, mordor.Tactics.Count, "the roster prepends Charge; an empty doctrine is a legitimate 'charge only' switch");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCatalog_AnyRejection_LogsOneSummaryWarning()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Nope"" } ] }");

        _sut.GetCatalog();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void GetCatalog_NoMoraleAggressionOrFormationsBlock_IsVanilla()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"" } ] }");

        var doctrine = _sut.GetCatalog().Resolve("mordor");

        Assert.IsTrue(doctrine.Morale.CanPanic);
        Assert.AreEqual(0f, doctrine.Morale.Bravery);
        Assert.IsTrue(doctrine.Aggression.IsVanilla);
        Assert.IsTrue(doctrine.Formations.IsEmpty);
        Assert.IsFalse(_sut.GetCatalog().HasFormationRouting);
    }

    [TestMethod]
    public void GetCatalog_MoraleBlock_IsRead()
    {
        WriteConfigWithCulture(@"""erebor"": { ""tactics"": [], ""morale"": { ""neverRout"": true, ""bravery"": 15 } }");

        var morale = _sut.GetCatalog().Resolve("erebor").Morale;

        Assert.IsFalse(morale.CanPanic);
        Assert.AreEqual(15f, morale.Bravery);
    }

    [TestMethod]
    public void GetCatalog_BraveryOutOfRange_RevertsToZeroAndWarns()
    {
        WriteConfigWithCulture(@"""erebor"": { ""tactics"": [], ""morale"": { ""neverRout"": true, ""bravery"": 200 } }");

        var morale = _sut.GetCatalog().Resolve("erebor").Morale;

        Assert.AreEqual(0f, morale.Bravery);
        Assert.IsFalse(morale.CanPanic, "the valid half of the block survives");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("morale.bravery")));
    }

    [TestMethod]
    public void GetCatalog_BraveryNaN_RevertsToZeroAndWarns()
    {
        WriteConfigWithCulture(@"""erebor"": { ""tactics"": [], ""morale"": { ""bravery"": NaN } }");

        Assert.AreEqual(0f, _sut.GetCatalog().Resolve("erebor").Morale.Bravery);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("morale.bravery")));
    }

    [TestMethod]
    public void GetCatalog_AggressionBlock_IsRead()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [], ""aggression"": { ""attack"": 1.5, ""shield"": 0.7, ""shooterError"": 1.3, ""chargeDistance"": 1.1 } }");

        var a = _sut.GetCatalog().Resolve("mordor").Aggression;

        Assert.AreEqual(1.5f, a.Attack);
        Assert.AreEqual(0.7f, a.Shield);
        Assert.AreEqual(1.3f, a.ShooterError);
        Assert.AreEqual(1.1f, a.ChargeDistance);
    }

    [TestMethod]
    public void GetCatalog_AggressionMultiplierOutOfRange_RevertsToOneAndWarns()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [], ""aggression"": { ""attack"": 10, ""shield"": 0.7 } }");

        var a = _sut.GetCatalog().Resolve("mordor").Aggression;

        Assert.AreEqual(1f, a.Attack);
        Assert.AreEqual(0.7f, a.Shield, "the valid multipliers survive");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("aggression.attack")));
    }

    [TestMethod]
    public void GetCatalog_AggressionMultiplierNaN_RevertsToOneAndWarns()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [], ""aggression"": { ""shooterError"": NaN } }");

        Assert.AreEqual(1f, _sut.GetCatalog().Resolve("mordor").Aggression.ShooterError);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("aggression.shooterError")));
    }

    [TestMethod]
    public void GetCatalog_AggressionShieldAndChargeDistance_AreValidatedLikeTheOthers()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [], ""aggression"": { ""shield"": 0.1, ""chargeDistance"": NaN } }");

        var a = _sut.GetCatalog().Resolve("mordor").Aggression;

        Assert.AreEqual(1f, a.Shield);
        Assert.AreEqual(1f, a.ChargeDistance);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("aggression.shield")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("aggression.chargeDistance")));
    }

    [TestMethod]
    public void GetCatalog_FormationsNull_IsNoRouting()
    {
        WriteConfigWithCulture(@"""aserai"": { ""tactics"": [], ""formations"": null }");

        Assert.IsTrue(_sut.GetCatalog().Resolve("aserai").Formations.IsEmpty);
        Assert.IsFalse(_sut.GetCatalog().HasFormationRouting);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("formations")));
    }

    [TestMethod]
    public void GetCatalog_FormationsBlock_IsRead_AndFlagsTheCatalog()
    {
        WriteConfigWithCulture(@"""aserai"": { ""tactics"": [], ""formations"": { ""harad_mumakil_rider"": ""heavycavalry"" } }");

        var catalog = _sut.GetCatalog();

        Assert.IsTrue(catalog.HasFormationRouting);
        Assert.IsTrue(catalog.Resolve("aserai").Formations.TryRoute("harad_mumakil_rider", out var c));
        Assert.AreEqual(TaleWorlds.Core.FormationClass.HeavyCavalry, c);
    }

    [TestMethod]
    public void GetCatalog_FormationsUnknownClass_DropsTheRowAndWarns()
    {
        WriteConfigWithCulture(@"""aserai"": { ""tactics"": [], ""formations"": { ""harad_mumakil_rider"": ""Elephants"", ""other"": ""Infantry"" } }");

        var formations = _sut.GetCatalog().Resolve("aserai").Formations;

        Assert.IsFalse(formations.TryRoute("harad_mumakil_rider", out _));
        Assert.IsTrue(formations.TryRoute("other", out _), "the valid row survives");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("formations['harad_mumakil_rider']") && m.Contains("Elephants")));
    }

    [TestMethod]
    public void GetCatalog_FormationsEmptyTroopId_DropsTheRowAndWarns()
    {
        WriteConfigWithCulture(@"""aserai"": { ""tactics"": [], ""formations"": { "" "": ""Infantry"" } }");

        Assert.IsTrue(_sut.GetCatalog().Resolve("aserai").Formations.IsEmpty);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("empty troop id")));
    }

    [TestMethod]
    public void GetCatalog_CalledTwice_LoadsOnce()
    {
        WriteConfigWithCulture(@"""mordor"": { ""tactics"": [ { ""id"": ""Charge"" } ] }");

        var first = _sut.GetCatalog();
        var second = _sut.GetCatalog();

        Assert.AreSame(first, second);
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Loaded")));
    }

    [TestMethod]
    public void GetCatalog_NoEngagementBlock_UsesTheDefaults()
    {
        WriteConfig("{ \"enabled\": true, \"doctrines\": { " + ValidDefault + " } }");

        var e = _sut.GetCatalog().Engagement;

        Assert.AreEqual(EngagementTunables.Default.CavalryMattersMetres, e.CavalryMattersMetres);
        Assert.AreEqual(EngagementTunables.Default.HighGroundMaxMetres, e.HighGroundMaxMetres);
        Assert.AreEqual(EngagementTunables.Default.HoldWhenEnemyWithinMetres, e.HoldWhenEnemyWithinMetres);
    }

    [TestMethod]
    public void GetCatalog_EngagementBlock_IsRead()
    {
        WriteConfig("{ \"enabled\": true, \"engagement\": { \"cavalryMattersMetres\": 30, \"highGroundMaxMetres\": 80, \"holdWhenEnemyWithinMetres\": 45 }, \"doctrines\": { " + ValidDefault + " } }");

        var e = _sut.GetCatalog().Engagement;

        Assert.AreEqual(30f, e.CavalryMattersMetres);
        Assert.AreEqual(80f, e.HighGroundMaxMetres);
        Assert.AreEqual(45f, e.HoldWhenEnemyWithinMetres);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void GetCatalog_EngagementOutOfRangeOrNaN_RevertsThatFieldAndWarns()
    {
        WriteConfig("{ \"enabled\": true, \"engagement\": { \"cavalryMattersMetres\": 1000, \"highGroundMaxMetres\": NaN, \"holdWhenEnemyWithinMetres\": -1 }, \"doctrines\": { " + ValidDefault + " } }");

        var e = _sut.GetCatalog().Engagement;

        Assert.AreEqual(EngagementTunables.Default.CavalryMattersMetres, e.CavalryMattersMetres);
        Assert.AreEqual(EngagementTunables.Default.HighGroundMaxMetres, e.HighGroundMaxMetres);
        Assert.AreEqual(EngagementTunables.Default.HoldWhenEnemyWithinMetres, e.HoldWhenEnemyWithinMetres);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("engagement.cavalryMattersMetres")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("engagement.highGroundMaxMetres")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("engagement.holdWhenEnemyWithinMetres")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }
}

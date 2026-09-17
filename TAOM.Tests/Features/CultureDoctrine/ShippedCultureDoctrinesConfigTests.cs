using System;
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
/// Pins the doctrine file actually shipped to players. The provider is fail-soft by design, so a
/// shipped file with a bad row would revert or drop it and the battle would look fine while
/// ignoring what was authored. These tests make that loud, and they pin the culture-id trap:
/// TAOM re-skins six vanilla cultures without changing their ids, so Rohan is <c>vlandia</c> and
/// Dunland is <c>empire</c>; a doctrine keyed on the LOTR name silently applies to nobody.
/// </summary>
[TestClass]
public class ShippedCultureDoctrinesConfigTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string ConfigPath =>
        Path.Combine(ModuleDataPath, "culture_doctrine", "culture_doctrines.json");

    private IModLogger _logger = null!;
    private CultureDoctrineConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new CultureDoctrineConfigProvider(pathService, _logger);
    }

    [TestMethod]
    public void ShippedConfig_FileExists()
        => Assert.IsTrue(File.Exists(ConfigPath), $"Shipped config missing at {ConfigPath}");

    [TestMethod]
    public void ShippedConfig_ParsesWithoutErrorOrRejection()
    {
        _sut.GetCatalog();

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_IsEnabledWithAFileAuthoredDefault()
    {
        var catalog = _sut.GetCatalog();

        Assert.IsTrue(catalog.Enabled);
        Assert.IsTrue(catalog.Default.IsDefault);
        Assert.IsTrue(catalog.Default.Tactics.Count >= 5, "the shipped default should be the vanilla set, not a stub");
    }

    [TestMethod]
    public void ShippedConfig_KeysTheReskinnedCulturesByVanillaId()
    {
        var ids = _sut.GetCatalog().CultureIds;

        CollectionAssert.Contains(ids.ToList(), "vlandia", "Rohan is keyed by its vanilla id");
        CollectionAssert.Contains(ids.ToList(), "empire", "Dunland is keyed by its vanilla id");
        CollectionAssert.DoesNotContain(ids.ToList(), "rohan");
        CollectionAssert.DoesNotContain(ids.ToList(), "rohirrim");
        CollectionAssert.DoesNotContain(ids.ToList(), "dunland");
    }

    [TestMethod]
    public void ShippedConfig_CoversTheFirstSliceCultures()
    {
        var ids = _sut.GetCatalog().CultureIds.ToList();

        foreach (var id in new[] { "erebor", "lindon", "vlandia", "mordor", "gundabad", "isengard", "empire", "gondor" })
            CollectionAssert.Contains(ids, id, id + " has no doctrine");
    }

    [TestMethod]
    public void ShippedConfig_EveryCultureRegistersItsTaomTacticOnTheRightSide()
    {
        var catalog = _sut.GetCatalog();

        Assert.IsTrue(catalog.Resolve("erebor").Tactics.Any(t => t.Tactic == DoctrineTactic.ShieldWall), "Dwarves carry ShieldWall");
        Assert.IsTrue(catalog.Resolve("mordor").Tactics.Any(t => t.Tactic == DoctrineTactic.InfantryMass), "Mordor carries InfantryMass");
        Assert.IsTrue(catalog.Resolve("gundabad").Tactics.Any(t => t.Tactic == DoctrineTactic.InfantryMass), "Gundabad carries InfantryMass");
        Assert.IsTrue(catalog.Resolve("vlandia").Tactics.Any(t => t.Tactic == DoctrineTactic.CavalryDominance), "Rohan carries CavalryDominance");
        var ring = catalog.Resolve("lindon").Tactics.SingleOrDefault(t => t.Tactic == DoctrineTactic.ArcherRing);
        Assert.IsNotNull(ring, "Elves carry ArcherRing");
        Assert.AreEqual(DoctrineSide.Defender, ring!.Side, "the ring is a defender's tactic; attacking Elves use the weighted vanilla set");
    }

    [TestMethod]
    public void ShippedConfig_EveryTaomTacticEntry_HasNoSkillFloor()
    {
        var catalog = _sut.GetCatalog();

        // A TAOM tactic IS the culture's doctrine; gating it on the commander's Tactics skill would
        // hand a low-skill Dwarven lord the vanilla charge and hide the feature in Custom Battle.
        foreach (var id in catalog.CultureIds)
            foreach (var entry in catalog.Resolve(id).Tactics.Where(t => !DoctrineTacticIds.IsVanilla(t.Tactic)))
                Assert.AreEqual(0, entry.MinTactics, $"{id}: {entry.Tactic} has a skill floor");
    }
}

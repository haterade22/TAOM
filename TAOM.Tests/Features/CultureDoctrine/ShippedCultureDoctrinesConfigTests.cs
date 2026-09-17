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

    /// <summary>Mike, 2026-09-16: Gundabad, Isengard and Dol Guldur never rout (their hatred of
    /// men and elves is too great); Dwarves and Elves never rout (pride); Rohan, Gondor, Mordor,
    /// Rhun, Harad, Dale, Dunland and the rest rout.</summary>
    [TestMethod]
    public void ShippedConfig_NeverRoutCultures_AreTheOnesMikeNamed()
    {
        var catalog = _sut.GetCatalog();
        var neverRout = new[] { "gundabad", "gundabad_raiders", "isengard", "dolguldur", "erebor", "erebor_warriors", "lindon", "lothlorien", "mirkwood", "mirkwood_stalkers", "rivendell" };
        var rout = new[] { "vlandia", "gondor", "gondor_soldiers", "mordor", "khuzait", "aserai", "umbar", "sturgia", "empire", "dunland_raiders", "battania", "goblin", "mistymountainorcs" };

        foreach (var id in neverRout)
            Assert.IsTrue(catalog.Resolve(id).Morale.NeverRout, id + " should never rout");
        foreach (var id in rout)
            Assert.IsFalse(catalog.Resolve(id).Morale.NeverRout, id + " should rout");
        Assert.IsFalse(catalog.Default.Morale.NeverRout, "an unlisted culture routs");
    }

    [TestMethod]
    public void ShippedConfig_NeverRoutCultures_CarryNoRetreatRow()
    {
        var catalog = _sut.GetCatalog();
        foreach (var id in catalog.CultureIds)
        {
            var doctrine = catalog.Resolve(id);
            if (doctrine.Morale.NeverRout)
                Assert.IsFalse(doctrine.Tactics.Any(t => t.Tactic == DoctrineTactic.CoordinatedRetreat), id + " never routs but registers CoordinatedRetreat");
        }
    }

    [TestMethod]
    public void ShippedConfig_EveryCultureCarriesAnAggressionProfile_WithinRange()
    {
        var catalog = _sut.GetCatalog();
        foreach (var id in catalog.CultureIds)
        {
            var a = catalog.Resolve(id).Aggression;
            foreach (var m in new[] { a.Attack, a.Shield, a.ShooterError, a.ChargeDistance })
                Assert.IsTrue(m >= CultureAggression.MinMultiplier && m <= CultureAggression.MaxMultiplier, id);
        }
        Assert.IsTrue(catalog.Resolve("mordor").Aggression.Attack > 1f, "orcs attack on sight");
        Assert.IsTrue(catalog.Resolve("lindon").Aggression.ShooterError < 1f, "elves shoot straighter");
        Assert.IsTrue(catalog.Resolve("erebor").Aggression.Shield > 1f, "dwarves raise shields");
        Assert.IsTrue(catalog.Resolve("vlandia").Aggression.ChargeDistance > 1f, "Rohan commits from further out");
    }

    [TestMethod]
    public void ShippedConfig_PhaseC_TacticsAreOnTheCulturesAndSidesTheyWereWrittenFor()
    {
        var catalog = _sut.GetCatalog();

        Assert.AreEqual(DoctrineSide.Defender, catalog.Resolve("erebor").Tactics.Single(t => t.Tactic == DoctrineTactic.TwoLineWall).Side);
        foreach (var orc in new[] { "mordor", "dolguldur", "gundabad", "gundabad_raiders", "mistymountainorcs", "goblin" })
            Assert.IsTrue(catalog.Resolve(orc).Tactics.Any(t => t.Tactic == DoctrineTactic.Envelop), orc + " carries Envelop");
        Assert.IsTrue(catalog.Resolve("isengard").Tactics.Any(t => t.Tactic == DoctrineTactic.ShieldWall), "Uruks are disciplined: the wall, not the mob");
        Assert.IsFalse(catalog.Resolve("isengard").Tactics.Any(t => t.Tactic == DoctrineTactic.InfantryMass));
        Assert.AreEqual(DoctrineSide.Attacker, catalog.Resolve("dunland_raiders").Tactics.Single(t => t.Tactic == DoctrineTactic.InfantryMass).Side, "the mob moved to the raiders");
        Assert.AreEqual(DoctrineSide.Attacker, catalog.Resolve("empire").Tactics.Single(t => t.Tactic == DoctrineTactic.HitAndRun).Side);
        Assert.AreEqual(DoctrineSide.Defender, catalog.Resolve("vlandia").Tactics.Single(t => t.Tactic == DoctrineTactic.EoredScreen).Side);
        foreach (var elf in new[] { "lindon", "lothlorien", "mirkwood", "mirkwood_stalkers", "rivendell" })
            Assert.AreEqual(DoctrineSide.Attacker, catalog.Resolve(elf).Tactics.Single(t => t.Tactic == DoctrineTactic.ArcherAdvance).Side, elf);
        Assert.IsTrue(catalog.Resolve("gondor").Tactics.Any(t => t.Tactic == DoctrineTactic.DisciplinedLine));
        Assert.IsTrue(catalog.Resolve("sturgia").Tactics.Any(t => t.Tactic == DoctrineTactic.ShieldWall) && catalog.Resolve("sturgia").Tactics.Any(t => t.Tactic == DoctrineTactic.DisciplinedLine), "Dale");
        // Rhun (Mike, 2026-09-16): heavy cavalry like Rohan, better foot and bow, few horse archers.
        Assert.IsTrue(catalog.Resolve("khuzait").Tactics.Any(t => t.Tactic == DoctrineTactic.CavalryDominance) && catalog.Resolve("khuzait").Tactics.Any(t => t.Tactic == DoctrineTactic.DisciplinedLine), "Rhun");
        Assert.IsTrue(catalog.Resolve("battania").Tactics.Any(t => t.Tactic == DoctrineTactic.CavalryDominance), "Khand");
        Assert.AreEqual(DoctrineSide.Attacker, catalog.Resolve("aserai").Tactics.Single(t => t.Tactic == DoctrineTactic.MumakVanguard).Side, "Harad");
    }

    [TestMethod]
    public void ShippedConfig_HaradRoutesTheMumakilRiderToHeavyCavalry_AndNobodyElseRoutes()
    {
        var catalog = _sut.GetCatalog();

        Assert.IsTrue(catalog.HasFormationRouting);
        Assert.IsTrue(catalog.Resolve("aserai").Formations.TryRoute("harad_mumakil_rider", out var c));
        Assert.AreEqual(TaleWorlds.Core.FormationClass.HeavyCavalry, c);
        foreach (var id in catalog.CultureIds.Where(i => i != "aserai"))
            Assert.IsTrue(catalog.Resolve(id).Formations.IsEmpty, id);
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.FormationPresets.Models;
using TAOM.Features.CompanionTactics.Roles;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

[TestClass]
public class HeroAutoAssignerTests
{
    private ICompanionRoleService _roles = null!;
    private HeroAutoAssigner _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _roles = Substitute.For<ICompanionRoleService>();
        _sut = new HeroAutoAssigner(_roles);
    }

    private static IHeroCombatAdapter MakeHero(string id = "h1")
    {
        var hero = Substitute.For<IHeroCombatAdapter>();
        hero.StringId.Returns(id);
        return hero;
    }

    private IHeroCombatAdapter Hero(string id, CombatRole role)
    {
        var hero = MakeHero(id);
        _roles.GetPrimaryRole(hero).Returns(role);
        return hero;
    }

    private static List<CaptainAssignment> Plan(HeroAutoAssigner sut,
        IReadOnlyList<IHeroCombatAdapter> heroes, params int[] classes)
        => sut.PlanCaptains(heroes, classes).ToList();

    // formationClass (DeploymentFormationClass): 0=Unset, 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=InfantryAndRanged, 6=CavalryAndHorseArcher

    [TestMethod]
    public void ScoreRoleForFormation_ShieldInfantryToInfantry_HighScore()
    {
        var s = _sut.ScoreRoleForFormation(CombatRole.ShieldInfantry, formationClass: 1);

        Assert.AreEqual(100, s);
    }

    [TestMethod]
    public void ScoreRoleForFormation_ArcherToRanged_HighScore()
    {
        var s = _sut.ScoreRoleForFormation(CombatRole.Archer, formationClass: 2);

        Assert.AreEqual(100, s);
    }

    [TestMethod]
    public void ScoreRoleForFormation_CavalryToCavalry_HighScore()
    {
        var s = _sut.ScoreRoleForFormation(CombatRole.Cavalry, formationClass: 3);

        Assert.AreEqual(100, s);
    }

    [TestMethod]
    public void ScoreRoleForFormation_HorseArcherToHorseArcher_HighScore()
    {
        var s = _sut.ScoreRoleForFormation(CombatRole.HorseArcher, formationClass: 4);

        Assert.AreEqual(100, s);
    }

    [TestMethod]
    public void ScoreRoleForFormation_ArcherToInfantry_LowScore()
    {
        var s = _sut.ScoreRoleForFormation(CombatRole.Archer, formationClass: 1);

        Assert.AreEqual(0, s);
    }

    [TestMethod]
    public void ScoreRoleForFormation_HeavyInfantryClass_PrefersMelee()
    {
        var meleeScore = _sut.ScoreRoleForFormation(CombatRole.TwoHanded, formationClass: 5);
        var rangedScore = _sut.ScoreRoleForFormation(CombatRole.Archer, formationClass: 5);

        Assert.IsTrue(meleeScore > rangedScore, $"melee={meleeScore} ranged={rangedScore}");
    }

    [TestMethod]
    public void ScoreHeroForFormation_DelegatesToRoleService()
    {
        var hero = MakeHero();
        _roles.GetPrimaryRole(hero).Returns(CombatRole.Cavalry);

        var s = _sut.ScoreHeroForFormation(hero, formationClass: 3);

        Assert.AreEqual(100, s);
        _roles.Received(1).GetPrimaryRole(hero);
    }

    [TestMethod]
    public void PlanCaptains_NoHeroes_ReturnsEmpty()
    {
        var result = Plan(_sut, new List<IHeroCombatAdapter>(), 1);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void PlanCaptains_NoSlots_ReturnsEmpty()
    {
        var heroes = new List<IHeroCombatAdapter> { Hero("a", CombatRole.ShieldInfantry) };

        var result = Plan(_sut, heroes);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void PlanCaptains_NullLists_ReturnEmpty()
    {
        var nullHeroes = _sut.PlanCaptains(null!, new[] { 1 });
        var nullClasses = _sut.PlanCaptains(new[] { Hero("a", CombatRole.Archer) }, null!);

        Assert.AreEqual(0, nullHeroes.Count);
        Assert.AreEqual(0, nullClasses.Count);
    }

    [TestMethod]
    public void PlanCaptains_ArcherAndShieldInfantry_EachLeadsTheMatchingFormation()
    {
        var heroes = new List<IHeroCombatAdapter>
        {
            Hero("a", CombatRole.Archer),
            Hero("b", CombatRole.ShieldInfantry),
        };

        var result = Plan(_sut, heroes, 1, 2);

        CollectionAssert.AreEqual(new List<CaptainAssignment> { new(1, 0), new(0, 1) }, result);
    }

    [TestMethod]
    public void PlanCaptains_TwoCavalryOneCavalrySlot_FirstCandidateLeads()
    {
        var heroes = new List<IHeroCombatAdapter>
        {
            Hero("a", CombatRole.Cavalry),
            Hero("b", CombatRole.Cavalry),
        };

        var result = Plan(_sut, heroes, 3);

        CollectionAssert.AreEqual(new List<CaptainAssignment> { new(0, 0) }, result);
    }

    [TestMethod]
    public void PlanCaptains_UnknownRole_IsNeverPlaced()
    {
        var heroes = new List<IHeroCombatAdapter> { Hero("a", CombatRole.Unknown) };

        var result = Plan(_sut, heroes, 1, 2, 3, 4, 5, 6);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void PlanCaptains_UnsetFormationClass_IsNeverFilled()
    {
        var heroes = new List<IHeroCombatAdapter> { Hero("a", CombatRole.ShieldInfantry) };

        var result = Plan(_sut, heroes, 0);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void PlanCaptains_ArcherWithMixedAndRangedSlots_TakesTheRangedSlot()
    {
        var heroes = new List<IHeroCombatAdapter> { Hero("a", CombatRole.Archer) };

        var result = Plan(_sut, heroes, 5, 2);

        CollectionAssert.AreEqual(new List<CaptainAssignment> { new(0, 1) }, result);
    }

    [TestMethod]
    public void PlanCaptains_ThreeMeleeHeroesTwoSlots_EachHeroAndSlotUsedOnce()
    {
        var heroes = new List<IHeroCombatAdapter>
        {
            Hero("a", CombatRole.TwoHanded),
            Hero("b", CombatRole.OneHanded),
            Hero("c", CombatRole.Polearm),
        };

        var result = Plan(_sut, heroes, 1, 0, 5);

        CollectionAssert.AreEqual(new List<CaptainAssignment> { new(0, 0), new(1, 2) }, result);
    }

    [TestMethod]
    public void PlanCaptains_NullHeroEntry_IsSkipped()
    {
        var heroes = new List<IHeroCombatAdapter> { null!, Hero("b", CombatRole.Cavalry) };

        var result = Plan(_sut, heroes, 3);

        CollectionAssert.AreEqual(new List<CaptainAssignment> { new(1, 0) }, result);
    }
}

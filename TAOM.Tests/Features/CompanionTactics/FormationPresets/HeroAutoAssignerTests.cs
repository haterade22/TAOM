using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.FormationPresets;
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

    // formationClass: 1=Infantry, 2=Ranged, 3=Cavalry, 4=HorseArcher, 5=HeavyInfantry, 6=LightCavalry

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
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.BattleActionBar;

namespace TAOM.Tests.Features.CompanionTactics.BattleActionBar;

[TestClass]
public class FormationCompositionAnalyzerTests
{
    private FormationCompositionAnalyzer _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new FormationCompositionAnalyzer();

    private static IFormationAdapter MakeFormation(int total, int ranged, int cavalry, int polearm, int shield)
    {
        var f = Substitute.For<IFormationAdapter>();
        f.CountOfUnits.Returns(total);
        f.RangedUnitCount.Returns(ranged);
        f.CavalryUnitCount.Returns(cavalry);
        f.PolearmUnitCount.Returns(polearm);
        f.ShieldUnitCount.Returns(shield);
        return f;
    }

    [TestMethod]
    public void Analyze_NullFormation_ReturnsAllFalse()
    {
        var c = _sut.Analyze(null);

        Assert.IsFalse(c.HasRanged);
        Assert.IsFalse(c.HasPolearm);
        Assert.IsFalse(c.HasShields);
        Assert.IsFalse(c.HasCavalry);
    }

    [TestMethod]
    public void Analyze_EmptyFormation_ReturnsAllFalse()
    {
        var f = MakeFormation(total: 0, ranged: 0, cavalry: 0, polearm: 0, shield: 0);

        var c = _sut.Analyze(f);

        Assert.IsFalse(c.HasRanged);
        Assert.IsFalse(c.HasPolearm);
        Assert.IsFalse(c.HasShields);
        Assert.IsFalse(c.HasCavalry);
    }

    [TestMethod]
    public void Analyze_SomeRanged_HasRangedTrue()
    {
        var f = MakeFormation(total: 20, ranged: 5, cavalry: 0, polearm: 0, shield: 0);

        var c = _sut.Analyze(f);

        Assert.IsTrue(c.HasRanged);
        Assert.IsFalse(c.HasCavalry);
    }

    [TestMethod]
    public void Analyze_NoRanged_HasRangedFalse()
    {
        var f = MakeFormation(total: 20, ranged: 0, cavalry: 0, polearm: 5, shield: 5);

        var c = _sut.Analyze(f);

        Assert.IsFalse(c.HasRanged);
        Assert.IsTrue(c.HasPolearm);
        Assert.IsTrue(c.HasShields);
    }

    [TestMethod]
    public void Analyze_SomeCavalry_HasCavalryTrue()
    {
        var f = MakeFormation(total: 20, ranged: 0, cavalry: 8, polearm: 0, shield: 0);

        var c = _sut.Analyze(f);

        Assert.IsTrue(c.HasCavalry);
        Assert.IsFalse(c.HasRanged);
    }

    [TestMethod]
    public void Analyze_AllRanged_IsRangedDominantTrue()
    {
        var f = MakeFormation(total: 20, ranged: 18, cavalry: 0, polearm: 0, shield: 0);

        var c = _sut.Analyze(f);

        Assert.IsTrue(c.HasRanged);
        Assert.IsTrue(c.IsRangedDominant);
    }

    [TestMethod]
    public void Analyze_FewRanged_IsRangedDominantFalse()
    {
        var f = MakeFormation(total: 20, ranged: 4, cavalry: 0, polearm: 0, shield: 0);

        var c = _sut.Analyze(f);

        Assert.IsTrue(c.HasRanged);
        Assert.IsFalse(c.IsRangedDominant);
    }

    [TestMethod]
    public void Analyze_AllCavalry_IsCavalryDominantTrue()
    {
        var f = MakeFormation(total: 10, ranged: 0, cavalry: 9, polearm: 0, shield: 0);

        var c = _sut.Analyze(f);

        Assert.IsTrue(c.HasCavalry);
        Assert.IsTrue(c.IsCavalryDominant);
    }

    [TestMethod]
    public void Analyze_FewCavalry_IsCavalryDominantFalse()
    {
        var f = MakeFormation(total: 20, ranged: 0, cavalry: 2, polearm: 0, shield: 0);

        var c = _sut.Analyze(f);

        Assert.IsTrue(c.HasCavalry);
        Assert.IsFalse(c.IsCavalryDominant);
    }

    [TestMethod]
    public void Analyze_MixedFormation_AllFlagsCorrect()
    {
        var f = MakeFormation(total: 30, ranged: 10, cavalry: 5, polearm: 8, shield: 12);

        var c = _sut.Analyze(f);

        Assert.IsTrue(c.HasRanged);
        Assert.IsTrue(c.HasCavalry);
        Assert.IsTrue(c.HasPolearm);
        Assert.IsTrue(c.HasShields);
        Assert.IsFalse(c.IsRangedDominant);   // 10/30 = 33%
        Assert.IsFalse(c.IsCavalryDominant);  // 5/30 = 17%
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

[TestClass]
public class NameplateRelationAlphaServiceTests
{
    private readonly NameplateRelationAlphaService _sut = new NameplateRelationAlphaService();

    [TestMethod]
    public void Adjust_Neutral_Unchanged()
        => Assert.AreEqual(0.35f, _sut.Adjust(0.35f, NameplateRelationPalette.Neutral, isTracked: false));

    [TestMethod]
    public void Adjust_SameFaction_Unchanged()
        => Assert.AreEqual(0.5f, _sut.Adjust(0.5f, NameplateRelationPalette.SameFaction, isTracked: false));

    [TestMethod]
    public void Adjust_EnemyUntracked_RaisesToHalf()
        => Assert.AreEqual(0.5f, _sut.Adjust(0.35f, NameplateRelationPalette.Enemy, isTracked: false));

    [TestMethod]
    public void Adjust_AllyUntracked_RaisesToHalf()
        => Assert.AreEqual(0.5f, _sut.Adjust(0.35f, NameplateRelationPalette.Ally, isTracked: false));

    [TestMethod]
    public void Adjust_EnemyTracked_KeepsPointEight()
        => Assert.AreEqual(0.8f, _sut.Adjust(0.8f, NameplateRelationPalette.Enemy, isTracked: true));

    [TestMethod]
    public void Adjust_EnemyAlreadyAboveHalf_NeverLowers()
        => Assert.AreEqual(1f, _sut.Adjust(1f, NameplateRelationPalette.Enemy, isTracked: false));

    [TestMethod]
    public void Adjust_ZeroTarget_StaysZero()
    {
        // Off-window plates come in at 0 and must stay hidden.
        Assert.AreEqual(0f, _sut.Adjust(0f, NameplateRelationPalette.Enemy, isTracked: false));
        Assert.AreEqual(0f, _sut.Adjust(0f, NameplateRelationPalette.Ally, isTracked: false));
    }

    [TestMethod]
    public void Adjust_NaNTarget_ReturnsNaNUntouched()
        => Assert.IsTrue(float.IsNaN(_sut.Adjust(float.NaN, NameplateRelationPalette.Enemy, isTracked: false)));

    [TestMethod]
    public void Adjust_UnknownRelation_Unchanged()
    {
        Assert.AreEqual(0.35f, _sut.Adjust(0.35f, 7, isTracked: false));
        Assert.AreEqual(0.35f, _sut.Adjust(0.35f, -1, isTracked: false));
    }

    [TestMethod]
    public void RaisedTargetAlpha_MatchesVanillaOwnFactionLevel()
    {
        // SettlementNameplateWidget._normalAllyAlphaTarget => 0.5f (v1.4.8 dump line 69).
        Assert.AreEqual(0.5f, NameplateRelationAlphaService.RaisedTargetAlpha);
    }
}

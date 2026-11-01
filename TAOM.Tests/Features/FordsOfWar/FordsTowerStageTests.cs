using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// The shown damage stage of one tower (H2): the engine recomputes the stage from the component's hit points on every hit, so a heal
/// between two hits would make it step back and read <c>_destructionStates[-1]</c>. The stage the adapter hands the engine only
/// ever moves forward.
/// </summary>
[TestClass]
public class FordsTowerStageTests
{
    [TestMethod]
    public void Advance_ABetterIndexThanTheLastOne_ReturnsTheLastOne()
    {
        var stage = new FordsTowerStage();

        Assert.AreEqual(2, stage.Advance(2));
        Assert.AreEqual(2, stage.Advance(1), "regeneration lifted the hit points back above a stage line");
        Assert.AreEqual(2, stage.Advance(0));
    }

    [TestMethod]
    public void Advance_AWorseIndex_MovesTheStageForward()
    {
        var stage = new FordsTowerStage();

        stage.Advance(1);

        Assert.AreEqual(3, stage.Advance(3));
    }

    [TestMethod]
    public void Advance_ANewTower_StartsAtStageZero()
    {
        var stage = new FordsTowerStage();

        Assert.AreEqual(0, stage.Advance(0));
    }
}

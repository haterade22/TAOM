using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

[TestClass]
public class OutlineDiffTests
{
    private const uint Gold = 0xFFD9B44Au;
    private const uint Red = 0xFFB03A2Eu;

    private readonly List<KeyValuePair<int, uint>> _paint = new();
    private readonly List<int> _clear = new();

    private void Plan(Dictionary<int, uint> painted, Dictionary<int, uint> wanted) => OutlineDiff.Plan(painted, wanted, _paint, _clear);

    [TestMethod]
    public void Plan_NothingPaintedAndSomeWanted_PaintsEveryWanted()
    {
        Plan(new Dictionary<int, uint>(), new Dictionary<int, uint> { [1] = Gold, [2] = Red });

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, _paint.ConvertAll(p => p.Key));
        Assert.AreEqual(0, _clear.Count);
    }

    [TestMethod]
    public void Plan_PaintedAndNothingWanted_ClearsEveryPainted()
    {
        Plan(new Dictionary<int, uint> { [1] = Gold, [2] = Red }, new Dictionary<int, uint>());

        CollectionAssert.AreEquivalent(new[] { 1, 2 }, _clear);
        Assert.AreEqual(0, _paint.Count);
    }

    [TestMethod]
    public void Plan_TheSameColourAlreadyPainted_DoesNothing()
    {
        Plan(new Dictionary<int, uint> { [1] = Gold }, new Dictionary<int, uint> { [1] = Gold });

        Assert.AreEqual(0, _paint.Count);
        Assert.AreEqual(0, _clear.Count);
    }

    [TestMethod]
    public void Plan_AUnitWhoseColourChanged_IsPaintedAgainAndNotCleared()
    {
        Plan(new Dictionary<int, uint> { [1] = Gold }, new Dictionary<int, uint> { [1] = Red });

        Assert.AreEqual(1, _paint.Count);
        Assert.AreEqual(Red, _paint[0].Value);
        Assert.AreEqual(0, _clear.Count);
    }

    [TestMethod]
    public void Plan_OnlyWhatWasPainted_IsEverCleared()
    {
        Plan(new Dictionary<int, uint> { [1] = Gold }, new Dictionary<int, uint> { [2] = Gold });

        CollectionAssert.AreEqual(new[] { 1 }, _clear);
        CollectionAssert.AreEqual(new[] { 2 }, _paint.ConvertAll(p => p.Key));
    }

    [TestMethod]
    public void Plan_ReusedLists_AreEmptiedFirst()
    {
        _paint.Add(new KeyValuePair<int, uint>(9, Gold));
        _clear.Add(9);

        Plan(new Dictionary<int, uint>(), new Dictionary<int, uint>());

        Assert.AreEqual(0, _paint.Count);
        Assert.AreEqual(0, _clear.Count);
    }
}

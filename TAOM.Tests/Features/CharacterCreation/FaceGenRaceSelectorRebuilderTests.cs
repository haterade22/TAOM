using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class FaceGenRaceSelectorRebuilderTests
{
    [TestMethod]
    public void BuildGlobalIndexMap_AllowedSubset_ReturnsCorrectGlobalIndices()
    {
        var allRaces = new[] { "human", "dwarf", "uruk", "elf", "orc" };
        var allowed = new[] { "elf", "human" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        CollectionAssert.AreEqual(new[] { 0, 3 }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void BuildGlobalIndexMap_PreservesEngineOrder_NotAllowedOrder()
    {
        var allRaces = new[] { "human", "dwarf", "uruk", "elf" };
        var allowed = new[] { "elf", "human", "dwarf" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        CollectionAssert.AreEqual(new[] { 0, 1, 3 }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void BuildGlobalIndexMap_CaseInsensitive()
    {
        var allRaces = new[] { "human", "DWARF", "uruk" };
        var allowed = new[] { "dwarf", "URUK" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        CollectionAssert.AreEqual(new[] { 1, 2 }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void BuildGlobalIndexMap_RaceNotInEngine_Skipped()
    {
        var allRaces = new[] { "human", "dwarf" };
        var allowed = new[] { "dwarf", "elf" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        CollectionAssert.AreEqual(new[] { 1 }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void BuildGlobalIndexMap_NullInputs_ReturnsEmpty()
    {
        Assert.AreEqual(0, FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(null, new[] { "x" }).Count);
        Assert.AreEqual(0, FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(new[] { "x" }, null).Count);
        Assert.AreEqual(0, FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(null, null).Count);
    }

    [TestMethod]
    public void BuildGlobalIndexMap_EmptyAllowed_ReturnsEmpty()
    {
        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(
            new[] { "human", "dwarf" }, new string[0]);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void MapFilteredIndexToGlobal_ValidIndex_ReturnsGlobal()
    {
        IReadOnlyList<int> map = new[] { 0, 3, 5 };

        Assert.AreEqual(0, FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(0, map));
        Assert.AreEqual(3, FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(1, map));
        Assert.AreEqual(5, FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(2, map));
    }

    [TestMethod]
    public void MapFilteredIndexToGlobal_OutOfRange_ReturnsMinusOne()
    {
        IReadOnlyList<int> map = new[] { 0, 3, 5 };

        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(-1, map));
        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(3, map));
        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(99, map));
    }

    [TestMethod]
    public void MapFilteredIndexToGlobal_NullMap_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(0, null));
    }

    [TestMethod]
    public void MapGlobalIndexToFiltered_PresentInMap_ReturnsFilteredPosition()
    {
        IReadOnlyList<int> map = new[] { 0, 3, 5 };

        Assert.AreEqual(0, FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(0, map));
        Assert.AreEqual(1, FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(3, map));
        Assert.AreEqual(2, FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(5, map));
    }

    [TestMethod]
    public void MapGlobalIndexToFiltered_NotInMap_ReturnsMinusOne()
    {
        IReadOnlyList<int> map = new[] { 0, 3, 5 };

        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(1, map));
        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(2, map));
        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(99, map));
    }

    [TestMethod]
    public void MapGlobalIndexToFiltered_NullMap_ReturnsMinusOne()
    {
        Assert.AreEqual(-1, FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(0, null));
    }

    [TestMethod]
    public void RoundTrip_FilteredToGlobalAndBack_IsIdentity()
    {
        var allRaces = new[] { "human", "dwarf", "uruk", "elf", "orc" };
        var allowed = new[] { "elf", "human", "uruk" };
        var map = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        for (int filtered = 0; filtered < map.Count; filtered++)
        {
            var global = FaceGenRaceSelectorRebuilder.MapFilteredIndexToGlobal(filtered, map);
            var roundTrip = FaceGenRaceSelectorRebuilder.MapGlobalIndexToFiltered(global, map);
            Assert.AreEqual(filtered, roundTrip, $"Round-trip failed for filtered index {filtered}");
        }
    }
}

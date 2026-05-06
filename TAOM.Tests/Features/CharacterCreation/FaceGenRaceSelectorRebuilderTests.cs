using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class FaceGenRaceSelectorRebuilderTests
{
    [TestMethod]
    public void BuildGlobalIndexMap_AllowedSubset_PreservesAllowedOrder()
    {
        var allRaces = new[] { "human", "dwarf", "uruk", "elf", "orc" };
        var allowed = new[] { "elf", "human" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        // elf (engine idx 3) first, human (engine idx 0) second — matches cultures.json order,
        // NOT engine order (which would put human at index 0 first).
        CollectionAssert.AreEqual(new[] { 3, 0 }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void BuildGlobalIndexMap_PreservesAllowedOrder_NotEngineOrder()
    {
        var allRaces = new[] { "human", "dwarf", "uruk", "elf" };
        var allowed = new[] { "elf", "human", "dwarf" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        // Result follows the allow-list (config) order, not engine order
        CollectionAssert.AreEqual(new[] { 3, 0, 1 }, (System.Collections.ICollection)result);
    }

    [TestMethod]
    public void BuildGlobalIndexMap_Mordor_UrukFirstNotHuman()
    {
        // Regression: in the first shipped version, BuildGlobalIndexMap iterated allRaces and
        // added entries when present in `allowed`. Engine puts `human` at index 0, so the result
        // was `[0, ...]` and the dropdown showed human FIRST despite cultures.json listing uruk
        // first. The fix iterates the allow-list (config order) and resolves each name to its
        // engine index, preserving the user's intended default-race-per-culture.
        var allRaces = new[] { "human", "dwarf", "uruk", "orc", "elf", "goblin" };
        var allowed = new[] { "uruk", "orc", "human" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        CollectionAssert.AreEqual(new[] { 2, 3, 0 }, (System.Collections.ICollection)result);
        Assert.AreEqual(2, result[0],
            "First filtered position must be uruk (engine idx 2), not human (idx 0)");
    }

    [TestMethod]
    public void BuildGlobalIndexMap_Isengard_UrukHaiBerserkerHumanInThatOrder()
    {
        // Sister regression: Isengard's allow-list is ["uruk_hai", "berserker", "human"].
        // Engine ordering puts human at 0; a naive intersect would surface human first.
        var allRaces = new[] { "human", "dwarf", "uruk", "uruk_hai", "berserker", "orc" };
        var allowed = new[] { "uruk_hai", "berserker", "human" };

        var result = FaceGenRaceSelectorRebuilder.BuildGlobalIndexMap(allRaces, allowed);

        CollectionAssert.AreEqual(new[] { 3, 4, 0 }, (System.Collections.ICollection)result);
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
    public void ShouldForceSwitchToDefault_CurrentRaceNotAllowed_AlwaysSwitches()
    {
        // filteredIdx == -1 means the player's current global race isn't in the allow-list
        // (e.g., switched culture from Mordor[uruk] to Erebor[dwarf] with uruk still selected)
        Assert.IsTrue(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(-1, firstApplyForThisCulture: false));
        Assert.IsTrue(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(-1, firstApplyForThisCulture: true));
    }

    [TestMethod]
    public void ShouldForceSwitchToDefault_FirstApply_NonDefaultRace_Switches()
    {
        // Regression: Isengard opens with _selectedRace = 0 (engine default = human).
        // Allow-list = [uruk_hai, berserker, human]. MapGlobalIndexToFiltered returns 2
        // (human's filtered position). On the first Apply for Isengard we want to snap
        // to filtered position 0 (uruk_hai), not preserve human.
        Assert.IsTrue(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(
            currentFilteredIdx: 2, firstApplyForThisCulture: true));
        Assert.IsTrue(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(
            currentFilteredIdx: 1, firstApplyForThisCulture: true));
    }

    [TestMethod]
    public void ShouldForceSwitchToDefault_FirstApply_AlreadyDefault_NoSwitch()
    {
        // First Apply but the player is already on filtered position 0 (Races[0]).
        // No switch needed — avoids a wasteful recursive Refresh.
        Assert.IsFalse(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(
            currentFilteredIdx: 0, firstApplyForThisCulture: true));
    }

    [TestMethod]
    public void ShouldForceSwitchToDefault_SubsequentApply_PreservesPlayerChoice()
    {
        // After the first Apply, the player has explicitly engaged the dropdown (or it
        // was force-switched). Subsequent Apply calls (e.g., gender change → Refresh(true))
        // must NOT override the player's selected race.
        Assert.IsFalse(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(
            currentFilteredIdx: 0, firstApplyForThisCulture: false));
        Assert.IsFalse(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(
            currentFilteredIdx: 1, firstApplyForThisCulture: false));
        Assert.IsFalse(FaceGenRaceSelectorRebuilder.ShouldForceSwitchToDefault(
            currentFilteredIdx: 2, firstApplyForThisCulture: false));
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

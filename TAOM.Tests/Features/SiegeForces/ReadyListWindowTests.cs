using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;
using TAOM.Adapters;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The window's index arithmetic over the engine's side-wide ready list: the entries one party appended start at
/// <c>from</c>, so a removal at window index i is a removal at <c>from + i</c> and can never reach another party's
/// entries. Built over an uninitialised <c>MapEventParty</c> (its constructor needs a campaign) and default list
/// elements, whose <c>Troop</c> is null, so the id and player reads are the null-troop answers; the real reads of
/// <c>PartyBase.Id</c>, <c>PartyBase.Side</c>, <c>CharacterObject.StringId</c> and <c>IsPlayerCharacter</c> need a live
/// campaign (<c>IsPlayerCharacter</c> reads <c>Game.Current</c>) and are pinned by SiegeForcesBindingTests.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ReadyListWindowTests
{
    private static MapEventParty Party() => (MapEventParty)FormatterServices.GetUninitializedObject(typeof(MapEventParty));

    // Each entry's priority float is its original position, so which entries remain says which were removed.
    private static List<(FlattenedTroopRosterElement, MapEventParty, float)> ListOf(int count, MapEventParty party) =>
        Enumerable.Range(0, count).Select(i => (default(FlattenedTroopRosterElement), party, (float)i)).ToList();

    private static float[] Positions(List<(FlattenedTroopRosterElement, MapEventParty, float)> list) =>
        list.Select(e => e.Item3).ToArray();

    [TestMethod]
    public void Count_IsTheLengthSinceTheStartOfTheWindow()
    {
        var party = Party();
        var list = ListOf(7, party);

        var window = new ReadyListWindow(party, list, from: 3);

        Assert.AreEqual(4, window.Count);
        Assert.AreEqual(7, window.ListCount, "the whole side's list, other parties' entries included");
    }

    [TestMethod]
    public void Count_FallsByOnePerRemoval_AndListCountWithIt()
    {
        var party = Party();
        var list = ListOf(7, party);
        var window = new ReadyListWindow(party, list, from: 3);

        window.RemoveAt(0);

        Assert.AreEqual(3, window.Count);
        Assert.AreEqual(6, window.ListCount);
    }

    [TestMethod]
    public void RemoveAt_RemovesTheEntryAtFromPlusIndex_AndNothingBefore()
    {
        var party = Party();
        var list = ListOf(6, party);
        var window = new ReadyListWindow(party, list, from: 2);

        window.RemoveAt(1);   // list index 3

        CollectionAssert.AreEqual(new[] { 0f, 1f, 2f, 4f, 5f }, Positions(list));
    }

    [TestMethod]
    public void RemoveAt_InDescendingOrder_RemovesEachEntryTheCallerNamed()
    {
        var party = Party();
        var list = ListOf(8, party);
        var window = new ReadyListWindow(party, list, from: 2);

        foreach (var index in new[] { 5, 3, 0 })
            window.RemoveAt(index);

        // Window indices 5, 3, 0 are list indices 7, 5, 2 (from = 2): 0 1 [2] 3 4 [5] 6 [7] leaves 0 1 3 4 6.
        CollectionAssert.AreEqual(new[] { 0f, 1f, 3f, 4f, 6f }, Positions(list));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(4)]
    [DataRow(100)]
    public void RemoveAt_OutsideTheWindow_Throws_AndTouchesNothing(int index)
    {
        var party = Party();
        var list = ListOf(7, party);
        var window = new ReadyListWindow(party, list, from: 3);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => window.RemoveAt(index));

        Assert.AreEqual(7, list.Count);
    }

    [TestMethod]
    public void RemoveAt_AnIndexBelowTheWindowStart_NeverReachesAnotherPartysEntry()
    {
        // from + (-1) would be the previous party's last entry: the bounds check must stop it before the offset applies.
        var party = Party();
        var list = ListOf(7, party);
        var window = new ReadyListWindow(party, list, from: 3);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => window.RemoveAt(-1));

        CollectionAssert.AreEqual(new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f }, Positions(list));
    }

    [TestMethod]
    public void CharacterIdAt_AnEntryWithNoTroop_IsNull()
    {
        var party = Party();
        var window = new ReadyListWindow(party, ListOf(3, party), from: 1);

        Assert.IsNull(window.CharacterIdAt(0));
        Assert.IsFalse(window.IsPlayerCharacterAt(1));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    public void CharacterIdAt_OutsideTheWindow_Throws(int index)
    {
        var party = Party();
        var window = new ReadyListWindow(party, ListOf(3, party), from: 1);

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => window.CharacterIdAt(index));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => window.IsPlayerCharacterAt(index));
    }

    [TestMethod]
    public void AWindowAtTheEndOfTheList_IsEmpty_WhenTheBaseModelAppendedNothing()
    {
        // A party with no healthy troops appends nothing: from equals the list's length.
        var party = Party();
        var list = ListOf(4, party);

        var window = new ReadyListWindow(party, list, from: 4);

        Assert.AreEqual(0, window.Count);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => window.RemoveAt(0));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(5)]
    public void Constructor_AStartOutsideTheList_Throws(int from)
    {
        var party = Party();

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ReadyListWindow(party, ListOf(4, party), from));
    }

    [TestMethod]
    public void Constructor_WithoutAPartyOrAList_Throws()
    {
        var party = Party();

        Assert.ThrowsException<ArgumentNullException>(() => new ReadyListWindow(null!, ListOf(1, party), 0));
        Assert.ThrowsException<ArgumentNullException>(() => new ReadyListWindow(party, null!, 0));
    }
}

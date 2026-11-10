using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;
using TAOM.Features.WarPledge;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// #1113: the saved pledges of a campaign. The store is a process singleton, so the reset and the load
/// paths are pinned here and the behavior's use of them in <see cref="WarPledgeBehaviorSessionResetTests"/>.
/// </summary>
[TestClass]
public class WarPledgeStoreTests
{
    private WarPledgeStore _sut = null!;

    [TestInitialize]
    public void SetUp() => _sut = new WarPledgeStore();

    [TestMethod]
    public void TryGetSide_NoPledge_ReturnsFalse()
    {
        Assert.IsFalse(_sut.TryGetSide("cardolan", out _));
        Assert.IsFalse(_sut.IsPledged("cardolan"));
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Pledge_FreeOrEvil_IsReadBack()
    {
        Assert.IsTrue(_sut.Pledge("cardolan", FactionSide.Free));
        Assert.IsTrue(_sut.Pledge("rhudaur", FactionSide.Evil));

        Assert.IsTrue(_sut.TryGetSide("cardolan", out var cardolan));
        Assert.AreEqual(FactionSide.Free, cardolan);
        Assert.IsTrue(_sut.TryGetSide("rhudaur", out var rhudaur));
        Assert.AreEqual(FactionSide.Evil, rhudaur);
        Assert.AreEqual(2, _sut.Count);
    }

    // FactionSide's zero value is Free, so a miss must set Neutral by hand.
    [TestMethod]
    public void TryGetSide_AnotherRealmPledged_GivesNeutralForTheMiss()
    {
        _sut.Pledge("rhudaur", FactionSide.Evil);

        Assert.IsFalse(_sut.TryGetSide("cardolan", out var side));
        Assert.AreEqual(FactionSide.Neutral, side);
    }

    // AlignmentService compares ids without case, so the override must too.
    [TestMethod]
    public void TryGetSide_OtherCase_FindsThePledge()
    {
        _sut.Pledge("cardolan", FactionSide.Free);

        Assert.IsTrue(_sut.TryGetSide("Cardolan", out var side));
        Assert.AreEqual(FactionSide.Free, side);
    }

    [TestMethod]
    public void Pledge_Neutral_IsRefused()
    {
        Assert.IsFalse(_sut.Pledge("cardolan", FactionSide.Neutral));
        Assert.IsFalse(_sut.IsPledged("cardolan"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void Pledge_BlankId_IsRefused(string id)
    {
        Assert.IsFalse(_sut.Pledge(id, FactionSide.Free));
        Assert.AreEqual(0, _sut.Count);
    }

    // A pledge is for good: the first one stands.
    [TestMethod]
    public void Pledge_AlreadyPledged_IsRefusedAndKeepsTheFirstSide()
    {
        _sut.Pledge("cardolan", FactionSide.Free);

        Assert.IsFalse(_sut.Pledge("cardolan", FactionSide.Evil));
        _sut.TryGetSide("cardolan", out var side);
        Assert.AreEqual(FactionSide.Free, side);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TryGetSide_BlankId_ReturnsFalse(string id)
    {
        _sut.Pledge("cardolan", FactionSide.Free);

        Assert.IsFalse(_sut.TryGetSide(id, out _));
    }

    [TestMethod]
    public void Snapshot_ListsEveryPledgeSortedById()
    {
        _sut.Pledge("rhudaur", FactionSide.Evil);
        _sut.Pledge("cardolan", FactionSide.Free);

        var rows = _sut.Snapshot();

        CollectionAssert.AreEqual(new[] { "cardolan", "rhudaur" }, rows.Select(r => r.Key).ToList());
        Assert.AreEqual(FactionSide.Free, rows[0].Value);
        Assert.AreEqual(FactionSide.Evil, rows[1].Value);
    }

    [TestMethod]
    public void ResetForNewSession_ClearsThePledgesAndTheCourtingDay()
    {
        _sut.Pledge("cardolan", FactionSide.Free);
        _sut.NextCourtingDay = 90;

        _sut.ResetForNewSession();

        Assert.AreEqual(0, _sut.Count);
        Assert.IsFalse(_sut.IsPledged("cardolan"));
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _sut.NextCourtingDay);
    }

    [TestMethod]
    public void CaptureRows_ThenLoadFrom_RestoresEveryPledge()
    {
        _sut.Pledge("cardolan", FactionSide.Free);
        _sut.Pledge("rhudaur", FactionSide.Evil);
        var rows = _sut.CaptureRows();
        var loaded = new WarPledgeStore();

        loaded.LoadFrom(rows, 74);

        Assert.AreEqual(2, loaded.Count);
        loaded.TryGetSide("cardolan", out var cardolan);
        loaded.TryGetSide("rhudaur", out var rhudaur);
        Assert.AreEqual(FactionSide.Free, cardolan);
        Assert.AreEqual(FactionSide.Evil, rhudaur);
        Assert.AreEqual(74, loaded.NextCourtingDay);
    }

    [TestMethod]
    public void CaptureRows_NoPledge_IsEmptyNotNull()
    {
        Assert.AreEqual(string.Empty, _sut.CaptureRows());
    }

    // The store outlives the campaign: a load replaces what the process held, it never merges.
    [TestMethod]
    public void LoadFrom_ReplacesWhatTheStoreHeld()
    {
        _sut.Pledge("dorwinion", FactionSide.Free);
        _sut.NextCourtingDay = 500;

        _sut.LoadFrom("rhudaur=Evil", 60);

        Assert.IsFalse(_sut.IsPledged("dorwinion"));
        Assert.IsTrue(_sut.IsPledged("rhudaur"));
        Assert.AreEqual(60, _sut.NextCourtingDay);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void LoadFrom_NoRows_LoadsNoPledge(string rows)
    {
        _sut.Pledge("cardolan", FactionSide.Free);

        _sut.LoadFrom(rows, WarPledgeStore.NoCourtingDay);

        Assert.AreEqual(0, _sut.Count);
    }

    // One bad row is skipped alone; the rows beside it load.
    [DataTestMethod]
    [DataRow("cardolan=Free;=Evil;rhudaur=Evil", DisplayName = "blank id")]
    [DataRow("cardolan=Free;umbar=Sideways;rhudaur=Evil", DisplayName = "unknown side")]
    [DataRow("cardolan=Free;umbar=Neutral;rhudaur=Evil", DisplayName = "neutral is no pledge")]
    [DataRow("cardolan=Free;umbar;rhudaur=Evil", DisplayName = "no separator")]
    [DataRow("cardolan=Free;umbar=7;rhudaur=Evil", DisplayName = "a number is not a side name")]
    [DataRow("cardolan=Free;;rhudaur=Evil;", DisplayName = "empty rows")]
    public void LoadFrom_ABadRow_IsSkippedAlone(string rows)
    {
        _sut.LoadFrom(rows, 10);

        Assert.AreEqual(2, _sut.Count, rows);
        Assert.IsTrue(_sut.IsPledged("cardolan"));
        Assert.IsTrue(_sut.IsPledged("rhudaur"));
        Assert.IsFalse(_sut.IsPledged("umbar"));
    }

    [TestMethod]
    public void LoadFrom_ADuplicateId_KeepsTheFirstRow()
    {
        _sut.LoadFrom("cardolan=Free;cardolan=Evil", 10);

        _sut.TryGetSide("cardolan", out var side);
        Assert.AreEqual(FactionSide.Free, side);
        Assert.AreEqual(1, _sut.Count);
    }

    // ---- threads: the party AI worker reads a side or a tier; the main thread writes a pledge ----
    // A published map is never changed: every write swaps in a new one, so a console command that ran while
    // the worker reads could not break a reader. The field is found by its type, so a rename does not break
    // these tests; a write in place does.

    private static FieldInfo MapField() =>
        typeof(WarPledgeStore).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => f.FieldType == typeof(Dictionary<string, FactionSide>));

    private static Dictionary<string, FactionSide> PublishedMap(WarPledgeStore store) =>
        (Dictionary<string, FactionSide>)MapField().GetValue(store)!;

    [TestMethod]
    public void MapField_IsVolatile_SoAReaderSeesTheSwap()
    {
        CollectionAssert.Contains(MapField().GetRequiredCustomModifiers(), typeof(IsVolatile));
    }

    [TestMethod]
    public void Pledge_SwapsInANewMap_AndLeavesThePublishedOneUnchanged()
    {
        _sut.Pledge("cardolan", FactionSide.Free);
        var published = PublishedMap(_sut);

        _sut.Pledge("rhudaur", FactionSide.Evil);

        Assert.AreNotSame(published, PublishedMap(_sut));
        Assert.AreEqual(1, published.Count, "a reader that holds the old map must not see it grow");
        Assert.IsFalse(published.ContainsKey("rhudaur"));
    }

    [TestMethod]
    public void Pledge_Refused_KeepsThePublishedMap()
    {
        _sut.Pledge("cardolan", FactionSide.Free);
        var published = PublishedMap(_sut);

        _sut.Pledge("cardolan", FactionSide.Evil);

        Assert.AreSame(published, PublishedMap(_sut));
    }

    [TestMethod]
    public void ResetForNewSession_SwapsTheMap_AndLeavesThePublishedOneUnchanged()
    {
        _sut.Pledge("cardolan", FactionSide.Free);
        var published = PublishedMap(_sut);

        _sut.ResetForNewSession();

        Assert.AreNotSame(published, PublishedMap(_sut));
        Assert.AreEqual(1, published.Count, "a clear in place would empty the map under a reader");
    }

    [TestMethod]
    public void LoadFrom_SwapsTheMapOnce_AndLeavesThePublishedOneUnchanged()
    {
        _sut.Pledge("dorwinion", FactionSide.Free);
        var published = PublishedMap(_sut);

        _sut.LoadFrom("cardolan=Free;rhudaur=Evil", 60);

        Assert.AreNotSame(published, PublishedMap(_sut));
        Assert.AreEqual(1, published.Count);
        Assert.IsTrue(published.ContainsKey("dorwinion"));
        Assert.AreEqual(2, PublishedMap(_sut).Count);
    }

    // The empty map is shared by every store and every reset, so nothing may ever be added to it.
    [TestMethod]
    public void Pledge_OnAFreshOrResetStore_NeverWritesIntoTheSharedEmptyMap()
    {
        var empty = PublishedMap(new WarPledgeStore());

        _sut.Pledge("cardolan", FactionSide.Free);
        _sut.ResetForNewSession();
        _sut.Pledge("rhudaur", FactionSide.Evil);
        _sut.LoadFrom(string.Empty, WarPledgeStore.NoCourtingDay);

        Assert.AreEqual(0, empty.Count);
        Assert.AreEqual(0, new WarPledgeStore().Count);
    }

    // A day below the "none" mark is a broken save value: it reads as none, so the anchor is set again.
    [DataTestMethod]
    [DataRow(-2)]
    [DataRow(int.MinValue)]
    public void LoadFrom_ACourtingDayBelowNone_ReadsAsNone(int day)
    {
        _sut.LoadFrom(string.Empty, day);

        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _sut.NextCourtingDay);
    }
}

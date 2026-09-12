using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FiefGranting;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// #565: the fief election's participation record. Every clan with a party in the winning assault
/// is remembered with its share of the side's contribution, the top contributor at 1.0 and the rest
/// relative to it. The record is pure over primitives so it is testable without the engine; the
/// campaign behavior converts MapEvent parties to (clanId, contribution) at the boundary.
/// </summary>
[TestClass]
public class FiefSiegeParticipationServiceTests
{
    private IModLogger _logger = null!;
    private FiefSiegeParticipationService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new FiefSiegeParticipationService(_logger);
    }

    private static List<KeyValuePair<string, int>> Parties(params (string clan, int contribution)[] rows)
    {
        var list = new List<KeyValuePair<string, int>>();
        foreach (var (clan, contribution) in rows)
            list.Add(new KeyValuePair<string, int>(clan, contribution));
        return list;
    }

    // ---------------------------------------------------------------- recording

    [TestMethod]
    public void RecordAssault_TopContributorHasShareOne_OthersRelativeToIt()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_lead", 40), ("clan_member", 10)));

        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_lead"), 0.0001f);
        Assert.AreEqual(0.25f, _sut.GetContributionShare("castle_a", "clan_member"), 0.0001f);
    }

    [TestMethod]
    public void RecordAssault_SumsSeveralPartiesOfOneClan()
    {
        // A clan fielding two parties is one claimant: its share is the sum, not the larger party.
        _sut.RecordAssault("castle_a", Parties(("clan_two", 30), ("clan_two", 20), ("clan_one", 50)));

        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_two"), 0.0001f);
        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_one"), 0.0001f);
    }

    [TestMethod]
    public void RecordAssault_IgnoresZeroAndNegativeContributions()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_zero", 0), ("clan_neg", -5), ("clan_real", 10)));

        Assert.IsTrue(_sut.HasRecord("castle_a"));
        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_real"), 0.0001f);
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_zero"));
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_neg"));
    }

    [TestMethod]
    public void RecordAssault_IgnoresNullOrEmptyClanIds()
    {
        _sut.RecordAssault("castle_a", Parties((null!, 40), ("", 40), ("clan_real", 10)));

        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_real"), 0.0001f);
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", ""));
    }

    [TestMethod]
    public void RecordAssault_WithNoPositiveContribution_LeavesNoRecord()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_zero", 0)));

        Assert.IsFalse(_sut.HasRecord("castle_a"));
    }

    [TestMethod]
    public void RecordAssault_WithNoParties_LeavesNoRecord()
    {
        _sut.RecordAssault("castle_a", Parties());
        _sut.RecordAssault("castle_b", null!);

        Assert.IsFalse(_sut.HasRecord("castle_a"));
        Assert.IsFalse(_sut.HasRecord("castle_b"));
    }

    [TestMethod]
    public void RecordAssault_ReplacesAnEarlierRecordForTheSameSettlement()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_first", 10)));

        _sut.RecordAssault("castle_a", Parties(("clan_second", 10)));

        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_first"));
        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_second"), 0.0001f);
    }

    [TestMethod]
    public void RecordAssault_ReplacingWithNothingPositive_DropsTheOldRecord()
    {
        // A second assault whose every party contributed nothing must not leave the first assault's
        // participants claiming a fief nobody fought for this time.
        _sut.RecordAssault("castle_a", Parties(("clan_first", 10)));

        _sut.RecordAssault("castle_a", Parties(("clan_second", 0)));

        Assert.IsFalse(_sut.HasRecord("castle_a"));
    }

    [TestMethod]
    public void RecordAssault_LeavesOtherSettlementsUntouched()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 10)));

        _sut.RecordAssault("castle_b", Parties(("clan_b", 10)));

        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_a"), 0.0001f);
        Assert.AreEqual(1f, _sut.GetContributionShare("castle_b", "clan_b"), 0.0001f);
    }

    [TestMethod]
    public void RecordAssault_SkipsAClanIdThatWouldBreakTheSaveEncoding()
    {
        // The snapshot is "clanId=contribution;clanId=contribution". An id carrying either separator
        // could not be restored, so it is dropped at record time with a warning rather than saved.
        _sut.RecordAssault("castle_a", Parties(("bad=id", 40), ("bad;id", 40), ("clan_real", 10)));

        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_real"), 0.0001f);
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "bad=id"));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void RecordAssault_WithNullSettlementId_IsIgnored()
    {
        _sut.RecordAssault(null!, Parties(("clan_a", 10)));

        Assert.IsFalse(_sut.HasRecord(null!));
        Assert.AreEqual(0f, _sut.GetContributionShare(null!, "clan_a"));
    }

    // ---------------------------------------------------------------- lookup

    [TestMethod]
    public void GetContributionShare_WithoutRecord_IsZero()
    {
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_a"));
        Assert.IsFalse(_sut.HasRecord("castle_a"));
    }

    [TestMethod]
    public void GetContributionShare_ForAClanAbsentFromTheAssault_IsZero()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 10)));

        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_absent"));
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", null!));
    }

    [TestMethod]
    public void GetContributionShare_IsNeverAboveOne()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", int.MaxValue), ("clan_a", int.MaxValue), ("clan_b", 1)));

        Assert.IsTrue(_sut.GetContributionShare("castle_a", "clan_a") <= 1f);
        Assert.IsTrue(_sut.GetContributionShare("castle_a", "clan_b") > 0f);
    }

    // ---------------------------------------------------------------- forget

    [TestMethod]
    public void Forget_DropsTheRecord()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 10)));

        _sut.Forget("castle_a");

        Assert.IsFalse(_sut.HasRecord("castle_a"));
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_a"));
    }

    [TestMethod]
    public void Forget_UnknownOrNullSettlement_DoesNothing()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 10)));

        _sut.Forget("castle_unknown");
        _sut.Forget(null!);

        Assert.IsTrue(_sut.HasRecord("castle_a"));
    }

    // ---------------------------------------------------------------- save round trip

    [TestMethod]
    public void SnapshotForSave_EncodesClansSortedById()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_b", 10), ("clan_a", 40), ("clan_a", 2)));

        var snapshot = _sut.SnapshotForSave();

        Assert.AreEqual(1, snapshot.Count);
        Assert.AreEqual("clan_a=42;clan_b=10", snapshot["castle_a"]);
    }

    [TestMethod]
    public void SnapshotForSave_ThenRestore_PreservesEveryShare()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 40), ("clan_b", 10), ("clan_c", 25)));
        _sut.RecordAssault("town_b", Parties(("clan_d", 7)));
        var snapshot = _sut.SnapshotForSave();

        var restored = new FiefSiegeParticipationService(_logger);
        restored.RestoreFromSave(snapshot);

        Assert.AreEqual(1f, restored.GetContributionShare("castle_a", "clan_a"), 0.0001f);
        Assert.AreEqual(0.25f, restored.GetContributionShare("castle_a", "clan_b"), 0.0001f);
        Assert.AreEqual(0.625f, restored.GetContributionShare("castle_a", "clan_c"), 0.0001f);
        Assert.AreEqual(1f, restored.GetContributionShare("town_b", "clan_d"), 0.0001f);
        Assert.AreEqual(0f, restored.GetContributionShare("castle_a", "clan_d"));
    }

    [TestMethod]
    public void SnapshotForSave_IsACopy()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 10)));

        var snapshot = _sut.SnapshotForSave();
        snapshot["castle_a"] = "clan_z=1";

        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_a"), 0.0001f);
    }

    [TestMethod]
    public void RestoreFromSave_Null_LeavesTheServiceEmpty()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 10)));

        _sut.RestoreFromSave(null!);

        Assert.IsFalse(_sut.HasRecord("castle_a"));
    }

    [TestMethod]
    public void RestoreFromSave_ReplacesWhateverWasHeldBefore()
    {
        // A loaded save must never merge with the previous campaign's record.
        _sut.RecordAssault("castle_old", Parties(("clan_old", 10)));

        _sut.RestoreFromSave(new Dictionary<string, string> { ["castle_new"] = "clan_new=5" });

        Assert.IsFalse(_sut.HasRecord("castle_old"));
        Assert.AreEqual(1f, _sut.GetContributionShare("castle_new", "clan_new"), 0.0001f);
    }

    [TestMethod]
    public void RestoreFromSave_SkipsMalformedPartsAndKeepsTheRest()
    {
        _sut.RestoreFromSave(new Dictionary<string, string>
        {
            ["castle_a"] = "clan_a=40;garbage;clan_b=notanumber;=7;clan_c=-3;clan_d=10",
            ["castle_b"] = "",
            ["castle_c"] = null!,
        });

        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_a"), 0.0001f);
        Assert.AreEqual(0.25f, _sut.GetContributionShare("castle_a", "clan_d"), 0.0001f);
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_b"));
        Assert.AreEqual(0f, _sut.GetContributionShare("castle_a", "clan_c"));
        Assert.IsFalse(_sut.HasRecord("castle_b"));
        Assert.IsFalse(_sut.HasRecord("castle_c"));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void RestoreFromSave_DuplicateClanId_KeepsTheFirstAndSkipsTheRest()
    {
        // Codex F3 (#565): the writer never emits a duplicate id, but a hand-edited string can, and
        // summing two longs unchecked wrapped A to long.MinValue, a negative share the policy then
        // scored as absent. A duplicate is malformed: first occurrence wins, the rest are skipped.
        _sut.RestoreFromSave(new Dictionary<string, string>
        {
            ["castle_a"] = "clan_a=9223372036854775807;clan_a=1;clan_b=1",
        });

        Assert.IsTrue(_sut.HasRecord("castle_a"));
        Assert.AreEqual(1f, _sut.GetContributionShare("castle_a", "clan_a"), 0.0001f);
        Assert.IsTrue(_sut.GetContributionShare("castle_a", "clan_b") > 0f);
        Assert.IsTrue(_sut.GetContributionShare("castle_a", "clan_b") < 1f);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void RestoreFromSave_WellFormedSnapshot_DoesNotWarn()
    {
        _sut.RestoreFromSave(new Dictionary<string, string> { ["castle_a"] = "clan_a=40;clan_b=10" });

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---------------------------------------------------------------- session reset

    [TestMethod]
    public void ResetForNewSession_ClearsEverything()
    {
        _sut.RecordAssault("castle_a", Parties(("clan_a", 10)));
        _sut.RecordAssault("castle_b", Parties(("clan_b", 10)));

        _sut.ResetForNewSession();

        Assert.IsFalse(_sut.HasRecord("castle_a"));
        Assert.IsFalse(_sut.HasRecord("castle_b"));
        Assert.AreEqual(0, _sut.SnapshotForSave().Count);
    }
}

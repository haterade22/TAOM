using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion;
using TAOM.Features.CultureConversion.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.CultureConversion;

[TestClass]
public class CultureConversionServiceTests
{
    private const string Town = "town_ES1";

    private ICultureConversionAdapter _adapter = null!;
    private ICultureConversionSettingsProvider _settings = null!;
    private IVolunteerRecruitmentService _recruitment = null!;
    private IModLogger _logger = null!;
    private CultureConversionStore _store = null!;
    private CultureConversionService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ICultureConversionAdapter>();
        _settings = Substitute.For<ICultureConversionSettingsProvider>();
        _recruitment = Substitute.For<IVolunteerRecruitmentService>();
        _logger = Substitute.For<IModLogger>();
        _store = new CultureConversionStore(_logger);
        _sut = new CultureConversionService(_store, _adapter, _settings, _recruitment, _logger);

        // Sensible defaults: feature on, 45-day hold, no loyalty gate, convert everyone, all cultures recruitable.
        _settings.IsEnabled.Returns(true);
        _settings.RequiredHoldDays.Returns(45);
        _settings.RequireStableLoyalty.Returns(false);
        _settings.MinLoyaltyToConvert.Returns(50f);
        _settings.ConvertPlayerOwnedSettlements.Returns(true);
        _recruitment.HasCulturePool(Arg.Any<string>()).Returns(true);
        _adapter.GetBoundVillageSettlementIds(Arg.Any<string>()).Returns(new List<string>());
        _adapter.IsFortification(Arg.Any<string>()).Returns(true);
        _adapter.IsPlayerOwned(Arg.Any<string>()).Returns(false);
        _adapter.SetSettlementCulture(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    // Gondor town conquered by a Mordor clan.
    private void GivenCrossCultureConquest(string original = "gondor", string owner = "mordor")
    {
        _adapter.GetCurrentCultureId(Town).Returns(original);
        _adapter.GetOwnerCultureId(Town).Returns(owner);
    }

    // --- OnSettlementConquered: queueing ---

    [TestMethod]
    public void OnSettlementConquered_CrossCulture_QueuesPendingTowardOwnerCulture()
    {
        GivenCrossCultureConquest();

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.IsTrue(_store.TryGet(Town, out var record));
        Assert.IsTrue(record.HasPending);
        Assert.AreEqual("mordor", record.PendingTargetCultureId);
        Assert.AreEqual(100.0, record.PendingStartDays.Value, 0.0001);
        Assert.AreEqual("gondor", record.OriginalCultureId);
        Assert.IsFalse(record.IsConverted, "Queuing must not apply the override yet (gradual).");
    }

    [TestMethod]
    public void OnSettlementConquered_SameCulture_DoesNotQueue()
    {
        GivenCrossCultureConquest(original: "gondor", owner: "gondor");

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.AreEqual(0, _store.Count);
    }

    [TestMethod]
    public void OnSettlementConquered_NotFortification_NoOp()
    {
        _adapter.IsFortification(Town).Returns(false);
        GivenCrossCultureConquest();

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.AreEqual(0, _store.Count);
    }

    [TestMethod]
    public void OnSettlementConquered_NullOwnerCulture_NoOp()
    {
        _adapter.GetCurrentCultureId(Town).Returns("gondor");
        _adapter.GetOwnerCultureId(Town).Returns((string)null);

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.AreEqual(0, _store.Count);
    }

    [TestMethod]
    public void OnSettlementConquered_TargetCultureHasNoRecruitmentPool_DoesNotQueue()
    {
        GivenCrossCultureConquest(original: "gondor", owner: "looters");
        _recruitment.HasCulturePool("looters").Returns(false);

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.AreEqual(0, _store.Count);
    }

    [TestMethod]
    public void OnSettlementConquered_OwnerCultureContainsPipeDelimiter_DoesNotQueue()
    {
        // The save format is '|'-delimited; an id containing '|' would corrupt a stored record. Reject it.
        _adapter.GetCurrentCultureId(Town).Returns("gondor");
        _adapter.GetOwnerCultureId(Town).Returns("mor|dor");

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.AreEqual(0, _store.Count);
    }

    [TestMethod]
    public void OnSettlementConquered_PlayerOwned_ConvertDisabled_DoesNotQueue()
    {
        _settings.ConvertPlayerOwnedSettlements.Returns(false);
        _adapter.IsPlayerOwned(Town).Returns(true);
        GivenCrossCultureConquest();

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.AreEqual(0, _store.Count);
    }

    [TestMethod]
    public void OnSettlementConquered_PlayerOwned_ConvertEnabled_Queues()
    {
        _settings.ConvertPlayerOwnedSettlements.Returns(true);
        _adapter.IsPlayerOwned(Town).Returns(true);
        GivenCrossCultureConquest();

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.IsTrue(_store.TryGet(Town, out var record) && record.HasPending);
    }

    [TestMethod]
    public void OnSettlementConquered_FeatureDisabled_NoOp()
    {
        _settings.IsEnabled.Returns(false);
        GivenCrossCultureConquest();

        _sut.OnSettlementConquered(Town, 100.0);

        Assert.AreEqual(0, _store.Count);
    }

    [TestMethod]
    public void OnSettlementConquered_ReconquestToThirdCulture_RestartsTimer()
    {
        GivenCrossCultureConquest(original: "gondor", owner: "mordor");
        _sut.OnSettlementConquered(Town, 100.0);

        // A different culture takes it before the first timer completes.
        _adapter.GetOwnerCultureId(Town).Returns("isengard");
        _sut.OnSettlementConquered(Town, 130.0);

        Assert.IsTrue(_store.TryGet(Town, out var record));
        Assert.AreEqual("isengard", record.PendingTargetCultureId);
        Assert.AreEqual(130.0, record.PendingStartDays.Value, 0.0001);
        Assert.IsFalse(record.IsConverted);
    }

    // --- RunDailyChecks: completion ---

    [TestMethod]
    public void RunDailyChecks_BeforeHoldElapsed_DoesNotConvert()
    {
        GivenCrossCultureConquest();
        _sut.OnSettlementConquered(Town, 100.0);

        _sut.RunDailyChecks(120.0); // 20 days < 45

        _adapter.DidNotReceive().SetSettlementCulture(Town, Arg.Any<string>());
        Assert.IsTrue(_store.TryGet(Town, out var record) && record.HasPending && !record.IsConverted);
    }

    [TestMethod]
    public void RunDailyChecks_AfterHoldElapsed_Converts()
    {
        GivenCrossCultureConquest();
        _sut.OnSettlementConquered(Town, 100.0);

        _sut.RunDailyChecks(150.0); // 50 days >= 45

        _adapter.Received().SetSettlementCulture(Town, "mordor");
        _adapter.Received().ResetVolunteers(Town);
        Assert.IsTrue(_store.TryGet(Town, out var record));
        Assert.IsTrue(record.IsConverted);
        Assert.AreEqual("mordor", record.AppliedCultureId);
        Assert.IsFalse(record.HasPending);
    }

    [TestMethod]
    public void RunDailyChecks_ConvertsBoundVillagesWithParent()
    {
        GivenCrossCultureConquest();
        _adapter.GetBoundVillageSettlementIds(Town).Returns(new List<string> { "village_ES1_1", "village_ES1_2" });
        _sut.OnSettlementConquered(Town, 100.0);

        _sut.RunDailyChecks(150.0);

        _adapter.Received().SetSettlementCulture("village_ES1_1", "mordor");
        _adapter.Received().SetSettlementCulture("village_ES1_2", "mordor");
        _adapter.Received().ResetVolunteers("village_ES1_1");
    }

    [TestMethod]
    public void RunDailyChecks_LoyaltyGateBelowThreshold_WaitsThenConverts()
    {
        _settings.RequireStableLoyalty.Returns(true);
        _settings.MinLoyaltyToConvert.Returns(50f);
        GivenCrossCultureConquest();
        _sut.OnSettlementConquered(Town, 100.0);

        _adapter.GetLoyalty(Town).Returns(30f);
        _sut.RunDailyChecks(150.0);
        _adapter.DidNotReceive().SetSettlementCulture(Town, "mordor");

        _adapter.GetLoyalty(Town).Returns(60f);
        _sut.RunDailyChecks(151.0);
        _adapter.Received().SetSettlementCulture(Town, "mordor");
    }

    [TestMethod]
    public void RunDailyChecks_OwnerNoLongerMatchesTarget_DropsStaleTimer()
    {
        GivenCrossCultureConquest(original: "gondor", owner: "mordor");
        _sut.OnSettlementConquered(Town, 100.0);

        // Owner reverted to the original culture's clan without an event reaching us.
        _adapter.GetOwnerCultureId(Town).Returns("gondor");
        _sut.RunDailyChecks(150.0);

        _adapter.DidNotReceive().SetSettlementCulture(Town, Arg.Any<string>());
        Assert.AreEqual(0, _store.Count, "An un-converted fief whose timer is dropped leaves no record.");
    }

    // --- R6: reconquest back to the original culture removes the override ---

    [TestMethod]
    public void ReconquestBackToOriginalCulture_RemovesOverride()
    {
        // 1. Gondor town conquered by Mordor → converts.
        GivenCrossCultureConquest(original: "gondor", owner: "mordor");
        _sut.OnSettlementConquered(Town, 100.0);
        _sut.RunDailyChecks(150.0);
        Assert.IsTrue(_store.IsConverted(Town));

        // 2. Gondor retakes it (record now exists; effective culture is mordor).
        _adapter.GetOwnerCultureId(Town).Returns("gondor");
        _sut.OnSettlementConquered(Town, 200.0);
        Assert.IsTrue(_store.TryGet(Town, out var queued) && queued.PendingTargetCultureId == "gondor");

        // 3. Hold elapses → reverts to gondor and the override is dropped entirely.
        _sut.RunDailyChecks(250.0);
        _adapter.Received().SetSettlementCulture(Town, "gondor");
        Assert.AreEqual(0, _store.Count, "Restoring the original culture must remove the record.");
    }

    // --- ReapplyConvertedCultures (save-load) ---

    [TestMethod]
    public void ReapplyConvertedCultures_ReappliesAppliedOverrideToTownAndVillages()
    {
        _store.Put(new SettlementConversionRecord(Town, "gondor", appliedCultureId: "mordor"));
        _adapter.GetBoundVillageSettlementIds(Town).Returns(new List<string> { "village_ES1_1" });

        _sut.ReapplyConvertedCultures();

        _adapter.Received().SetSettlementCulture(Town, "mordor");
        _adapter.Received().SetSettlementCulture("village_ES1_1", "mordor");
    }

    [TestMethod]
    public void ReapplyConvertedCultures_PendingOnlyRecord_NotReapplied()
    {
        // A fief mid-first-conversion (pending, not yet applied) must NOT have its culture changed on load.
        _store.Put(new SettlementConversionRecord(Town, "gondor", pendingStartDays: 100.0, pendingTargetCultureId: "mordor"));

        _sut.ReapplyConvertedCultures();

        _adapter.DidNotReceive().SetSettlementCulture(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ReapplyConvertedCultures_UnresolvableCulture_RemovesStaleRecord()
    {
        // If a converted-to culture was removed in a later mod version, SetSettlementCulture fails (culture
        // can't resolve). Leaving the record would keep IsConverted==true while Settlement.Culture stayed at
        // the XML original, sending recruitment down the converted branch with the wrong pool. Purge instead.
        _store.Put(new SettlementConversionRecord(Town, "gondor", appliedCultureId: "removed_culture"));
        _adapter.SetSettlementCulture(Town, "removed_culture").Returns(false);
        _adapter.GetBoundVillageSettlementIds(Town).Returns(new List<string> { "village_ES1_1" });

        _sut.ReapplyConvertedCultures();

        Assert.AreEqual(0, _store.Count, "A conversion whose culture no longer resolves must be purged.");
        // Villages are not re-applied once the town's re-apply fails.
        _adapter.DidNotReceive().SetSettlementCulture("village_ES1_1", Arg.Any<string>());
    }

    [TestMethod]
    public void ReapplyConvertedCultures_FeatureDisabled_StillReappliesExistingConversions()
    {
        // MCM contract (TaomSettings hint): "Disabling stops NEW conversions; already-converted settlements
        // stay converted." ReapplyConvertedCultures must run regardless of IsEnabled — guard this promise.
        _settings.IsEnabled.Returns(false);
        _store.Put(new SettlementConversionRecord(Town, "gondor", appliedCultureId: "mordor"));

        _sut.ReapplyConvertedCultures();

        _adapter.Received().SetSettlementCulture(Town, "mordor");
    }

    [TestMethod]
    public void ReapplyConvertedCultures_DoesNotResetVolunteers()
    {
        // Re-apply only restores Settlement.Culture; it must NOT clear volunteer slots. Hero.VolunteerTypes
        // is save-persisted (already reset+repopulated at conversion time), so a reset on every load would
        // needlessly wipe recruits. Guards against a copy-paste of ApplyConversion's ResetVolunteers call.
        _store.Put(new SettlementConversionRecord(Town, "gondor", appliedCultureId: "mordor"));

        _sut.ReapplyConvertedCultures();

        _adapter.DidNotReceive().ResetVolunteers(Arg.Any<string>());
    }
}

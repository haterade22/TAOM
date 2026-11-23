using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CampaignStartOptions;
using TAOM.Features.CampaignStartOptions.Hooks;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CampaignStartOptions;

/// <summary>Resolver, store, pin, mismatch notice and console editor for the four phase 2 rules.</summary>
[TestClass]
public class CampaignStartPhase2Tests
{
    private sealed class FakeData : IAdvancedStartDataAdapter
    {
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public bool IsAvailable { get; set; } = true;
        public bool IsNewCampaign { get; set; }

        public bool TryGet<T>(string key, out T value)
        {
            if (Values.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default!;
            return false;
        }

        public bool Add<T>(string key, string categoryId, T value)
        {
            if (!IsAvailable || Values.ContainsKey(key)) return false;
            Values[key] = value!;
            return true;
        }

        public bool Set<T>(string key, T value)
        {
            if (!IsAvailable || !Values.TryGetValue(key, out var raw) || raw is not T) return false;
            Values[key] = value!;
            return true;
        }
    }

    private FakeData _data = null!;
    private ICampaignStartDefaults _defaults = null!;
    private IModLogger _logger = null!;
    private StartDefaults _mcm = null!;

    [TestInitialize]
    public void Setup()
    {
        _data = new FakeData();
        _mcm = new StartDefaults(true, 30, 44, true, momentumVictory: false, eyeOfSauron: true, aiPartyScaling: true, AlignmentRules.Strict);
        _defaults = Substitute.For<ICampaignStartDefaults>();
        _defaults.Current().Returns(_ => _mcm);
        _logger = Substitute.For<IModLogger>();
    }

    private CampaignStartOptionsStore Store() => new CampaignStartOptionsStore(_data, _logger);

    private void PinAllAsMcm()
    {
        _data.Values[CampaignStartOptionKeys.VersionKey] = 1;
        _data.Values[CampaignStartOptionKeys.WarOfTheRingKey] = true;
        _data.Values[CampaignStartOptionKeys.Day1Key] = 30;
        _data.Values[CampaignStartOptionKeys.Day2Key] = 44;
        _data.Values[CampaignStartOptionKeys.TroopWeightKey] = true;
        _data.Values[CampaignStartOptionKeys.VictoryKey] = false;
        _data.Values[CampaignStartOptionKeys.EyeOfSauronKey] = true;
        _data.Values[CampaignStartOptionKeys.AiPartyScalingKey] = true;
        _data.Values[CampaignStartOptionKeys.AlignmentKey] = CampaignStartOptionKeys.AlignmentStandardItem;
    }

    // ---- resolver ----

    [TestMethod]
    public void Resolver_NothingOnTheScreen_GivesTheDefaults_AndStandardAlignment()
    {
        var d = new StartDefaults(true, 30, 44, true, momentumVictory: true, eyeOfSauron: false, aiPartyScaling: false);

        var r = CampaignStartOptionsResolver.Resolve(new ScreenValues(), d);

        Assert.IsTrue(r.MomentumVictory);
        Assert.IsFalse(r.EyeOfSauron);
        Assert.IsFalse(r.AiPartyScaling);
        Assert.AreEqual(CampaignStartOptionKeys.AlignmentStandardItem, r.AlignmentPreset);
        Assert.IsNull(r.Warning);
    }

    [TestMethod]
    public void Resolver_ScreenValues_BeatTheDefaults()
    {
        var d = new StartDefaults(true, 30, 44, true, momentumVictory: false, eyeOfSauron: true, aiPartyScaling: true);
        var screen = new ScreenValues
        {
            WarCanBeWon = true,
            EyeHunters = false,
            LargeAiArmies = false,
            AlignmentPreset = CampaignStartOptionKeys.AlignmentRelaxedItem,
        };

        var r = CampaignStartOptionsResolver.Resolve(screen, d);

        Assert.IsTrue(r.MomentumVictory);
        Assert.IsFalse(r.EyeOfSauron);
        Assert.IsFalse(r.AiPartyScaling);
        Assert.AreEqual(CampaignStartOptionKeys.AlignmentRelaxedItem, r.AlignmentPreset);
    }

    [TestMethod]
    public void Resolver_UnknownAlignmentPreset_BecomesStandard_WithAWarning()
    {
        var r = CampaignStartOptionsResolver.Resolve(
            new ScreenValues { AlignmentPreset = "taom_alignment_chaos" }, new StartDefaults(true, 30, 44, true));

        Assert.AreEqual(CampaignStartOptionKeys.AlignmentStandardItem, r.AlignmentPreset);
        StringAssert.Contains(r.Warning, "taom_alignment_chaos");
    }

    // ---- pin and store ----

    [TestMethod]
    public void NewCampaign_PinsThePhase2Keys_AndTheStoreReadsThemBack()
    {
        _data.IsNewCampaign = true;
        _data.Values[CampaignStartOptionKeys.WarCanBeWonOption] = true;
        _data.Values[CampaignStartOptionKeys.AlignmentPresetOption] = CampaignStartOptionKeys.AlignmentStrictItem;
        var store = Store();

        new CampaignStartSession(_data, store, _defaults, _logger).Begin();

        Assert.AreEqual(true, store.MomentumVictory);
        Assert.AreEqual(true, store.EyeOfSauron, "left alone: the default");
        Assert.AreEqual(true, store.AiPartyScaling);
        Assert.AreEqual(CampaignStartOptionKeys.AlignmentStrictItem, store.AlignmentPreset);
        Assert.AreSame(AlignmentRules.Strict, store.AlignmentRules);
    }

    [TestMethod]
    public void StandardPreset_IsPinned_ButPinsNoRules()
    {
        PinAllAsMcm();
        var store = Store();

        Assert.AreEqual(CampaignStartOptionKeys.AlignmentStandardItem, store.AlignmentPreset);
        Assert.IsNull(store.AlignmentRules);
    }

    [TestMethod]
    public void AnUnknownPinnedPreset_IsIgnored_WithAWarning()
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.AlignmentKey] = "taom_alignment_chaos";
        var store = Store();

        Assert.IsNull(store.AlignmentPreset);
        Assert.IsNull(store.AlignmentRules);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("taom_alignment_chaos")));
    }

    [TestMethod]
    public void ACampaignPinnedBeforePhase2_HasNoPhase2Values_SoThoseRulesFollowMcm()
    {
        _data.Values[CampaignStartOptionKeys.VersionKey] = 1;
        _data.Values[CampaignStartOptionKeys.TroopWeightKey] = false;
        var store = Store();

        Assert.IsTrue(store.IsPinned);
        Assert.IsNull(store.MomentumVictory);
        Assert.IsNull(store.EyeOfSauron);
        Assert.IsNull(store.AiPartyScaling);
        Assert.IsNull(store.AlignmentRules);
    }

    // ---- mismatch ----

    [TestMethod]
    public void Mismatch_NothingPinned_NeverDiffers() =>
        Assert.IsFalse(CampaignStartMismatch.Differs(Store(), _mcm));

    [TestMethod]
    public void Mismatch_PinnedEqualToMcm_DoesNotDiffer()
    {
        PinAllAsMcm();
        Assert.IsFalse(CampaignStartMismatch.Differs(Store(), _mcm));
    }

    [DataTestMethod]
    [DataRow(CampaignStartOptionKeys.WarOfTheRingKey, false)]
    [DataRow(CampaignStartOptionKeys.TroopWeightKey, false)]
    [DataRow(CampaignStartOptionKeys.VictoryKey, true)]
    [DataRow(CampaignStartOptionKeys.EyeOfSauronKey, false)]
    [DataRow(CampaignStartOptionKeys.AiPartyScalingKey, false)]
    public void Mismatch_AnyOnePinnedFlagThatDiffers_IsFound(string key, bool value)
    {
        PinAllAsMcm();
        _data.Values[key] = value;

        Assert.IsTrue(CampaignStartMismatch.Differs(Store(), _mcm));
    }

    [TestMethod]
    public void Mismatch_ADifferentDay_AndADifferentPreset_AreFound()
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.Day2Key] = 90;
        Assert.IsTrue(CampaignStartMismatch.Differs(Store(), _mcm));

        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.AlignmentKey] = CampaignStartOptionKeys.AlignmentRelaxedItem;
        Assert.IsTrue(CampaignStartMismatch.Differs(Store(), _mcm), "Relaxed against MCM's strict switches");
    }

    [TestMethod]
    public void Mismatch_AStrictPreset_WhenMcmIsStrict_DoesNotDiffer()
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.AlignmentKey] = CampaignStartOptionKeys.AlignmentStrictItem;

        Assert.IsFalse(CampaignStartMismatch.Differs(Store(), _mcm));
    }

    // ---- the notice and its behavior ----

    private CampaignStartMismatchNotice Notice(out ICampaignStartNoticeAdapter shown)
    {
        shown = Substitute.For<ICampaignStartNoticeAdapter>();
        return new CampaignStartMismatchNotice(Store(), _defaults, _data, shown, _logger);
    }

    [TestMethod]
    public void Notice_ALoadedCampaignThatDiffers_ShowsTheMessage()
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.VictoryKey] = true;
        var sut = Notice(out var shown);

        Assert.IsTrue(sut.ShowIfDifferent());
        shown.Received(1).ShowCampaignKeepsStartChoices();
    }

    [TestMethod]
    public void Notice_ANewCampaign_NeverShowsIt()
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.VictoryKey] = true;
        _data.IsNewCampaign = true;
        var sut = Notice(out var shown);

        Assert.IsFalse(sut.ShowIfDifferent());
        shown.DidNotReceive().ShowCampaignKeepsStartChoices();
    }

    [TestMethod]
    public void Notice_NothingDiffers_OrNothingPinned_StaysQuiet()
    {
        PinAllAsMcm();
        Assert.IsFalse(Notice(out var shown).ShowIfDifferent());

        _data.Values.Clear();
        Assert.IsFalse(Notice(out var shown2).ShowIfDifferent());
        shown.DidNotReceive().ShowCampaignKeepsStartChoices();
        shown2.DidNotReceive().ShowCampaignKeepsStartChoices();
    }

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void Behavior_AsksOncePerSession_AndSwallowsAFault()
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.VictoryKey] = true;
        var notice = Notice(out var shown);
        var sut = new CampaignStartOptionsBehavior(notice, _logger);

        sut.OnSessionLaunched(null);
        sut.OnSessionLaunched(null);

        shown.Received(1).ShowCampaignKeepsStartChoices();

        var failing = Substitute.For<ICampaignStartNoticeAdapter>();
        failing.When(a => a.ShowCampaignKeepsStartChoices()).Do(_ => throw new System.InvalidOperationException("boom"));
        var faulty = new CampaignStartOptionsBehavior(
            new CampaignStartMismatchNotice(Store(), _defaults, _data, failing, _logger), _logger);

        faulty.OnSessionLaunched(null); // must not throw
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("boom")));
    }

    // ---- console editor ----

    private readonly ICoopSessionProvider _coop = Substitute.For<ICoopSessionProvider>();
    private readonly IPartySizeCacheInvalidator _partySizeCache = Substitute.For<IPartySizeCacheInvalidator>();

    private CampaignOptionEditor Editor(out CampaignStartOptionsStore store)
    {
        store = Store();
        return new CampaignOptionEditor(store, _defaults, _logger, _coop, _partySizeCache);
    }

    [TestMethod]
    public void Editor_NotPinned_ChangesNothing()
    {
        var sut = Editor(out _);

        StringAssert.Contains(sut.List(), "older save");
        StringAssert.Contains(sut.Set("troop_weight", "off"), "older save");
        Assert.AreEqual(0, _data.Values.Count);
    }

    [TestMethod]
    public void Editor_List_ShowsPinnedAndMcmValues_AndFlagsADifference()
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.VictoryKey] = true;
        var sut = Editor(out _);

        var text = sut.List();

        StringAssert.Contains(text, "war_can_be_won = on   [MCM: off]");
        StringAssert.Contains(text, "troop_weight = on   [MCM: on]");
        StringAssert.Contains(text, "alignment = standard");
        StringAssert.Contains(text, "differs from MCM");
    }

    [TestMethod]
    public void Editor_SetFlag_WritesIntoTheCampaign_AndTheStoreSeesIt()
    {
        PinAllAsMcm();
        var sut = Editor(out var store);

        var reply = sut.Set("war_can_be_won", "ON");

        Assert.AreEqual(true, _data.Values[CampaignStartOptionKeys.VictoryKey]);
        Assert.AreEqual(true, store.MomentumVictory);
        StringAssert.Contains(reply, "war_can_be_won is now on");
    }

    [TestMethod]
    public void Editor_SetAlignment_WritesThePresetId()
    {
        PinAllAsMcm();
        var sut = Editor(out var store);

        sut.Set("alignment", "Relaxed");

        Assert.AreEqual(CampaignStartOptionKeys.AlignmentRelaxedItem, _data.Values[CampaignStartOptionKeys.AlignmentKey]);
        Assert.AreSame(AlignmentRules.Relaxed, store.AlignmentRules);
    }

    [TestMethod]
    public void Editor_SetOnACampaignPinnedBeforePhase2_AddsTheMissingKey()
    {
        _data.Values[CampaignStartOptionKeys.VersionKey] = 1;
        var sut = Editor(out var store);

        sut.Set("eye_hunters", "off");

        Assert.AreEqual(false, store.EyeOfSauron);
    }

    [DataTestMethod]
    [DataRow("nonsense", "on")]
    [DataRow("troop_weight", "maybe")]
    [DataRow("alignment", "chaos")]
    [DataRow("war_day1", "abc")]
    [DataRow("war_day1", "0")]
    [DataRow("war_day1", "366")]
    [DataRow("war_day1", "44")]
    [DataRow("war_day2", "30")]
    [DataRow("war_day2", "400")]
    public void Editor_BadInput_ChangesNothing(string name, string value)
    {
        PinAllAsMcm();
        var before = new Dictionary<string, object>(_data.Values);
        var sut = Editor(out _);

        var reply = sut.Set(name, value);

        CollectionAssert.AreEquivalent(before, _data.Values);
        Assert.IsFalse(reply.Contains(" is now "), reply);
    }

    [TestMethod]
    public void Editor_Days_AreCheckedAgainstTheOtherDay()
    {
        PinAllAsMcm();
        var sut = Editor(out var store);

        sut.Set("war_day2", "100");
        sut.Set("war_day1", "70");

        Assert.AreEqual((70, 100), store.WarPhaseDays);
    }

    [TestMethod]
    public void Editor_ASuccessfulSet_DropsThePartySizeCaches()
    {
        PinAllAsMcm();
        var sut = Editor(out _);

        sut.Set("troop_weight", "off");

        _partySizeCache.Received(1).InvalidateAll();
    }

    [TestMethod]
    public void Editor_ABadOrRefusedSet_LeavesThePartySizeCachesAlone()
    {
        PinAllAsMcm();
        var sut = Editor(out _);

        sut.Set("troop_weight", "maybe");
        _coop.IsCoopClient.Returns(true);
        sut.Set("troop_weight", "off");

        _partySizeCache.DidNotReceive().InvalidateAll();
    }

    [TestMethod]
    public void Editor_OnACoopClient_RefusesToWrite()
    {
        PinAllAsMcm();
        _coop.IsCoopClient.Returns(true);
        var before = new Dictionary<string, object>(_data.Values);
        var sut = Editor(out _);

        var reply = sut.Set("war_can_be_won", "on");

        StringAssert.Contains(reply, "Refused");
        CollectionAssert.AreEquivalent(before, _data.Values);
        StringAssert.Contains(sut.List(), "war_can_be_won", "list stays readable");
    }

    [TestMethod]
    public void Editor_OnACoopHost_RefusesToWrite()
    {
        PinAllAsMcm();
        _coop.IsSessionActive.Returns(true);
        var before = new Dictionary<string, object>(_data.Values);
        var sut = Editor(out _);

        var reply = sut.Set("war_can_be_won", "on");

        StringAssert.Contains(reply, "Refused");
        CollectionAssert.AreEquivalent(before, _data.Values);
        _partySizeCache.DidNotReceive().InvalidateAll();
    }

    [DataTestMethod]
    [DataRow("war_day1", "40", 40, 44)]
    [DataRow("war_day2", "80", 30, 80)]
    public void Editor_SetDay_WhenTheStoredPairIsInvalid_WritesBothKeys(string name, string value, int day1, int day2)
    {
        PinAllAsMcm();
        _data.Values[CampaignStartOptionKeys.Day1Key] = 50; // day 2 (44) is before day 1: the pair is invalid
        var sut = Editor(out var store);
        Assert.IsNull(store.WarPhaseDays);

        var reply = sut.Set(name, value);

        StringAssert.Contains(reply, " is now ");
        Assert.AreEqual((day1, day2), store.WarPhaseDays);
        Assert.AreEqual(day1, _data.Values[CampaignStartOptionKeys.Day1Key]);
        Assert.AreEqual(day2, _data.Values[CampaignStartOptionKeys.Day2Key]);
    }

    [TestMethod]
    public void Editor_SetDay_WhenTheStoredPairIsMissing_WritesBothKeys()
    {
        _data.Values[CampaignStartOptionKeys.VersionKey] = 1;
        var sut = Editor(out var store);

        sut.Set("war_day1", "10");

        Assert.AreEqual((10, 44), store.WarPhaseDays);
    }
}

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CampaignStartOptions;

namespace TAOM.Tests.Features.CampaignStartOptions;

[TestClass]
public class CampaignStartOptionsStoreTests
{
    // An in-memory stand-in for the campaign's option data: typed keys, Add keeps an existing key.
    private sealed class FakeData : IAdvancedStartDataAdapter
    {
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public bool IsAvailable { get; set; } = true;
        public bool IsNewCampaign { get; set; } = true;

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

    [TestInitialize]
    public void Setup()
    {
        _data = new FakeData();
        _defaults = Substitute.For<ICampaignStartDefaults>();
        _defaults.Current().Returns(new StartDefaults(true, 30, 44, true));
        _logger = Substitute.For<IModLogger>();
    }

    private CampaignStartSession SessionOver(CampaignStartOptionsStore store) =>
        new CampaignStartSession(_data, store, _defaults, _logger);

    private CampaignStartOptionsStore Create() => new CampaignStartOptionsStore(_data, _logger);

    private void PinFully(int version = 1, bool wotr = true, int d1 = 60, int d2 = 90, bool weight = false)
    {
        _data.Values[CampaignStartOptionKeys.VersionKey] = version;
        _data.Values[CampaignStartOptionKeys.WarOfTheRingKey] = wotr;
        _data.Values[CampaignStartOptionKeys.Day1Key] = d1;
        _data.Values[CampaignStartOptionKeys.Day2Key] = d2;
        _data.Values[CampaignStartOptionKeys.TroopWeightKey] = weight;
    }

    // ---- reading ----

    [TestMethod]
    public void NoMarker_GivesNulls_SoReadersFollowMcm()
    {
        _data.Values[CampaignStartOptionKeys.TroopWeightKey] = false; // a stray key without the marker is ignored
        var sut = Create();

        Assert.IsFalse(sut.IsPinned);
        Assert.IsNull(sut.WarOfTheRing);
        Assert.IsNull(sut.WarPhaseDays);
        Assert.IsNull(sut.TroopWeight);
    }

    [TestMethod]
    public void Marker_GivesThePinnedValues()
    {
        PinFully();
        var sut = Create();

        Assert.IsTrue(sut.IsPinned);
        Assert.AreEqual(true, sut.WarOfTheRing);
        Assert.AreEqual((60, 90), sut.WarPhaseDays);
        Assert.AreEqual(false, sut.TroopWeight);
    }

    [TestMethod]
    public void NoCampaign_GivesNulls_AndIsNotRemembered()
    {
        _data.IsAvailable = false;
        var sut = Create();
        Assert.IsFalse(sut.IsPinned);

        // The campaign appears afterwards: the earlier "nothing" must not have been cached.
        _data.IsAvailable = true;
        PinFully();

        Assert.IsTrue(sut.IsPinned);
    }

    [DataTestMethod]
    [DataRow(0, 10)]
    [DataRow(10, 366)]
    [DataRow(50, 50)]
    [DataRow(80, 40)]
    public void BadDays_WarnAndFallBack_WithoutTouchingTheOtherValues(int d1, int d2)
    {
        PinFully(d1: d1, d2: d2);
        var sut = Create();

        Assert.IsNull(sut.WarPhaseDays);
        Assert.AreEqual(false, sut.TroopWeight);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("days")));
    }

    [TestMethod]
    public void WrongTypedKey_ReadsAsAbsent()
    {
        PinFully();
        _data.Values[CampaignStartOptionKeys.TroopWeightKey] = "not a bool";

        Assert.IsNull(Create().TroopWeight);
    }

    [TestMethod]
    public void NewerVersion_Warns_AndReadsWhatItCan()
    {
        PinFully(version: 7);
        var sut = Create();

        Assert.AreEqual(true, sut.WarOfTheRing);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("version 7")));
    }

    // ---- the session start ----

    [TestMethod]
    public void Begin_OnANewCampaign_PinsEverySettingWithItsDefault()
    {
        var sut = Create();

        SessionOver(sut).Begin();

        Assert.AreEqual(CampaignStartOptionKeys.CurrentVersion, _data.Values[CampaignStartOptionKeys.VersionKey]);
        Assert.AreEqual(true, _data.Values[CampaignStartOptionKeys.WarOfTheRingKey]);
        Assert.AreEqual(30, _data.Values[CampaignStartOptionKeys.Day1Key]);
        Assert.AreEqual(44, _data.Values[CampaignStartOptionKeys.Day2Key]);
        Assert.AreEqual(true, _data.Values[CampaignStartOptionKeys.TroopWeightKey]);
        Assert.IsTrue(sut.IsPinned, "the snapshot is taken after the pin");
    }

    [TestMethod]
    public void Begin_PinsTheScreenChoices()
    {
        _data.Values[CampaignStartOptionKeys.ScenarioOption] = CampaignStartOptionKeys.SandboxScenarioItem;
        _data.Values[CampaignStartOptionKeys.StartDateOption] = CampaignStartOptionKeys.StartLateItem;
        _data.Values[CampaignStartOptionKeys.TroopWeightOption] = false;
        var sut = Create();

        SessionOver(sut).Begin();

        Assert.AreEqual(false, sut.WarOfTheRing);
        Assert.AreEqual((60, 90), sut.WarPhaseDays);
        Assert.AreEqual(false, sut.TroopWeight);
    }

    [TestMethod]
    public void Begin_OnALoadedSave_PinsNothing()
    {
        _data.IsNewCampaign = false;
        var sut = Create();

        SessionOver(sut).Begin();

        Assert.AreEqual(0, _data.Values.Count);
        Assert.IsFalse(sut.IsPinned);
    }

    [TestMethod]
    public void Begin_OnALoadedSave_ReadsItsPinnedValues()
    {
        _data.IsNewCampaign = false;
        PinFully();
        var sut = Create();

        SessionOver(sut).Begin();

        Assert.AreEqual((60, 90), sut.WarPhaseDays);
    }

    [TestMethod]
    public void Begin_WithoutACampaign_PinsNothing()
    {
        _data.IsAvailable = false;

        SessionOver(Create()).Begin();

        Assert.AreEqual(0, _data.Values.Count);
    }

    [TestMethod]
    public void Begin_Twice_IsIdempotent()
    {
        var sut = Create();
        SessionOver(sut).Begin();
        var first = new Dictionary<string, object>(_data.Values);

        // Even if the defaults moved (MCM edited) between the two calls, the pinned values stay.
        _defaults.Current().Returns(new StartDefaults(false, 5, 6, false));
        SessionOver(sut).Begin();

        CollectionAssert.AreEquivalent(first, _data.Values);
        Assert.AreEqual(30, sut.WarPhaseDays!.Value.Phase1Day);
    }

    [TestMethod]
    public void Begin_OverAHalfPinnedCampaign_AddsOnlyTheMissingKeys()
    {
        _data.Values[CampaignStartOptionKeys.VersionKey] = 1;
        _data.Values[CampaignStartOptionKeys.TroopWeightKey] = false;
        var sut = Create();

        SessionOver(sut).Begin();

        Assert.AreEqual(false, _data.Values[CampaignStartOptionKeys.TroopWeightKey], "an existing key is kept");
        Assert.AreEqual(44, _data.Values[CampaignStartOptionKeys.Day2Key], "a missing key is added");
    }

    [TestMethod]
    public void Begin_ForgetsThePreviousSessionsSnapshot()
    {
        PinFully(d1: 60, d2: 90);
        var sut = Create();
        Assert.AreEqual((60, 90), sut.WarPhaseDays);

        // A second campaign in the same process: a different data set, and Begin must not serve the old values.
        _data.Values.Clear();
        _data.IsNewCampaign = false;
        SessionOver(sut).Begin();

        Assert.IsNull(sut.WarPhaseDays);
        Assert.IsFalse(sut.IsPinned);
    }

    [TestMethod]
    public void Begin_LogsWhatItPinned()
    {
        SessionOver(Create()).Begin();

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.StartsWith("CampaignStartOptions.Pinned") && System.Text.RegularExpressions.Regex.IsMatch(m, "troopWeight=(True|False) warCanBeWon=")));
    }
}

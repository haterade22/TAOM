using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FactionPower;

namespace TAOM.Tests.Features.FactionPower;

/// <summary>What the start screen pins into a new campaign: the pure resolver, then the session over a real store.</summary>
[TestClass]
public class FactionPowerStartTests
{
    private static readonly IReadOnlyList<FactionPowerEntry> Realms = new[]
    {
        new FactionPowerEntry("gondor", "Gondor", "free"),
        new FactionPowerEntry("mordor", "Mordor", "evil"),
        new FactionPowerEntry("isengard", "Isengard", "evil"),
        new FactionPowerEntry("anduinvale", "Anduin Vale", "neutral"),
    };

    private static readonly IReadOnlyDictionary<string, int> NoSliders = new Dictionary<string, int>();

    private static IReadOnlyDictionary<string, int> Resolve(string? item, IReadOnlyDictionary<string, int>? sliders = null, List<string>? warnings = null) =>
        FactionPowerStartResolver.Resolve(item, sliders ?? NoSliders, Realms, warnings == null ? null : warnings.Add);

    // ---- the resolver ----

    [DataTestMethod]
    [DataRow(null)]
    [DataRow(FactionPowerScreenKeys.StandardItem)]
    public void Resolve_StandardOrNoChoice_WritesNothing_EvenWithSlidersPresent(string? item)
    {
        var sliders = new Dictionary<string, int> { ["mordor"] = 150 };

        Assert.AreEqual(0, Resolve(item, sliders).Count);
    }

    [TestMethod]
    public void Resolve_EvilPreset_WritesExactlyTheEvilRealmsAt150()
    {
        var result = Resolve(FactionPowerScreenKeys.EvilItem);

        CollectionAssert.AreEquivalent(new[] { "mordor", "isengard" }, result.Keys.ToList());
        Assert.IsTrue(result.Values.All(v => v == 150));
    }

    [TestMethod]
    public void Resolve_FreePreset_WritesExactlyTheFreeRealmsAt150_AndNeverNeutral()
    {
        var result = Resolve(FactionPowerScreenKeys.FreeItem);

        CollectionAssert.AreEquivalent(new[] { "gondor" }, result.Keys.ToList());
        Assert.AreEqual(150, result["gondor"]);
    }

    [TestMethod]
    public void Resolve_SidePreset_IgnoresSlidersLeftOverFromCustom()
    {
        var sliders = new Dictionary<string, int> { ["gondor"] = 50 };

        var result = Resolve(FactionPowerScreenKeys.EvilItem, sliders);

        Assert.IsFalse(result.ContainsKey("gondor"));
    }

    [TestMethod]
    public void Resolve_Custom_PassesTheMovedSlidersThrough_AndSkipsOnesAt100()
    {
        var sliders = new Dictionary<string, int> { ["gondor"] = 50, ["mordor"] = 200, ["isengard"] = 100 };

        var result = Resolve(FactionPowerScreenKeys.CustomItem, sliders);

        CollectionAssert.AreEquivalent(new[] { "gondor", "mordor" }, result.Keys.ToList());
        Assert.AreEqual(50, result["gondor"]);
        Assert.AreEqual(200, result["mordor"]);
    }

    [DataTestMethod]
    [DataRow(49)]
    [DataRow(201)]
    [DataRow(int.MinValue)]
    public void Resolve_Custom_ASliderOutOfRange_IsSkippedWithAWarning_NeverClamped(int percent)
    {
        var warnings = new List<string>();

        var result = Resolve(FactionPowerScreenKeys.CustomItem, new Dictionary<string, int> { ["mordor"] = percent, ["gondor"] = 75 }, warnings);

        Assert.IsFalse(result.ContainsKey("mordor"));
        Assert.AreEqual(75, result["gondor"], "the other slider is kept");
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "mordor");
    }

    [TestMethod]
    public void Resolve_Custom_ASliderForAnUnknownRealm_IsIgnored()
    {
        var result = Resolve(FactionPowerScreenKeys.CustomItem, new Dictionary<string, int> { ["rohan_old"] = 150 });

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Resolve_AnUnknownPresetItem_WritesNothing_AndWarns()
    {
        var warnings = new List<string>();

        var result = Resolve("taom_fp_preset_chaos", warnings: warnings);

        Assert.AreEqual(0, result.Count);
        StringAssert.Contains(warnings.Single(), "taom_fp_preset_chaos");
    }

    [TestMethod]
    public void ScreenKeys_NeverCollideWithAStoredKey()
    {
        // The store reads taom_fp_<culture>; a screen key under that prefix must not name a catalog culture.
        var screenKeys = FactionPowerScreenKeys.PresetItems.Append(FactionPowerScreenKeys.PresetOption)
            .Concat(Realms.Select(e => FactionPowerScreenKeys.SliderKey(e.CultureId)));
        foreach (var key in screenKeys)
            Assert.IsFalse(Realms.Any(e => FactionPowerStore.KeyPrefix + e.CultureId == key), key);
    }

    // ---- the session, over the real store ----

    private sealed class Rig
    {
        public readonly FakeAdvancedStartData Data = new FakeAdvancedStartData();
        public readonly IModLogger Logger = Substitute.For<IModLogger>();
        public readonly FactionPowerStore Store;
        public readonly FactionPowerSession Session;

        public Rig()
        {
            var catalog = new FixedCatalog(Realms);
            Store = new FactionPowerStore(Data, catalog, Logger);
            Session = new FactionPowerSession(Data, catalog, Store, Logger);
        }
    }

    private sealed class FixedCatalog : IFactionPowerCatalog
    {
        public FixedCatalog(IReadOnlyList<FactionPowerEntry> entries) => Entries = entries;
        public IReadOnlyList<FactionPowerEntry> Entries { get; }
    }

    [TestMethod]
    public void Begin_NewCampaign_EvilPreset_StoresTheEvilRealmsAt150_AndLogsIt()
    {
        var rig = new Rig();
        rig.Data.Values[FactionPowerScreenKeys.PresetOption] = FactionPowerScreenKeys.EvilItem;

        rig.Session.Begin(true);

        Assert.AreEqual(150, rig.Store.Percent("mordor"));
        Assert.AreEqual(150, rig.Store.Percent("isengard"));
        Assert.AreEqual(100, rig.Store.Percent("gondor"));
        Assert.AreEqual(1f, rig.Store.Multiplier("anduinvale"));
        Assert.AreEqual(FactionPowerStore.CurrentVersion, rig.Data.Values[FactionPowerStore.VersionKey]);
        rig.Logger.Received().LogInfo(Arg.Is<string>(m => m.StartsWith("FactionPower.Pinned: preset=" + FactionPowerScreenKeys.EvilItem) && m.Contains("mordor=150")));
        rig.Logger.Received().LogInfo(Arg.Is<string>(m => m.StartsWith("FactionPower.Loaded:") && m.Contains("mordor=150")));
    }

    [TestMethod]
    public void Begin_NewCampaign_Custom_StoresTheMovedSliders()
    {
        var rig = new Rig();
        rig.Data.Values[FactionPowerScreenKeys.PresetOption] = FactionPowerScreenKeys.CustomItem;
        rig.Data.Values[FactionPowerScreenKeys.SliderKey("gondor")] = 80;
        rig.Data.Values[FactionPowerScreenKeys.SliderKey("mordor")] = 175;

        rig.Session.Begin(true);

        Assert.AreEqual(80, rig.Store.Percent("gondor"));
        Assert.AreEqual(175, rig.Store.Percent("mordor"));
        Assert.AreEqual(100, rig.Store.Percent("isengard"));
    }

    [TestMethod]
    public void Begin_NewCampaign_NoScreenChoice_StoresNothing_AllRealmsAt100()
    {
        var rig = new Rig();

        rig.Session.Begin(true);

        Assert.AreEqual(0, rig.Store.Snapshot().Count);
        Assert.IsFalse(rig.Data.Values.Keys.Any(k => k.StartsWith(FactionPowerStore.KeyPrefix)), "no key is written for Standard");
        rig.Logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.StartsWith("FactionPower.Pinned")));
    }

    [TestMethod]
    public void Begin_LoadedSave_IsNeverPinned_ItKeepsItsStoredValues()
    {
        var rig = new Rig();
        rig.Data.Values[FactionPowerStore.KeyPrefix + "gondor"] = 120;
        rig.Data.Values[FactionPowerScreenKeys.PresetOption] = FactionPowerScreenKeys.EvilItem;

        rig.Session.Begin(false);

        Assert.AreEqual(120, rig.Store.Percent("gondor"));
        Assert.AreEqual(100, rig.Store.Percent("mordor"), "a screen key in a loaded save changes nothing");
    }

    [TestMethod]
    public void Begin_SecondCampaignInOneProcess_StartsEmpty()
    {
        var rig = new Rig();
        rig.Data.Values[FactionPowerScreenKeys.PresetOption] = FactionPowerScreenKeys.EvilItem;
        rig.Session.Begin(true);
        Assert.AreEqual(150, rig.Store.Percent("mordor"));

        rig.Data.Values.Clear();
        rig.Session.Begin(true);

        Assert.AreEqual(100, rig.Store.Percent("mordor"));
    }

    [TestMethod]
    public void Begin_NoCampaign_WritesNothing_AndDoesNotThrow()
    {
        var rig = new Rig();
        rig.Data.IsAvailable = false;

        rig.Session.Begin(true);

        Assert.AreEqual(0, rig.Data.Values.Count);
        Assert.AreEqual(100, rig.Store.Percent("mordor"));
    }

    [TestMethod]
    public void Begin_AWriteThatFails_WarnsWithTheRealm_AndStillLoads()
    {
        var rig = new Rig();
        var settings = Substitute.For<IFactionPowerSettings>();
        settings.TrySet("mordor", 150).Returns(false);
        settings.TrySet("isengard", 150).Returns(true);
        var session = new FactionPowerSession(rig.Data, new FixedCatalog(Realms), settings, rig.Logger);
        rig.Data.Values[FactionPowerScreenKeys.PresetOption] = FactionPowerScreenKeys.EvilItem;

        session.Begin(true);

        rig.Logger.Received().LogWarning(Arg.Is<string>(m => m.StartsWith("FactionPower.PinFailed") && m.Contains("mordor")));
        settings.Received(1).Reload();
    }
}

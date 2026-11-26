using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FactionPower;

namespace TAOM.Tests.Features.FactionPower;

[TestClass]
public class FactionPowerEditorTests
{
    private FakeAdvancedStartData _data = null!;
    private ICoopSessionProvider _coop = null!;
    private IPartySizeCacheInvalidator _cache = null!;
    private FactionPowerEditor _editor = null!;
    private FactionPowerStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _data = new FakeAdvancedStartData();
        _coop = Substitute.For<ICoopSessionProvider>();
        _cache = Substitute.For<IPartySizeCacheInvalidator>();
        var catalog = new TestCatalog(
            new FactionPowerEntry("mordor", "Mordor", "evil"),
            new FactionPowerEntry("isengard", "Isengard", "evil"),
            new FactionPowerEntry("gondor", "Gondor", "free"));
        var logger = Substitute.For<IModLogger>();
        _store = new FactionPowerStore(_data, catalog, logger);
        _editor = new FactionPowerEditor(_store, catalog, logger, _coop, _cache);
    }

    private sealed class TestCatalog : IFactionPowerCatalog
    {
        public TestCatalog(params FactionPowerEntry[] entries) => Entries = entries;
        public IReadOnlyList<FactionPowerEntry> Entries { get; }
    }

    [TestMethod]
    public void Set_WritesTheValue_AndInvalidatesThePartySizeCaches()
    {
        var reply = _editor.Set("mordor", "150");

        Assert.AreEqual(1.5f, _store.Multiplier("mordor"));
        _cache.Received(1).InvalidateAll();
        StringAssert.Contains(reply, "150");
    }

    [TestMethod]
    public void Set_IsCaseInsensitiveOnTheCultureName()
    {
        _editor.Set("MORDOR", "150");

        Assert.AreEqual(1.5f, _store.Multiplier("mordor"));
    }

    [TestMethod]
    public void Set_RefusedOnACoopClient_WritesNothing_AndInvalidatesNothing()
    {
        _coop.IsCoopClient.Returns(true);

        var reply = _editor.Set("mordor", "150");

        StringAssert.Contains(reply, "Refused");
        Assert.AreEqual(0, _data.Values.Count);
        _cache.DidNotReceive().InvalidateAll();
    }

    [TestMethod]
    public void Set_RefusedDuringALiveCoopSession_OnTheHostToo()
    {
        _coop.IsSessionActive.Returns(true);

        var reply = _editor.Set("mordor", "150");

        StringAssert.Contains(reply, "Refused");
        Assert.AreEqual(0, _data.Values.Count);
        _cache.DidNotReceive().InvalidateAll();
    }

    [DataTestMethod]
    [DataRow("mordor", "49")]
    [DataRow("mordor", "201")]
    [DataRow("mordor", "abc")]
    [DataRow("mordor", "")]
    [DataRow("mordor", "1.5")]
    [DataRow("atlantis", "150")]
    public void Set_RefusesABadPercentOrAnUnknownCulture_AndChangesNothing(string culture, string percent)
    {
        var reply = _editor.Set(culture, percent);

        Assert.AreEqual(0, _data.Values.Count);
        _cache.DidNotReceive().InvalidateAll();
        Assert.IsFalse(reply.Contains("is now"));
    }

    [TestMethod]
    public void Set_WithNoCampaign_SaysSo_AndInvalidatesNothing()
    {
        _data.IsAvailable = false;

        var reply = _editor.Set("mordor", "150");

        StringAssert.Contains(reply, "Could not write");
        _cache.DidNotReceive().InvalidateAll();
    }

    [TestMethod]
    public void Preset_Evil50_WritesTheEvilCulturesOnly_AndInvalidates()
    {
        _editor.Preset("evil50");

        Assert.AreEqual(1.5f, _store.Multiplier("mordor"));
        Assert.AreEqual(1.5f, _store.Multiplier("isengard"));
        Assert.AreEqual(1f, _store.Multiplier("gondor"));
        _cache.Received(1).InvalidateAll();
    }

    [TestMethod]
    public void Preset_StoppedPartway_StillRefreshesThePartySizeCache()
    {
        _data.Values[FactionPowerStore.KeyPrefix + "isengard"] = "written by a newer TAOM";

        var reply = _editor.Preset("evil50");

        StringAssert.Contains(reply, "Could not write");
        _cache.Received(1).InvalidateAll();
    }

    [TestMethod]
    public void Preset_Reset_PutsEveryChangedCultureBackToOne()
    {
        _editor.Preset("evil50");
        _editor.Preset("free50");

        _editor.Preset("reset");

        Assert.AreEqual(1f, _store.Multiplier("mordor"));
        Assert.AreEqual(1f, _store.Multiplier("gondor"));
    }

    [TestMethod]
    public void Preset_Standard_ChangesNothing_ButIsNotAnError()
    {
        var reply = _editor.Preset("standard");

        Assert.AreEqual(0, _data.Values.Count);
        Assert.IsFalse(reply.StartsWith("Unknown"));
    }

    [TestMethod]
    public void Preset_Unknown_IsRefused_AndChangesNothing()
    {
        var reply = _editor.Preset("evil500");

        StringAssert.Contains(reply, "evil50");
        Assert.AreEqual(0, _data.Values.Count);
        _cache.DidNotReceive().InvalidateAll();
    }

    [TestMethod]
    public void Preset_RefusedOnACoopClient()
    {
        _coop.IsCoopClient.Returns(true);

        var reply = _editor.Preset("evil50");

        StringAssert.Contains(reply, "Refused");
        Assert.AreEqual(0, _data.Values.Count);
    }

    [TestMethod]
    public void List_ShowsEveryCatalogCultureWithItsSideAndPercent()
    {
        _editor.Set("mordor", "150");

        var reply = _editor.List();

        StringAssert.Contains(reply, "mordor");
        StringAssert.Contains(reply, "150");
        StringAssert.Contains(reply, "gondor");
        StringAssert.Contains(reply, "100");
        StringAssert.Contains(reply, "evil");
    }
}

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Core.Persistence;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Core.Persistence;

/// <summary>
/// The count-and-chunks SyncData loop the momentum behavior uses: a count
/// key, then that many chunk keys. A saved count that is negative or past <see cref="SyncDataChunker.MaxChunks"/>
/// is a corrupt or foreign value, so the payload is ignored with a warning instead of being looped over.
/// </summary>
[TestClass]
public class SyncDataChunkerSyncTests
{
    private const string CountKey = "_test_v1_count";
    private const string Prefix = "_test_v1_";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    [TestMethod]
    public void SyncChunks_RoundTrip_WritesTheCountAndOneKeyPerChunkAndReadsThemBack()
    {
        var store = new EngineDataStore { Loading = false };
        var saved = new List<string> { "alpha", "beta", "gamma" };

        SyncDataChunker.SyncChunks(store, CountKey, Prefix, saved, _logger, "[Test]");

        Assert.AreEqual(3, store.Get<int>(CountKey));
        Assert.AreEqual("alpha", store.Get<string>(Prefix + "0"));
        Assert.AreEqual("gamma", store.Get<string>(Prefix + "2"));
        Assert.IsFalse(store.Has(Prefix + "3"));

        store.Loading = true;
        var loaded = SyncDataChunker.SyncChunks(store, CountKey, Prefix, null, _logger, "[Test]");

        CollectionAssert.AreEqual(saved, new List<string>(loaded));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SyncChunks_SavingNothing_WritesACountOfZero()
    {
        var store = new EngineDataStore { Loading = false };

        SyncDataChunker.SyncChunks(store, CountKey, Prefix, null, _logger, "[Test]");

        Assert.AreEqual(0, store.Get<int>(CountKey));
        Assert.IsFalse(store.Has(Prefix + "0"));
    }

    [TestMethod]
    public void SyncChunks_LoadingWithoutARecord_ReturnsNoChunks()
    {
        var loaded = SyncDataChunker.SyncChunks(new EngineDataStore { Loading = true }, CountKey, Prefix, null, _logger, "[Test]");

        Assert.AreEqual(0, loaded.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SyncChunks_LoadingANullChunk_ReturnsAnEmptyString()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed(CountKey, 1);
        store.Seed<string?>(Prefix + "0", null);

        var loaded = SyncDataChunker.SyncChunks(store, CountKey, Prefix, null, _logger, "[Test]");

        Assert.AreEqual(string.Empty, loaded[0]);
    }

    [TestMethod]
    public void SyncChunks_AbsurdCount_IsIgnored()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed(CountKey, SyncDataChunker.MaxChunks + 1);
        store.Seed(Prefix + "0", "stale");

        var loaded = SyncDataChunker.SyncChunks(store, CountKey, Prefix, null, _logger, "[Test]");

        Assert.AreEqual(0, loaded.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.StartsWith("[Test]") && m.Contains("chunk count")));
    }

    [TestMethod]
    public void SyncChunks_NegativeCount_IsIgnored()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed(CountKey, -1);

        var loaded = SyncDataChunker.SyncChunks(store, CountKey, Prefix, null, _logger, "[Test]");

        Assert.AreEqual(0, loaded.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("chunk count")));
    }

    [TestMethod]
    public void SyncChunks_CountAtTheCap_IsRead()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed(CountKey, SyncDataChunker.MaxChunks);

        var loaded = SyncDataChunker.SyncChunks(store, CountKey, Prefix, null, _logger, "[Test]");

        Assert.AreEqual(SyncDataChunker.MaxChunks, loaded.Count);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}

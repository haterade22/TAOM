using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.MissionPerf.AnimMemory;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// The engine signature address cache on synthetic byte arrays and an in-memory file: a remembered address is used only
/// when the signature still matches there, anything wrong with the file or the key falls back to the full scan with at
/// most one log line, and a changed DLL (stamp or header fields) never reuses another build's addresses. A text with the
/// signature twice proves a hit: a full scan of it would report two matches.
/// </summary>
[TestClass]
public class EngineSignatureCacheTests
{
    private const string Stamp = "1000|638000000000000000";

    private FakeCacheAdapter _adapter = null!;
    private RecordingLogger _logger = null!;
    private EngineSignatureCache _cache = null!;

    [TestInitialize]
    public void SetUp()
    {
        _adapter = new FakeCacheAdapter { Stamp = Stamp };
        _logger = new RecordingLogger();
        _cache = new EngineSignatureCache(_adapter, _logger);
    }

    private static byte[] Headers() => SyntheticNativeImage.Headers(SyntheticNativeImage.Standard);

    private static byte[] OneSite(int at) => SyntheticNativeImage.TextWithSiteAt(at, 0x3010, 0x2020);

    private static byte[] TwoSites(int first, int second)
    {
        var text = SyntheticNativeImage.TextWithSiteAt(first, 0x3010, 0x2020);
        SyntheticNativeImage.PutSite(text, second, 0x3010, 0x2020);
        return text;
    }

    private (SignatureMatch Match, string Path) Resolve(byte[] text, byte[]? headers = null)
    {
        var session = _cache.Begin(ClipBudgetSignature.ModuleName, headers ?? Headers(), 0x1000, 0x200);
        var match = ClipBudgetSignature.Resolve(text, 0x1000, session);
        session.Complete();
        return (match, session.Path);
    }

    [TestMethod]
    public void Begin_NoCacheFile_ScansAndWritesTheAddress()
    {
        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual(0x1040, match.LoadSiteRva);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _adapter.Writes);
        StringAssert.Contains(_adapter.Text, "animmem.clip 1040");
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_CachedAddressStillMatches_UsesItWithoutScanning()
    {
        Resolve(OneSite(0x40));

        var (match, path) = Resolve(TwoSites(0x40, 0x100));

        Assert.AreEqual(1, match.MatchCount, "a full scan of this text finds two matches, so the cache answered");
        Assert.AreEqual(0x1040, match.LoadSiteRva);
        Assert.AreEqual(0x3010, match.CounterRva);
        Assert.AreEqual("cached", path);
        Assert.AreEqual(1, _adapter.Writes, "nothing changed, so nothing is rewritten");
    }

    [TestMethod]
    public void Begin_CachedAddressNoLongerMatches_ScansAgainAndRewrites()
    {
        Resolve(OneSite(0x40));

        var (match, path) = Resolve(OneSite(0x80));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual(0x1080, match.LoadSiteRva);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(2, _adapter.Writes);
        StringAssert.Contains(_adapter.Text, "animmem.clip 1080");
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_CachedAddressNowMatchesNothing_ReportsNoMatchAndDropsTheEntry()
    {
        Resolve(OneSite(0x40));

        var (match, path) = Resolve(new byte[0x200]);

        Assert.AreEqual(0, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.IsFalse(_adapter.Text!.Contains("animmem.clip"));
    }

    [TestMethod]
    [DataRow("1000")]
    [DataRow("-1")]
    [DataRow("7FFFFFFF")]
    [DataRow("FFFFFFFF")]
    public void Begin_CachedAddressOutsideTheCodeOrNegative_ScansInstead(string rva)
    {
        _adapter.Text = Entry(rva);

        var (match, path) = Resolve(TwoSites(0x40, 0x100));

        Assert.AreEqual(2, match.MatchCount, "the cached address was refused, so the full scan ran");
        Assert.AreEqual("scanned", path);
    }

    [TestMethod]
    [DataRow("not a cache at all")]
    [DataRow("taom-engine-signature-cache 2\nkey x\nanimmem.clip 1040\n")]
    [DataRow("taom-engine-signature-cache 1\n")]
    [DataRow("\u0000\u0000\u0000\u0001")]
    public void Begin_CorruptCacheFile_ScansAndLogsOneLine(string text)
    {
        _adapter.Text = text;

        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.StartsWith(_logger.Lines[0], "INFO [SigCache] ");
        StringAssert.Contains(_adapter.Text, "animmem.clip 1040");
    }

    [TestMethod]
    [DataRow("animmem.clip")]
    [DataRow("animmem.clip zz")]
    [DataRow("animmem.clip 1040 extra")]
    public void Begin_BadEntryLineUnderTheRightKey_ScansAndLogsOneLine(string line)
    {
        Resolve(OneSite(0x40));
        var good = _adapter.Text!;
        _adapter.Text = good.Replace("animmem.clip 1040", line);

        var (match, path) = Resolve(TwoSites(0x40, 0x100));

        Assert.AreEqual(2, match.MatchCount, "the damaged file was ignored whole");
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_EmptyCacheFile_ScansWithoutALogLine()
    {
        _adapter.Text = "";

        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_ReadingTheCacheThrows_ScansAndLogsOneLine()
    {
        _adapter.ThrowOnRead = true;

        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "could not be read");
    }

    [TestMethod]
    public void Complete_WritingTheCacheThrows_StillReturnsTheScanAndLogsOneLine()
    {
        _adapter.ThrowOnWrite = true;

        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "could not be written");
    }

    [TestMethod]
    public void Begin_StampingTheModuleThrows_ScansAndLogsOneLine()
    {
        _adapter.ThrowOnStamp = true;

        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_ModuleFileChanged_DoesNotReuseTheOldAddresses()
    {
        Resolve(OneSite(0x40));
        _adapter.Stamp = "1001|638000000000000000";

        var (match, path) = Resolve(TwoSites(0x40, 0x100));

        Assert.AreEqual(2, match.MatchCount, "another size: the old address is not looked at");
        Assert.AreEqual("scanned", path);
    }

    [TestMethod]
    public void Begin_LastWriteTimeChanged_DoesNotReuseTheOldAddresses()
    {
        Resolve(OneSite(0x40));
        _adapter.Stamp = "1000|638000000000001000";

        var (match, path) = Resolve(TwoSites(0x40, 0x100));

        Assert.AreEqual(2, match.MatchCount);
        Assert.AreEqual("scanned", path);
    }

    [TestMethod]
    [DataRow(0x88, DisplayName = "PE TimeDateStamp")]
    [DataRow(0xD8, DisplayName = "PE CheckSum")]
    [DataRow(0xD0, DisplayName = "PE SizeOfImage")]
    [DataRow(0x9C, DisplayName = "PE SizeOfCode")]
    [DataRow(0xA8, DisplayName = "PE entry point")]
    public void Begin_HeaderFieldChanged_DoesNotReuseTheOldAddresses(int headerOffset)
    {
        Resolve(OneSite(0x40));
        var changed = Headers();
        changed[headerOffset] = 0x5A;

        var (match, path) = Resolve(TwoSites(0x40, 0x100), changed);

        Assert.AreEqual(2, match.MatchCount);
        Assert.AreEqual("scanned", path);
        StringAssert.Contains(_adapter.Text, "animmem.clip 1040", "the cache is rewritten for the new build");
    }

    [TestMethod]
    public void Begin_SameModuleAgain_KeyIsStableAcrossRuns()
    {
        Resolve(OneSite(0x40));
        var first = _adapter.Text;
        Resolve(OneSite(0x40));
        Assert.AreEqual(first, _adapter.Text);
    }

    [TestMethod]
    public void Begin_HeadersThatAreNotAPeImage_ScansAndWritesNothing()
    {
        var (match, path) = Resolve(OneSite(0x40), new byte[4096]);

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(0, _adapter.Writes);
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_ModuleFileNotFound_StillCachesUnderTheHeaderFieldsAlone()
    {
        _adapter.Stamp = null;
        Resolve(OneSite(0x40));

        var (match, path) = Resolve(TwoSites(0x40, 0x100));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("cached", path);
    }

    [TestMethod]
    public void Resolve_NoSession_ScansLikeBefore()
    {
        var match = ClipBudgetSignature.Resolve(TwoSites(0x40, 0x100), 0x1000);

        Assert.AreEqual(2, match.MatchCount);
    }

    private static string Entry(string rva) =>
        "taom-engine-signature-cache 1\nkey " + KeyFor(Stamp) + "\nanimmem.clip " + rva + "\n";

    private static string KeyFor(string stamp) =>
        EngineSignatureCache.BuildKey(stamp, Headers(), 0x1000, 0x200)!;

    private sealed class FakeCacheAdapter : IEngineSignatureCacheAdapter
    {
        public string? Text { get; set; }
        public string? Stamp { get; set; }
        public int Writes { get; private set; }
        public bool ThrowOnRead { get; set; }
        public bool ThrowOnWrite { get; set; }
        public bool ThrowOnStamp { get; set; }

        public string? ReadCache() => ThrowOnRead ? throw new InvalidOperationException("read") : Text;

        public void WriteCache(string text)
        {
            if (ThrowOnWrite)
                throw new InvalidOperationException("write");
            Text = text;
            Writes++;
        }

        public string? ModuleFileStamp(string moduleFileName) =>
            ThrowOnStamp ? throw new InvalidOperationException("stamp") : Stamp;
    }
}

/// <summary>An in-memory cache file shared by several services, as the one file on disk is between launches.</summary>
internal sealed class SharedCacheFile : IEngineSignatureCacheAdapter
{
    private string? _text;

    public string? ReadCache() => _text;

    public void WriteCache(string text) => _text = text;

    public string? ModuleFileStamp(string moduleFileName) => "5000|638000000000000000";
}

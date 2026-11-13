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
/// most one log line per failure, a changed DLL (stamp, header fields or code section) never reuses another build's
/// addresses, and neither does a changed pattern. A text with the signature twice proves a hit: a full scan of it would
/// report two matches.
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

    private static readonly string ClipKey = "animmem.clip#" + SignatureScanSession.PatternHash(ClipBudgetSignature.Parse(ClipBudgetSignature.Pattern));

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
        StringAssert.Contains(_adapter.Text, ClipKey + " 1040");
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
        StringAssert.Contains(_adapter.Text, ClipKey + " 1080");
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Begin_CachedAddressNowMatchesNothing_ReportsNoMatchAndDropsTheEntry()
    {
        Resolve(OneSite(0x40));

        var (match, path) = Resolve(new byte[0x200]);

        Assert.AreEqual(0, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.IsFalse(_adapter.Text!.Contains(ClipKey));
    }

    [TestMethod]
    [DataRow("1000")]
    [DataRow("800")]
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
    [DataRow("taom-engine-signature-cache 1\nkey x\nanimmem.clip 1040\n")]
    [DataRow("taom-engine-signature-cache 3\nkey x\n")]
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
        StringAssert.Contains(_adapter.Text, ClipKey + " 1040");
    }

    [TestMethod]
    [DataRow("animmem.clip")]
    [DataRow("animmem.clip zz")]
    [DataRow("animmem.clip 1040 extra")]
    public void Begin_BadEntryLineUnderTheRightKey_ScansAndLogsOneLine(string line)
    {
        Resolve(OneSite(0x40));
        var good = _adapter.Text!;
        _adapter.Text = good.Replace(ClipKey + " 1040", line);

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
        Assert.AreEqual(0, _adapter.Writes, "a session that could not read the file does not replace it");
    }

    [TestMethod]
    public void Begin_ReadingAndWritingBothThrow_LogsOneLineAndOnlyForTheRead()
    {
        _adapter.ThrowOnRead = true;
        _adapter.ThrowOnWrite = true;

        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
    }

    [TestMethod]
    public void Begin_ReadFailsInOneSession_TheOtherConsumersEntriesSurvive()
    {
        var file = new SharedCacheFile();
        var guardSide = new EngineSignatureCache(file, _logger);
        var session = guardSide.Begin("TaleWorlds.Native.dll", Headers(), 0x1000, 0x200);
        session.Find("skeleton.guard", OneSite(0x80), ClipBudgetSignature.Parse("48 C1 FF 04"), 2);
        session.Complete();
        var before = file.Text;
        file.ThrowOnRead = true;

        var (match, _) = ResolveWith(new EngineSignatureCache(file, _logger), OneSite(0x40));
        file.ThrowOnRead = false;

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual(before, file.Text, "the file the probe could not read is left as the guard wrote it");
    }

    [TestMethod]
    public void Begin_CacheFileOverTheLimit_IsTreatedAsDamagedAndRewritten()
    {
        _adapter.Text = new string('x', EngineSignatureCache.MaxTextLength + 1);

        var (match, path) = Resolve(OneSite(0x40));

        Assert.AreEqual(1, match.MatchCount);
        Assert.AreEqual("scanned", path);
        Assert.AreEqual(1, _logger.Lines.Count);
        StringAssert.Contains(_logger.Lines[0], "larger than");
        StringAssert.Contains(_adapter.Text, ClipKey + " 1040", "the damaged file is replaced");
        Assert.IsTrue(_adapter.Text!.Length < 1000);
    }

    [TestMethod]
    public void Begin_CacheFileAtTheLimit_IsStillRead()
    {
        Resolve(OneSite(0x40));
        _adapter.Text += new string('\n', EngineSignatureCache.MaxTextLength - _adapter.Text!.Length);
        Assert.AreEqual(EngineSignatureCache.MaxTextLength, _adapter.Text.Length);

        var (match, path) = Resolve(TwoSites(0x40, 0x100));

        Assert.AreEqual(1, match.MatchCount, "the cache answered");
        Assert.AreEqual("cached", path);
    }

    [TestMethod]
    public void Begin_PatternEditedUnderTheSameId_RescansAndDropsTheOldEntry()
    {
        var oldPattern = ClipBudgetSignature.Parse("48 C1 FF 04");
        var newPattern = ClipBudgetSignature.Parse("48 C1 FF ?");
        var text = new byte[0x200];
        Array.Copy(new byte[] { 0x48, 0xC1, 0xFF, 0x04 }, 0, text, 0x40, 4);
        Array.Copy(new byte[] { 0x48, 0xC1, 0xFF, 0x05 }, 0, text, 0x100, 4);
        var first = _cache.Begin("TaleWorlds.Native.dll", Headers(), 0x1000, 0x200);
        first.Find("some.id", text, oldPattern, 2);
        first.Complete();
        Assert.AreEqual(1, _adapter.Writes);

        var second = _cache.Begin("TaleWorlds.Native.dll", Headers(), 0x1000, 0x200);
        var hits = second.Find("some.id", text, newPattern, 2);
        second.Complete();

        Assert.AreEqual(2, hits.Count, "the old address was not used for the new pattern: the full scan ran and found both");
        Assert.IsFalse(second.WasCached("some.id"));
        Assert.AreEqual(2, _adapter.Writes, "the stale entry is written away");
        Assert.IsFalse(_adapter.Text!.Contains("some.id"));
    }

    [TestMethod]
    public void Begin_PatternEditedAndFoundOnce_ReplacesTheOldEntry()
    {
        var oldPattern = ClipBudgetSignature.Parse("48 C1 FF 04");
        var newPattern = ClipBudgetSignature.Parse("48 C1 FF 04 ?");
        var text = new byte[0x200];
        Array.Copy(new byte[] { 0x48, 0xC1, 0xFF, 0x04, 0x00 }, 0, text, 0x40, 5);
        var first = _cache.Begin("TaleWorlds.Native.dll", Headers(), 0x1000, 0x200);
        first.Find("some.id", text, oldPattern, 2);
        first.Complete();

        var second = _cache.Begin("TaleWorlds.Native.dll", Headers(), 0x1000, 0x200);
        second.Find("some.id", text, newPattern, 2);
        second.Complete();

        Assert.IsFalse(_adapter.Text!.Contains(SignatureScanSession.PatternHash(oldPattern)), "the entry of the old pattern is not kept");
        StringAssert.Contains(_adapter.Text, "some.id#" + SignatureScanSession.PatternHash(newPattern) + " 1040");
    }

    [TestMethod]
    public void PatternHash_IsStableAndSeesEveryByteAndWildcard()
    {
        Assert.AreEqual("93FC4317", SignatureScanSession.PatternHash(new[] { 1, 2, 3 }));
        Assert.AreEqual("51F11CEC", SignatureScanSession.PatternHash(new[] { 0x48, -1 }));
        Assert.AreNotEqual(SignatureScanSession.PatternHash(new[] { 0x48, -1 }), SignatureScanSession.PatternHash(new[] { 0x48, 0 }));
        Assert.AreNotEqual(SignatureScanSession.PatternHash(new[] { 1, 2 }), SignatureScanSession.PatternHash(new[] { 2, 1 }));
    }

    [TestMethod]
    public void Begin_FailureLogLines_NeverCarryTheExceptionMessage()
    {
        _adapter.Leak = @"Could not find file 'C:\Users\SecretAccount\Documents\Mount and Blade II Bannerlord\TAOM\engine_signature_cache.txt'.";
        _adapter.ThrowOnRead = true;
        Resolve(OneSite(0x40));
        _adapter.ThrowOnRead = false;
        _adapter.ThrowOnWrite = true;
        Resolve(OneSite(0x50));
        _adapter.ThrowOnWrite = false;
        _adapter.ThrowOnStamp = true;
        Resolve(OneSite(0x60));

        Assert.AreEqual(3, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        foreach (var line in _logger.Lines)
        {
            Assert.IsFalse(line.Contains("SecretAccount"), line);
            Assert.IsFalse(line.Contains("Users"), line);
            StringAssert.Contains(line, "IOException");
        }
    }

    [TestMethod]
    public void Begin_TextRvaOrTextSizeChanged_DoesNotReuseTheOldAddresses()
    {
        Resolve(OneSite(0x40));

        var otherSize = _cache.Begin(ClipBudgetSignature.ModuleName, Headers(), 0x1000, 0x300);
        var bySize = ClipBudgetSignature.Resolve(TwoSites(0x40, 0x100), 0x1000, otherSize);
        var otherRva = _cache.Begin(ClipBudgetSignature.ModuleName, Headers(), 0x2000, 0x200);
        var byRva = ClipBudgetSignature.Resolve(TwoSites(0x40, 0x100), 0x2000, otherRva);

        Assert.AreEqual(2, bySize.MatchCount);
        Assert.AreEqual(2, byRva.MatchCount);
    }

    [TestMethod]
    public void Resolve_TwoSessionsOnOneFile_EachKeepsTheOthersEntries()
    {
        var file = new SharedCacheFile();
        var guardSide = new EngineSignatureCache(file, _logger);
        var probeSide = new EngineSignatureCache(file, _logger);
        var guardSession = guardSide.Begin("TaleWorlds.Native.dll", Headers(), 0x1000, 0x200);
        guardSession.Find("skeleton.guard", OneSite(0x80), ClipBudgetSignature.Parse("48 C1 FF 04"), 2);
        guardSession.Complete();

        var (_, path) = ResolveWith(probeSide, OneSite(0x40));
        var again = ResolveWith(guardSide, TwoSites(0x40, 0x100));

        Assert.AreEqual("scanned", path);
        Assert.AreEqual("cached", again.Path, "the clip entry written by the second consumer is read by the first");
        StringAssert.Contains(file.Text, "skeleton.guard#");
        StringAssert.Contains(file.Text, ClipKey);
    }

    private (SignatureMatch Match, string Path) ResolveWith(EngineSignatureCache cache, byte[] text)
    {
        var session = cache.Begin(ClipBudgetSignature.ModuleName, Headers(), 0x1000, 0x200);
        var match = ClipBudgetSignature.Resolve(text, 0x1000, session);
        session.Complete();
        return (match, session.Path);
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
        StringAssert.Contains(_adapter.Text, ClipKey + " 1040", "the cache is rewritten for the new build");
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
        "taom-engine-signature-cache 2\nkey " + KeyFor(Stamp) + "\n" + ClipKey + " " + rva + "\n";

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

        public string? Leak { get; set; }

        public string? ReadCache(int maxChars) => ThrowOnRead ? throw Fail("read")
            : Text == null || Text.Length <= maxChars ? Text : Text.Substring(0, maxChars);

        public void WriteCache(string text)
        {
            if (ThrowOnWrite)
                throw Fail("write");
            Text = text;
            Writes++;
        }

        public string? ModuleFileStamp(string moduleFileName) =>
            ThrowOnStamp ? throw Fail("stamp") : Stamp;

        private Exception Fail(string what) => Leak == null
            ? new InvalidOperationException(what)
            : new System.IO.IOException(Leak);
    }
}

/// <summary>An in-memory cache file shared by several services, as the one file on disk is between launches.</summary>
internal sealed class SharedCacheFile : IEngineSignatureCacheAdapter
{
    public string? Text { get; private set; }

    public bool ThrowOnRead { get; set; }

    public string? ReadCache(int maxChars) => ThrowOnRead ? throw new System.IO.IOException("locked")
        : Text == null || Text.Length <= maxChars ? Text : Text.Substring(0, maxChars);

    public void WriteCache(string text) => Text = text;

    public string? ModuleFileStamp(string moduleFileName) => "5000|638000000000000000";
}

// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.SkeletonBuffer;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Features.MissionPerf.AnimMemory;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>
/// The install decisions and their order against a synthetic loaded image and a fake memory adapter: every reason a pool
/// is not guarded says so in exactly one line, nothing is written to engine code before the code blocks are protected,
/// each pool installs or reports on its own, and a failure after the allocation never leaves a half-installed state.
/// </summary>
[TestClass]
public class SkeletonBufferGuardServiceTests
{
    private const long Base = 0x7FF600000000;
    private const long Cave = 0x7FF5FFF00000;
    private const long Cave2 = Cave + 0x40;
    private const long Counter1 = Cave + 0x1000;
    private const long Counter2 = Cave + 0x1040;
    private const int TextRva = 0x1000;
    private const int GlobalRva = 0x3010;
    private const int WatchOffset = 0x40;
    private const int GuardOffset = 0x73;
    private const int Pool2Offset = 0x1A0;
    private const int Pool2CallOffset = 0x1C9;   // 0x29 after the pool 2 load, as in v1.5.4
    private const int CalleeOffset = 0x220;
    private const int Guard2Offset = 0x240;
    private const int SiteRva = TextRva + GuardOffset;
    private const int ResumeRva = SiteRva + 13;
    private const int Site2Rva = TextRva + Guard2Offset;
    private const int Resume2Rva = Site2Rva + 13;

    private ISkeletonBufferSettingsProvider _settings = null!;
    private ISkeletonBufferMemoryAdapter _memory = null!;
    private IDedicatedServerProvider _server = null!;
    private RecordingLogger _logger = null!;
    private SkeletonBufferGuardService _sut = null!;
    private byte[] _text = null!;

    private static readonly byte[] GuardOriginal =
    {
        0x41, 0x8B, 0xF4, 0x4C, 0x89, 0x7C, 0x24, 0x20, 0xF0, 0x0F, 0xC1, 0x75, 0x00,
        0x45, 0x8D, 0x7C, 0x24, 0xFF, 0x8B, 0xC6, 0xC1, 0xE8, 0x0B,
    };

    private static readonly byte[] Guard2Original =
    {
        0x4C, 0x89, 0x7C, 0x24, 0x20, 0x44, 0x8B, 0xFA, 0xF0, 0x44, 0x0F, 0xC1, 0x39,
        0x8D, 0x7A, 0xFF, 0x41, 0x8B, 0xC7, 0xC1, 0xE8, 0x0D,
    };

    private static readonly (string Name, int Va, int Size)[] Sections =
    {
        (".text", TextRva, 0x400),
        (".rdata", 0x2000, 0x100),
        (".data", 0x3000, 0x100),
    };

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<ISkeletonBufferSettingsProvider>();
        _settings.GuardEnabled.Returns(true);
        _settings.WatchEnabled.Returns(true);
        _memory = Substitute.For<ISkeletonBufferMemoryAdapter>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _logger = new RecordingLogger();

        _text = BuildText();
        _memory.GetModuleBase("TaleWorlds.Native.dll").Returns(Base);
        _memory.Read(Base, 4096).Returns(SyntheticNativeImage.Headers(Sections));
        _memory.Read(Base + TextRva, 0x400).Returns(_ => _text);
        _memory.AllocateNear(Arg.Any<long>(), Arg.Any<long>(), 0x2000).Returns(Cave);
        _memory.Write(Arg.Any<long>(), Arg.Any<byte[]>()).Returns(true);
        _memory.MakeExecutable(Arg.Any<long>(), Arg.Any<int>()).Returns(true);
        _memory.PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(EngineCodePatchResult.Written);

        _sut = new SkeletonBufferGuardService(_settings, _memory, _server, _logger);
    }

    private static byte[] BuildText(bool watch = true, bool guard = true, bool foreign = false, int guardOffset = GuardOffset,
        int globalRva = GlobalRva, bool secondWatch = false, bool secondGuard = false, bool pool2 = true,
        int pool2GlobalRva = GlobalRva, bool secondPool2 = false, bool guard2 = true, bool foreign2 = false,
        bool secondGuard2 = false, bool callLink = true, int guard2Offset = Guard2Offset)
    {
        var text = new byte[0x400];
        for (var i = 0; i < text.Length; i++) text[i] = 0xCC;
        if (watch) PutWatch(text, WatchOffset, globalRva);
        if (pool2) PutPool2(text, Pool2Offset, pool2GlobalRva);
        if (pool2 && callLink) PutCall(text, Pool2CallOffset, CalleeOffset);
        if (secondPool2) PutPool2(text, 0x2C0, pool2GlobalRva);
        if (secondWatch) PutWatch(text, 0x200, globalRva);
        if (guard) Array.Copy(GuardOriginal, 0, text, guardOffset, GuardOriginal.Length);
        if (secondGuard) Array.Copy(GuardOriginal, 0, text, 0x300, GuardOriginal.Length);
        if (guard2) Array.Copy(Guard2Original, 0, text, guard2Offset, Guard2Original.Length);
        if (secondGuard2) Array.Copy(Guard2Original, 0, text, 0x340, Guard2Original.Length);
        if (foreign)
        {
            var shape = new byte[]
            {
                0xE9, 0x11, 0x22, 0x33, 0x44, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
                0x45, 0x8D, 0x7C, 0x24, 0xFF, 0x8B, 0xC6, 0xC1, 0xE8, 0x0B,
            };
            Array.Copy(shape, 0, text, guardOffset, shape.Length);
        }
        if (foreign2)
        {
            var shape = new byte[]
            {
                0xE9, 0x11, 0x22, 0x33, 0x44, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
                0x8D, 0x7A, 0xFF, 0x41, 0x8B, 0xC7, 0xC1, 0xE8, 0x0D,
            };
            Array.Copy(shape, 0, text, guard2Offset, shape.Length);
        }
        return text;
    }

    private static void PutWatch(byte[] text, int offset, int globalRva)
    {
        var bytes = new byte[]
        {
            0x48, 0x8B, 0x0D, 0, 0, 0, 0,
            0x48, 0x81, 0xC1, 0xD0, 0x09, 0x00, 0x00,
            0x48, 0x63, 0x81, 0x50, 0x02, 0x00, 0x00,
            0x48, 0x69, 0xE8, 0x28, 0x01, 0x00, 0x00,
        };
        Array.Copy(bytes, 0, text, offset, bytes.Length);
        SyntheticNativeImage.PutInt32(text, offset + 3, globalRva - (TextRva + offset + 7));
    }

    private static void PutPool2(byte[] text, int offset, int globalRva)
    {
        var bytes = new byte[]
        {
            0x48, 0x8B, 0x15, 0, 0, 0, 0,
            0x48, 0x81, 0xC2, 0x28, 0x0C, 0x00, 0x00,
            0x48, 0x89, 0x5C, 0x24, 0x50,
            0x48, 0x63, 0x82, 0x50, 0x02, 0x00, 0x00,
            0x48, 0x69, 0xD8, 0x28, 0x01, 0x00, 0x00,
        };
        Array.Copy(bytes, 0, text, offset, bytes.Length);
        SyntheticNativeImage.PutInt32(text, offset + 3, globalRva - (TextRva + offset + 7));
    }

    private static void PutCall(byte[] text, int offset, int targetOffset)
    {
        text[offset] = 0xE8;
        SyntheticNativeImage.PutInt32(text, offset + 1, targetOffset - (offset + 5));
    }

    private void UseText(byte[] text) => _text = text;

    private void SetPatch(int siteRva, EngineCodePatchResult result) =>
        _memory.PatchCode(Base + siteRva, Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(result);

    private void AssertNoEngineWrite()
    {
        _memory.DidNotReceiveWithAnyArgs().AllocateNear(default, default, default);
        _memory.DidNotReceiveWithAnyArgs().Write(default, default!);
        _memory.DidNotReceiveWithAnyArgs().MakeExecutable(default, default);
        _memory.DidNotReceiveWithAnyArgs().PatchCode(default, default!, default!);
    }

    private string TheOneLine(string level)
    {
        Assert.AreEqual(1, _logger.Lines.Count, "exactly one line expected, got: " + string.Join(" | ", _logger.Lines));
        StringAssert.StartsWith(_logger.Lines[0], level + " [SkeletonBuffer] ");
        return _logger.Lines[0];
    }

    /// <summary>The one configuration or reason line of a pool ("guard ON pool N:" or "guard OFF pool N:").</summary>
    private string PoolLine(int pool, string level)
    {
        var lines = _logger.Lines.Where(l => l.Contains("guard ON pool " + pool + ":") || l.Contains("guard OFF pool " + pool + ":")).ToList();
        Assert.AreEqual(1, lines.Count, "exactly one line for pool " + pool + ", got: " + string.Join(" | ", _logger.Lines));
        StringAssert.StartsWith(lines[0], level + " [SkeletonBuffer] ");
        return lines[0];
    }

    private static byte[] Cave1Bytes() => SkeletonBufferCave.BuildCave(Cave, Counter1, Base + ResumeRva)!;

    private static byte[] Cave2Bytes() => SkeletonBufferCave.BuildPool2Cave(Cave2, Counter2, Base + Resume2Rva)!;

    private static byte[] Site1Jump() => SkeletonBufferCave.BuildSiteJump(Base + SiteRva, Cave)!;

    private static byte[] Site2Jump() => SkeletonBufferCave.BuildSiteJump(Base + Site2Rva, Cave2)!;

    // ---- the install ----

    [TestMethod]
    public void Install_EverythingMatches_WritesBothCavesProtectsOnceThenPatchesSite1ThenSite2()
    {
        var cave1 = Cave1Bytes();
        var cave2 = Cave2Bytes();
        var site1 = Site1Jump();
        var site2 = Site2Jump();

        _sut.Install();

        Received.InOrder(() =>
        {
            _memory.AllocateNear(Arg.Any<long>(), Arg.Any<long>(), 0x2000);
            _memory.Write(Cave, Arg.Is<byte[]>(b => b.SequenceEqual(cave1)));
            _memory.Write(Cave2, Arg.Is<byte[]>(b => b.SequenceEqual(cave2)));
            _memory.MakeExecutable(Cave, 0x1000);
            _memory.PatchCode(Base + SiteRva, Arg.Is<byte[]>(b => b.SequenceEqual(GuardOriginal.Take(13))),
                Arg.Is<byte[]>(b => b.SequenceEqual(site1)));
            _memory.PatchCode(Base + Site2Rva, Arg.Is<byte[]>(b => b.SequenceEqual(Guard2Original.Take(13))),
                Arg.Is<byte[]>(b => b.SequenceEqual(site2)));
        });
        _memory.Received(1).MakeExecutable(Arg.Any<long>(), Arg.Any<int>());
        _memory.Received(2).Write(Arg.Any<long>(), Arg.Any<byte[]>());
        _memory.DidNotReceiveWithAnyArgs().Free(default);
    }

    [TestMethod]
    public void Install_EverythingMatches_AsksForPagesBelowTheModuleWithinJumpRangeOfBothResumePoints()
    {
        _sut.Install();

        // highest: just below the module; lowest: the farthest the LATER resume point (pool 2's) can be from the cave in a rel32 jump.
        _memory.Received(1).AllocateNear(Base - 0x2000, Base + Resume2Rva - 0x7FF00000L, 0x2000);
    }

    [TestMethod]
    public void Install_EverythingMatches_LogsOneConfigurationLinePerPoolAndExposesTheTarget()
    {
        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.StartsWith(_logger.Lines[0],
            "INFO [SkeletonBuffer] guard ON pool 1: site=0x1073 resume=0x1080 cave=0x7FF5FFF00000 counter=0x7FF5FFF01000 scanMs=");
        StringAssert.StartsWith(_logger.Lines[1],
            "INFO [SkeletonBuffer] guard ON pool 2: site=0x1240 resume=0x124D cave=0x7FF5FFF00040 counter=0x7FF5FFF01040 scanMs=");
        var target = _sut.Target;
        Assert.IsNotNull(target);
        Assert.AreEqual(Base + GlobalRva, target!.GlobalAddress);
        Assert.IsTrue(target.Pool1Guarded);
        Assert.AreEqual(Counter1, target.Pool1CounterAddress);
        Assert.IsTrue(target.WatchPool2);
        Assert.IsTrue(target.Pool2Guarded);
        Assert.AreEqual(Counter2, target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_CalledTwice_InstallsOnce()
    {
        _sut.Install();
        _sut.Install();

        _memory.Received(2).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());   // one per pool, not four
        _memory.Received(1).Read(Base, 4096);
        Assert.AreEqual(2, _logger.Lines.Count);
    }

    [TestMethod]
    public void Install_HappyPath_BothPatchesAreTheLastAdapterCalls()
    {
        _sut.Install();

        var calls = _memory.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ToList();
        var first = calls.IndexOf("PatchCode");
        Assert.IsTrue(first >= 0);
        Assert.IsTrue(calls.LastIndexOf("Write") < calls.IndexOf("MakeExecutable"), "both caves are written before the page is protected");
        Assert.IsTrue(calls.IndexOf("MakeExecutable") < first);
        Assert.AreEqual(calls.Count - 2, first, "the two PatchCode calls are the last adapter calls");
        Assert.AreEqual(calls.Count - 1, calls.LastIndexOf("PatchCode"));
    }

    // ---- stands aside, one line each ----

    [TestMethod]
    public void Install_DedicatedServer_StandsAsideBeforeTouchingAnyMemory()
    {
        _server.IsDedicatedServer.Returns(true);

        _sut.Install();

        StringAssert.Contains(TheOneLine("INFO"), "guard OFF: dedicated server");
        _memory.DidNotReceiveWithAnyArgs().GetModuleBase(default!);
        _memory.DidNotReceiveWithAnyArgs().Read(default, default);
        AssertNoEngineWrite();
        Assert.IsNull(_sut.Target);
    }

    [TestMethod]
    public void Install_GuardSettingOff_WritesNothingForEitherPoolButKeepsTheWatchTarget()
    {
        _settings.GuardEnabled.Returns(false);

        _sut.Install();

        StringAssert.Contains(TheOneLine("INFO"), "guard OFF: setting off");
        AssertNoEngineWrite();
        Assert.IsNotNull(_sut.Target);
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
        Assert.AreEqual(0, _sut.Target.Pool1CounterAddress);
        Assert.AreEqual(0, _sut.Target.Pool2CounterAddress);
        Assert.AreEqual(Base + GlobalRva, _sut.Target.GlobalAddress);
    }

    [TestMethod]
    public void Install_VanillaTuningIsLoaded_TaomStillDecidesByTheBytesAndGuardsPool2()
    {
        // No module-id stand-aside: VanillaTuning installs at OnSubModuleLoad, before this phase, so its jump shows at site 1.
        // It has no pool 2 guard, so TAOM writes that one.
        UseText(BuildText(guard: false, foreign: true));

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        var pool1 = PoolLine(1, "INFO");
        StringAssert.Contains(pool1, "guard OFF pool 1: already guarded by another module");
        StringAssert.EndsWith(pool1, "so TAOM writes nothing at this site");
        StringAssert.StartsWith(PoolLine(2, "INFO"), "INFO [SkeletonBuffer] guard ON pool 2: site=0x1240");
        Assert.IsNotNull(_sut.Target, "the watch still runs, read-only");
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
        Assert.AreEqual(0, _sut.Target.Pool1CounterAddress, "TAOM cannot count another module's overflows");
        Assert.IsTrue(_sut.Target.Pool2Guarded);
        Assert.AreEqual(Counter2, _sut.Target.Pool2CounterAddress);

        // Only pool 2's code block is written, in its own slot; the allocation is reachable from pool 2's resume point.
        _memory.Received(1).Write(Arg.Any<long>(), Arg.Any<byte[]>());
        _memory.Received(1).Write(Cave2, Arg.Is<byte[]>(b => b.SequenceEqual(Cave2Bytes())));
        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        _memory.Received(1).PatchCode(Base + Site2Rva, Arg.Any<byte[]>(), Arg.Is<byte[]>(b => b.SequenceEqual(Site2Jump())));
        _memory.Received(1).AllocateNear(Base - 0x2000, Base + Resume2Rva - 0x7FF00000L, 0x2000);
    }

    [TestMethod]
    public void Install_AnotherModulesPatchAtBothSites_ReportsBothGuardedByAnotherModuleAndWritesNothing()
    {
        UseText(BuildText(guard: false, foreign: true, guard2: false, foreign2: true));

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.Contains(PoolLine(1, "INFO"), "already guarded by another module");
        StringAssert.Contains(PoolLine(2, "INFO"), "already guarded by another module");
        AssertNoEngineWrite();
        Assert.IsTrue(_sut.Target!.Pool1Guarded, "the watch must not warn of a freeze another module's guard prevents");
        Assert.IsTrue(_sut.Target.Pool2Guarded);
        Assert.AreEqual(0, _sut.Target.Pool1CounterAddress);
        Assert.AreEqual(0, _sut.Target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_AnotherModulesPatchAtPool2sSiteOnly_PoolOneIsStillGuarded()
    {
        UseText(BuildText(guard2: false, foreign2: true));

        _sut.Install();

        StringAssert.StartsWith(PoolLine(1, "INFO"), "INFO [SkeletonBuffer] guard ON pool 1: site=0x1073");
        StringAssert.Contains(PoolLine(2, "INFO"), "already guarded by another module");
        _memory.Received(1).Write(Arg.Any<long>(), Arg.Any<byte[]>());
        _memory.Received(1).Write(Cave, Arg.Is<byte[]>(b => b.SequenceEqual(Cave1Bytes())));
        _memory.Received(1).PatchCode(Base + SiteRva, Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.AreEqual(Counter1, _sut.Target!.Pool1CounterAddress);
        Assert.IsTrue(_sut.Target.Pool2Guarded);
        Assert.AreEqual(0, _sut.Target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_GuardSettingOffWithOnePoolForeign_SaysTheForeignPoolAndTheSettingOnce()
    {
        _settings.GuardEnabled.Returns(false);
        UseText(BuildText(guard: false, foreign: true));

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.Contains(PoolLine(1, "INFO"), "already guarded by another module");
        Assert.AreEqual(1, _logger.Lines.Count(l => l.Contains("setting off")));
        AssertNoEngineWrite();
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    // ---- the second pool's watch ----

    [TestMethod]
    public void Install_Pool2FoundOnceAndReadingTheSameGlobal_TheTargetWatchesIt()
    {
        _sut.Install();

        Assert.IsTrue(_sut.Target!.WatchPool2);
        Assert.AreEqual(0, _logger.Lines.Count(l => l.Contains("pool 2 watch OFF")), "no pool 2 watch line when it resolves");
    }

    [TestMethod]
    public void Install_Pool2FoundOnceAndReadingTheSameGlobal_StaysWatchedWhenTheGuardInstalls()
    {
        _sut.Install();

        Assert.IsTrue(_sut.Target!.Pool1Guarded);
        Assert.IsTrue(_sut.Target.WatchPool2, "the guard's rebuilt target keeps the pool 2 flag");
    }

    [TestMethod]
    public void Install_Pool2MatchesNothing_WatchesPool1OnlyAndPool2SaysWhyItIsNotGuarded()
    {
        UseText(BuildText(pool2: false));

        _sut.Install();

        Assert.IsFalse(_sut.Target!.WatchPool2);
        Assert.AreEqual(3, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.StartsWith(_logger.Lines[0], "WARN [SkeletonBuffer] pool 2 watch OFF: ");
        StringAssert.Contains(_logger.Lines[0], "matched nothing");
        StringAssert.Contains(_logger.Lines[0], "pool 1 only");
        StringAssert.StartsWith(PoolLine(1, "INFO"), "INFO [SkeletonBuffer] guard ON pool 1");
        StringAssert.Contains(PoolLine(2, "WARN"), "the pool 2 signature matched nothing");
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_Pool2MatchesTwice_WatchesPool1OnlyAndDoesNotGuardPool2()
    {
        UseText(BuildText(secondPool2: true));

        _sut.Install();

        Assert.IsFalse(_sut.Target!.WatchPool2);
        StringAssert.Contains(_logger.Lines[0], "pool 2 signature matched 2 or more times");
        StringAssert.Contains(PoolLine(2, "WARN"), "pool 2 signature matched 2 or more times");
        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
    }

    [TestMethod]
    public void Install_Pool2ReadsAnotherGlobal_WatchesPool1OnlyAndDoesNotGuardPool2()
    {
        UseText(BuildText(pool2GlobalRva: GlobalRva + 8));

        _sut.Install();

        Assert.IsFalse(_sut.Target!.WatchPool2);
        StringAssert.Contains(_logger.Lines[0], "pool 2 load reads the global 0x3018, not the watch site's 0x3010");
        StringAssert.Contains(PoolLine(2, "WARN"), "pool 2 load reads the global 0x3018");
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_Pool2Missing_DoesNotStopPool1FromInstalling()
    {
        UseText(BuildText(pool2: false));

        _sut.Install();

        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        _memory.Received(1).PatchCode(Base + SiteRva, Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
    }

    [TestMethod]
    public void Install_GuardSettingOffAndPool2Missing_StillSaysSoOnce()
    {
        _settings.GuardEnabled.Returns(false);
        UseText(BuildText(pool2: false));

        _sut.Install();

        Assert.AreEqual(1, _logger.Lines.Count(l => l.Contains("pool 2 watch OFF")));
    }

    // ---- the second pool's guard site ----

    [TestMethod]
    public void Install_Pool2GuardSignatureMatchesNothing_PoolOneStillInstallsAndPool2SaysWhy()
    {
        UseText(BuildText(guard2: false));

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.StartsWith(PoolLine(1, "INFO"), "INFO [SkeletonBuffer] guard ON pool 1");
        StringAssert.Contains(PoolLine(2, "WARN"), "the guard signature matched nothing in .text");
        _memory.Received(1).PatchCode(Base + SiteRva, Arg.Any<byte[]>(), Arg.Any<byte[]>());
        _memory.Received(1).Write(Arg.Any<long>(), Arg.Any<byte[]>());
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
        Assert.IsTrue(_sut.Target.WatchPool2, "the pool 2 watch does not depend on its guard");
    }

    [TestMethod]
    public void Install_Pool2GuardSignatureMatchesTwice_Pool2WritesNothing()
    {
        UseText(BuildText(secondGuard2: true));

        _sut.Install();

        StringAssert.Contains(PoolLine(2, "WARN"), "the guard signature matched 2 or more times in .text");
        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.IsFalse(_sut.Target!.Pool2Guarded);
    }

    [TestMethod]
    public void Install_Pool2SiteNotInTheFunctionThePool2LoadCalls_Pool2WritesNothing()
    {
        UseText(BuildText(callLink: false));

        _sut.Install();

        StringAssert.Contains(PoolLine(2, "WARN"), "not in the function the pool 2 load calls");
        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.IsFalse(_sut.Target!.Pool2Guarded);
        Assert.IsTrue(_sut.Target.Pool1Guarded);
    }

    [TestMethod]
    public void Install_Pool2SiteFarFromTheCallee_Pool2WritesNothing()
    {
        UseText(BuildText(guard2Offset: 0x2A1));   // 0x81 bytes after the callee entry at 0x220

        _sut.Install();

        StringAssert.Contains(PoolLine(2, "WARN"), "not in the function the pool 2 load calls");
        Assert.IsFalse(_sut.Target!.Pool2Guarded);
    }

    [TestMethod]
    public void Install_Pool1SignatureMissing_Pool2StillInstallsInItsOwnSlot()
    {
        UseText(BuildText(guard: false));

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.Contains(PoolLine(1, "WARN"), "the guard signature matched nothing in .text");
        StringAssert.StartsWith(PoolLine(2, "INFO"), "INFO [SkeletonBuffer] guard ON pool 2: site=0x1240");
        _memory.Received(1).Write(Cave2, Arg.Is<byte[]>(b => b.SequenceEqual(Cave2Bytes())));
        _memory.Received(1).Write(Arg.Any<long>(), Arg.Any<byte[]>());
        _memory.Received(1).PatchCode(Base + Site2Rva, Arg.Any<byte[]>(), Arg.Any<byte[]>());
        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.IsFalse(_sut.Target!.Pool1Guarded, "pool 1 is not guarded, so its on-screen warning must stay possible");
        Assert.AreEqual(0, _sut.Target.Pool1CounterAddress);
        Assert.IsTrue(_sut.Target.Pool2Guarded);
        Assert.AreEqual(Counter2, _sut.Target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_Pool1SiteNotNearTheWatchSite_Pool2StillInstalls()
    {
        UseText(BuildText(guardOffset: 0x180));

        _sut.Install();

        StringAssert.Contains(PoolLine(1, "WARN"), "not in the same function");
        StringAssert.StartsWith(PoolLine(2, "INFO"), "INFO [SkeletonBuffer] guard ON pool 2");
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsTrue(_sut.Target.Pool2Guarded);
    }

    // ---- the module and its signatures ----

    [TestMethod]
    public void Install_ModuleNotLoaded_OneLineAndNoTarget()
    {
        _memory.GetModuleBase("TaleWorlds.Native.dll").Returns(0L);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "TaleWorlds.Native.dll is not loaded");
        AssertNoEngineWrite();
        Assert.IsNull(_sut.Target);
    }

    [TestMethod]
    public void Install_HeadersUnreadable_OneLine()
    {
        _memory.Read(Base, 4096).Returns((byte[]?)null);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "headers");
        AssertNoEngineWrite();
    }

    [TestMethod]
    public void Install_HeadersNotPe_OneLine()
    {
        _memory.Read(Base, 4096).Returns(new byte[4096]);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "PE headers did not parse");
    }

    [TestMethod]
    public void Install_NoTextSection_OneLine()
    {
        _memory.Read(Base, 4096).Returns(SyntheticNativeImage.Headers((".data", 0x3000, 0x100)));

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "no .text section");
    }

    [TestMethod]
    public void Install_NoDataSection_OneLine()
    {
        _memory.Read(Base, 4096).Returns(SyntheticNativeImage.Headers((".text", TextRva, 0x400)));

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "no .data section");
    }

    [TestMethod]
    public void Install_TextUnreadable_OneLine()
    {
        _memory.Read(Base + TextRva, 0x400).Returns((byte[]?)null);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "could not read");
        AssertNoEngineWrite();
    }

    [TestMethod]
    public void Install_WatchPatternMatchesNothing_NoTargetAndNoWrite()
    {
        UseText(BuildText(watch: false));

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "watch signature matched nothing");
        AssertNoEngineWrite();
        Assert.IsNull(_sut.Target);
    }

    [TestMethod]
    public void Install_WatchPatternMatchesTwice_NoTargetAndNoWrite()
    {
        UseText(BuildText(secondWatch: true));

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "watch signature matched 2 or more times");
        AssertNoEngineWrite();
        Assert.IsNull(_sut.Target);
    }

    [TestMethod]
    public void Install_GuardPatternMatchesNothingForEitherPool_KeepsTheWatchTargetUnguarded()
    {
        UseText(BuildText(guard: false, guard2: false));

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.Contains(PoolLine(1, "WARN"), "guard signature matched nothing");
        StringAssert.Contains(PoolLine(2, "WARN"), "guard signature matched nothing");
        AssertNoEngineWrite();
        Assert.IsNotNull(_sut.Target);
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_GuardPatternMatchesTwice_PoolOneWritesNothing()
    {
        UseText(BuildText(secondGuard: true, guard2: false));

        _sut.Install();

        StringAssert.Contains(PoolLine(1, "WARN"), "guard signature matched 2 or more times");
        AssertNoEngineWrite();
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
    }

    [TestMethod]
    public void Install_GlobalOutsideData_OneLineAndNoTarget()
    {
        UseText(BuildText(globalRva: 0x2010));   // inside .rdata

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "not an aligned 8-byte slot inside .data");
        AssertNoEngineWrite();
        Assert.IsNull(_sut.Target);
    }

    [TestMethod]
    public void Install_GlobalMisaligned_OneLineAndNoTarget()
    {
        UseText(BuildText(globalRva: 0x3014));

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "not an aligned 8-byte slot inside .data");
        Assert.IsNull(_sut.Target);
    }

    [TestMethod]
    public void Install_GuardSiteNotNearTheWatchSite_PoolOneWritesNothing()
    {
        UseText(BuildText(guardOffset: 0x180, guard2: false));   // 0x140 bytes after the watch site: another function

        _sut.Install();

        StringAssert.Contains(PoolLine(1, "WARN"), "not in the same function");
        AssertNoEngineWrite();
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
    }

    [TestMethod]
    public void Install_GuardSiteBeforeTheWatchSite_PoolOneWritesNothing()
    {
        UseText(BuildText(guardOffset: 0x10, guard2: false));

        _sut.Install();

        StringAssert.Contains(PoolLine(1, "WARN"), "not in the same function");
        AssertNoEngineWrite();
    }

    // ---- failures after the allocation ----

    [TestMethod]
    public void Install_AllocationFails_OneLineAndNothingWritten()
    {
        _memory.AllocateNear(Arg.Any<long>(), Arg.Any<long>(), 0x2000).Returns(0L);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "no free memory within jump range");
        _memory.DidNotReceiveWithAnyArgs().Write(default, default!);
        _memory.DidNotReceiveWithAnyArgs().PatchCode(default, default!, default!);
        _memory.DidNotReceiveWithAnyArgs().Free(default);
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_PagesOutOfJumpRange_FreesThemAndWritesNothing()
    {
        _memory.AllocateNear(Arg.Any<long>(), Arg.Any<long>(), 0x2000).Returns(Base - 0x90000000L);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "out of reach");
        _memory.Received(1).Free(Base - 0x90000000L);
        _memory.DidNotReceiveWithAnyArgs().Write(default, default!);
        _memory.DidNotReceiveWithAnyArgs().PatchCode(default, default!, default!);
    }

    [TestMethod]
    public void Install_CaveWriteFails_FreesThePagesAndNeverPatchesASite()
    {
        _memory.Write(Arg.Any<long>(), Arg.Any<byte[]>()).Returns(false);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "writing the code block failed");
        _memory.Received(1).Free(Cave);
        _memory.DidNotReceiveWithAnyArgs().MakeExecutable(default, default);
        _memory.DidNotReceiveWithAnyArgs().PatchCode(default, default!, default!);
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
    }

    [TestMethod]
    public void Install_CaveTwoWriteFails_FreesThePagesAndNeverProtectsOrPatches()
    {
        _memory.Write(Cave2, Arg.Any<byte[]>()).Returns(false);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "writing the code block failed");
        _memory.Received(1).Free(Cave);
        _memory.DidNotReceiveWithAnyArgs().MakeExecutable(default, default);
        _memory.DidNotReceiveWithAnyArgs().PatchCode(default, default!, default!);
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_CaveProtectFails_FreesThePagesAndNeverPatchesASite()
    {
        _memory.MakeExecutable(Arg.Any<long>(), Arg.Any<int>()).Returns(false);

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "protecting the code block failed");
        Received.InOrder(() =>
        {
            _memory.Write(Cave, Arg.Any<byte[]>());
            _memory.Write(Cave2, Arg.Any<byte[]>());
            _memory.MakeExecutable(Cave, 0x1000);
            _memory.Free(Cave);
        });
        _memory.DidNotReceiveWithAnyArgs().PatchCode(default, default!, default!);
    }

    // ---- the site writes, every combination of the two outcomes ----

    [TestMethod]
    public void Install_BothSitesWritten_KeepsThePagesAndGuardsBothPools()
    {
        _sut.Install();

        _memory.DidNotReceiveWithAnyArgs().Free(default);
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
        Assert.IsTrue(_sut.Target.Pool2Guarded);
        Assert.AreEqual(Counter1, _sut.Target.Pool1CounterAddress);
        Assert.AreEqual(Counter2, _sut.Target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_Site1WrittenAndSite2NotWritten_KeepsThePagesBecauseSite1JumpsIntoThem()
    {
        SetPatch(Site2Rva, EngineCodePatchResult.NotWritten);

        _sut.Install();

        StringAssert.StartsWith(PoolLine(1, "INFO"), "INFO [SkeletonBuffer] guard ON pool 1");
        StringAssert.Contains(PoolLine(2, "WARN"), "the engine code was left unchanged");
        _memory.DidNotReceiveWithAnyArgs().Free(default);
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
        Assert.AreEqual(Counter1, _sut.Target.Pool1CounterAddress);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
        Assert.AreEqual(0, _sut.Target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_Site1NotWrittenAndSite2Written_KeepsThePagesBecauseSite2JumpsIntoThem()
    {
        SetPatch(SiteRva, EngineCodePatchResult.NotWritten);

        _sut.Install();

        StringAssert.Contains(PoolLine(1, "WARN"), "the engine code was left unchanged");
        StringAssert.StartsWith(PoolLine(2, "INFO"), "INFO [SkeletonBuffer] guard ON pool 2");
        _memory.DidNotReceiveWithAnyArgs().Free(default);
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.AreEqual(0, _sut.Target.Pool1CounterAddress);
        Assert.IsTrue(_sut.Target.Pool2Guarded);
        Assert.AreEqual(Counter2, _sut.Target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_NeitherSiteWritten_FreesThePagesOnceAndReportsBothPools()
    {
        _memory.PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(EngineCodePatchResult.NotWritten);

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.Contains(PoolLine(1, "WARN"), "the engine code was left unchanged");
        StringAssert.Contains(PoolLine(2, "WARN"), "the engine code was left unchanged");
        _memory.Received(1).Free(Cave);
        _memory.Received(2).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
        Assert.AreEqual(0, _sut.Target.Pool1CounterAddress);
        Assert.AreEqual(0, _sut.Target.Pool2CounterAddress);
    }

    [TestMethod]
    public void Install_Site1StateUnknown_KeepsThePagesLogsAnErrorAndWritesNoSecondSite()
    {
        SetPatch(SiteRva, EngineCodePatchResult.Unknown);

        _sut.Install();

        StringAssert.Contains(PoolLine(1, "ERROR"), "could not be confirmed");
        StringAssert.Contains(PoolLine(2, "WARN"), "not attempted");
        _memory.DidNotReceiveWithAnyArgs().Free(default);
        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_Site2StateUnknownAfterSite1Written_KeepsThePagesAndLogsAnError()
    {
        SetPatch(Site2Rva, EngineCodePatchResult.Unknown);

        _sut.Install();

        StringAssert.StartsWith(PoolLine(1, "INFO"), "INFO [SkeletonBuffer] guard ON pool 1");
        StringAssert.Contains(PoolLine(2, "ERROR"), "could not be confirmed");
        _memory.DidNotReceiveWithAnyArgs().Free(default);
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_Site1NotWrittenThenSite2StateUnknown_KeepsThePages()
    {
        SetPatch(SiteRva, EngineCodePatchResult.NotWritten);
        SetPatch(Site2Rva, EngineCodePatchResult.Unknown);

        _sut.Install();

        StringAssert.Contains(PoolLine(2, "ERROR"), "could not be confirmed");
        _memory.DidNotReceiveWithAnyArgs().Free(default);
    }

    [TestMethod]
    public void Install_Site1PatchThrows_KeepsThePagesAndNeverPatchesSite2()
    {
        _memory.PatchCode(Base + SiteRva, Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Install();

        var pool1 = PoolLine(1, "ERROR");
        StringAssert.Contains(pool1, "could not be confirmed");
        StringAssert.Contains(pool1, "InvalidOperationException: boom");
        StringAssert.Contains(PoolLine(2, "WARN"), "not attempted");
        Assert.IsFalse(_logger.Lines.Any(l => l.Contains("guard OFF:")), "no global line: " + string.Join(" | ", _logger.Lines));
        _memory.DidNotReceiveWithAnyArgs().Free(default);
        _memory.Received(1).PatchCode(Arg.Any<long>(), Arg.Any<byte[]>(), Arg.Any<byte[]>());
        Assert.IsFalse(_sut.Target!.Pool1Guarded);
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_Site1PatchThrowsWithOnlyPool1Planned_LogsOnePoolLineAndKeepsThePages()
    {
        UseText(BuildText(guard2: false, foreign2: true));
        _memory.PatchCode(Base + SiteRva, Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Install();

        StringAssert.Contains(PoolLine(1, "ERROR"), "InvalidOperationException: boom");
        Assert.IsFalse(_logger.Lines.Any(l => l.Contains("guard OFF:")), string.Join(" | ", _logger.Lines));
        _memory.DidNotReceiveWithAnyArgs().Free(default);
    }

    [TestMethod]
    public void Install_Site2PatchThrowsAfterSite1Written_KeepsThePagesAndPool1StaysGuarded()
    {
        _memory.PatchCode(Base + Site2Rva, Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Install();

        Assert.AreEqual(2, _logger.Lines.Count, string.Join(" | ", _logger.Lines));
        StringAssert.Contains(_logger.Lines[0], "guard ON pool 1");
        StringAssert.StartsWith(_logger.Lines[1], "ERROR [SkeletonBuffer] guard OFF pool 2: ");
        StringAssert.Contains(_logger.Lines[1], "could not be confirmed");
        _memory.DidNotReceiveWithAnyArgs().Free(default);
        Assert.IsTrue(_sut.Target!.Pool1Guarded);
    }

    [TestMethod]
    public void Install_Site1NotWrittenThenSite2PatchThrows_KeepsThePages()
    {
        SetPatch(SiteRva, EngineCodePatchResult.NotWritten);
        _memory.PatchCode(Base + Site2Rva, Arg.Any<byte[]>(), Arg.Any<byte[]>()).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Install();

        Assert.IsTrue(_logger.Lines.Any(l => l.StartsWith("ERROR ") && l.Contains("could not be confirmed")), string.Join(" | ", _logger.Lines));
        _memory.DidNotReceiveWithAnyArgs().Free(default);
    }

    [TestMethod]
    public void Install_OnlyPool2Planned_AndItsWriteLeavesTheOriginalBytes_FreesThePages()
    {
        UseText(BuildText(guard: false, foreign: true));
        SetPatch(Site2Rva, EngineCodePatchResult.NotWritten);

        _sut.Install();

        StringAssert.Contains(PoolLine(2, "WARN"), "the engine code was left unchanged");
        _memory.Received(1).Free(Cave);
        Assert.IsTrue(_sut.Target!.Pool1Guarded, "pool 1 is still guarded by the other module");
        Assert.IsFalse(_sut.Target.Pool2Guarded);
    }

    [TestMethod]
    public void Install_PatchesAreOrderedSite1ThenSite2()
    {
        _sut.Install();

        Received.InOrder(() =>
        {
            _memory.PatchCode(Base + SiteRva, Arg.Any<byte[]>(), Arg.Any<byte[]>());
            _memory.PatchCode(Base + Site2Rva, Arg.Any<byte[]>(), Arg.Any<byte[]>());
        });
    }

    [TestMethod]
    public void Install_AdapterThrowsAfterTheAllocation_FreesThePagesAndLogsOneLine()
    {
        _memory.MakeExecutable(Arg.Any<long>(), Arg.Any<int>()).Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "InvalidOperationException");
        _memory.Received(1).Free(Cave);
        _memory.DidNotReceiveWithAnyArgs().PatchCode(default, default!, default!);
    }

    [TestMethod]
    public void Install_AdapterThrowsBeforeTheAllocation_NeverThrowsOut()
    {
        _memory.GetModuleBase("TaleWorlds.Native.dll").Returns(_ => throw new InvalidOperationException("boom"));

        _sut.Install();

        StringAssert.Contains(TheOneLine("WARN"), "InvalidOperationException");
        AssertNoEngineWrite();
    }
}

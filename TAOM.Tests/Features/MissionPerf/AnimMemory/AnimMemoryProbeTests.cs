using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf.AnimMemory;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// The once-per-process arming of the clip memory probe against a synthetic image: the header line,
/// every reason it disables itself for, the NaN budget gate, and the reads after arming.
/// </summary>
[TestClass]
public class AnimMemoryProbeTests
{
    private const long Base = SyntheticNativeImage.Base;

    private INativeModuleMemoryAdapter _memory = null!;
    private IAnimationLoadingAdapter _loading = null!;
    private IModLogger _logger = null!;
    private AnimMemoryProbe _probe = null!;

    [TestInitialize]
    public void SetUp()
    {
        _memory = Substitute.For<INativeModuleMemoryAdapter>();
        _loading = Substitute.For<IAnimationLoadingAdapter>();
        _logger = Substitute.For<IModLogger>();
        _memory.GetModuleBase("TaleWorlds.Native.dll").Returns(Base);
        _memory.Copy(Base, 4096).Returns(SyntheticNativeImage.Headers(SyntheticNativeImage.Standard));
        _memory.Copy(Base + 0x1000, 0x200).Returns(SyntheticNativeImage.TextWithSiteAt(0x40, 0x3010, 0x2020));
        _memory.ReadInt32(Base + 0x2020).Returns(0x4B400000);
        _memory.ReadInt32(Base + 0x3010).Returns(5000000);
        _probe = new AnimMemoryProbe(_memory, _loading, _logger);
    }

    [TestMethod]
    public void EnsureArmed_SyntheticImage_ReturnsTrueAndLogsHeader()
    {
        const string prefix = "[AnimMem] armed: TaleWorlds.Native.dll base=0x180000000 text=0x1000+0x200 loadSite=0x1040 budgetSite=0x1065 counter=0x3010 budget=0x2020 budgetBytes=12582912 scanMs=";

        var armed = _probe.EnsureArmed();

        Assert.IsTrue(armed);
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith(prefix) && Regex.IsMatch(s, @"scanMs=\d+\.\d$")));
        _logger.Received(1).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureArmed_CalledTwice_ScansOnceAndLogsOnce()
    {
        Assert.IsTrue(_probe.EnsureArmed());
        Assert.IsTrue(_probe.EnsureArmed());

        _memory.Received(1).Copy(Base + 0x1000, 0x200);
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[AnimMem] armed: ")));
        _logger.Received(1).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureArmed_ModuleNotLoaded_DisablesWithReason()
    {
        _memory.GetModuleBase("TaleWorlds.Native.dll").Returns(0L);

        AssertDisables("TaleWorlds.Native.dll is not loaded in this process");
    }

    [TestMethod]
    public void EnsureArmed_HeadersNotPe_DisablesWithReason()
    {
        _memory.Copy(Base, 4096).Returns(new byte[4096]);

        AssertDisables("the module's PE headers did not parse");
    }

    [TestMethod]
    public void EnsureArmed_NoTextSection_DisablesWithReason()
    {
        _memory.Copy(Base, 4096).Returns(HeadersWithout(".text"));

        AssertDisables("no .text section in the module headers");
    }

    [TestMethod]
    public void EnsureArmed_NoRdataSection_DisablesWithReason()
    {
        _memory.Copy(Base, 4096).Returns(HeadersWithout(".rdata"));

        AssertDisables("no .rdata section in the module headers");
    }

    [TestMethod]
    public void EnsureArmed_NoDataSection_DisablesWithReason()
    {
        _memory.Copy(Base, 4096).Returns(HeadersWithout(".data"));

        AssertDisables("no .data section in the module headers");
    }

    [TestMethod]
    public void EnsureArmed_NoSite_DisablesWithReason()
    {
        _memory.Copy(Base + 0x1000, 0x200).Returns(new byte[0x200]);

        AssertDisables("the eviction-pass signature matched nothing in .text, so this engine build differs from the one it was written for");
    }

    [TestMethod]
    public void EnsureArmed_TwoSites_DisablesWithReason()
    {
        var text = SyntheticNativeImage.TextWithSiteAt(0x40, 0x3010, 0x2020);
        SyntheticNativeImage.PutSite(text, 0x100, 0x3010, 0x2020);
        _memory.Copy(Base + 0x1000, 0x200).Returns(text);

        AssertDisables("the eviction-pass signature matched 2 or more times in .text, so the site is ambiguous");
    }

    [TestMethod]
    public void EnsureArmed_CounterTargetInRdata_DisablesWithReason()
    {
        _memory.Copy(Base + 0x1000, 0x200).Returns(SyntheticNativeImage.TextWithSiteAt(0x40, 0x2010, 0x2020));

        AssertDisables("the loaded-bytes target 0x2010 is not an aligned 4-byte address inside .data");
        _memory.DidNotReceive().ReadInt32(Arg.Any<long>());
    }

    [TestMethod]
    public void EnsureArmed_CounterTargetMisaligned_DisablesWithReason()
    {
        _memory.Copy(Base + 0x1000, 0x200).Returns(SyntheticNativeImage.TextWithSiteAt(0x40, 0x3011, 0x2020));

        AssertDisables("the loaded-bytes target 0x3011 is not an aligned 4-byte address inside .data");
        _memory.DidNotReceive().ReadInt32(Arg.Any<long>());
    }

    [TestMethod]
    public void EnsureArmed_BudgetTargetOutsideRdata_DisablesWithReason()
    {
        _memory.Copy(Base + 0x1000, 0x200).Returns(SyntheticNativeImage.TextWithSiteAt(0x40, 0x3010, 0x3020));

        AssertDisables("the budget target 0x3020 is not an aligned 4-byte address inside .rdata");
        _memory.DidNotReceive().ReadInt32(Arg.Any<long>());
    }

    [TestMethod]
    public void EnsureArmed_BudgetFloatWrong_DisablesWithReason()
    {
        _memory.ReadInt32(Base + 0x2020).Returns(0x4B000000);

        AssertDisables("the budget float reads 8388608, expected 12582912 (12 MiB)");
    }

    [TestMethod]
    public void EnsureArmed_BudgetFloatNaN_Disables()
    {
        _memory.ReadInt32(Base + 0x2020).Returns(0x7FC00000);

        Assert.IsFalse(_probe.EnsureArmed());

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[AnimMem] disabled for this process: ") && s.Contains("reads NaN")));
        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.StartsWith("[AnimMem] armed: ")));
    }

    [TestMethod]
    public void EnsureArmed_CounterNegative_DisablesWithReason()
    {
        _memory.ReadInt32(Base + 0x3010).Returns(-4);

        AssertDisables("the loaded-bytes counter reads -4, a negative byte count");
    }

    [TestMethod]
    public void EnsureArmed_CopyThrows_DisablesWithReason()
    {
        _memory.Copy(Arg.Any<long>(), Arg.Any<int>()).Returns(x => throw new InvalidOperationException("boom"));

        AssertDisables("reading module memory threw InvalidOperationException: boom");
    }

    [TestMethod]
    public void EnsureArmed_AfterDisable_ReturnsFalseWithoutLoggingAgain()
    {
        _memory.GetModuleBase("TaleWorlds.Native.dll").Returns(0L);

        Assert.IsFalse(_probe.EnsureArmed());
        Assert.IsFalse(_probe.EnsureArmed());

        _logger.Received(1).LogInfo(Arg.Any<string>());
        _memory.Received(1).GetModuleBase(Arg.Any<string>());
    }

    [TestMethod]
    public void ReadLoadedBytes_NegativeAfterArming_DisablesForProcessWithOneLine()
    {
        Assert.IsTrue(_probe.EnsureArmed());
        _memory.ReadInt32(Base + 0x3010).Returns(-1);

        Assert.AreEqual(-1, _probe.ReadLoadedBytes());

        Assert.IsFalse(_probe.EnsureArmed());
        _logger.Received(1).LogInfo(AnimMemLine.Disabled("the loaded-bytes counter reads -1, a negative byte count"));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[AnimMem] disabled for this process: ")));
    }

    [TestMethod]
    public void ReadLoadedBytes_BeforeArming_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(() => _probe.ReadLoadedBytes());
        _memory.DidNotReceive().ReadInt32(Arg.Any<long>());
    }

    [TestMethod]
    public void ReadLoadedBytes_ReadsTheCounterAddress()
    {
        Assert.IsTrue(_probe.EnsureArmed());
        _memory.ReadInt32(Base + 0x3010).Returns(7340032);

        Assert.AreEqual(7340032, _probe.ReadLoadedBytes());
    }

    [TestMethod]
    public void IsAnyClipLoading_DelegatesToTheAdapter()
    {
        _loading.IsAnyAnimationLoadingFromDisk().Returns(true);

        Assert.IsTrue(_probe.IsAnyClipLoading());
    }

    [TestMethod]
    public void BudgetBytes_AfterArming_Is12582912()
    {
        Assert.IsTrue(_probe.EnsureArmed());

        Assert.AreEqual(12582912, _probe.BudgetBytes);
    }

    private void AssertDisables(string reason)
    {
        Assert.IsFalse(_probe.EnsureArmed());

        _logger.Received(1).LogInfo(AnimMemLine.Disabled(reason));
        _logger.Received(1).LogInfo(Arg.Any<string>());
    }

    private static byte[] HeadersWithout(string name) =>
        SyntheticNativeImage.Headers(SyntheticNativeImage.Standard.Where(s => s.Name != name).ToArray());
}

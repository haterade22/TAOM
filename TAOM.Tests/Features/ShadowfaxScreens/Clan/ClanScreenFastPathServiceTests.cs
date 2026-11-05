using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Clan;

[TestClass]
public class ClanScreenFastPathServiceTests
{
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private ICoopPresenceProvider _coop = null!;
    private FakeStampClock _clock = null!;
    private ClanScreenFastPathService _sut = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.ClanFastOpen.Returns(true);
        _coop = Substitute.For<ICoopPresenceProvider>();
        _coop.IsCoopActive.Returns(false);
        _clock = new FakeStampClock { Frequency = 10000, Now = 5000 };
        _logger = Substitute.For<IModLogger>();
        _sut = new ClanScreenFastPathService(_settings, _coop, _clock, _logger);
    }

    [TestMethod]
    public void ShouldSkipListBuild_FastPathOn_IsTrue()
    {
        Assert.IsTrue(_sut.ShouldSkipListBuild());
    }

    [TestMethod]
    public void ShouldSkipListBuild_ToggleOff_IsFalse()
    {
        _settings.ClanFastOpen.Returns(false);

        Assert.IsFalse(_sut.ShouldSkipListBuild());
    }

    [TestMethod]
    public void ShouldSkipListBuild_CoopActive_IsFalse()
    {
        _coop.IsCoopActive.Returns(true);

        Assert.IsFalse(_sut.ShouldSkipListBuild());
    }

    [TestMethod]
    public void ShouldSkipListBuild_CoopProviderThrows_CountsAsOff()
    {
        _coop.IsCoopActive.Throws(new InvalidOperationException("coop probe gone"));

        Assert.IsFalse(_sut.ShouldSkipListBuild());
    }

    [TestMethod]
    public void LogInstall_OneSite_LogsInfo()
    {
        _sut.LogInstall(1);

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Clan fast open installed")));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void LogInstall_AnyOtherCount_LogsOneWarningNamingTheCount(int swapped)
    {
        _sut.LogInstall(swapped);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not installed") && m.Contains("swapped " + swapped)));
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void Counting_OutsideAnOpenWindow_RecordsNothing()
    {
        _sut.ShouldSkipListBuild();
        _sut.EndBuild(_sut.BeginBuild());

        _sut.BeginOpen();

        Assert.AreEqual("fast open on; list builds: 0 run, 0 skipped, 0.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void BeginBuild_OutsideAnOpenWindow_ReturnsZeroWithoutReadingTheClock()
    {
        _clock.ThrowOnRead = 1;

        Assert.AreEqual(0L, _sut.BeginBuild());
    }

    [TestMethod]
    public void EndOpen_AfterOneRunAndTwoSkippedBuilds_PinsTheDetailFormat()
    {
        _sut.BeginOpen();
        _sut.ShouldSkipListBuild();
        _sut.ShouldSkipListBuild();
        var start = _sut.BeginBuild();
        _clock.Advance(381);
        _sut.EndBuild(start);

        Assert.AreEqual("fast open on; list builds: 1 run, 2 skipped, 38.1 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndOpen_ToggleOff_SaysSoAndCountsEveryBuildAsRun()
    {
        _settings.ClanFastOpen.Returns(false);
        _sut.BeginOpen();
        for (var i = 0; i < 3; i++)
        {
            _sut.ShouldSkipListBuild();
            var start = _sut.BeginBuild();
            _clock.Advance(100);
            _sut.EndBuild(start);
        }

        Assert.AreEqual("fast open off; list builds: 3 run, 0 skipped, 30.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndOpen_ClosesTheWindow_SoLaterBuildsAreNotCounted()
    {
        _sut.BeginOpen();
        _sut.EndOpen();
        Assert.AreEqual(0L, _sut.BeginBuild());

        _sut.BeginOpen();
        Assert.AreEqual("fast open on; list builds: 0 run, 0 skipped, 0.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndBuild_ZeroStart_CountsNothing()
    {
        _sut.BeginOpen();
        _sut.EndBuild(0);

        Assert.AreEqual("fast open on; list builds: 0 run, 0 skipped, 0.0 ms", _sut.EndOpen());
    }

    [TestMethod]
    public void EndOpen_BeforeAnyBeginOpen_ReturnsAnEmptyDetail()
    {
        Assert.AreEqual("", _sut.EndOpen());
    }
}

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CheatScreenGuard;
using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Tests.Features.CheatScreenGuard;

[TestClass]
public class CheatScreenGuardServiceTests
{
    private ICheatScreenGuardSettingsProvider _settings = null!;
    private IInquiryAdapter _notice = null!;
    private IModLogger _logger = null!;
    private CheatScreenGuardService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICheatScreenGuardSettingsProvider>();
        _notice = Substitute.For<IInquiryAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new CheatScreenGuardService(_settings, _notice, _logger);
    }

    private void AssertOneNotice() =>
        _notice.Received(1).ShowMessage("taom_cheat_screen_guard_notice", CheatScreenGuardService.NoticeFallback, null, null);

    [TestMethod]
    public void ShowCheatList_ToggleOn_ReturnsTrueAndSaysNothing()
    {
        _settings.CheatListsEnabled.Returns(true);

        Assert.IsTrue(_sut.ShowCheatList(CheatScreen.Inventory));

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _notice.DidNotReceive().ShowMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ShowCheatList_ToggleOff_ReturnsFalse()
        => Assert.IsFalse(_sut.ShowCheatList(CheatScreen.Inventory));

    [TestMethod]
    public void ShowCheatList_FirstHiddenList_LogsOneInfoLineAndShowsOneNotice()
    {
        _sut.ShowCheatList(CheatScreen.Inventory);

        _logger.Received(1).LogInfo(Arg.Any<string>());
        AssertOneNotice();
    }

    [TestMethod]
    public void ShowCheatList_LaterHiddenLists_SayNothingMore()
    {
        _sut.ShowCheatList(CheatScreen.Inventory);
        _sut.ShowCheatList(CheatScreen.PartyScreen);

        _logger.Received(1).LogInfo(Arg.Any<string>());
        AssertOneNotice();
    }

    [TestMethod]
    public void ShowCheatList_SettingsThrow_HidesTheList()
    {
        _settings.CheatListsEnabled.Returns(_ => throw new InvalidOperationException("boom"));

        Assert.IsFalse(_sut.ShowCheatList(CheatScreen.Inventory));
    }

    [TestMethod]
    public void ShowCheatList_LoggerAndNoticeThrow_StillReturnsFalse()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log"));
        _notice.When(n => n.ShowMessage(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("notice"));

        Assert.IsFalse(_sut.ShowCheatList(CheatScreen.Inventory));
    }

    [TestMethod]
    public void LogInstall_BothSitesGated_WritesOneOnInfoLine()
    {
        _sut.LogInstall(1, 1);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[CheatScreenGuard] ON:")));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void LogInstall_ASiteNotGated_WritesOneWarningNamingIt()
    {
        _sut.LogInstall(0, 1);

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("inventory gate off")));
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }
}

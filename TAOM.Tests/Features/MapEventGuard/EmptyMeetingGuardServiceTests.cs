using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard;
using TAOM.Tests.Features.LoadTimeStamps;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>#1099 follow-up: a meeting nobody can speak in opens the Leave menu instead of a null conversation.</summary>
[TestClass]
public class EmptyMeetingGuardServiceTests
{
    private IEmptyMeetingAdapter _meeting = null!;
    private IGameMenuAdapter _menus = null!;
    private IOutcomeLog _outcomes = null!;
    private IModLogger _logger = null!;
    private EmptyMeetingGuardService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _meeting = Substitute.For<IEmptyMeetingAdapter>();
        _menus = Substitute.For<IGameMenuAdapter>();
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(true);
        _outcomes = Substitute.For<IOutcomeLog>();
        _logger = Substitute.For<IModLogger>();
        _service = new EmptyMeetingGuardService(_meeting, _menus, _outcomes, _logger);
    }

    [TestMethod]
    public void ShouldDivertMeeting_PartnerCanSpeak_ReturnsFalseAndTouchesNothing()
    {
        _meeting.FindPartnerWithNoSpeaker().Returns((string?)null);

        Assert.IsFalse(_service.ShouldDivertMeeting());

        _menus.DidNotReceive().EnsureMenuOpen(Arg.Any<string>());
        _outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ShouldDivertMeeting_NobodyToSpeak_OpensTheLeaveMenuAndReturnsTrue()
    {
        _meeting.FindPartnerWithNoSpeaker().Returns("party_B");

        Assert.IsTrue(_service.ShouldDivertMeeting());

        _menus.Received(1).EnsureMenuOpen(EmptyMeetingGuardService.MenuId);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("party_B")));
    }

    [TestMethod]
    public void ShouldDivertMeeting_MenuWouldNotOpen_ReturnsFalseAndReportsNoPass()
    {
        _meeting.FindPartnerWithNoSpeaker().Returns("party_B");
        _menus.EnsureMenuOpen(Arg.Any<string>()).Returns(false);

        Assert.IsFalse(_service.ShouldDivertMeeting());

        _outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        var manifest = RepoPaths.ReadSource("tools/ingame_checks/1099.toml");
        string pattern = Regex.Match(manifest, @"^fail = \['(.+empty-meeting.+)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.AreNotEqual(string.Empty, pattern, "1099.toml has no empty-meeting fail pattern.");
        var failPattern = new Regex(pattern);
        _logger.Received(1).LogError(Arg.Is<string>(m => failPattern.IsMatch(m)));
    }

    [TestMethod]
    public void ShouldDivertMeeting_Diverted_WritesTheCheckLineTheManifestReads()
    {
        var logger = Substitute.For<IModLogger>();
        var service = new EmptyMeetingGuardService(_meeting, _menus, new OutcomeLog(logger, new FakeStampClock(), () => 0), logger);
        _meeting.FindPartnerWithNoSpeaker().Returns("party_B");

        service.ShouldDivertMeeting();

        string line = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!)
            .Single(l => l.StartsWith("[Check] ", StringComparison.Ordinal));
        var manifest = RepoPaths.ReadSource("tools/ingame_checks/1099.toml");
        string pattern = Regex.Match(manifest, @"^pass = \['(.+empty-meeting.+)'\]", RegexOptions.Multiline).Groups[1].Value;
        Assert.AreNotEqual(string.Empty, pattern, "1099.toml has no empty-meeting pass pattern.");
        StringAssert.Matches(line, new Regex(pattern), "the manifest would report this check as not reached.");
    }

    [TestMethod]
    public void Leave_ClosesTheEncounter()
    {
        _service.Leave();

        _meeting.Received(1).LeaveEncounter();
    }
}

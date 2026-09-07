using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Guards the decision half of the kingdom-vote deadlock fix (#547). The patches themselves take
/// sealed engine view models and cannot run outside a campaign, so every judgement they make lives
/// here instead: is this ballot stale, and has the player already been told about it.
/// </summary>
[TestClass]
public class KingdomVoteDeadlockServiceTests
{
    private IInquiryAdapter _inquiry;
    private IModLogger _logger;
    private KingdomVoteDeadlockService _sut;

    [TestInitialize]
    public void Setup()
    {
        _inquiry = Substitute.For<IInquiryAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new KingdomVoteDeadlockService(_inquiry, _logger);
    }

    private static IKingdomBallotAdapter Ballot(bool isStale, string key = "b1", string title = "Call Gondor to war")
    {
        var ballot = Substitute.For<IKingdomBallotAdapter>();
        ballot.IsStale.Returns(isStale);
        ballot.BallotKey.Returns(key);
        ballot.Title.Returns(title);
        return ballot;
    }

    // ---- ShouldSuppressBallot -------------------------------------------------------------

    [TestMethod]
    public void ShouldSuppressBallot_StaleBallot_ReturnsTrue()
    {
        Assert.IsTrue(_sut.ShouldSuppressBallot(Ballot(isStale: true)));
    }

    [TestMethod]
    public void ShouldSuppressBallot_FreshBallot_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldSuppressBallot(Ballot(isStale: false)));
    }

    [TestMethod]
    public void ShouldSuppressBallot_NullBallot_ReturnsFalse()
    {
        // Nothing to judge, so vanilla decides. The ExecuteFinalSelection backstop still covers
        // the window if vanilla goes on to open a cancelled election.
        Assert.IsFalse(_sut.ShouldSuppressBallot(null));
    }

    [TestMethod]
    public void ShouldSuppressBallot_StalenessCheckThrows_SuppressesAndWarns()
    {
        // Deliberately NOT "defer to vanilla". Vanilla is not a safe default at this call site: its
        // multi-clan branch builds the unclosable window, and its single-clan branch calls
        // GetChosenOutcomeText() on a null _chosenOutcome and throws. A ballot we could not judge is
        // withdrawn; the player recovers it by reopening the Kingdom screen.
        var ballot = Substitute.For<IKingdomBallotAdapter>();
        ballot.IsStale.Returns(_ => throw new InvalidOperationException("engine drift"));

        Assert.IsTrue(_sut.ShouldSuppressBallot(ballot));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("engine drift")));
    }

    // ---- AnnounceLapsedBallot -------------------------------------------------------------

    [TestMethod]
    public void AnnounceLapsedBallot_StaleBallot_ShowsLocalizedNoticeNamingTheVote()
    {
        _sut.AnnounceLapsedBallot(Ballot(isStale: true, title: "Call Dale to war"));

        _inquiry.Received(1).ShowMessage(
            KingdomVoteDeadlockService.LapseKey,
            KingdomVoteDeadlockService.LapseFallback,
            "VOTE",
            "Call Dale to war",
            null,
            null);
    }

    [TestMethod]
    public void AnnounceLapsedBallot_SameBallotTwice_ShowsOnce()
    {
        var ballot = Ballot(isStale: true, key: "same");

        _sut.AnnounceLapsedBallot(ballot);
        _sut.AnnounceLapsedBallot(ballot);

        _inquiry.Received(1).ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AnnounceLapsedBallot_DifferentBallots_ShowsEach()
    {
        _sut.AnnounceLapsedBallot(Ballot(isStale: true, key: "a"));
        _sut.AnnounceLapsedBallot(Ballot(isStale: true, key: "b"));

        _inquiry.Received(2).ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void AnnounceLapsedBallot_NullBallot_ShowsNothing()
    {
        _sut.AnnounceLapsedBallot(null);

        _inquiry.DidNotReceiveWithAnyArgs().ShowMessage(null, null, null, null, null, null);
    }

    [TestMethod]
    public void AnnounceLapsedBallot_NullTitle_StillAnnouncesWithEmptyVariable()
    {
        var ballot = Substitute.For<IKingdomBallotAdapter>();
        ballot.IsStale.Returns(true);
        ballot.BallotKey.Returns("k");
        ballot.Title.Returns((string)null);

        _sut.AnnounceLapsedBallot(ballot);

        _inquiry.Received(1).ShowMessage(
            KingdomVoteDeadlockService.LapseKey,
            KingdomVoteDeadlockService.LapseFallback,
            "VOTE",
            string.Empty,
            null,
            null);
    }

    [TestMethod]
    public void AnnounceLapsedBallot_PresenterThrows_DoesNotPropagate()
    {
        _inquiry
            .When(x => x.ShowMessage(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("presenter down"));

        // The caller is a Harmony patch mid-teardown of a stuck window. A throw here would leave
        // the window open, which is the exact defect this feature exists to remove.
        _sut.AnnounceLapsedBallot(Ballot(isStale: true));

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("presenter down")));
    }

    [TestMethod]
    public void AnnounceLapsedBallot_ManyDistinctBallots_DoesNotGrowUnbounded()
    {
        for (int i = 0; i < KingdomVoteDeadlockService.AnnouncedCap * 3; i++)
            _sut.AnnounceLapsedBallot(Ballot(isStale: true, key: "k" + i));

        Assert.IsTrue(
            _sut.AnnouncedCount <= KingdomVoteDeadlockService.AnnouncedCap,
            $"dedupe set grew to {_sut.AnnouncedCount}, cap is {KingdomVoteDeadlockService.AnnouncedCap}");
    }
}

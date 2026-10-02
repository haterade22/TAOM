using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Mike, 2026-10-02: a taken-over lord's "starting gold should be his own treasury if they can. If they
/// wanted to start with 1K they would've made a character from scratch." The engine assigns the player
/// 1,000 gold after every character-creation handler (FinalizeCharacterCreationState), which replaced
/// the treasury of every lord Player Switcher handed over; after character creation it is put back.
/// </summary>
[TestClass]
public class TakeoverTreasuryServiceTests
{
    private PlayerSwitchSessionStore _session = null!;
    private IPlayerIdentityAdapter _identity = null!;
    private IModLogger _logger = null!;
    private TakeoverTreasuryService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _session = new PlayerSwitchSessionStore();
        _identity = Substitute.For<IPlayerIdentityAdapter>();
        _identity.SetPlayerGold(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _logger = Substitute.For<IModLogger>();
        _sut = new TakeoverTreasuryService(_session, _identity, _logger);
    }

    [TestMethod]
    public void RestoreIfTakenOver_AfterATakeover_GivesTheLordBackTheirTreasury()
    {
        _session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AssumeIdentity, "lord_1_75", 18500);

        Assert.IsTrue(_sut.RestoreIfTakenOver());

        _identity.Received(1).SetPlayerGold("lord_1_75", 18500);
        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains("lord_1_75") && m.Contains("18500")));
    }

    [TestMethod]
    public void RestoreIfTakenOver_AfterAHandoverThatFinishedWithErrors_StillRestores()
    {
        // The player is that lord either way.
        _session.RecordOutcome(SwitchOutcome.SwitchedWithErrors, SwitchPath.AssumeIdentity, "lord_1_75", 18500);

        Assert.IsTrue(_sut.RestoreIfTakenOver());

        _identity.Received(1).SetPlayerGold("lord_1_75", 18500);
    }

    [TestMethod]
    public void RestoreIfTakenOver_ALordWithAnEmptyTreasury_StartsWithNothing()
    {
        // Zero is a treasury, not "nothing recorded": the engine's 1,000 must not outlive it.
        _session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AssumeIdentity, "lord_1_75", 0);

        Assert.IsTrue(_sut.RestoreIfTakenOver());

        _identity.Received(1).SetPlayerGold("lord_1_75", 0);
    }

    [DataTestMethod]
    [DataRow(SwitchOutcome.NotAttempted)]
    [DataRow(SwitchOutcome.Blocked)]
    [DataRow(SwitchOutcome.Failed)]
    public void RestoreIfTakenOver_WhenThePlayerKeptTheirOwnCharacter_LeavesTheGoldToTheCaller(SwitchOutcome outcome)
    {
        _session.RecordOutcome(outcome, SwitchPath.AssumeIdentity, "lord_1_75", 18500);

        Assert.IsFalse(_sut.RestoreIfTakenOver());

        _identity.DidNotReceiveWithAnyArgs().SetPlayerGold(default!, default);
    }

    [TestMethod]
    public void RestoreIfTakenOver_AfterAnAdoptedWanderer_LeavesTheGoldToTheCaller()
    {
        // A wanderer leads the clan the player made, which gets a new character's starting gold
        // (StartupResources, phase 9).
        _session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AdoptIntoPlayerClan, "wanderer_1", 2000);

        Assert.IsFalse(_sut.RestoreIfTakenOver());

        _identity.DidNotReceiveWithAnyArgs().SetPlayerGold(default!, default);
    }

    [TestMethod]
    public void RestoreIfTakenOver_WithNoHeroRecorded_WritesNothingButStillReportsTheTakeover()
    {
        _session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AssumeIdentity, "", 18500);

        Assert.IsTrue(_sut.RestoreIfTakenOver());

        _identity.DidNotReceiveWithAnyArgs().SetPlayerGold(default!, default);
    }

    [TestMethod]
    public void RestoreIfTakenOver_WithNoGoldRecorded_WritesNothingButStillReportsTheTakeover()
    {
        _session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AssumeIdentity, "lord_1_75", -1);

        Assert.IsTrue(_sut.RestoreIfTakenOver());

        _identity.DidNotReceiveWithAnyArgs().SetPlayerGold(default!, default);
    }

    [TestMethod]
    public void RestoreIfTakenOver_WhenTheHeroIsNotThePlayer_WarnsInsteadOfClaimingTheRestore()
    {
        _identity.SetPlayerGold("lord_1_75", 18500).Returns(false);
        _session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AssumeIdentity, "lord_1_75", 18500);

        Assert.IsTrue(_sut.RestoreIfTakenOver());

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("keeps")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("lord_1_75")));
    }

    [TestMethod]
    public void RestoreIfTakenOver_AfterANewCreationStarted_LeavesTheGoldToTheCaller()
    {
        _session.RecordOutcome(SwitchOutcome.Switched, SwitchPath.AssumeIdentity, "lord_1_75", 18500);
        _session.ResetForNewCreation();

        Assert.IsFalse(_sut.RestoreIfTakenOver());

        _identity.DidNotReceiveWithAnyArgs().SetPlayerGold(default!, default);
    }
}

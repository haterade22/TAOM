using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. The kingdom-join offer, and specifically the gate on it.
///
/// The predecessor mod raised this prompt without checking whether a handover had happened, so an
/// ordinary character creation could be asked whether it wanted to join a kingdom; its own feature
/// doc recorded that as a quirk. These tests exist to keep that from coming back.
/// </summary>
[TestClass]
public class KingdomJoinOfferServiceTests
{
    private const string KingdomId = "erebor_kingdom";

    private IPlayerSwitchSession _session = null!;
    private IKingdomJoinAdapter _kingdoms = null!;
    private IInquiryAdapter _inquiry = null!;
    private IPlayerSwitchPolicyProvider _policy = null!;
    private IModLogger _logger = null!;
    private KingdomJoinOfferService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _session = Substitute.For<IPlayerSwitchSession>();
        _kingdoms = Substitute.For<IKingdomJoinAdapter>();
        _inquiry = Substitute.For<IInquiryAdapter>();
        _policy = Substitute.For<IPlayerSwitchPolicyProvider>();
        _logger = Substitute.For<IModLogger>();

        _policy.Current.Returns(PlayerSwitchPolicy.Default);
        _session.LastOutcome.Returns(SwitchOutcome.Switched);
        _session.LastPath.Returns(SwitchPath.AdoptIntoPlayerClan);
        _kingdoms.FindJoinableKingdomForPlayerCulture().Returns(KingdomId);
        _kingdoms.GetKingdomName(KingdomId).Returns("Erebor");

        _sut = new KingdomJoinOfferService(_session, _kingdoms, _inquiry, _policy, _logger);
    }

    private void AssertNoPrompt()
        => _inquiry.DidNotReceive().ShowTwoOptionInquiry(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Action>(), Arg.Any<Action>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<System.Collections.Generic.IReadOnlyDictionary<string, string>>(), Arg.Any<bool>());

    [TestMethod]
    public void AnAdoptedPlayerWithAKingdomlessClan_IsOffered()
    {
        _sut.OfferIfEarned();

        _inquiry.Received(1).ShowTwoOptionInquiry(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Action>(), Arg.Any<Action>(),
            "KINGDOM", "Erebor",
            Arg.Any<System.Collections.Generic.IReadOnlyDictionary<string, string>>(), Arg.Any<bool>());
    }

    [TestMethod]
    public void APlayerWhoNeverSwitched_IsNotAsked()
    {
        _session.LastOutcome.Returns(SwitchOutcome.NotAttempted);

        _sut.OfferIfEarned();

        AssertNoPrompt();
    }

    [TestMethod]
    public void ABlockedOrFailedHandover_IsNotAsked()
    {
        foreach (var outcome in new[] { SwitchOutcome.Blocked, SwitchOutcome.Failed })
        {
            _inquiry.ClearReceivedCalls();
            _session.LastOutcome.Returns(outcome);

            _sut.OfferIfEarned();

            AssertNoPrompt();
        }
    }

    [TestMethod]
    public void APlayerWhoTookOverALord_IsNotAsked()
    {
        _session.LastPath.Returns(SwitchPath.AssumeIdentity);

        _sut.OfferIfEarned();

        AssertNoPrompt();
        _kingdoms.DidNotReceive().FindJoinableKingdomForPlayerCulture();
    }

    [TestMethod]
    public void WhenThereIsNoKingdomToJoin_NothingIsAsked()
    {
        _kingdoms.FindJoinableKingdomForPlayerCulture().Returns(string.Empty);

        _sut.OfferIfEarned();

        AssertNoPrompt();
    }

    [TestMethod]
    public void WhenTheFeatureIsDisabled_NothingIsAsked()
    {
        _policy.Current.Returns(PlayerSwitchPolicy.Disabled);

        _sut.OfferIfEarned();

        AssertNoPrompt();
        _kingdoms.DidNotReceive().FindJoinableKingdomForPlayerCulture();
    }

    [TestMethod]
    public void AcceptingTheOffer_JoinsThatExactKingdom()
    {
        Action? accept = null;
        _inquiry.ShowTwoOptionInquiry(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Do<Action>(a => accept = a), Arg.Any<Action>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<System.Collections.Generic.IReadOnlyDictionary<string, string>>(), Arg.Any<bool>());

        _sut.OfferIfEarned();
        Assert.IsNotNull(accept, "the affirmative action must be supplied");

        accept!();

        _kingdoms.Received(1).JoinPlayerClanToKingdom(KingdomId);
    }

    [TestMethod]
    public void DecliningTheOffer_ChangesNothing()
    {
        Action? decline = null;
        _inquiry.ShowTwoOptionInquiry(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Action>(), Arg.Do<Action>(a => decline = a),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<System.Collections.Generic.IReadOnlyDictionary<string, string>>(), Arg.Any<bool>());

        _sut.OfferIfEarned();
        decline!();

        _kingdoms.DidNotReceive().JoinPlayerClanToKingdom(Arg.Any<string>());
    }
}

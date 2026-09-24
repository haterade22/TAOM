using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.PlayerSwitcher;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// v1.5.0 made OnCharacterCreationIsOverEvent an MbEvent&lt;int&gt; fired ten times (index 0..9). The
/// offer is raised exactly once, in the last phase, after Advanced Starting Options (phase 8) has
/// settled the player's kingdom.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class KingdomJoinOfferBehaviorTests
{
    private IKingdomJoinOfferService _offer;
    private IModLogger _logger;
    private KingdomJoinOfferBehavior _sut;

    [TestInitialize]
    public void Setup()
    {
        _offer = Substitute.For<IKingdomJoinOfferService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new KingdomJoinOfferBehavior(_offer, _logger);
    }

    [TestMethod]
    public void OnCharacterCreationIsOver_LastPhase_OffersOnce()
    {
        _sut.OnCharacterCreationIsOver(9);

        _offer.Received(1).OfferIfEarned();
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void OnCharacterCreationIsOver_EarlierPhase_DoesNotOffer(int index)
    {
        _sut.OnCharacterCreationIsOver(index);

        _offer.DidNotReceive().OfferIfEarned();
    }

    [TestMethod]
    public void OnCharacterCreationIsOver_AllTenPhases_OffersExactlyOnce()
    {
        for (var index = 0; index < 10; index++)
            _sut.OnCharacterCreationIsOver(index);

        _offer.Received(1).OfferIfEarned();
    }

    [TestMethod]
    public void OnCharacterCreationIsOver_ServiceThrows_LogsAndDoesNotThrow()
    {
        _offer.When(o => o.OfferIfEarned()).Do(_ => throw new InvalidOperationException("boom"));

        _sut.OnCharacterCreationIsOver(9);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("kingdom-join offer failed")));
    }
}

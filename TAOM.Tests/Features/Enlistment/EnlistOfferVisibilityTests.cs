using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The enlist offer is now SHOWN for two verdicts and hidden for the rest, and the split is only
/// safe because <see cref="EnlistGateResult"/> is decided by an ORDERED ladder: reaching a verdict
/// proves every verdict above it passed. That makes the ladder's order load-bearing on a UI
/// decision, which is exactly the kind of coupling that breaks silently when someone reorders the
/// checks for readability. Both halves are pinned here.
/// </summary>
[TestClass]
public class EnlistOfferVisibilityTests
{
    private EnlistmentStore _store = null!;
    private ICommanderLordAdapter _commander = null!;
    private IPlayerContextAdapter _playerContext = null!;
    private IEnlistmentConfigProvider _config = null!;
    private EnlistmentDialogGateService _gate = null!;

    [TestInitialize]
    public void Setup()
    {
        _store = new EnlistmentStore(Substitute.For<IModLogger>());
        _commander = Substitute.For<ICommanderLordAdapter>();
        _playerContext = Substitute.For<IPlayerContextAdapter>();
        _config = Substitute.For<IEnlistmentConfigProvider>();
        _config.GetConfig().Returns(new EnlistmentCoreConfig());
        _gate = new EnlistmentDialogGateService(_store, _commander, _playerContext, _config, null);

        _commander.IsLord("lord_1_1").Returns(true);
        _playerContext.IsUnderMercenaryService().Returns(false);
        _playerContext.GetPlayerKingdomId().Returns((string)null);
    }

    private void GiveCommanderALiveColumn() =>
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false,
            partyId: "lord_party_1", partyIsActive: true, name: "Lord Test"));

    private void MakeCommanderPartyless() =>
        _commander.GetSnapshot("lord_1_1").Returns(new CommanderSnapshot(
            exists: true, isAlive: true, isPrisoner: false, partyId: null, name: "Lord Test"));

    // ---- the visibility split ------------------------------------------------------------

    /// <summary>
    /// CommanderUnavailable is deliberately absent from the shown set. It fires for every governor
    /// and every garrison-sitting lord, so greying it would park a permanently-dead line near the
    /// top of a large fraction of all noble conversations for the whole campaign — and unlike the
    /// war case, a lord with no column is visibly not leading one.
    /// </summary>
    [DataTestMethod]
    [DataRow(EnlistGateResult.Ok, true)]
    [DataRow(EnlistGateResult.AtWarWithYourKingdom, true)]
    [DataRow(EnlistGateResult.AlreadyEnlisted, false)]
    [DataRow(EnlistGateResult.NotALord, false)]
    [DataRow(EnlistGateResult.UnderMercenaryContract, false)]
    [DataRow(EnlistGateResult.CommanderUnavailable, false)]
    [DataRow(EnlistGateResult.FeatureDisabled, false)]
    public void ShowsOfferFor_EachVerdict_ShowsOnlyOkAndAtWar(EnlistGateResult verdict, bool expected)
    {
        Assert.AreEqual(expected, EnlistmentDialogBehavior.ShowsOfferFor(verdict));
    }

    [DataTestMethod]
    [DataRow(EnlistGateResult.Ok, true)]
    [DataRow(EnlistGateResult.AtWarWithYourKingdom, false)]
    [DataRow(EnlistGateResult.CommanderUnavailable, false)]
    public void OfferIsTakeable_EachVerdict_OnlyACleanOkIsClickable(EnlistGateResult verdict, bool expected)
    {
        Assert.AreEqual(expected, EnlistmentDialogBehavior.OfferIsTakeable(verdict));
    }

    /// <summary>
    /// Drift guard. A new verdict added to the enum has to be classified show-greyed or hide by a
    /// human; without this the rows above would silently stop covering the whole set.
    /// </summary>
    [TestMethod]
    public void EnlistGateResult_MemberCount_MatchesTheSplitPinnedHere()
    {
        Assert.AreEqual(
            7,
            Enum.GetValues(typeof(EnlistGateResult)).Length,
            "EnlistGateResult changed. Decide explicitly whether the new verdict SHOWS greyed or HIDES, then extend the rows in this class.");
    }

    // ---- the ladder order the split rests on ---------------------------------------------

    /// <summary>
    /// The mandated pin. A mercenary player talking to a lord with no party fails TWO gates; the
    /// ladder must report the earlier one. If the party/snapshot check ever moves above the
    /// mercenary check, this flips to CommanderUnavailable and the "reaching a verdict proves the
    /// ones above it passed" reasoning behind the visibility split stops holding.
    /// </summary>
    [TestMethod]
    public void CanEnlistWith_MercenaryPlayerAndPartylessLord_ReturnsUnderMercenaryContract()
    {
        _playerContext.IsUnderMercenaryService().Returns(true);
        MakeCommanderPartyless();

        Assert.AreEqual(EnlistGateResult.UnderMercenaryContract, _gate.CanEnlistWith("lord_1_1"));
    }

    /// <summary>
    /// AtWarWithYourKingdom is the ONLY greyed verdict, so it must sit at the bottom of the ladder.
    /// A mercenary player whose commander is also at war with their kingdom must not see the greyed
    /// war line — the mercenary contract is the real blocker and hides the option outright.
    /// </summary>
    [TestMethod]
    public void CanEnlistWith_MercenaryPlayerAndCommanderAtWar_ReturnsUnderMercenaryContractNotAtWar()
    {
        _playerContext.IsUnderMercenaryService().Returns(true);
        _playerContext.GetPlayerKingdomId().Returns("rohan_kingdom");
        _commander.IsAtWarWithFaction("lord_1_1", "rohan_kingdom").Returns(true);
        GiveCommanderALiveColumn();

        Assert.AreEqual(EnlistGateResult.UnderMercenaryContract, _gate.CanEnlistWith("lord_1_1"));
    }

    /// <summary>
    /// The same ordering point from the other side: a partyless lord who is ALSO at war hides the
    /// line rather than greying it, because CommanderUnavailable is decided first.
    /// </summary>
    [TestMethod]
    public void CanEnlistWith_PartylessLordAtWarWithPlayerKingdom_ReturnsCommanderUnavailableNotAtWar()
    {
        _playerContext.GetPlayerKingdomId().Returns("rohan_kingdom");
        _commander.IsAtWarWithFaction("lord_1_1", "rohan_kingdom").Returns(true);
        MakeCommanderPartyless();

        Assert.AreEqual(EnlistGateResult.CommanderUnavailable, _gate.CanEnlistWith("lord_1_1"));
    }

    /// <summary>The greyed case is genuinely reachable — a healthy commander, blocked only by the war.</summary>
    [TestMethod]
    public void CanEnlistWith_HealthyCommanderAtWarWithPlayerKingdom_ReturnsAtWarAndIsShownGreyed()
    {
        _playerContext.GetPlayerKingdomId().Returns("rohan_kingdom");
        _commander.IsAtWarWithFaction("lord_1_1", "rohan_kingdom").Returns(true);
        GiveCommanderALiveColumn();

        var verdict = _gate.CanEnlistWith("lord_1_1");

        Assert.AreEqual(EnlistGateResult.AtWarWithYourKingdom, verdict);
        Assert.IsTrue(EnlistmentDialogBehavior.ShowsOfferFor(verdict));
        Assert.IsFalse(EnlistmentDialogBehavior.OfferIsTakeable(verdict));
    }
}

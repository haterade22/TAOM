using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #550. A campaign saved before the takeover path learned to reassign clan leadership has
/// the AI king as Clan.PlayerClan.Leader and the player as an ordinary member. Vanilla keys the
/// player's part in every kingdom election off the clan LEADER, so such a player can never vote and
/// the decision popup renders already resolved and unclosable. The repair runs once per session
/// launch and promotes the player through vanilla succession, but only in exactly the state the
/// takeover would have produced; every other state is a reason to stand still.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PlayerClanLeadershipServiceTests
{
    private const string Hero = "lord_1_75";
    private const string Clan = "clan_empire_west_1";
    private const string ClanName = "House of Hurin";
    private const string King = "lord_1_7";

    private IPlayerIdentityAdapter _identity = null!;
    private ICoopSessionProvider _coop = null!;
    private IInquiryAdapter _inquiry = null!;
    private IModLogger _logger = null!;
    private PlayerClanLeadershipService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _identity = Substitute.For<IPlayerIdentityAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _inquiry = Substitute.For<IInquiryAdapter>();
        _logger = Substitute.For<IModLogger>();

        _coop.IsAuthority.Returns(true);
        _identity.PromoteToClanLeader(Hero).Returns(true);

        _sut = new PlayerClanLeadershipService(_identity, _coop, _inquiry, _logger);
    }

    private void Given(
        string heroId = Hero,
        string heroClanId = Clan,
        string playerClanId = Clan,
        string leaderId = King,
        bool inPlay = true)
    {
        _identity.GetPlayerClanLeadership().Returns(
            new PlayerClanLeadership(heroId, heroClanId, playerClanId, ClanName, leaderId, inPlay));
    }

    private void AssertNothingChanged()
    {
        _identity.DidNotReceive().PromoteToClanLeader(Arg.Any<string>());
        _inquiry.DidNotReceive().ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------- The one state that is repaired ----------

    [TestMethod]
    public void RepairIfNeeded_TakenOverClanLedByAnotherHero_PromotesThePlayer()
    {
        Given();

        var repaired = _sut.RepairIfNeeded();

        Assert.IsTrue(repaired);
        _identity.Received(1).PromoteToClanLeader(Hero);
    }

    [TestMethod]
    public void RepairIfNeeded_Repairs_TellsThePlayerWhichClanTheyNowLead()
    {
        Given();

        _sut.RepairIfNeeded();

        _inquiry.Received(1).ShowMessage(
            PlayerClanLeadershipService.RepairedKey,
            PlayerClanLeadershipService.RepairedFallback,
            "CLAN", ClanName,
            Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void RepairIfNeeded_Repairs_LogsBothHeroes()
    {
        Given();

        _sut.RepairIfNeeded();

        _logger.Received().LogInfo(Arg.Is<string>(m => m.Contains(Hero) && m.Contains(King)));
    }

    // ---------- Every state that must NOT be touched ----------

    [TestMethod]
    public void RepairIfNeeded_PlayerAlreadyLeads_DoesNothing()
    {
        Given(leaderId: Hero);

        Assert.IsFalse(_sut.RepairIfNeeded());
        AssertNothingChanged();
    }

    [TestMethod]
    public void RepairIfNeeded_VanillaStartClan_DoesNothing()
    {
        // player_faction is the clan vanilla creates for every new character. It is never a
        // takeover, so whatever its leader field says is not this feature's business.
        Given(heroClanId: "player_faction", playerClanId: "player_faction");

        Assert.IsFalse(_sut.RepairIfNeeded());
        AssertNothingChanged();
    }

    [TestMethod]
    public void RepairIfNeeded_PlayerClanPointerOnAnotherClan_DoesNothingAndWarns()
    {
        // The #550 "related defect": ReassignPlayerClan failed silently and Clan.PlayerClan still
        // points at the throwaway clan. Promoting here would call SetLeader on THAT clan, which
        // assigns hero.Clan and would drag the lord out of his real house.
        Given(heroClanId: Clan, playerClanId: "player_faction_stale");

        Assert.IsFalse(_sut.RepairIfNeeded());
        AssertNothingChanged();
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("player_faction_stale")));
    }

    [TestMethod]
    public void RepairIfNeeded_ClanHasNoLeader_DoesNothingAndWarns()
    {
        // ChangeClanLeaderAction reads the old leader's gold unguarded; a leaderless clan would NRE
        // inside the engine. Vanilla never leaves a clan leaderless for long, so warn and wait.
        Given(leaderId: string.Empty);

        Assert.IsFalse(_sut.RepairIfNeeded());
        AssertNothingChanged();
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void RepairIfNeeded_PlayerNotInPlay_DoesNothing()
    {
        // Dead, disabled or not spawned. The adapter folds all three into one flag because
        // Hero.IsAlive alone is !IsDead and would let a disabled hero reach vanilla succession.
        Given(inPlay: false);

        Assert.IsFalse(_sut.RepairIfNeeded());
        AssertNothingChanged();
    }

    [TestMethod]
    public void RepairIfNeeded_CoopClient_DoesNothing()
    {
        // Only the authority peer mutates shared campaign state (coop-interop).
        _coop.IsAuthority.Returns(false);
        Given();

        Assert.IsFalse(_sut.RepairIfNeeded());
        AssertNothingChanged();
    }

    [TestMethod]
    public void RepairIfNeeded_NoSnapshot_DoesNothing()
    {
        _identity.GetPlayerClanLeadership().Returns(PlayerClanLeadership.None);

        Assert.IsFalse(_sut.RepairIfNeeded());
        AssertNothingChanged();
    }

    [TestMethod]
    public void RepairIfNeeded_EngineDeclinedThePromotion_ReportsFalseAndStaysQuiet()
    {
        Given();
        _identity.PromoteToClanLeader(Hero).Returns(false);

        Assert.IsFalse(_sut.RepairIfNeeded());
        _inquiry.DidNotReceive().ShowMessage(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------- Failure is survivable ----------

    [TestMethod]
    public void RepairIfNeeded_AdapterThrows_LogsAndReturnsFalse()
    {
        Given();
        _identity.When(a => a.PromoteToClanLeader(Hero))
            .Do(_ => throw new System.InvalidOperationException("engine said no"));

        var repaired = _sut.RepairIfNeeded();

        Assert.IsFalse(repaired);
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("engine said no")));
    }

    [TestMethod]
    public void RepairIfNeeded_SnapshotThrows_LogsAndReturnsFalse()
    {
        _identity.When(a => a.GetPlayerClanLeadership())
            .Do(_ => throw new System.InvalidOperationException("no campaign"));

        Assert.IsFalse(_sut.RepairIfNeeded());
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("no campaign")));
    }
}

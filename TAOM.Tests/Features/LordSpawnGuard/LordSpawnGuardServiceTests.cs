using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LordSpawnGuard;

namespace TAOM.Tests.Features.LordSpawnGuard;

[TestClass]
public class LordSpawnGuardServiceTests
{
    private const string Hero = "lord_5_3";
    private const string Faction = "clan_battania_3";
    private const string LandlessCulture = "battania";

    private ILordSpawnGuardAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private LordSpawnGuardService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = Substitute.For<ILordSpawnGuardAdapter>();
        _logger = Substitute.For<IModLogger>();
        _sut = new LordSpawnGuardService(_adapter, _logger);

        // Default: a healthy faction — has a home settlement, so nothing to repair.
        _adapter.GetHeroMapFactionId(Hero).Returns(Faction);
        _adapter.GetHeroCultureId(Hero).Returns(LandlessCulture);
        _adapter.FactionHasInitialHomeSettlement(Hero).Returns(true);
        _adapter.AnySettlementHasHeroCulture(Hero).Returns(true);
        _adapter.SetFactionInitialHomeSettlement(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    /// The full crash precondition: no faction anchor AND the hero's culture owns nothing.
    private void GivenTheVanillaPathWouldThrow()
    {
        _adapter.FactionHasInitialHomeSettlement(Hero).Returns(false);
        _adapter.AnySettlementHasHeroCulture(Hero).Returns(false);
    }

    // ---------------------------------------------------------------- no-op cases

    // Both no-op tests below stub an anchor candidate deliberately. Without it the arrange leaves
    // every FindAnchor rung null, so the service writes nothing whether the guard is present or
    // not, and `DidNotReceive` holds in both worlds — an assertion that cannot fail. With the stub,
    // deleting the guard named in each test makes exactly that test go red.
    // (Deep review 2026-08-04, M4/L4; the arrange-side form of the vacuous-assertion ban in
    // docs/reviews/lessons/testing-qa.md.)

    [TestMethod]
    public void EnsureSpawnAnchor_FactionAlreadyHasHomeSettlement_WritesNothing()
    {
        // Reddens if `LordSpawnGuardService`'s FactionHasInitialHomeSettlement early-return is removed.
        _adapter.AnySettlementHasHeroCulture(Hero).Returns(false);
        _adapter.GetHeroHomeSettlementId(Hero).Returns("town_K3");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.DidNotReceive().SetFactionInitialHomeSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureSpawnAnchor_CultureOwnsSettlements_WritesNothing()
    {
        // Vanilla's Settlement.All.First(x => x.Culture == hero.Culture) resolves fine here,
        // so a missing anchor is not our problem to fix.
        // Reddens if the AnySettlementHasHeroCulture early-return is removed.
        _adapter.FactionHasInitialHomeSettlement(Hero).Returns(false);
        _adapter.GetHeroHomeSettlementId(Hero).Returns("town_K3");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.DidNotReceive().SetFactionInitialHomeSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureSpawnAnchor_HealthyFaction_DoesNotScanSettlementsForTheCulture()
    {
        // The culture scan walks Settlement.All; the anchor check is a single property read.
        // Cheapest gate must run first.
        _sut.EnsureSpawnAnchor(Hero);

        _adapter.DidNotReceive().AnySettlementHasHeroCulture(Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureSpawnAnchor_UnresolvableHero_WritesNothing()
    {
        _adapter.GetHeroMapFactionId(Hero).Returns((string)null);
        GivenTheVanillaPathWouldThrow();

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.DidNotReceive().SetFactionInitialHomeSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void EnsureSpawnAnchor_BlankHeroId_WritesNothing()
    {
        _sut.EnsureSpawnAnchor(null);
        _sut.EnsureSpawnAnchor("");

        _adapter.DidNotReceive().SetFactionInitialHomeSettlement(Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------------------------------------------------------------- the repair

    [TestMethod]
    public void EnsureSpawnAnchor_LandlessCultureAndNoAnchor_SetsHomeSettlement()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetHeroHomeSettlementId(Hero).Returns("town_K3");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "town_K3");
    }

    [TestMethod]
    public void EnsureSpawnAnchor_PrefersHomeSettlementOverEveryOtherCandidate()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetHeroHomeSettlementId(Hero).Returns("town_K3");
        _adapter.GetHeroBornSettlementId(Hero).Returns("town_K1");
        _adapter.GetClanLeaderSettlementId(Hero).Returns("castle_K2");
        _adapter.GetNearestFriendlySettlementId(Hero).Returns("town_K4");
        _adapter.GetNearestSettlementId(Hero).Returns("town_RU1");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "town_K3");
    }

    [TestMethod]
    public void EnsureSpawnAnchor_FallsBackToBornSettlement()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetHeroBornSettlementId(Hero).Returns("town_K1");
        _adapter.GetClanLeaderSettlementId(Hero).Returns("castle_K2");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "town_K1");
    }

    [TestMethod]
    public void EnsureSpawnAnchor_FallsBackToClanLeaderSettlement()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetClanLeaderSettlementId(Hero).Returns("castle_K2");
        _adapter.GetNearestFriendlySettlementId(Hero).Returns("town_K4");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "castle_K2");
    }

    [TestMethod]
    public void EnsureSpawnAnchor_FallsBackToNearestFriendlySettlement()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetNearestFriendlySettlementId(Hero).Returns("town_K4");
        _adapter.GetNearestSettlementId(Hero).Returns("town_RU1");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "town_K4");
    }

    [TestMethod]
    public void EnsureSpawnAnchor_FallsBackToNearestSettlementOfAnyAllegiance()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetNearestSettlementId(Hero).Returns("town_RU1");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "town_RU1");
    }

    [TestMethod]
    public void EnsureSpawnAnchor_BlankCandidatesAreSkipped()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetHeroHomeSettlementId(Hero).Returns("");
        _adapter.GetHeroBornSettlementId(Hero).Returns("   ");
        _adapter.GetClanLeaderSettlementId(Hero).Returns("castle_K2");

        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "castle_K2");
    }

    // ---------------------------------------------------------------- failure handling

    [TestMethod]
    public void EnsureSpawnAnchor_NoCandidateAnywhere_WarnsOncePerFaction()
    {
        GivenTheVanillaPathWouldThrow();

        _sut.EnsureSpawnAnchor(Hero);
        _sut.EnsureSpawnAnchor(Hero);
        _sut.EnsureSpawnAnchor(Hero);

        _adapter.DidNotReceive().SetFactionInitialHomeSettlement(Arg.Any<string>(), Arg.Any<string>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(Faction)));
    }

    [TestMethod]
    public void EnsureSpawnAnchor_WriteRejected_WarnsOncePerFaction()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetNearestSettlementId(Hero).Returns("town_RU1");
        _adapter.SetFactionInitialHomeSettlement(Hero, "town_RU1").Returns(false);

        _sut.EnsureSpawnAnchor(Hero);
        _sut.EnsureSpawnAnchor(Hero);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(Faction)));
    }

    [TestMethod]
    public void EnsureSpawnAnchor_SuccessfulRepair_IsWrittenOnceAndLoggedOnce()
    {
        GivenTheVanillaPathWouldThrow();
        _adapter.GetHeroHomeSettlementId(Hero).Returns("town_K3");
        // The write persists (Clan.InitialHomeSettlement is a [SaveableProperty]), so the anchor
        // check reads true from then on — that read-back is what makes the repair a one-off.
        _adapter.When(a => a.SetFactionInitialHomeSettlement(Hero, "town_K3"))
                .Do(_ => _adapter.FactionHasInitialHomeSettlement(Hero).Returns(true));

        _sut.EnsureSpawnAnchor(Hero);
        _sut.EnsureSpawnAnchor(Hero);

        _adapter.Received(1).SetFactionInitialHomeSettlement(Hero, "town_K3");
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains(Faction) && m.Contains("town_K3")));
    }

    [TestMethod]
    public void EnsureSpawnAnchor_AdapterThrows_DoesNotPropagate()
    {
        // Runs inside a prefix on a vanilla campaign tick — a fault here must never be worse
        // than the crash it is guarding against.
        _adapter.FactionHasInitialHomeSettlement(Hero).Throws(new InvalidOperationException("boom"));

        _sut.EnsureSpawnAnchor(Hero);

        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("boom")));
    }
}

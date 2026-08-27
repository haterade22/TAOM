using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.PlayerSwitcher;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Issue #514. The handover, proven offline. Ordering is the point of these tests, not decoration:
/// vanilla KillCharacterAction destroys the abandoned character-creation clan only when it is no
/// longer Clan.PlayerClan (KillCharacterAction line 133 guards on victim.Clan != Clan.PlayerClan),
/// so reassigning the player clan BEFORE removing the created hero is what stops an orphan clan
/// living in the campaign forever. Reordering those two steps breaks a save, silently.
/// </summary>
[TestClass]
public class HeroSwitchServiceTests
{
    private const string Target = "dain";
    private const string Original = "created_hero";
    private const string OriginalClan = "player_faction";
    private const string OriginalParty = "player_party_1";
    private const string TargetClan = "clan_erebor";
    private const string Career = "career_warrior";

    private IPlayerIdentityAdapter _identity = null!;
    private ICareerCreationHandler _career = null!;
    private IPlayerSwitchPolicyProvider _policy = null!;
    private IModLogger _logger = null!;
    private HeroSwitchService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _identity = Substitute.For<IPlayerIdentityAdapter>();
        _career = Substitute.For<ICareerCreationHandler>();
        _policy = Substitute.For<IPlayerSwitchPolicyProvider>();
        _logger = Substitute.For<IModLogger>();

        _identity.CanReassignPlayerClan.Returns(true);
        _identity.IsSwitchable(Target).Returns(true);
        _identity.Capture(Target, Career).Returns(
            new SwitchTicket(Original, OriginalClan, OriginalParty, TargetClan, Career));
        _policy.Current.Returns(PlayerSwitchPolicy.Default);

        _sut = new HeroSwitchService(_identity, _career, _policy, _logger);
    }

    private static SwitchPlan TakeoverPlan(bool gold = false)
        => new SwitchPlan(Target, SwitchPath.AssumeIdentity, gold, Career);

    private static SwitchPlan AdoptionPlan(bool gold = false)
        => new SwitchPlan(Target, SwitchPath.AdoptIntoPlayerClan, gold, Career);

    private void AssertNothingWasMutated()
    {
        _identity.DidNotReceive().ApplyPlayerCharacter(Arg.Any<string>());
        _identity.DidNotReceive().ReassignPlayerClan(Arg.Any<string>());
        _identity.DidNotReceive().RemoveOriginalHero(Arg.Any<string>());
        _identity.DidNotReceive().AdoptIntoPlayerClan(Arg.Any<string>());
        _identity.DidNotReceive().AbsorbOriginalParty(Arg.Any<string>());
        _identity.DidNotReceive().TransferGold(Arg.Any<string>(), Arg.Any<string>());
        _career.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------- The ordered takeover ----------

    [TestMethod]
    public void Takeover_RunsEveryStepInTheOrderTheEngineRequires()
    {
        var outcome = _sut.Execute(TakeoverPlan());

        Assert.AreEqual(SwitchOutcome.Switched, outcome);

        Received.InOrder(() =>
        {
            _identity.Capture(Target, Career);
            _identity.ApplyPlayerCharacter(Target);
            _identity.ReassignPlayerClan(TargetClan);
            _career.OnCareerSelected(Target, Career);
            _identity.MarkClanAndKingdomKnown(Target);
            _identity.RemoveOriginalHero(Original);
            _identity.ClearPendingNotifications();
        });
    }

    [TestMethod]
    public void Takeover_ReassignsThePlayerClanBeforeRemovingTheCreatedHero()
    {
        _sut.Execute(TakeoverPlan());

        Received.InOrder(() =>
        {
            _identity.ReassignPlayerClan(TargetClan);
            _identity.RemoveOriginalHero(Original);
        });
    }

    [TestMethod]
    public void Takeover_NeverAbsorbsTheCharacterCreationParty()
    {
        _sut.Execute(TakeoverPlan());

        _identity.DidNotReceive().AbsorbOriginalParty(Arg.Any<string>());
    }

    [TestMethod]
    public void Takeover_DoesNotAdoptAnybody()
    {
        _sut.Execute(TakeoverPlan());

        _identity.DidNotReceive().AdoptIntoPlayerClan(Arg.Any<string>());
    }

    // ---------- The ordered adoption ----------

    [TestMethod]
    public void Adoption_MakesTheHeroClanLeaderBeforeHandingOverControl()
    {
        var outcome = _sut.Execute(AdoptionPlan());

        Assert.AreEqual(SwitchOutcome.Switched, outcome);

        Received.InOrder(() =>
        {
            _identity.Capture(Target, Career);
            _identity.AdoptIntoPlayerClan(Target);
            _identity.ApplyPlayerCharacter(Target);
            _career.OnCareerSelected(Target, Career);
            _identity.MarkClanAndKingdomKnown(Target);
            _identity.RemoveOriginalHero(Original);
            _identity.AbsorbOriginalParty(OriginalParty);
            _identity.ClearPendingNotifications();
        });
    }

    [TestMethod]
    public void Adoption_NeverReassignsThePlayerClan()
    {
        _sut.Execute(AdoptionPlan());

        _identity.DidNotReceive().ReassignPlayerClan(Arg.Any<string>());
    }

    [TestMethod]
    public void Adoption_AbsorbsTheStartingPartyAfterTheCreatedHeroIsGone()
    {
        _sut.Execute(AdoptionPlan());

        Received.InOrder(() =>
        {
            _identity.RemoveOriginalHero(Original);
            _identity.AbsorbOriginalParty(OriginalParty);
        });
    }

    [TestMethod]
    public void Adoption_AbsorbsByTheCapturedIdAndNothingElse()
    {
        _sut.Execute(AdoptionPlan());

        _identity.Received(1).AbsorbOriginalParty(OriginalParty);
    }

    // ---------- Gold ----------

    [TestMethod]
    public void GoldMovesOnlyWhenThePlanAsksForIt()
    {
        _sut.Execute(TakeoverPlan(gold: false));
        _identity.DidNotReceive().TransferGold(Arg.Any<string>(), Arg.Any<string>());

        _sut.Execute(TakeoverPlan(gold: true));
        _identity.Received(1).TransferGold(Original, Target);
    }

    // ---------- Preconditions: nothing may be touched ----------

    [TestMethod]
    public void AnInvalidPlan_IsNotAttempted()
    {
        var outcome = _sut.Execute(SwitchPlan.None);

        Assert.AreEqual(SwitchOutcome.NotAttempted, outcome);
        AssertNothingWasMutated();
    }

    [TestMethod]
    public void ADisabledFeature_IsNotAttempted()
    {
        _policy.Current.Returns(PlayerSwitchPolicy.Disabled);

        var outcome = _sut.Execute(TakeoverPlan());

        Assert.AreEqual(SwitchOutcome.NotAttempted, outcome);
        AssertNothingWasMutated();
    }

    [TestMethod]
    public void AFailedReflectionProbe_BlocksAndLatchesTheFeatureOff()
    {
        _identity.CanReassignPlayerClan.Returns(false);

        var outcome = _sut.Execute(TakeoverPlan());

        Assert.AreEqual(SwitchOutcome.Blocked, outcome);
        AssertNothingWasMutated();
        _policy.Received(1).DisableForSession(Arg.Any<string>());
    }

    [TestMethod]
    public void AnUnswitchableTarget_IsBlocked()
    {
        _identity.IsSwitchable(Target).Returns(false);

        var outcome = _sut.Execute(TakeoverPlan());

        Assert.AreEqual(SwitchOutcome.Blocked, outcome);
        AssertNothingWasMutated();
    }

    [TestMethod]
    public void ACaptureThatYieldsNoTicket_IsBlockedBeforeAnythingMutates()
    {
        _identity.Capture(Target, Career).Returns(SwitchTicket.None);

        var outcome = _sut.Execute(TakeoverPlan());

        Assert.AreEqual(SwitchOutcome.Blocked, outcome);
        _identity.DidNotReceive().ApplyPlayerCharacter(Arg.Any<string>());
        _identity.DidNotReceive().RemoveOriginalHero(Arg.Any<string>());
    }

    [TestMethod]
    public void ATakeoverWithNoTargetClan_IsBlockedRatherThanReassigningToNothing()
    {
        _identity.Capture(Target, Career).Returns(
            new SwitchTicket(Original, OriginalClan, OriginalParty, string.Empty, Career));

        var outcome = _sut.Execute(TakeoverPlan());

        Assert.AreEqual(SwitchOutcome.Blocked, outcome);
        _identity.DidNotReceive().ApplyPlayerCharacter(Arg.Any<string>());
    }

    // ---------- Failure is survivable ----------

    [TestMethod]
    public void AThrowPartway_IsReportedAsFailedAndLogged_NotRethrown()
    {
        _identity.When(a => a.ApplyPlayerCharacter(Target))
            .Do(_ => throw new System.InvalidOperationException("engine said no"));

        var outcome = _sut.Execute(TakeoverPlan());

        Assert.AreEqual(SwitchOutcome.Failed, outcome, "character creation must still complete");
        _logger.Received().LogError(Arg.Is<string>(m => m.Contains("engine said no")));
    }

    [TestMethod]
    public void AnEmptyCareerId_SkipsTheReKeyRatherThanWritingAnEmptyCareer()
    {
        _identity.Capture(Target, string.Empty).Returns(
            new SwitchTicket(Original, OriginalClan, OriginalParty, TargetClan, string.Empty));

        _sut.Execute(new SwitchPlan(Target, SwitchPath.AssumeIdentity, false, string.Empty));

        _career.DidNotReceive().OnCareerSelected(Arg.Any<string>(), Arg.Any<string>());
    }

    // ---------- Standing regression guard ----------

    [TestMethod]
    public void TheHandoverNeverTouchesRaceOrStartupGrants()
    {
        _sut.Execute(TakeoverPlan());

        // The whole design rests on running at handler priority 1100, after TAOM's own 1050 grants
        // have already landed on the throwaway hero. If a future refactor made this service grant
        // race or startup resources, it would overwrite the lord's own race (Sauron's race="sauron"
        // becoming a culture default) and RacePersistenceService would then persist the damage.
        // The adapter has no such method, and this test exists so that adding one is a deliberate act.
        var members = typeof(IPlayerIdentityAdapter).GetMethods();
        foreach (var m in members)
        {
            StringAssert.DoesNotMatch(m.Name, new System.Text.RegularExpressions.Regex("(?i)race|startup|grant"),
                $"IPlayerIdentityAdapter.{m.Name} looks like a grant; the 1100 ordering exists so grants are not re-run");
        }
    }
}

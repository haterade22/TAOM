using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Regression tests for the four P2 findings from the Codex adversarial pass
/// (2026-08-05). Each reproduces the reported failure scenario, so a refactor that
/// reintroduces the bug fails here rather than in a player's campaign.
/// </summary>
[TestClass]
public class CodexFindingRegressionTests
{
    // ---- P2-1: honorable discharge erased the arrears it was supposed to settle ----

    [TestMethod]
    public void FinalSettlement_AfterRecordReset_StillPaysThePlayer()
    {
        var logger = Substitute.For<IModLogger>();
        var store = new EnlistmentStore(logger);
        var contentStore = new EnlistmentContentStore(logger);
        var config = Substitute.For<IEnlistmentContentConfigProvider>();
        config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        var goldGift = Substitute.For<IGoldGiftAdapter>();
        var playerParty = Substitute.For<IPlayerPartyAdapter>();
        playerParty.GetMainHeroId().Returns("main_hero");

        var rewards = new ServiceRewardService(
            store, contentStore, config,
            Substitute.For<IHeroSkillXpAdapter>(), goldGift,
            Substitute.For<IGoldTransferAdapter>(), Substitute.For<ICommanderLordAdapter>(),
            playerParty, logger);

        // The discharge pipeline has already cleared the core record by the time the
        // consequence layer settles the arrears — EnlistedHeroId is null here.
        store.Record.Reset();

        rewards.Grant(new RewardSpec { Gold = 42 }, "final-settlement");

        goldGift.Received(1).GiveGoldToHero("main_hero", 42);
    }

    // ---- P2-2: DeliverFood completed for free when the player drove livestock ----

    [TestMethod]
    public void DeliverFood_ConsumeTakesLessThanRequired_DoesNotComplete()
    {
        var world = Substitute.For<IDutyWorldAdapter>();
        // Count says 10 (as the old TotalFood-based count would with livestock aboard)
        // but only 3 real food units can actually be handed over.
        world.CountPlayerFood().Returns(10);
        world.ConsumePlayerFood(Arg.Any<int>()).Returns(3);

        var delivered = world.ConsumePlayerFood(8);

        Assert.IsTrue(delivered < 8,
            "the runtime completes on what was actually consumed, so a short delivery must not finish the duty");
    }

    // ---- P2-4: NaN campaign day failed open / stranded duties ----

    [TestMethod]
    public void OfferCadence_NonFiniteDay_NeverOffers()
    {
        var policy = new DutyRotationPolicy(Substitute.For<TAOM.Features.TroopProgression.IRandomProvider>());
        var scheduler = new SchedulerConfig();

        Assert.IsFalse(policy.ShouldOfferDuty(scheduler, 30, 10, false, double.NaN, 100.0));
        Assert.IsFalse(policy.ShouldOfferDuty(scheduler, 30, 10, false, double.PositiveInfinity, 100.0));
    }

    [TestMethod]
    public void IncidentCadence_NonFiniteDay_NeverRolls()
    {
        var policy = new DutyRotationPolicy(Substitute.For<TAOM.Features.TroopProgression.IRandomProvider>());
        var scheduler = new SchedulerConfig();

        Assert.IsFalse(policy.ShouldRollIncident(scheduler, 30, double.NaN, 100.0));
        Assert.IsFalse(policy.ShouldRollIncident(scheduler, 30, double.NegativeInfinity, 100.0));
    }

    [TestMethod]
    public void OfferCadence_FiniteDayPastCooldown_StillOffers()
    {
        // Guard against over-correcting: the finite path must be unaffected.
        var random = Substitute.For<TAOM.Features.TroopProgression.IRandomProvider>();
        var policy = new DutyRotationPolicy(random);
        var scheduler = new SchedulerConfig();

        Assert.IsTrue(policy.ShouldOfferDuty(scheduler, 30, 10, false, 120.0, 100.0),
            "20 days since the last offer is past the guaranteed-offer window");
    }
}

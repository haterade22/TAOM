using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Arachnophobia;
using TAOM.Features.Spider;

namespace TAOM.Tests.Features.Arachnophobia;

[TestClass]
public class ArachnophobiaServiceTests
{
    private const string GoblinRider = "goblin_spider_rider";
    private const string Brood = "taom_spider_brood_pale";
    private const string Lurker = "goblin_lurker";
    private const string Raider = "goblin_raider";
    private const string CheckId = "taom.arachnophobia.apply";

    private ITroopMountCatalogAdapter _catalog;
    private IOutcomeLog _outcomes;
    private IModLogger _logger;
    private ArachnophobiaService _service;

    private static TroopMountRecord Rider(string id, params string[] items) => new(id, isHero: false, items);

    private static TroopUpgradeRecord Upgrade(string id, params string[] targets) => new(id, targets);

    [TestInitialize]
    public void Setup()
    {
        _catalog = Substitute.For<ITroopMountCatalogAdapter>();
        _outcomes = Substitute.For<IOutcomeLog>();
        _logger = Substitute.For<IModLogger>();

        _catalog.ItemExists(Arg.Any<string>()).Returns(true);
        _catalog.ReadRiders(SpiderConfig.SpiderMonsterId).Returns(new[]
        {
            Rider(GoblinRider, "spider_mount_mountain_a1"),
            Rider(Brood, "spider_mount_pale"),
        });
        _catalog.ReadUpgrades().Returns(Array.Empty<TroopUpgradeRecord>());
        _catalog.SetUpgradeTargets(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>()).Returns(true);
        _catalog.MarkObsolete(Arg.Any<string>()).Returns(true);

        _service = new ArachnophobiaService(_catalog, _outcomes, _logger);
    }

    private void AssertNoWrites()
    {
        _catalog.DidNotReceiveWithAnyArgs().ReplaceMount(default!, default!, default!, default!);
        _catalog.DidNotReceiveWithAnyArgs().SetUpgradeTargets(default!, default!);
        _catalog.DidNotReceiveWithAnyArgs().HideFromEncyclopedia(default!);
        _catalog.DidNotReceiveWithAnyArgs().MarkObsolete(default!);
    }

    // ── IsHidden ──

    [TestMethod]
    public void IsHidden_BeforeAnyGame_ReturnsFalse() => Assert.IsFalse(_service.IsHidden(GoblinRider));

    [TestMethod]
    public void IsHidden_Null_ReturnsFalse()
    {
        _service.ApplyForGame(true, true);
        Assert.IsFalse(_service.IsHidden(null));
    }

    // ── Mode off ──

    [TestMethod]
    public void ApplyForGame_ModeOff_TouchesNothing()
    {
        _service.ApplyForGame(false, true);

        _catalog.DidNotReceiveWithAnyArgs().ReadRiders(default!);
        AssertNoWrites();
        _outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void ApplyForGame_ModeOffAfterAGameWithItOn_EmptiesTheHiddenSet()
    {
        _service.ApplyForGame(true, true);
        _service.ApplyForGame(false, true);
        Assert.IsFalse(_service.IsHidden(GoblinRider));
    }

    // ── The hidden set and the swap ──

    [TestMethod]
    public void ApplyForGame_ModeOn_HidesEveryRider()
    {
        _service.ApplyForGame(true, true);
        Assert.IsTrue(_service.IsHidden(GoblinRider));
        Assert.IsTrue(_service.IsHidden(Brood));
    }

    [TestMethod]
    public void ApplyForGame_ModeOn_LeavesOtherTroopsVisible()
    {
        _service.ApplyForGame(true, true);
        Assert.IsFalse(_service.IsHidden(Lurker));
    }

    [TestMethod]
    public void ApplyForGame_HeroOnASpider_IsNeitherHiddenNorSwapped()
    {
        // A hero's BattleEquipments is its saved gear (CharacterObject.cs:113-123). A write would go into the save.
        _catalog.ReadRiders(SpiderConfig.SpiderMonsterId).Returns(new[] { new TroopMountRecord("lord_x", isHero: true, new[] { "spider_mount_a" }), Rider(Brood, "spider_mount_pale") });

        _service.ApplyForGame(true, true);

        Assert.IsFalse(_service.IsHidden("lord_x"));
        _catalog.DidNotReceive().ReplaceMount("lord_x", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyForGame_ModeOn_WritesEachRidersWargAndTheHarness()
    {
        _service.ApplyForGame(true, true);

        _catalog.Received(1).ReplaceMount(GoblinRider, "spider_mount_mountain_a1", "warg_brown", "warg_saddle");
        _catalog.Received(1).ReplaceMount(Brood, "spider_mount_pale", "fell_warg", "warg_saddle");
    }

    [TestMethod]
    public void ApplyForGame_RiderWithTwoSpiderItems_ReplacesBoth()
    {
        _catalog.ReadRiders(SpiderConfig.SpiderMonsterId).Returns(new[] { Rider(GoblinRider, "spider_mount_a", "spider_mount_brown") });

        _service.ApplyForGame(true, true);

        _catalog.Received(1).ReplaceMount(GoblinRider, "spider_mount_a", "warg_brown", "warg_saddle");
        _catalog.Received(1).ReplaceMount(GoblinRider, "spider_mount_brown", "warg_dark", "warg_saddle");
    }

    [TestMethod]
    public void ApplyForGame_ModeOn_ReadsTheRidersOnce()
    {
        // The swap removes the Monster that the read keys on, so a second read after the swap finds nobody.
        _service.ApplyForGame(true, true);
        _catalog.Received(1).ReadRiders(SpiderConfig.SpiderMonsterId);
    }

    [TestMethod]
    public void ApplyForGame_SecondGame_RebuildsTheSetFromThatGamesData()
    {
        _service.ApplyForGame(true, true);
        _catalog.ReadRiders(SpiderConfig.SpiderMonsterId).Returns(new[] { Rider("new_spider_troop", "spider_mount_a") });

        _service.ApplyForGame(true, true);

        Assert.IsTrue(_service.IsHidden("new_spider_troop"));
        Assert.IsFalse(_service.IsHidden(GoblinRider));
    }

    // ── Nothing to work with ──

    [DataTestMethod]
    [DataRow("warg_brown")]
    [DataRow("warg_dark")]
    [DataRow("fell_warg")]
    [DataRow("warg_saddle")]
    public void ApplyForGame_RequiredItemMissing_LogsAnErrorAndChangesNothing(string missing)
    {
        _catalog.ItemExists(missing).Returns(false);

        _service.ApplyForGame(true, true);

        AssertNoWrites();
        Assert.IsFalse(_service.IsHidden(GoblinRider));
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains(missing)));
        _outcomes.Received(1).Fail(CheckId, "campaign", Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyForGame_NoRidersFound_ReportsAFailure()
    {
        // A second game in one process that kept the first game's swap, or a spider item moved off Monster.spider.
        _catalog.ReadRiders(SpiderConfig.SpiderMonsterId).Returns(Array.Empty<TroopMountRecord>());

        _service.ApplyForGame(true, true);

        AssertNoWrites();
        _outcomes.Received(1).Fail(CheckId, "campaign", Arg.Any<string>());
        _outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void ApplyForGame_ReadThrows_LogsAndHidesNothing()
    {
        _catalog.ReadRiders(SpiderConfig.SpiderMonsterId).Returns<IReadOnlyList<TroopMountRecord>>(_ => throw new InvalidOperationException("boom"));

        _service.ApplyForGame(true, true);

        Assert.IsFalse(_service.IsHidden(GoblinRider));
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("boom")));
        _outcomes.Received(1).Fail(CheckId, "campaign", Arg.Any<string>());
    }

    [TestMethod]
    public void ApplyForGame_RouteFailureAfterTheSwap_KeepsTheHiddenSet()
    {
        // Volunteers, garrison roots and the Emissary read IsHidden, so they stay closed. The other routes do not.
        _catalog.ReadUpgrades().Returns<IReadOnlyList<TroopUpgradeRecord>>(_ => throw new InvalidOperationException("boom"));

        _service.ApplyForGame(true, true);

        Assert.IsTrue(_service.IsHidden(GoblinRider));
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("boom")));
        _outcomes.Received(1).Fail(CheckId, "campaign", Arg.Any<string>());
    }

    // ── Game-type routes ──

    [TestMethod]
    public void ApplyForGame_Campaign_HidesRidersFromTheEncyclopedia()
    {
        _service.ApplyForGame(true, isCampaign: true);

        _catalog.Received(1).HideFromEncyclopedia(GoblinRider);
        _catalog.Received(1).HideFromEncyclopedia(Brood);
    }

    [TestMethod]
    public void ApplyForGame_Campaign_NeverMarksObsolete()
    {
        // Only Custom Battle reads IsObsolete for a soldier (ArmyCompositionGroupVM, CustomBattleService).
        _service.ApplyForGame(true, isCampaign: true);
        _catalog.DidNotReceiveWithAnyArgs().MarkObsolete(default!);
    }

    [TestMethod]
    public void ApplyForGame_CustomBattle_MarksRidersObsolete()
    {
        _service.ApplyForGame(true, isCampaign: false);

        _catalog.Received(1).MarkObsolete(GoblinRider);
        _catalog.Received(1).MarkObsolete(Brood);
    }

    [TestMethod]
    public void ApplyForGame_CustomBattle_LeavesTheCampaignOnlyDataAlone()
    {
        // CharacterObject is not registered in a Custom Battle (CustomGame.cs:99).
        _service.ApplyForGame(true, isCampaign: false);

        _catalog.DidNotReceive().ReadUpgrades();
        _catalog.DidNotReceiveWithAnyArgs().HideFromEncyclopedia(default!);
    }

    [TestMethod]
    public void ApplyForGame_ObsoleteSetterMissing_LogsAnErrorAndReportsAFailure()
    {
        _catalog.MarkObsolete(Arg.Any<string>()).Returns(false);

        _service.ApplyForGame(true, isCampaign: false);

        _logger.Received(1).LogError(Arg.Any<string>());
        _outcomes.Received(1).Fail(CheckId, "custom", Arg.Any<string>());
    }

    // ── Upgrade edges ──

    private static IReadOnlyCollection<string> Exactly(params string[] ids)
        => Arg.Is<IReadOnlyCollection<string>>(kept => kept.SequenceEqual(ids));

    [TestMethod]
    public void ApplyForGame_VisibleTroopWithAHiddenTarget_KeepsItsOtherTargets()
    {
        _catalog.ReadUpgrades().Returns(new[] { Upgrade(Lurker, GoblinRider, Raider) });

        _service.ApplyForGame(true, true);

        _catalog.Received(1).SetUpgradeTargets(Lurker, Exactly(Raider));
    }

    [TestMethod]
    public void ApplyForGame_VisibleTroopWithOnlyAHiddenTarget_KeepsNoTargets()
    {
        _catalog.ReadUpgrades().Returns(new[] { Upgrade(Lurker, GoblinRider) });

        _service.ApplyForGame(true, true);

        _catalog.Received(1).SetUpgradeTargets(Lurker, Exactly());
    }

    [TestMethod]
    public void ApplyForGame_HiddenTroopWithAHiddenTarget_KeepsNoTargets()
    {
        // A leftover Spider Rider in an old save cannot upgrade into another spider troop either.
        _catalog.ReadUpgrades().Returns(new[] { Upgrade(GoblinRider, Brood) });

        _service.ApplyForGame(true, true);

        _catalog.Received(1).SetUpgradeTargets(GoblinRider, Exactly());
    }

    [TestMethod]
    public void ApplyForGame_HiddenTroopWithAVisibleTarget_KeepsIt()
    {
        _catalog.ReadUpgrades().Returns(new[] { Upgrade(GoblinRider, Raider, Brood) });

        _service.ApplyForGame(true, true);

        _catalog.Received(1).SetUpgradeTargets(GoblinRider, Exactly(Raider));
    }

    [TestMethod]
    public void ApplyForGame_TroopWithNoHiddenTarget_IsNotWritten()
    {
        _catalog.ReadUpgrades().Returns(new[] { Upgrade(Lurker, Raider) });

        _service.ApplyForGame(true, true);

        _catalog.DidNotReceiveWithAnyArgs().SetUpgradeTargets(default!, default!);
    }

    [TestMethod]
    public void ApplyForGame_UpgradeWriteFails_LogsAnErrorAndFinishesTheOtherRoutes()
    {
        _catalog.ReadUpgrades().Returns(new[] { Upgrade(Lurker, GoblinRider) });
        _catalog.SetUpgradeTargets(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>()).Returns(false);

        _service.ApplyForGame(true, true);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains(Lurker)));
        _catalog.Received(1).HideFromEncyclopedia(GoblinRider);
        _outcomes.Received(1).Fail(CheckId, "campaign", Arg.Any<string>());
    }

    // ── The in-game check ──

    [TestMethod]
    public void ApplyForGame_Campaign_WritesThePassCheck()
    {
        _catalog.ReplaceMount(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(3);
        _catalog.ReadUpgrades().Returns(new[] { Upgrade(Lurker, GoblinRider, Raider) });

        _service.ApplyForGame(true, isCampaign: true);

        _outcomes.Received(1).Pass(CheckId, "campaign", "hidden=2 sets=6 edges=1");
        _outcomes.DidNotReceiveWithAnyArgs().Fail(default!, default!, default!);
    }

    [TestMethod]
    public void ApplyForGame_CustomBattle_WritesThePassCheck()
    {
        _catalog.ReplaceMount(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(1);

        _service.ApplyForGame(true, isCampaign: false);

        _outcomes.Received(1).Pass(CheckId, "custom", "hidden=2 sets=2 edges=0");
    }
}

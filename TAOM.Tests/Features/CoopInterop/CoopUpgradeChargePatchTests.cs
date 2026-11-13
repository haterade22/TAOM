using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Features.CoopInterop.Hooks;
using TAOM.Tests.Features.SpecialResources;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Patch144 charges upgrade_cost after Coop's server applies a party screen. Run through real Harmony on a
/// local fake with the same shape (static, Hero and the upgrade list), because the postfix binds by position.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // the fake target's signature names engine types, as Coop's does
public class CoopUpgradeChargePatchTests
{
    public static class FakeDoneHandler
    {
        public static bool Applied;

        public static void ApplyUpgradedTroopHistory(Hero mainHero, List<Tuple<CharacterObject, CharacterObject, int>> history)
            => Applied = true;
    }

    private Harmony _harmony = null!;
    private ISpecialResourceEventAdapter _events = null!;
    private EarningTestKit _kit = null!;

    [TestInitialize]
    public void Setup()
    {
        FakeDoneHandler.Applied = false;
        _events = Substitute.For<ISpecialResourceEventAdapter>();
        _events.DescribeHero("g1").Returns(EarningTestKit.Remote("g1"));
        _kit = new EarningTestKit(session: true, dedicated: true);
        CoopUpgradeChargePatch.Initialize(_events, _kit.Sut);
        _harmony = new Harmony("taom.tests.patch144." + Guid.NewGuid());
        _harmony.Patch(
            AccessTools.Method(typeof(FakeDoneHandler), nameof(FakeDoneHandler.ApplyUpgradedTroopHistory)),
            postfix: new HarmonyMethod(typeof(CoopUpgradeChargePatch), nameof(CoopUpgradeChargePatch.Postfix)));
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony.UnpatchAll(_harmony.Id);
        CoopUpgradeChargePatch.Initialize(null, null);
    }

    private static Hero NamedHero(string id)
    {
        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        hero.StringId = id;
        return hero;
    }

    private static Tuple<CharacterObject, CharacterObject, int> Line(string? targetId, int count)
        => Tuple.Create(
            new CharacterObject { StringId = "recruit" },
            targetId == null ? null! : new CharacterObject { StringId = targetId },
            count);

    private static void ApplyScreen(params Tuple<CharacterObject, CharacterObject, int>[] lines)
        => FakeDoneHandler.ApplyUpgradedTroopHistory(NamedHero("g1"), lines.ToList());

    [TestMethod]
    public void Postfix_UpgradesApplied_ChargesTheUpgradingPlayer()
    {
        ApplyScreen(Line("elite", 3));

        Assert.IsTrue(FakeDoneHandler.Applied);
        _kit.Service.Received(1).SpendForUpgrade("g1", "k", "c", "elite", 3);
    }

    [TestMethod]
    public void Postfix_NoUpgradeInTheScreen_ReadsNoHeroAndChargesNobody()
    {
        ApplyScreen();

        _events.DidNotReceiveWithAnyArgs().DescribeHero(default!);
        _kit.Service.DidNotReceiveWithAnyArgs().SpendForUpgrade(default, default, default, default, default);
    }

    [TestMethod]
    public void Postfix_NotInitialized_LeavesTheScreenAlone()
    {
        CoopUpgradeChargePatch.Initialize(null, null);

        ApplyScreen(Line("elite", 3));

        Assert.IsTrue(FakeDoneHandler.Applied);
        _kit.Service.DidNotReceiveWithAnyArgs().SpendForUpgrade(default, default, default, default, default);
    }

    // Coop's handler still has prisoner effects and the roster order to apply after this call.
    [TestMethod]
    public void Postfix_TheChargeThrows_TheExceptionStaysOutOfCoop()
    {
        _kit.Service.When(s => s.SpendForUpgrade("g1", "k", "c", "elite", 3))
            .Do(_ => throw new InvalidOperationException("charge failed"));

        ApplyScreen(Line("elite", 3));

        Assert.IsTrue(FakeDoneHandler.Applied);
    }

    [TestMethod]
    public void ToUpgradeLines_NoHistory_ReturnsNoLine()
    {
        Assert.AreEqual(0, CoopUpgradeChargePatch.ToUpgradeLines(null).Count);
    }

    [TestMethod]
    public void ToUpgradeLines_LinesWithoutATargetOrACount_AreSkipped()
    {
        var lines = CoopUpgradeChargePatch.ToUpgradeLines(new List<Tuple<CharacterObject, CharacterObject, int>>
        {
            null!,
            Line(null, 2),
            Line("elite_zero", 0),
            Line("elite_negative", -1),
            Line("elite_a", 2),
        });

        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual("elite_a", lines[0].TargetTroopId);
        Assert.AreEqual(2, lines[0].Count);
    }

    [TestMethod]
    public void Prepare_CoopTypeAbsent_SkipsThePatch()
    {
        // Another test class may have loaded the installed Coop DLLs into this AppDomain; the type is then present.
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType("GameInterface.Services.Party.Handlers.PartyDoneLogicHandler", false) != null))
            Assert.Inconclusive("The real Coop assemblies are loaded in this AppDomain.");
        Assert.IsFalse(CoopUpgradeChargePatch.Prepare());
    }
}

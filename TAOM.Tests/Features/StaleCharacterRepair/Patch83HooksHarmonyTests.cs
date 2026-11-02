using System;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Features.StaleCharacterRepair;
using TAOM.Features.StaleCharacterRepair.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StaleCharacterRepair;

/// <summary>
/// Patch83's birth seam through real Harmony on the real <c>HeroCreator.DeliverOffSpring</c> (harmony-patches.md,
/// "Prefix and finalizer pair"). The Finalizer's line is what the #1076 manifest judges, so these cases are the proof
/// behind its verdicts. With no campaign the real target throws on its first statement, which is the crash case; a test
/// prefix at the lowest priority skips the original for the finished birth.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class Patch83HooksHarmonyTests
{
    private const string HarmonyId = "taom.tests.patch83.birth";

    private static Hero _child = null!;

    private Harmony _harmony = null!;
    private IStaleCharacterRepairService _service = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static bool FinishTheBirth(ref Hero __result)
    {
        __result = _child;
        return false;
    }

    private static Hero NamedHero(string id)
    {
        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        hero.StringId = id;
        return hero;
    }

    [TestInitialize]
    public void Setup()
    {
        _child = NamedHero("child");
        _service = Substitute.For<IStaleCharacterRepairService>();
        Patch83_StaleCharacterRepair.Initialize(_service);
        _harmony = new Harmony(HarmonyId);
        _harmony.CreateClassProcessor(typeof(Patch83_OffspringParentRepair)).Patch();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony?.UnpatchAll(HarmonyId);
        Patch83_StaleCharacterRepair.ResetForUnload();
    }

    private void SkipTheOriginal() =>
        _harmony.Patch(
            AccessTools.Method(typeof(HeroCreator), nameof(HeroCreator.DeliverOffSpring)),
            prefix: new HarmonyMethod(AccessTools.Method(typeof(Patch83HooksHarmonyTests), nameof(FinishTheBirth)))
            {
                priority = Priority.Last,
            });

    private void RepairOnly(string heroId) =>
        _service.RepairOffspringParent(Arg.Is<IHeroCharacterStateAdapter>(p => p.HeroId == heroId)).Returns(true);

    [DataTestMethod]
    [DataRow("mother", DisplayName = "only the mother repaired")]
    [DataRow("father", DisplayName = "only the father repaired")]
    public void Birth_OneParentRepaired_ReportsTheFinishedBirth(string repaired)
    {
        // Assigning the second result instead of or-ing it would lose a repaired mother.
        RepairOnly(repaired);
        SkipTheOriginal();

        Assert.AreSame(_child, HeroCreator.DeliverOffSpring(NamedHero("mother"), NamedHero("father"), false));

        _service.Received(1).ReportBirthAfterRepair("child", null!);
    }

    [TestMethod]
    public void Birth_NoParentRepaired_ReportsNothing()
    {
        SkipTheOriginal();

        HeroCreator.DeliverOffSpring(NamedHero("mother"), NamedHero("father"), false);

        _service.Received(2).RepairOffspringParent(Arg.Any<IHeroCharacterStateAdapter>());
        _service.DidNotReceive().ReportBirthAfterRepair(Arg.Any<string>(), Arg.Any<Exception>());
    }

    [TestMethod]
    public void Birth_TargetThrowsAfterARepair_ReportsTheFailureAndKeepsTheThrow()
    {
        RepairOnly("father");

        Assert.ThrowsException<NullReferenceException>(
            () => HeroCreator.DeliverOffSpring(NamedHero("mother"), NamedHero("father"), false));

        _service.Received(1).ReportBirthAfterRepair(null!, Arg.Any<NullReferenceException>());
    }

    [TestMethod]
    public void Birth_NoFather_ChecksOnlyTheMother()
    {
        // CampaignCheats.GenerateChild passes a null spouse for an unmarried hero.
        SkipTheOriginal();

        Assert.AreSame(_child, HeroCreator.DeliverOffSpring(NamedHero("mother"), null!, true));

        _service.Received(1).RepairOffspringParent(Arg.Is<IHeroCharacterStateAdapter>(p => p.HeroId == "mother"));
    }

    [TestMethod]
    public void Birth_NoServiceYet_RunsTheTargetUntouched()
    {
        Patch83_StaleCharacterRepair.ResetForUnload();
        SkipTheOriginal();

        Assert.AreSame(_child, HeroCreator.DeliverOffSpring(NamedHero("mother"), NamedHero("father"), false));
    }

    [TestMethod]
    public void PreAfterLoadPostfix_ServiceSet_RunsTheSweepThenTheCensus()
    {
        Patch83_StaleCharacterRepair.Postfix();

        Received.InOrder(() =>
        {
            _service.RepairStaleCharacters();
            _service.ReportHollowHeroCharacters();
        });
    }
}

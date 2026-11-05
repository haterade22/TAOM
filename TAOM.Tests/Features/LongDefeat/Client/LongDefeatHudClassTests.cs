using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>The lord's class on the HUD strip after the wave number and on the end panel (phase2-plan.md section 5, owed since the Slice 5 review).</summary>
[TestClass]
public class LongDefeatHudClassTests
{
    private static (LongDefeatHudVM Vm, LongDefeatSession Session, SurvivalRun Run) Panel(string className)
    {
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) },
            new WavePlan(new[] { new WaveSpec(20, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
            new ShopPlan(600, new[] { new ShopOffer("militia", "{=TAOM_LD_Offer_Militia}Westfold Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f, IntermissionSeconds = 10f },
            RunOptions.Default)
        {
            ClassName = className,
        };
        var run = new SurvivalRun(plan);
        run.Begin();
        var session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.Refresh();
        return (vm, session, run);
    }

    [TestMethod]
    public void TheStrip_NamesTheClassAfterTheWaveNumber()
    {
        var (vm, _, _) = Panel("{=TAOM_LD_Class_Archer}Archer");

        Assert.AreEqual("Preparation, Archer", vm.WaveText);
    }

    [TestMethod]
    public void TheStrip_ForARunWithNoClass_IsTheWaveTextAsItWas()
    {
        var (vm, _, _) = Panel("");

        Assert.AreEqual("Preparation", vm.WaveText);
    }

    [TestMethod]
    public void TheWaveLine_PutsTheClassAfterTheWave_AndKeepsTheWaveAloneWithNoClass()
    {
        Assert.AreEqual("Wave 4, Two Handed Infantry", LongDefeatHudText.WithClass("Wave 4", "{=TAOM_LD_Class_TwoHanded}Two Handed Infantry"));
        Assert.AreEqual("Wave 4", LongDefeatHudText.WithClass("Wave 4", ""));
    }

    [TestMethod]
    public void TheEndPanel_NamesTheClass_AndKeepsItAfterTheMissionClears()
    {
        var (vm, session, run) = Panel("{=TAOM_LD_Class_Archer}Archer");
        run.Abandon();

        session.Clear();
        vm.Refresh();

        StringAssert.StartsWith(vm.EndLines, "Class: Archer\n");
        StringAssert.Contains(vm.EndLines, "Waves cleared: 0");
    }

    [TestMethod]
    public void TheEndPanel_ForARunWithNoClass_HasNoClassLine()
    {
        var (vm, _, run) = Panel("");
        run.Abandon();
        vm.Refresh();

        Assert.IsFalse(vm.EndLines.Contains("Class:"));
        Assert.AreEqual("Waves cleared: 0\nKills: 0\nDefenders lost in cleared waves: 0", vm.EndLines);
    }
}

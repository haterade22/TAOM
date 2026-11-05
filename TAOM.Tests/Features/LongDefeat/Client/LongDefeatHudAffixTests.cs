using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>
/// The HUD's affix line (phase2-plan.md section 5): the running wave's affixes during a wave, and between waves the next wave's, with its health
/// and damage factors once it is past the authored list (the Slice 2 note on the Slice 3 row: <c>WaveStart</c> no longer carries the factors).
/// </summary>
[TestClass]
public class LongDefeatHudAffixTests
{
    private static AffixLabel Label(string id, string english) => new(id, "{=TAOM_LD_Affix_Test_" + id + "}" + english);

    private static RunView View(RunPhase phase, IReadOnlyList<AffixLabel>? wave = null, IReadOnlyList<AffixLabel>? next = null, float health = 1f, float damage = 1f) =>
        new(phase, 5, 4, 100, 30f, 40, 0, 0, 0f, new List<ShopOffer>()) { WaveAffixes = wave ?? new AffixLabel[0], NextAffixes = next ?? new AffixLabel[0], NextHealthFactor = health, NextDamageFactor = damage };

    [TestMethod]
    public void DuringAWave_TheLineNamesTheWavesAffixes()
    {
        var view = View(RunPhase.Wave, wave: new[] { Label("lash", "The White Hand's Whip"), Label("hide", "Orthanc Plate") });

        Assert.AreEqual("Affixes: The White Hand's Whip, Orthanc Plate", LongDefeatHudText.AffixLine(view));
    }

    [TestMethod]
    public void DuringAWave_WithNone_TheLineIsEmpty_EvenPastTheAuthoredList()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.AffixLine(View(RunPhase.Wave, health: 1.5f, damage: 1.2f)));
    }

    [TestMethod]
    public void BetweenWaves_TheLineNamesTheNextWavesAffixes()
    {
        var view = View(RunPhase.Intermission, next: new[] { Label("drums", "War Drums of Orthanc") });

        Assert.AreEqual("Next wave: War Drums of Orthanc", LongDefeatHudText.AffixLine(view));
    }

    [TestMethod]
    public void BetweenWaves_PastTheAuthoredList_TheLineAddsTheNextWavesFactors()
    {
        var view = View(RunPhase.Intermission, next: new[] { Label("drums", "War Drums of Orthanc") }, health: 1.10f, damage: 1.06f);

        Assert.AreEqual("Next wave: War Drums of Orthanc   Next wave attackers: health 110%, damage 106%", LongDefeatHudText.AffixLine(view));
    }

    [TestMethod]
    public void BetweenWaves_WithNoAffix_ButAScaledWave_TheLineIsJustTheFactors()
    {
        var view = View(RunPhase.Preparation, health: 1.5f, damage: 1.09f);

        Assert.AreEqual("Next wave attackers: health 150%, damage 109%", LongDefeatHudText.AffixLine(view));
    }

    [TestMethod]
    public void BetweenWaves_InsideTheAuthoredListWithNoAffix_TheLineIsEmpty()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.AffixLine(View(RunPhase.Intermission)));
    }

    [TestMethod]
    public void AFactorThatIsNotANumber_IsNoFactor()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.AffixLine(View(RunPhase.Intermission, health: float.NaN, damage: float.PositiveInfinity)));
    }

    [TestMethod]
    public void AnAffixWithNoName_ShowsItsId()
    {
        var view = View(RunPhase.Wave, wave: new[] { new AffixLabel("hide", "hide") });

        Assert.AreEqual("Affixes: hide", LongDefeatHudText.AffixLine(view));
    }

    [TestMethod]
    public void TheEndedRun_ShowsNoLine()
    {
        Assert.AreEqual(string.Empty, LongDefeatHudText.AffixLine(View(RunPhase.Ended, wave: new[] { Label("lash", "Whip") }, next: new[] { Label("lash", "Whip") })));
    }

    // ---- through the view model, over a real run ----

    private static LongDefeatHudVM Hud(int startWave, ulong seed, out SurvivalRun run)
    {
        var names = AffixPlan.Ids.ToDictionary(id => id, id => "{=TAOM_LD_Affix_Isengard_" + id + "}Isengard " + id);
        var affixes = new AffixPlan(new[] { new AffixStage(5, 1), new AffixStage(10, 2) }, new Dictionary<string, IReadOnlyDictionary<string, float>>(), names);
        var waves = new WavePlan(
            new[] { new WaveSpec(110, new[] { new WaveTroop("orc", FormationSlot.Infantry, 1) }) },
            new WaveScaling(10, 300),
            new EndlessScale(0.05f, 0.03f, 3f, 3, new Dictionary<string, string>()));
        var plan = new RunPlan("helms", new[] { new GarrisonRow("fow_militia", FormationSlot.Infantry, 30) }, waves,
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "fow_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default with { PreparationSeconds = 1f }, new RunOptions { StartWave = startWave, Seed = seed }) { Affixes = affixes };
        run = new SurvivalRun(plan);
        run.Begin();
        var session = new LongDefeatSession();
        session.Fill(run);
        var vm = new LongDefeatHudVM(session, () => { });
        vm.Refresh();
        return vm;
    }

    [TestMethod]
    public void TheStrip_InPreparation_NamesTheFirstWavesAffixAndFactors()
    {
        var vm = Hud(startWave: 5, seed: 2, out _);

        Assert.AreEqual("Next wave: Isengard hide   Next wave attackers: health 120%, damage 112%", vm.AffixText);
    }

    [TestMethod]
    public void TheStrip_InTheWave_NamesTheRunningWavesAffix()
    {
        var vm = Hud(startWave: 5, seed: 2, out var run);

        for (var i = 0; i < 5 && run.Phase != RunPhase.Wave; i++)
            run.Tick(new RunSnapshot(1f, 0, 0, 40, 0, 0, 1000));
        vm.Refresh();

        Assert.AreEqual("Affixes: Isengard hide", vm.AffixText);
    }

    [TestMethod]
    public void TheStrip_BeforeAnyAffix_IsEmpty()
    {
        var vm = Hud(startWave: 1, seed: 1, out _);

        Assert.AreEqual(string.Empty, vm.AffixText);
    }
}

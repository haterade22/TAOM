using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

[TestClass]
public class LongDefeatChecksTests
{
    private static LdBootFacts Good() => new(true, true, true, false, true, true, true);

    private static void Is(CheckResult expected, CheckVerdict v) => Assert.AreEqual(expected, v.Result, v.Detail);

    private static Purse Purse(int start = 600, int bounty = 40, int bonus = 200, int early = 0, int spent = 180)
    {
        var p = new Purse(start);
        p.Earn(PurseSource.Bounty, bounty);
        p.Earn(PurseSource.WaveBonus, bonus);
        p.Earn(PurseSource.EarlyStart, early);
        if (spent > 0)
            p.TrySpend(spent, PurseSink.Packs);
        return p;
    }

    [TestMethod]
    public void Ids_AreTheManifestSpelling()
    {
        Assert.AreEqual("taom.long-defeat.boot", LongDefeatChecks.BootId);
        Assert.AreEqual("taom.long-defeat.general", LongDefeatChecks.GeneralId);
        Assert.AreEqual("taom.long-defeat.muster", LongDefeatChecks.MusterId);
        Assert.AreEqual("taom.long-defeat.keep", LongDefeatChecks.KeepId);
        Assert.AreEqual("taom.long-defeat.wave", LongDefeatChecks.WaveId);
        Assert.AreEqual("taom.long-defeat.stale", LongDefeatChecks.StaleId);
        Assert.AreEqual("taom.long-defeat.no-rout", LongDefeatChecks.NoRoutId);
        Assert.AreEqual("taom.long-defeat.gold", LongDefeatChecks.GoldId);
        Assert.AreEqual("taom.long-defeat.shop", LongDefeatChecks.ShopId);
        Assert.AreEqual("taom.long-defeat.early-start", LongDefeatChecks.EarlyStartId);
        Assert.AreEqual("taom.long-defeat.player-death", LongDefeatChecks.PlayerDeathId);
        Assert.AreEqual("taom.long-defeat.end", LongDefeatChecks.EndId);
        Assert.AreEqual("taom.long-defeat.best-run", LongDefeatChecks.BestRunId);
        Assert.AreEqual("taom.long-defeat.scale", LongDefeatChecks.ScaleId);
    }

    [TestMethod]
    public void Boot_NeedsEveryFact()
    {
        Is(CheckResult.Pass, LongDefeatChecks.Boot(Good()));
        Is(CheckResult.Fail, LongDefeatChecks.Boot(Good() with { DeploymentFinished = false }));
        Is(CheckResult.Fail, LongDefeatChecks.Boot(Good() with { SiegeTeamAi = false }));
        Is(CheckResult.Fail, LongDefeatChecks.Boot(Good() with { HasMainAgent = false }));
        Is(CheckResult.Fail, LongDefeatChecks.Boot(Good() with { IsFriendlyMission = true }));
    }

    [TestMethod]
    public void General_NeedsMainAsGeneralAndOrderOwner()
    {
        Is(CheckResult.Pass, LongDefeatChecks.General(Good()));
        Is(CheckResult.Fail, LongDefeatChecks.General(Good() with { GeneralIsMain = false }));
        Is(CheckResult.Fail, LongDefeatChecks.General(Good() with { IsPlayerGeneral = false }));
        Is(CheckResult.Fail, LongDefeatChecks.General(Good() with { OrderOwnerIsMain = false }));
    }

    [TestMethod]
    public void Muster_FailsOnFarOffMeshPileOrOverlap_PassesAtTheLimit()
    {
        // The first game run read 9 of 20 on the point, with overlap 0: the soldiers had left their slots. A pile is most of a batch, as with 41 of 68.
        Is(CheckResult.Pass, LongDefeatChecks.Muster(onPoint: 10, overlap: 6, far: 0, offMesh: 0, batch: 20));
        Is(CheckResult.Fail, LongDefeatChecks.Muster(11, 0, 0, 0, 20));
        Is(CheckResult.Pass, LongDefeatChecks.Muster(6, 0, 0, 0, 5));
        Is(CheckResult.Fail, LongDefeatChecks.Muster(7, 0, 0, 0, 5));
        Is(CheckResult.Fail, LongDefeatChecks.Muster(0, 7, 0, 0, 20));
        Is(CheckResult.Fail, LongDefeatChecks.Muster(0, 0, 1, 0, 20));
        Is(CheckResult.Fail, LongDefeatChecks.Muster(0, 0, 0, 1, 20));
        Is(CheckResult.Fail, LongDefeatChecks.MusterPlaceLost(FormationSlot.Infantry));
    }

    [TestMethod]
    public void Keep_FailsWithoutAPoint_AndNamesIt()
    {
        Is(CheckResult.Fail, LongDefeatChecks.Keep(null));
        var v = LongDefeatChecks.Keep(new LdPoint(138.4f, 159.5f, 32.5f));
        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "at=(138.4,159.5,32.5)");
    }

    [TestMethod]
    public void Keep_NamesItsSourceAndRadius()
    {
        var v = LongDefeatChecks.Keep(new LdPoint(1f, 2f, 3f), "recorded", 7f);

        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "source=recorded");
        StringAssert.Contains(v.Detail, "radius=7.0");
    }

    [TestMethod]
    public void BootFailed_FailsAndSaysWhy()
    {
        var v = LongDefeatChecks.BootFailed("no spawn frame for defender foot");

        Is(CheckResult.Fail, v);
        StringAssert.Contains(v.Detail, "reason=no spawn frame for defender foot");
    }

    [TestMethod]
    public void Fault_FailsAndNamesTheSiteAndTheException()
    {
        var v = LongDefeatChecks.Fault("OnMissionTick", new System.InvalidOperationException("x"));

        Is(CheckResult.Fail, v);
        StringAssert.Contains(v.Detail, "where=OnMissionTick");
        StringAssert.Contains(v.Detail, "error=InvalidOperationException");
    }

    [TestMethod]
    public void Keep_NaNCoordinate_Fails()
    {
        Is(CheckResult.Fail, LongDefeatChecks.Keep(new LdPoint(float.NaN, 1f, 1f)));
        Is(CheckResult.Fail, LongDefeatChecks.Keep(new LdPoint(1f, float.PositiveInfinity, 1f)));
    }

    [TestMethod]
    public void Wave_PassesWhenFelledIsWithinTheStragglerShare()
    {
        var v = LongDefeatChecks.Wave(new WaveStats(1, 100, 11, 20, 40f), 110, 0.10f);
        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "kills=100 felled=11 lost=20 seconds=40.0");
        Is(CheckResult.Fail, LongDefeatChecks.Wave(new WaveStats(1, 98, 12, 20, 40f), 110, 0.10f));
    }

    [TestMethod]
    public void Wave_TheStragglerShareCoversTheSummonsToo_AsTheStalemateRulesCountDoes()
    {
        // Wave 8: 170 roster soldiers and one summon of 20. The rule fells up to 10 percent of 190, so 19 roster soldiers felled is legitimate.
        var stats = new WaveStats(1, 151, 19, 0, 40f) { Summoned = 20 };

        Is(CheckResult.Pass, LongDefeatChecks.Wave(stats, 170, 0.10f));
        Is(CheckResult.Fail, LongDefeatChecks.Wave(stats with { Felled = 20, Kills = 150 }, 170, 0.10f));
        Is(CheckResult.Fail, LongDefeatChecks.Wave(new WaveStats(1, 151, 19, 0, 40f), 170, 0.10f));
    }

    [TestMethod]
    public void Wave_KillsAndFelledShortOfTheCount_Fails()
    {
        Is(CheckResult.Fail, LongDefeatChecks.Wave(new WaveStats(1, 90, 5, 0, 40f), 110, 0.10f));
        Is(CheckResult.Pass, LongDefeatChecks.Wave(new WaveStats(1, 105, 5, 0, 40f), 110, 0.10f));
    }

    [TestMethod]
    public void Wave_NaNShare_Fails()
    {
        Is(CheckResult.Fail, LongDefeatChecks.Wave(new WaveStats(1, 110, 0, 0, 40f), 110, float.NaN));
    }

    [TestMethod]
    public void Stale_ClearedPasses_StrandedFailsOnlyForAStalemate()
    {
        Is(CheckResult.Pass, LongDefeatChecks.StaleCleared());
        Is(CheckResult.Fail, LongDefeatChecks.StaleStranded(RunEndReason.Stalemate, 40));
        Is(CheckResult.Skip, LongDefeatChecks.StaleStranded(RunEndReason.DefendersDead, 1));
        Is(CheckResult.Skip, LongDefeatChecks.StaleStranded(RunEndReason.Abandoned, 40));
        StringAssert.Contains(LongDefeatChecks.StaleStranded(RunEndReason.DefendersDead, 1).Detail, "standing=1");
    }

    [TestMethod]
    public void NoRout_NeedsNoFleeingAndSomeKills()
    {
        var stats = new RunStats();
        Is(CheckResult.Skip, LongDefeatChecks.NoRout(stats));
        stats.AttackerKilled();
        Is(CheckResult.Pass, LongDefeatChecks.NoRout(stats));
        stats.ObserveFleeing(1);
        Is(CheckResult.Fail, LongDefeatChecks.NoRout(stats));

        var fled = new RunStats();
        fled.AttackerKilled();
        fled.AttackerFled();
        Is(CheckResult.Fail, LongDefeatChecks.NoRout(fled));
    }

    [TestMethod]
    public void Gold_BalanceMustMatchEarnedAndSpent()
    {
        var stats = new RunStats();
        stats.AttackerKilled();
        var v = LongDefeatChecks.Gold(Purse(), stats, wavesCleared: 1);
        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "balance=660");
    }

    [TestMethod]
    public void Gold_TheBooksMustBalance_EachClauseFailsOnItsOwn()
    {
        Is(CheckResult.Pass, LongDefeatChecks.Gold(600, 60, 100, 560, 40, 20, 0, 5, 1));
        Is(CheckResult.Fail, LongDefeatChecks.Gold(600, 60, 100, 561, 40, 20, 0, 5, 1));
        Is(CheckResult.Fail, LongDefeatChecks.Gold(600, 60, 100, 559, 40, 20, 0, 5, 1));
        Is(CheckResult.Fail, LongDefeatChecks.Gold(600, 60, 100, 560, 0, 20, 0, 5, 1));
        Is(CheckResult.Fail, LongDefeatChecks.Gold(600, 60, 100, 560, 40, 0, 0, 5, 1));
        Is(CheckResult.Pass, LongDefeatChecks.Gold(600, 0, 0, 600, 0, 0, 0, 0, 0));
    }

    [TestMethod]
    public void Gold_BountyOwedWhenThereWereKills_BonusOwedWhenAWaveCleared()
    {
        var stats = new RunStats();
        stats.AttackerKilled();
        Is(CheckResult.Fail, LongDefeatChecks.Gold(Purse(bounty: 0), stats, 1));
        Is(CheckResult.Fail, LongDefeatChecks.Gold(Purse(bonus: 0), stats, 1));
    }

    [TestMethod]
    public void Gold_ALosingRunWithNoClearOwesNoBonus_AndNoKillsOwesNoBounty()
    {
        Is(CheckResult.Pass, LongDefeatChecks.Gold(Purse(bounty: 0, bonus: 0, spent: 0), new RunStats(), 0));
    }

    [TestMethod]
    public void Shop_NeedsTheFullPackBackAndJudgesNothingElse()
    {
        var v = LongDefeatChecks.Shop(requested: 12, spawned: 12, onPoint: 24, overlap: 9);
        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "on_point=24");
        StringAssert.Contains(v.Detail, "overlap=9");
        Is(CheckResult.Fail, LongDefeatChecks.Shop(12, 11, 0, 0));
    }

    [TestMethod]
    public void EarlyStart_PaysTheFormula_CapAndRateApply()
    {
        var r = RunRules.Default;
        Is(CheckResult.Pass, LongDefeatChecks.EarlyStart(45f, 90, r));
        Is(CheckResult.Fail, LongDefeatChecks.EarlyStart(45f, 91, r));
        Is(CheckResult.Pass, LongDefeatChecks.EarlyStart(100f, 120, r));
        Is(CheckResult.Fail, LongDefeatChecks.EarlyStart(100f, 200, r));
        Is(CheckResult.Pass, LongDefeatChecks.EarlyStart(0.2f, 1, r));
    }

    [TestMethod]
    public void EarlyStart_NaNSecondsOrRate_ExpectsNothing()
    {
        Is(CheckResult.Pass, LongDefeatChecks.EarlyStart(float.NaN, 0, RunRules.Default));
        Is(CheckResult.Fail, LongDefeatChecks.EarlyStart(float.NaN, 120, RunRules.Default));
        Is(CheckResult.Fail, LongDefeatChecks.EarlyStart(45f, 90, RunRules.Default with { EarlyGoldPerSecond = float.NaN }));
    }

    [TestMethod]
    public void PlayerDeath_PassesOnlyWhenAdopted()
    {
        var v = LongDefeatChecks.PlayerDeath(true, "respawn");
        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "via=respawn");
        Is(CheckResult.Fail, LongDefeatChecks.PlayerDeath(false, "respawn"));
    }

    [TestMethod]
    public void End_NeedsTheReport()
    {
        var r = new RunResult(RunEndReason.TargetReached, 2, 2, 150);
        var v = LongDefeatChecks.End(r, reportBuilt: true);
        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "reason=TargetReached");
        Is(CheckResult.Fail, LongDefeatChecks.End(r, false));
    }

    [TestMethod]
    public void Record_RoutesEachResultToItsLogMethod_AndAcceptsNoLog()
    {
        var log = new RecordingOutcomeLog();
        LongDefeatChecks.Record(log, "id.a-b", "k", new CheckVerdict(CheckResult.Pass, "d=1"));
        LongDefeatChecks.Record(log, "id.a-b", "k", new CheckVerdict(CheckResult.Fail, "d=2"));
        LongDefeatChecks.Record(log, "id.a-b", "k", new CheckVerdict(CheckResult.Skip, "d=3"));
        CollectionAssert.AreEqual(new[] { "pass id.a-b k d=1", "fail id.a-b k d=2", "skip id.a-b k d=3" }, log.Lines);

        LongDefeatChecks.Record(null, "id.a-b", "k", new CheckVerdict(CheckResult.Pass, ""));
    }

    [TestMethod]
    public void BestRun_PassesOnlyWhenTheReadBackRunIsTheSavedOne()
    {
        var run = new BestRun(3, 200);
        Is(CheckResult.Pass, LongDefeatChecks.BestRunSaved(run, new BestRun(3, 200)));
        Is(CheckResult.Fail, LongDefeatChecks.BestRunSaved(run, new BestRun(3, 199)));
        StringAssert.Contains(LongDefeatChecks.BestRunSaved(run, new BestRun(3, 199)).Detail, "read_back=3/199");
        StringAssert.Contains(LongDefeatChecks.BestRunSaved(run, new BestRun(3, 200)).Detail, "read_back=same");
        StringAssert.Contains(LongDefeatChecks.BestRunSaved(run, null, "IOException").Detail, "read_back=none");
        Is(CheckResult.Fail, LongDefeatChecks.BestRunSaved(run, null, "IOException"));
    }

    [TestMethod]
    public void BestRun_AKeptRun_IsSkippedNotFailed()
    {
        Is(CheckResult.Skip, LongDefeatChecks.BestRunKept(new BestRun(1, 1), new BestRun(4, 9)));
    }

    [TestMethod]
    public void MenuId_IsTheManifestSpelling() => Assert.AreEqual("taom.long-defeat.menu", LongDefeatChecks.MenuId);

    [TestMethod]
    public void Menu_PassesWithAFortress_FailsWithNone_AndSkipsOnADedicatedServer()
    {
        Is(CheckResult.Pass, LongDefeatChecks.Menu(fortresses: 1, dedicatedServer: false));
        Is(CheckResult.Fail, LongDefeatChecks.Menu(fortresses: 0, dedicatedServer: false));
        Is(CheckResult.Skip, LongDefeatChecks.Menu(fortresses: 1, dedicatedServer: true));
        Is(CheckResult.Skip, LongDefeatChecks.Menu(fortresses: 0, dedicatedServer: true));
        StringAssert.Contains(LongDefeatChecks.Menu(2, false).Detail, "fortresses=2");
    }

    // ---- Scale (the endless waves) ----

    private static readonly string[] Mix = { "orc2", "skirm" };

    private static LdScaleSample Sample(float limit = 110f, string troop = "orc2", float max = 100f, long blows = 40) => new(troop, limit, max, blows);

    private static CheckVerdict Scale(float health = 1.1f, float damage = 1.06f, LdScaleSample? spawn = null, LdScaleSample? late = null,
        int steps = 0, int over = 0, long blows = 40) =>
        LongDefeatChecks.Scale(3, health, damage, steps, spawn ?? Sample(), late, Mix, blows, over);

    [TestMethod]
    public void Scale_TheSampleAtTheFactor_AndInTheMix_Passes()
    {
        var v = Scale(late: Sample(limit: 109.5f, troop: "skirm"));

        Is(CheckResult.Pass, v);
        StringAssert.StartsWith(v.Detail, "w=3 health=1.100");
        StringAssert.Contains(v.Detail, "spawn=1.100");
        StringAssert.Contains(v.Detail, "late=1.095");
        StringAssert.Contains(v.Detail, "over_room=0");
        StringAssert.Contains(v.Detail, "amplified=40");
    }

    [TestMethod]
    public void Scale_NoLateSample_StillPasses_AndSaysSo()
    {
        var v = Scale();

        Is(CheckResult.Pass, v);
        StringAssert.Contains(v.Detail, "late=none");
    }

    [TestMethod]
    public void Scale_NoSampleAtSpawn_Fails()
    {
        Is(CheckResult.Fail, LongDefeatChecks.Scale(3, 1.1f, 1.06f, 0, null, null, Mix, 40, 0));
        Is(CheckResult.Fail, Scale(spawn: Sample(troop: "")));
    }

    [DataTestMethod]
    [DataRow(108.8f)]
    [DataRow(111.2f)]
    [DataRow(100f)]
    public void Scale_ASpawnSampleMoreThanOnePercentOffTheFactor_Fails(float limit) => Is(CheckResult.Fail, Scale(spawn: Sample(limit: limit)));

    [TestMethod]
    public void Scale_ALateSampleOffTheFactor_Fails_BecauseTheEngineMayHaveUndoneTheWrite()
    {
        Is(CheckResult.Fail, Scale(late: Sample(limit: 100f)));
    }

    [TestMethod]
    public void Scale_ASampleOfATroopThePlanDidNotMix_Fails_SoAMissedTierStepIsSeen()
    {
        Is(CheckResult.Fail, Scale(spawn: Sample(troop: "orc")));
        Is(CheckResult.Fail, Scale(late: Sample(limit: 110f, troop: "orc")));
    }

    [TestMethod]
    public void Scale_DamageAboveOneNeedsAnAmplifiedBlow_AndFactorOneNeedsNone()
    {
        Is(CheckResult.Fail, Scale(damage: 1.06f, blows: 0));
        Is(CheckResult.Pass, Scale(damage: 1f, blows: 0));
    }

    [TestMethod]
    public void Scale_AnOverfilledField_Fails()
    {
        Is(CheckResult.Fail, Scale(over: 1));
    }

    [TestMethod]
    public void Scale_NaNAnywhere_Fails()
    {
        Is(CheckResult.Fail, Scale(spawn: Sample(max: 0f)));
        Is(CheckResult.Fail, Scale(spawn: Sample(limit: float.NaN)));
        Is(CheckResult.Fail, Scale(health: float.NaN));
        Is(CheckResult.Fail, Scale(damage: float.NaN));
    }
}

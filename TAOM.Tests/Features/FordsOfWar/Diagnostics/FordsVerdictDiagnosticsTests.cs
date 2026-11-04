using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

/// <summary>The check 4 layout part and the links check 9 names when melee never lands.</summary>
[TestClass]
public class FordsVerdictDiagnosticsTests
{
    private static FordsRunEvidence Run()
    {
        var run = new FordsRunEvidence { Run = 1, Facts = new FordsMatchFacts(60f, 8f, 12f, 2, 150, 200) };
        for (var side = 0; side < 2; side++)
        {
            for (var lane = 0; lane < 2; lane++)
                run.Lanes.Add(new FordsLaneEvidence { Side = side == 0 ? "West" : "East", Lane = lane });
        }

        return run;
    }

    private static FordsVerdictRow Row(FordsRunEvidence run, int check) => FordsVerdicts.Judge(new FordsSessionEvidence(), run)[check - 1];

    private static FordsPart PartOf(FordsVerdictRow row, string name) => row.Parts.Single(p => p.Name == name);

    private static void Started(FordsRunEvidence run) => run.Stages.Add(new FordsStageMark { Name = "matchStarted" });

    private static FordsTowerEvidence Tower(FordsRunEvidence run, string id) => run.Towers[id] = new FordsTowerEvidence { Owner = "East", Kind = "Tower" };

    [TestMethod]
    public void Check04_Layout_WithoutAReportedLayout_IsNotSeen()
    {
        var run = Run();
        Started(run);

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(run, 4), "layout").Result);
    }

    [TestMethod]
    public void Check04_Layout_AllTowersBuiltAndBothKeepsLocked_Passes()
    {
        var run = Run();
        Started(run);
        run.Start.Layout.Reported = true;

        Assert.AreEqual(FordsResult.Pass, PartOf(Row(run, 4), "layout").Result);
    }

    [TestMethod]
    public void Check04_Layout_AMissingTower_FailsAndNamesTheIds()
    {
        var run = Run();
        Started(run);
        run.Start.Layout.Reported = true;
        run.Start.Layout.MissingTowerIds.Add("tower_west_1_0");
        run.Start.Layout.MissingTowerIds.Add("tower_west_1_1");

        var part = PartOf(Row(run, 4), "layout");

        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, "tower_west_1_0");
        StringAssert.Contains(part.Numbers, "tower_west_1_1");
    }

    [DataTestMethod]
    [DataRow(true, false, "keepOpen=West")]
    [DataRow(false, true, "keepOpen=East")]
    [DataRow(true, true, "keepOpen=West,East")]
    public void Check04_Layout_AKeepOpenAtStart_FailsAndNamesTheSide(bool west, bool east, string expected)
    {
        var run = Run();
        Started(run);
        run.Start.Layout.Reported = true;
        run.Start.Layout.KeepOpenWest = west;
        run.Start.Layout.KeepOpenEast = east;

        var part = PartOf(Row(run, 4), "layout");

        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, expected);
    }

    [TestMethod]
    public void Check09_Info_NamesEveryLinkOfTheChain()
    {
        var run = Run();
        run.Lanes[3].CommandsGiven["AttackTower"] = 4;
        run.Lanes[1].CommandsGiven["AttackTower"] = 1;
        run.Lanes[0].CommandsGiven["MoveTo"] = 90;
        var tower = Tower(run, "t1");
        tower.GunnerBlowsMelee = 7;
        tower.GunnerBlowsRanged = 2;
        tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Weaponless] = 3;
        tower.Blows[(int)TowerBlowClass.Ranged, (int)TowerDamageReason.Locked] = 5;

        var info = Row(run, 9).Info;

        StringAssert.Contains(info, "attackTowerGiven=5 (W1:1 E1:4)");
        StringAssert.Contains(info, "nativeTowerBlows=8");
        StringAssert.Contains(info, "gunnerBlows=7 melee/2 ranged");
        StringAssert.Contains(info, "Weaponless=3", "the existing breakdown stays");
    }

    [TestMethod]
    public void Check09_Info_NoAttackTowerEverGiven_SaysZeroWithoutALaneListOrLadder()
    {
        var run = Run();
        Tower(run, "t1");

        var info = Row(run, 9).Info;

        StringAssert.Contains(info, "attackTowerGiven=0 nativeTowerBlows=0");
        StringAssert.Contains(info, "ladder -");
    }

    [TestMethod]
    public void Check09_Info_PrintsTheLadderPerStreamThatWasOrderedAtATower()
    {
        var run = Run();
        var west1 = run.Lanes[1];
        west1.CommandsGiven["AttackTower"] = 2;
        west1.AttackTarget = "tower_east_1_0";
        west1.AttackUnitSeconds = 120.0;
        west1.AttackFlaggedUnitSeconds = 100.0;
        west1.AttackContactUnitSeconds = 40.5;
        west1.TowerBlows = 15;
        west1.AppliedBlows = 12;
        west1.GunnerBlows = 4;
        west1.MeleeTowerBlows = 9;
        run.Lanes[0].AttackUnitSeconds = 50.0;

        var info = Row(run, 9).Info;

        StringAssert.Contains(info, "ladder W1>tower_east_1_0 orders=2 underS=120.0 flaggedS=100.0 contactS=40.5 towerBlows=15 melee=9 gunnerBlows=4 applied=12");
        Assert.IsFalse(info.Contains("W0>"), "a stream that was never ordered at a tower has no ladder row");
    }

    [TestMethod]
    public void Check09_Info_NamesEachTowersLowestHitPoints_HealedTotalAppliedDamageAndMeleeRawSummary()
    {
        var run = Run();
        var tower = Tower(run, "t1");
        tower.MinRulesHp = 420;
        tower.HealedTotal = 75;
        tower.AppliedDamage[(int)TowerBlowClass.Melee] = 300;
        tower.AppliedDamage[(int)TowerBlowClass.Ranged] = 40;
        tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Applied] = 4;
        tower.MeleeRaw[0] = 2;
        tower.MeleeRaw[100] = 2;
        Tower(run, "untouched");

        var info = Row(run, 9).Info;

        StringAssert.Contains(info, "t1:minHp=420 healed=75 applied=Melee:300,Ranged:40 meleeRaw=n4/min0/med50.0/max100");
        Assert.IsFalse(info.Contains("untouched:"), "a tower nobody struck has no row");
    }

    [TestMethod]
    public void Check09_Melee_RuleIsUnchangedByTheNewEvidence()
    {
        var run = Run();
        var tower = Tower(run, "t1");
        run.Lanes[0].AttackContactUnitSeconds = 500;
        tower.GunnerBlowsMelee = 50;

        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(run, 9), "melee").Result, "contact and gunner blows alone neither pass nor fail melee");

        tower.MeleeNearUnitSeconds = 30;
        Assert.AreEqual(FordsResult.Fail, PartOf(Row(run, 9), "melee").Result);

        tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Applied] = 1;
        Assert.AreEqual(FordsResult.Pass, PartOf(Row(run, 9), "melee").Result);
    }

    [TestMethod]
    public void Check09_Bypass_NoMarchingStreamEverJudged_IsNotSeen()
    {
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(Run(), 9), "bypass").Result);
    }

    [TestMethod]
    public void Check09_Bypass_StreamsJudgedAndNoneHadMostOfItsUnitsPastTheTower_PassesAndCountsTheSamples()
    {
        var run = Run();
        run.Lanes[0].MarchSamples = 40;
        run.Lanes[3].MarchSamples = 12;
        run.Lanes[3].PastSamples = 1;
        run.Lanes[3].MaxPastRun = 1;

        var part = PartOf(Row(run, 9), "bypass");

        Assert.AreEqual(FordsResult.Pass, part.Result);
        StringAssert.Contains(part.Numbers, "samplesJudged=52 failingSamples=1");
    }

    [TestMethod]
    public void Check09_Bypass_MostUnitsPastTheTowerOnTwoLooksInARow_FailsAndNamesTheWorstStreamTowerAndShare()
    {
        var run = Run();
        run.Lanes[0].MarchSamples = 40;
        run.Lanes[2].MarchSamples = 30;
        run.Lanes[2].PastSamples = 3;
        run.Lanes[2].MaxPastRun = 2;
        run.Lanes[2].MaxPastShare = 0.8333;
        run.Lanes[2].PastTower = "tower_west_0_0";
        run.Lanes[2].FirstPastMt = 612.5;
        run.Lanes[1].MarchSamples = 20;
        run.Lanes[1].PastSamples = 2;
        run.Lanes[1].MaxPastRun = 2;
        run.Lanes[1].MaxPastShare = 0.6;
        run.Lanes[1].PastTower = "tower_east_1_0";

        var part = PartOf(Row(run, 9), "bypass");

        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, "samplesJudged=90 failingSamples=5");
        StringAssert.Contains(part.Numbers, "worst=E0 past tower_west_0_0: 83.3% of its units, 2 looks in a row, from mt=612.5");
        StringAssert.Contains(part.Numbers, "also=W1");
    }

    [TestMethod]
    public void Check09_Bypass_FailingLooksThatNeverRepeatedInARow_DoNotFail()
    {
        var run = Run();
        run.Lanes[2].MarchSamples = 30;
        run.Lanes[2].PastSamples = 6;
        run.Lanes[2].MaxPastRun = 1;
        run.Lanes[2].MaxPastShare = 0.9;

        var part = PartOf(Row(run, 9), "bypass");

        Assert.AreEqual(FordsResult.Pass, part.Result, "one look with most units past is a straggling wave, not a march by the tower");
        StringAssert.Contains(part.Numbers, "failingSamples=6");
    }

    [TestMethod]
    public void Check03_Exceptions_WatchedAndNoneSwallowed_Passes()
    {
        var run = Run();
        run.Exceptions.Watching = true;

        Assert.AreEqual(FordsResult.Pass, PartOf(Row(run, 3), "exceptions").Result);
    }

    [TestMethod]
    public void Check03_Exceptions_NeverWatched_IsNotSeen()
    {
        Assert.AreEqual(FordsResult.NotSeen, PartOf(Row(Run(), 3), "exceptions").Result);
    }

    [TestMethod]
    public void Check03_Exceptions_ASwallowedException_FailsAndNamesTheFirstTypeAndSite()
    {
        var run = Run();
        run.Exceptions.Watching = true;
        run.Exceptions.Count = 3;
        run.Exceptions.FirstType = "IndexOutOfRangeException";
        run.Exceptions.FirstSite = "Patch37.Tick";
        run.Exceptions.FirstMt = 61.5;

        var part = PartOf(Row(run, 3), "exceptions");

        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, "swallowed=3 first=IndexOutOfRangeException@Patch37.Tick mt=61.5");
    }

    [TestMethod]
    public void Check04_Info_ListsTheAlarmShareOfEachStream()
    {
        var run = Run();
        run.Lanes[0].UnitSeconds = 200;
        run.Lanes[0].AlarmedUnitSeconds = 200;
        run.Lanes[3].UnitSeconds = 100;
        run.Lanes[3].AlarmedUnitSeconds = 25;

        StringAssert.Contains(Row(run, 4).Info, "alarmed=W0:1.00 E1:0.25");
    }

    [TestMethod]
    public void Check09_Info_NamesTheTopMeleeProbeKeysOfEachStruckTower_MostCommonFirst()
    {
        var run = Run();
        var tower = Tower(run, "t1");
        tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.RawZero] = 9;
        tower.MeleeProbe["S/Cut/tip0/pas0/alt0/arm0/hit"] = 2;
        tower.MeleeProbe["T/Pierce/tip0/pas0/alt0/arm0/b<1"] = 7;
        tower.MeleeProbe["T/Pierce/tip0/pas1/alt0/arm0/b0"] = 5;
        tower.MeleeProbe["S/Blunt/tip0/pas0/alt0/arm0/hit"] = 1;
        tower.MeleeProbe["T/Cut/tip0/pas0/alt0/arm0/b0"] = 1;

        var info = Row(run, 9).Info;

        StringAssert.Contains(info, "probe=T/Pierce/tip0/pas0/alt0/arm0/b<1:7,T/Pierce/tip0/pas1/alt0/arm0/b0:5,S/Cut/tip0/pas0/alt0/arm0/hit:2");
        Assert.IsFalse(info.Contains("S/Blunt"), "only the top three keys are named");
    }

    [TestMethod]
    public void Check09_Info_AStruckTowerWithNoProbeBlow_HasNoProbeColumn_AndTheVerdictIsUnchanged()
    {
        var run = Run();
        var tower = Tower(run, "t1");
        tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Applied] = 1;

        var info = Row(run, 9).Info;

        Assert.IsFalse(info.Contains("probe="));
    }

    [TestMethod]
    public void Check09_Info_NamesTheFlooredBlowsAndTheirDamageOfEachStruckTower()
    {
        var run = Run();
        var tower = Tower(run, "t1");
        tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Applied] = 4;
        tower.FlooredBlows = 3;
        tower.FlooredDamage = 30;

        var info = Row(run, 9).Info;

        StringAssert.Contains(info, "floored=3/30");
    }

    [TestMethod]
    public void Check09_Info_ATowerRowCarriesTheDamagePerContactSecond()
    {
        var run = Run();
        var tower = Tower(run, "t1");
        tower.AppliedDamage[(int)TowerBlowClass.Melee] = 300;
        tower.AppliedDamage[(int)TowerBlowClass.Ranged] = 100;
        tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Applied] = 4;
        tower.ContactUnitSeconds = 80.0;
        var other = Tower(run, "t2");
        other.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Applied] = 1;

        var info = Row(run, 9).Info;

        StringAssert.Contains(info, "t1:minHp=-");
        StringAssert.Contains(info, "dmgPerContactS=5.0");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(info, "dmgPerContactS=").Count, "a tower with no contact seconds prints none");
    }
}

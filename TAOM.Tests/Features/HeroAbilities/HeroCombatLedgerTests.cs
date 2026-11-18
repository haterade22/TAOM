using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAbilities.Domain;

namespace TAOM.Tests.Features.HeroAbilities;

/// <summary>
/// The hero balance tally: one sheet for each hero body, with the kills, the damage he deals and takes, how long he lives
/// and what felled him, and one summary line for each hero across his bodies.
/// </summary>
[TestClass]
public class HeroCombatLedgerTests
{
    private const string Gimli = "taom_hero_gimli";
    private const string Legolas = "taom_hero_legolas";

    private sealed class Body
    {
    }

    [TestMethod]
    public void Add_NumbersTheBodiesOfOneHero()
    {
        var ledger = new HeroCombatLedger<Body>();
        ledger.Add(new Body(), Gimli, 0f);
        ledger.Add(new Body(), Legolas, 0f);
        var second = ledger.Add(new Body(), Gimli, 50f);
        Assert.AreEqual(2, second.Serial);
        Assert.AreEqual(50f, second.SpawnedAt);
    }

    [TestMethod]
    public void Add_TheSameBodyTwice_KeepsTheFirstSheet()
    {
        var ledger = new HeroCombatLedger<Body>();
        var body = new Body();
        var first = ledger.Add(body, Gimli, 0f);
        Assert.AreSame(first, ledger.Add(body, Gimli, 9f));
        Assert.AreEqual(1, ledger.All.Count());
    }

    [TestMethod]
    public void Hits_CountOnlyForListedBodies()
    {
        var ledger = new HeroCombatLedger<Body>();
        var gimli = new Body();
        var orc = new Body();
        ledger.Add(gimli, Gimli, 0f);
        ledger.Dealt(gimli, 60);
        ledger.Dealt(orc, 999);
        ledger.Taken(gimli, 25);
        ledger.Taken(gimli, 15);
        ledger.Taken(orc, 999);
        var sheet = ledger.Of(gimli)!;
        Assert.AreEqual(60, sheet.Dealt);
        Assert.AreEqual(1, sheet.HitsLanded);
        Assert.AreEqual(40, sheet.Taken);
        Assert.AreEqual(2, sheet.HitsTaken);
        Assert.IsNull(ledger.Of(orc));
    }

    [TestMethod]
    public void Hits_WithNoDamage_AreNotCounted()
    {
        var ledger = new HeroCombatLedger<Body>();
        var gimli = new Body();
        ledger.Add(gimli, Gimli, 0f);
        ledger.Dealt(gimli, 0);
        ledger.Taken(gimli, -5);
        var sheet = ledger.Of(gimli)!;
        Assert.AreEqual(0, sheet.HitsLanded);
        Assert.AreEqual(0, sheet.HitsTaken);
    }

    [TestMethod]
    public void Fell_IsRecordedOnce_AndAHitAfterItIsNotCounted()
    {
        var ledger = new HeroCombatLedger<Body>();
        var gimli = new Body();
        ledger.Add(gimli, Gimli, 10f);
        var fell = ledger.Fell(gimli, 130f, "uruk_hai_berserker", "melee", downed: true);
        Assert.IsNotNull(fell);
        Assert.IsNull(ledger.Fell(gimli, 140f, "other", "missile", downed: true));
        ledger.Taken(gimli, 50);
        Assert.AreEqual(120f, fell!.SecondsAlive(500f), 0.001f);
        Assert.AreEqual("uruk_hai_berserker", fell.Killer);
        Assert.AreEqual(0, fell.HitsTaken);
    }

    [TestMethod]
    public void NoteHealth_KeepsTheHighestLimit()
    {
        var ledger = new HeroCombatLedger<Body>();
        var gimli = new Body();
        var sheet = ledger.Add(gimli, Gimli, 0f);
        sheet.NoteHealth(100f);
        sheet.NoteHealth(300f);
        sheet.NoteHealth(float.NaN);
        sheet.NoteHealth(200f);
        Assert.AreEqual(300f, sheet.MaxHealth);
    }

    [TestMethod]
    public void Line_NamesEveryFigure()
    {
        var ledger = new HeroCombatLedger<Body>();
        var gimli = new Body();
        var sheet = ledger.Add(gimli, Gimli, 10f);
        sheet.NoteHealth(300f);
        ledger.Kill(gimli);
        ledger.Kill(gimli);
        ledger.Dealt(gimli, 120);
        ledger.Taken(gimli, 45);
        ledger.Fell(gimli, 70f, "uruk", "missile", downed: true);
        Assert.AreEqual(
            "hero=taom_hero_gimli body=1 state=fell alive_s=60 kills=2 dealt=120 hits=1 taken=45 hit_by=1 max_health=300 health=0 killer=uruk blow=missile played=0",
            sheet.Line(200f, 0f));
    }

    [TestMethod]
    public void Line_ForALiveBody_SaysAlive()
    {
        var ledger = new HeroCombatLedger<Body>();
        var sheet = ledger.Add(new Body(), Legolas, 0f);
        sheet.NoteHealth(150f);
        StringAssert.StartsWith(sheet.Line(90f, 140.4f), "hero=taom_hero_legolas body=1 state=alive alive_s=90 ");
        StringAssert.Contains(sheet.Line(90f, 140.4f), " health=140 killer=none blow=none played=0");
    }

    [TestMethod]
    public void Summary_AddsTheBodiesOfEachHero_WithRatesPerMinute()
    {
        var ledger = new HeroCombatLedger<Body>();
        var first = new Body();
        var second = new Body();
        ledger.Add(first, Gimli, 0f);
        ledger.Kill(first);
        ledger.Kill(first);
        ledger.Dealt(first, 300);
        ledger.Taken(first, 100);
        ledger.Fell(first, 60f, "uruk", "melee", downed: true);
        ledger.Add(second, Gimli, 100f);
        ledger.Kill(second);
        ledger.Dealt(second, 150);

        var lines = ledger.Summary(160f).ToList();
        Assert.AreEqual(1, lines.Count);
        Assert.AreEqual(
            "hero=taom_hero_gimli bodies=2 falls=1 alive_s=120 kills=3 dealt=450 taken=100 kills_per_min=1.5 dealt_per_min=225 taken_per_min=50",
            lines[0]);
    }

    [TestMethod]
    public void Fled_IsNotAFall_InTheLineOrTheSummary()
    {
        var ledger = new HeroCombatLedger<Body>();
        var aragorn = new Body();
        ledger.Add(aragorn, "taom_hero_aragorn", 0f);
        var sheet = ledger.Fell(aragorn, 60f, "none", "none", downed: false)!;
        StringAssert.Contains(sheet.Line(60f, 80f), "state=fled");
        StringAssert.Contains(sheet.Line(60f, 80f), "health=80");
        StringAssert.Contains(ledger.Summary(60f).Single(), "bodies=1 falls=0 ");
    }

    [TestMethod]
    public void Dealt_AfterTheFall_StillCounts_LikeTheKill()
    {
        var ledger = new HeroCombatLedger<Body>();
        var legolas = new Body();
        ledger.Add(legolas, Legolas, 0f);
        ledger.Fell(legolas, 10f, "uruk", "melee", downed: true);
        ledger.Dealt(legolas, 40);
        ledger.Kill(legolas);
        var sheet = ledger.Of(legolas)!;
        Assert.AreEqual(40, sheet.Dealt);
        Assert.AreEqual(1, sheet.Kills);
    }

    [TestMethod]
    public void Played_IsNamedOnTheLine()
    {
        var ledger = new HeroCombatLedger<Body>();
        var sheet = ledger.Add(new Body(), Gimli, 0f);
        sheet.Played = true;
        StringAssert.EndsWith(sheet.Line(5f, 10f), " played=1");
    }

    [TestMethod]
    public void Sample_ReadsOnlyStandingBodies_AndMarksThePlayedOne()
    {
        var ledger = new HeroCombatLedger<Body>();
        var gimli = new Body();
        var legolas = new Body();
        ledger.Add(gimli, Gimli, 0f);
        ledger.Add(legolas, Legolas, 0f);
        ledger.Fell(legolas, 5f, "uruk", "melee", downed: true);
        ledger.Sample(b => 300f, gimli);
        Assert.AreEqual(300f, ledger.Of(gimli)!.MaxHealth);
        Assert.IsTrue(ledger.Of(gimli)!.Played);
        Assert.AreEqual(0f, ledger.Of(legolas)!.MaxHealth);
    }

    [TestMethod]
    public void EndLines_GiveTheStandingBodies_ThenTheSummaries()
    {
        var ledger = new HeroCombatLedger<Body>();
        var gimli = new Body();
        var legolas = new Body();
        ledger.Add(gimli, Gimli, 0f);
        ledger.Add(legolas, Legolas, 0f);
        ledger.Fell(legolas, 5f, "uruk", "melee", downed: true);
        var lines = ledger.EndLines(60f, b => 77f).ToList();
        Assert.AreEqual(3, lines.Count);
        StringAssert.StartsWith(lines[0], "hero=taom_hero_gimli body=1 state=alive");
        StringAssert.Contains(lines[0], "health=77");
        StringAssert.StartsWith(lines[1], "summary hero=taom_hero_gimli ");
        StringAssert.StartsWith(lines[2], "summary hero=taom_hero_legolas ");
    }

    [TestMethod]
    public void Summary_OfABodyWithNoTimeAlive_GivesZeroRates()
    {
        var ledger = new HeroCombatLedger<Body>();
        ledger.Add(new Body(), Legolas, 5f);
        StringAssert.Contains(ledger.Summary(5f).Single(), "kills_per_min=0.0 dealt_per_min=0 taken_per_min=0");
    }
}

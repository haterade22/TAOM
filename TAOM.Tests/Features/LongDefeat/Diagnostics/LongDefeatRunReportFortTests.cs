using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Diagnostics;

/// <summary>
/// The fortification fields of the report (phase3-plan.md section 7). Schema stays 2 and every field is additive (D26): a run with no fortification still writes each at its empty value, so a reader finds the same shape.
/// </summary>
[TestClass]
public class LongDefeatRunReportFortTests
{
    private static readonly DateTime Started = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private static JObject Report(RunStats stats, Purse purse, RunOptions? options = null, RunFort? fort = null) =>
        JObject.Parse(LongDefeatRunReport.Build("helms", options ?? RunOptions.Default, RunProvenance.Unknown, new RunResult(RunEndReason.TargetReached, 1, 1, 3), stats, purse,
            Started, Started.AddMinutes(40), 1, null, fort));

    private static RunStats OneWave(FortWave? fort = null)
    {
        var stats = new RunStats();
        stats.CloseWave(1, 120f, 0, 1f, 1f, null, null, fort);
        return stats;
    }

    [TestMethod]
    public void Build_ARunWithNoFortification_WritesEveryFieldAtItsEmptyValue_InSchemaTwo()
    {
        var report = Report(OneWave(), new Purse(600));

        Assert.AreEqual(2, (int)report["schema"]!);
        Assert.AreEqual(JTokenType.Null, report["startGold"]!.Type);
        Assert.AreEqual(0, ((JArray)report["purchases"]!).Count);
        var levels = (JObject)report["fortifications"]!;
        Assert.AreEqual((0, 0, 0), ((int)levels["gateLevel"]!, (int)levels["towerLevel"]!, (int)levels["volleyLevel"]!));
        var spentOn = (JObject)((JObject)report["gold"]!)["spentOn"]!;
        CollectionAssert.AreEquivalent(new[] { "packs", "gate", "towers", "fieldCare", "volley" }, spentOn.Properties().Select(p => p.Name).ToArray());
        Assert.IsTrue(spentOn.Properties().All(p => (int)p.Value == 0));
        var wave = (JObject)report["waves"]!.Single();
        var gate = (JObject)wave["gate"]!;
        Assert.AreEqual((0, 0, 0, false), ((int)gate["level"]!, (int)gate["startHitPoints"]!, (int)gate["maxHitPoints"]!, (bool)gate["fell"]!));
        Assert.AreEqual(JTokenType.Null, gate["fellAtSeconds"]!.Type);
        var posts = (JObject)wave["posts"]!;
        Assert.AreEqual((0, 0, 0, 0), ((int)posts["filled"]!, (int)posts["short"]!, (int)posts["manned"]!, (int)posts["lost"]!));
        foreach (var key in new[] { "volley", "towerVolley" })
        {
            var volley = (JObject)wave[key]!;
            Assert.AreEqual((0, 0, 0, 0, 0), ((int)volley["uses"]!, (int)volley["arrows"]!, (int)volley["hits"]!, (int)volley["kills"]!, (int)volley["withheld"]!), key);
        }
    }

    [TestMethod]
    public void Build_TheStartGoldIsEchoed_AndANullOneStaysNull()
    {
        Assert.AreEqual(5000, (int)Report(OneWave(), new Purse(5000), new RunOptions { StartGold = 5000 })["startGold"]!);
        Assert.AreEqual(0, (int)Report(OneWave(), new Purse(0), new RunOptions { StartGold = 0 })["startGold"]!, "0 is a start gold, not none");
        Assert.AreEqual(JTokenType.Null, Report(OneWave(), new Purse(600))["startGold"]!.Type);
    }

    [TestMethod]
    public void Build_SpentOnSplitsTheSpentGoldBySink_AndTheSinksSumToSpent()
    {
        var purse = new Purse(5000);
        purse.TrySpend(180, PurseSink.Packs);
        purse.TrySpend(200, PurseSink.Gate);
        purse.TrySpend(74, PurseSink.Gate);
        purse.TrySpend(250, PurseSink.Towers);
        purse.TrySpend(150, PurseSink.FieldCare);
        purse.TrySpend(350, PurseSink.Volley);

        var gold = (JObject)Report(OneWave(), purse)["gold"]!;
        var spentOn = (JObject)gold["spentOn"]!;

        Assert.AreEqual((180, 274, 250, 150, 350), ((int)spentOn["packs"]!, (int)spentOn["gate"]!, (int)spentOn["towers"]!, (int)spentOn["fieldCare"]!, (int)spentOn["volley"]!));
        Assert.AreEqual((int)gold["spent"]!, spentOn.Properties().Sum(p => (int)p.Value));
    }

    [TestMethod]
    public void Build_ListsEveryPurchaseInOrder_WithItsBasePriceItsPriceAndItsShare()
    {
        var fort = new RunFort(new[]
        {
            new FortPurchase(1, PurchaseKind.Pack, "militia", 0, 180, 144, 0f),
            new FortPurchase(1, PurchaseKind.VolleyLevel, "", 1, 350, 280, 0f),
            new FortPurchase(2, PurchaseKind.GateRepair, "", 0, 200, 60, 0.3712f),
            new FortPurchase(2, PurchaseKind.GateLevel, "", 1, 200, 160, 0f),
            new FortPurchase(2, PurchaseKind.TowerLevel, "", 1, 250, 200, 0f),
            new FortPurchase(2, PurchaseKind.FieldCare, "", 0, 150, 120, 0.75f),
        }, 1, 1, 1);

        var report = Report(OneWave(), new Purse(5000), fort: fort);

        var rows = ((JArray)report["purchases"]!).Cast<JObject>().ToList();
        CollectionAssert.AreEqual(new[] { "pack", "volleyLevel", "gateRepair", "gateLevel", "towerLevel", "fieldCare" }, rows.Select(r => (string)r["kind"]!).ToArray());
        Assert.AreEqual((1, "militia", 0, 180, 144), ((int)rows[0]["beforeWave"]!, (string)rows[0]["id"]!, (int)rows[0]["level"]!, (int)rows[0]["basePrice"]!, (int)rows[0]["price"]!));
        Assert.AreEqual((1, 350, 280), ((int)rows[1]["level"]!, (int)rows[1]["basePrice"]!, (int)rows[1]["price"]!));
        Assert.AreEqual((2, 200, 60), ((int)rows[2]["beforeWave"]!, (int)rows[2]["basePrice"]!, (int)rows[2]["price"]!));
        Assert.AreEqual(0.371, (double)rows[2]["share"]!, 1e-9, "a share is written to three decimals");
        Assert.AreEqual(0.75, (double)rows[5]["share"]!, 1e-9);
        Assert.AreEqual("", (string)rows[1]["id"]!, "only a pack names an offer");
        var levels = (JObject)report["fortifications"]!;
        Assert.AreEqual((1, 1, 1), ((int)levels["gateLevel"]!, (int)levels["towerLevel"]!, (int)levels["volleyLevel"]!));
    }

    [TestMethod]
    public void Build_AGateRepair_WritesEachGatesShare_AndOnlyARepairDoes()
    {
        var fort = new RunFort(new[]
        {
            new FortPurchase(2, PurchaseKind.GateRepair, "", 0, 100, 76, 0.742f, new[] { 0.371f, 0.371f }),
            new FortPurchase(2, PurchaseKind.FieldCare, "", 0, 150, 150, 0.75f),
        }, 0, 0, 0);

        var rows = ((JArray)Report(OneWave(), new Purse(5000), fort: fort)["purchases"]!).Cast<JObject>().ToList();

        CollectionAssert.AreEqual(new[] { 0.371, 0.371 }, rows[0]["shares"]!.Select(t => (double)t).ToArray());
        Assert.AreEqual(0.742, (double)rows[0]["share"]!, 1e-9, "share stays the total over the gates");
        Assert.IsNull(rows[1]["shares"], "care has one share, written in share");
    }

    [TestMethod]
    public void Build_AGateRepairWithNoShareList_WritesItsShareAsTheOnlyGate()
    {
        var fort = new RunFort(new[] { new FortPurchase(2, PurchaseKind.GateRepair, "", 0, 200, 200, 1f) }, 0, 0, 0);

        var row = (JObject)((JArray)Report(OneWave(), new Purse(5000), fort: fort)["purchases"]!).Single();

        CollectionAssert.AreEqual(new[] { 1.0 }, row["shares"]!.Select(t => (double)t).ToArray());
    }

    [TestMethod]
    public void Build_EveryPurchaseKind_HasAName_AndNoTwoShareOne()
    {
        var kinds = Enum.GetValues(typeof(PurchaseKind)).Cast<PurchaseKind>().ToList();
        var fort = new RunFort(kinds.Select(k => new FortPurchase(1, k, "", 0, 100, 100, 0f)).ToList(), 0, 0, 0);

        var report = Report(OneWave(), new Purse(5000), fort: fort);

        var names = ((JArray)report["purchases"]!).Select(r => (string)r["kind"]!).ToList();
        Assert.AreEqual(kinds.Count, names.Count);
        Assert.IsTrue(names.All(n => !string.IsNullOrEmpty(n)));
        Assert.AreEqual(kinds.Count, names.Distinct().Count());
    }

    [TestMethod]
    public void Build_AWavesGateRow_CarriesWhatTheRunSawOfIt()
    {
        var stats = OneWave(new FortWave(new GateWaveRecord(2, 22500f, 30000f, true, 312.5f), new PostsWaveRecord(8, 2, 6, 1), new VolleyWaveRecord(3, 36, 21, 12, 1), new VolleyWaveRecord(2, 12, 7, 3, 0)));

        var wave = (JObject)Report(stats, new Purse(600))["waves"]!.Single();

        var gate = (JObject)wave["gate"]!;
        Assert.AreEqual((2, 22500, 30000, true), ((int)gate["level"]!, (int)gate["startHitPoints"]!, (int)gate["maxHitPoints"]!, (bool)gate["fell"]!));
        Assert.AreEqual(312.5, (double)gate["fellAtSeconds"]!, 1e-6);
        var posts = (JObject)wave["posts"]!;
        Assert.AreEqual((8, 2, 6, 1), ((int)posts["filled"]!, (int)posts["short"]!, (int)posts["manned"]!, (int)posts["lost"]!));
        var volley = (JObject)wave["volley"]!;
        Assert.AreEqual((3, 36, 21, 12, 1), ((int)volley["uses"]!, (int)volley["arrows"]!, (int)volley["hits"]!, (int)volley["kills"]!, (int)volley["withheld"]!));
        var tower = (JObject)wave["towerVolley"]!;
        Assert.AreEqual((2, 12, 7, 3, 0), ((int)tower["uses"]!, (int)tower["arrows"]!, (int)tower["hits"]!, (int)tower["kills"]!, (int)tower["withheld"]!));
    }

    [TestMethod]
    public void Build_NoExistingFieldChangesItsName()
    {
        var report = Report(OneWave(), new Purse(600));

        foreach (var key in new[] { "schema", "fortress", "build", "balance", "seed", "class", "startWave", "startRerolls", "boons", "rerolls", "kills", "waves", "gold", "poison", "wavesFelled" })
            Assert.IsNotNull(report[key], key);
        var wave = (JObject)report["waves"]!.Single();
        foreach (var key in new[] { "wave", "kills", "felled", "lost", "seconds", "affixes", "healthFactor", "damageFactor", "summoned", "commander" })
            Assert.IsNotNull(wave[key], key);
    }
}

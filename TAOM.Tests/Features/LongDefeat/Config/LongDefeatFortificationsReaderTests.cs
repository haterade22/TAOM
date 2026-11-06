using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The <c>fortifications</c> block of shop.json (phase3-plan.md section 5). The cases live in tools/tests/long_defeat_fort_cases.json, written by hand from the plan's rules table, and the Python mirror
/// (<c>shipped_fortifications</c>) reads the same file, so the two loaders cannot drift apart while both suites pass.
/// </summary>
[TestClass]
public class LongDefeatFortificationsReaderTests
{
    private static JObject Cases() => JObject.Parse(File.ReadAllText(RepoPaths.RepoPath("tools", "tests", "long_defeat_fort_cases.json")));

    private static JArray Rows(params float[] numbers) => new(numbers.Select(n => (object)n));

    private static JObject Normalized(FortPlan plan, int warnings)
    {
        var volley = plan.Towers?.Volley;
        return new JObject
        {
            ["gate"] = plan.Gate == null ? JValue.CreateNull() : new JObject
            {
                ["repairPrice"] = plan.Gate.RepairPrice,
                ["levels"] = new JArray(plan.Gate.Levels.Select(l => new JArray(l.HitPointsPercent, l.Price))),
            },
            ["towers"] = plan.Towers == null ? JValue.CreateNull() : new JObject
            {
                ["troop"] = plan.Towers.Troop,
                ["levels"] = new JArray(plan.Towers.Levels.Select(l => new JArray(l.Posts, l.Volley, l.Price))),
                ["volley"] = volley == null ? JValue.CreateNull() : new JObject
                {
                    ["item"] = volley.ItemId, ["arrows"] = volley.Arrows, ["firstSeconds"] = volley.FirstSeconds, ["cooldownSeconds"] = volley.CooldownSeconds,
                    ["radius"] = volley.Radius, ["range"] = volley.Range, ["originHeight"] = volley.OriginHeight,
                },
            },
            ["care"] = plan.Care == null ? JValue.CreateNull() : new JObject { ["share"] = plan.Care.Share, ["price"] = plan.Care.Price, ["perIntermission"] = plan.Care.PerIntermission },
            ["volley"] = plan.Volley == null ? JValue.CreateNull() : new JObject
            {
                ["item"] = plan.Volley.ItemId,
                ["originHeight"] = plan.Volley.OriginHeight,
                ["levels"] = new JArray(plan.Volley.Levels.Select(l => new JArray(l.Price, l.Arrows, l.CooldownSeconds, l.Radius, l.Range, l.Aim == VolleyAimMode.Auto ? "auto" : "crosshair"))),
            },
            ["warnings"] = warnings,
        };
    }

    /// <summary>Equal as JSON, numbers within 1e-6 (the chosen case values are exact in 32-bit float).</summary>
    private static bool Same(JToken expected, JToken actual)
    {
        if (expected.Type is JTokenType.Integer or JTokenType.Float && actual.Type is JTokenType.Integer or JTokenType.Float)
            return Math.Abs(expected.Value<double>() - actual.Value<double>()) < 1e-6;
        if (expected is JObject eo && actual is JObject ao)
            return eo.Count == ao.Count && eo.Properties().All(p => ao.TryGetValue(p.Name, out var other) && Same(p.Value, other));
        if (expected is JArray ea && actual is JArray aa)
            return ea.Count == aa.Count && ea.Zip(aa, Same).All(x => x);
        return JToken.DeepEquals(expected, actual);
    }

    [TestMethod]
    public void EverySharedCase_ReadsAsTheCaseFileSays()
    {
        var failures = new System.Collections.Generic.List<string>();
        var cases = (JArray)Cases()["cases"]!;
        Assert.IsTrue(cases.Count >= 40, "the case file is the parity contract with the Python loader");
        foreach (var c in cases.Cast<JObject>())
        {
            var logger = Substitute.For<IModLogger>();
            var scope = new LongDefeatConfigScope(logger, "shop.json");
            var shop = new JObject();
            if (!(bool)c["absent"]!)
                shop["fortifications"] = c["block"]!.DeepClone();

            var plan = FortificationsReader.Read(shop, "shops.test", scope);
            var warnings = logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IModLogger.LogWarning));
            var expected = c["expected"]!;
            var ok = expected.Type == JTokenType.Null ? plan == null && warnings == 0 : plan != null && Same(expected, Normalized(plan, warnings));
            if (!ok)
                failures.Add($"{(string)c["name"]!}: expected {expected.ToString(Newtonsoft.Json.Formatting.None)}, got {(plan == null ? "null" : Normalized(plan, warnings).ToString(Newtonsoft.Json.Formatting.None))}");
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void TheShopReader_HandsTheBlockToThisReader_AndAShopWithoutOneHasNoFortPlan()
    {
        var logger = Substitute.For<IModLogger>();
        var shop = "{'shops':{'rohan':{'startingGold':600,'offers':[{'id':'m','troop':'t','class':'Infantry','packSize':12,'price':180}]," +
                   "'fortifications':{'fieldCare':{'share':0.5,'price':100}}},'plain':{'startingGold':1,'offers':[]}}}";

        var shops = ShopReader.Read(shop, new LongDefeatConfigScope(logger, "shop.json"));

        Assert.AreEqual(0.5f, shops["rohan"].Fort!.Care!.Share);
        Assert.IsNull(shops["rohan"].Fort!.Gate);
        Assert.IsNull(shops["plain"].Fort, "no key, no plan, and no warning for it");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    private IModLogger LoadWithTowers(string towers)
    {
        var logger = Substitute.For<IModLogger>();
        const string mix = "{'troop':'a_inf','class':'Infantry','weight':3}";
        var waves = "{'rules':{},'bounties':{'a_inf':4},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300},'waves':[{'count':110,'mix':[" + mix + "]}]}}}";
        var shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','troop':'d_inf','class':'Infantry','packSize':12,'price':180}]," +
                   "'fortifications':{'towers':{'troop':'d_inf','levels':[{'posts':4,'price':250},{'posts':8,'price':300}]}}}}}";
        var fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard','playerTroop':'lord_4_1','waves':'r','shop':'sh'," +
                       "'garrison':[{'troop':'d_inf','class':'Infantry','count':100}],'keep':null,'towers':" + towers + "}";
        var affixes = "{'names':{'isengard':{'lash':'{=A1}L','drums':'{=A2}D','blades':'{=A3}B','eye':'{=A4}E','discipline':'{=A5}I','hide':'{=A6}H','terror':'{=A7}T','hammer':'{=A8}M'}}}";
        LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + fortress + "]}", waves, shop, logger, affixes, "{'boons':[]}", "{'classes':[]}").Fortresses.Single();
        return logger;
    }

    private static string Spots(int n) => string.Join(",", Enumerable.Range(0, n).Select(i => "{'x':" + i + ",'y':2,'z':3,'fx':0,'fy':1}"));

    [TestMethod]
    public void ATowerWithFewerRecordedPostsThanItsTopLevel_FillsWhatItHas_WithOneWarning()
    {
        var logger = LoadWithTowers("[{'id':'hornburg','posts':[" + Spots(5) + "]}]");

        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("hornburg") && m.Contains("5") && m.Contains("8")));
    }

    [TestMethod]
    public void ATowerWithAllThePostsItsTopLevelAsks_CostsNoWarning()
    {
        LoadWithTowers("[{'id':'hornburg','posts':[" + Spots(8) + "]}]").DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void AWarning_NamesTheFileAndTheKey()
    {
        var logger = Substitute.For<IModLogger>();
        var shop = new JObject { ["fortifications"] = JObject.Parse("{'gate':{'repairPrice':0,'levels':[]}}") };

        FortificationsReader.Read(shop, "shops.rohan", new LongDefeatConfigScope(logger, "shop.json"));

        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shop.json") && m.Contains("shops.rohan.fortifications.gate.repairPrice")));
    }
}

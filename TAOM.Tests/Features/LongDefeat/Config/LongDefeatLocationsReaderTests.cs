using System;
using System.Collections.Generic;
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
/// The <c>armoury</c>, <c>yard</c> and <c>marshal</c> blocks of a shop in shop.json (phase4-plan.md section 5). The cases live in tools/tests/long_defeat_location_cases.json, written by hand from the
/// plan's rules table, and the Python mirror (<c>read_locations</c>) reads the same file, so the two loaders cannot drift apart while both suites pass.
/// </summary>
[TestClass]
public class LongDefeatLocationsReaderTests
{
    private static JObject CaseFile() => JObject.Parse(File.ReadAllText(RepoPaths.RepoPath("tools", "tests", "long_defeat_location_cases.json")));

    private static JObject Normalized(LocationsPlan plan, int warnings) => new()
    {
        ["armoury"] = plan.Armoury == null ? JValue.CreateNull() : new JObject
        {
            ["reach"] = plan.Armoury.Reach,
            ["sets"] = new JArray(plan.Armoury.Sets.Select(set => new JObject
            {
                ["id"] = set.Id,
                ["name"] = set.Name,
                ["text"] = set.Text,
                ["kind"] = set.Kind == GearKind.Armour ? "armour" : "weapons",
                ["classes"] = set.Classes == null ? JValue.CreateNull() : new JArray(set.Classes),
                ["price"] = set.Price,
                ["slots"] = new JArray(set.Slots.Select(x => new JArray(x.Slot, x.ItemId))),
            })),
        },
        ["yard"] = plan.Yard == null ? JValue.CreateNull() : new JObject
        {
            ["reach"] = plan.Yard.Reach,
            ["engines"] = new JArray(plan.Yard.Engines.Select(e => new JArray(e.Kind, e.Price))),
        },
        ["marshal"] = plan.Marshal == null ? JValue.CreateNull() : new JObject
        {
            ["troop"] = plan.Marshal.Troop, ["price"] = plan.Marshal.Price, ["name"] = plan.Marshal.Name, ["text"] = plan.Marshal.Text,
        },
        ["warnings"] = warnings,
    };

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

    private static int Warnings(IModLogger logger) => logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IModLogger.LogWarning));

    [TestMethod]
    public void EverySharedCase_ReadsAsTheCaseFileSays()
    {
        var file = CaseFile();
        var classIds = file["classIds"]!.Select(t => (string)t!).ToList();
        var cases = (JArray)file["cases"]!;
        Assert.IsTrue(cases.Count >= 80, "the case file is the parity contract with the Python loader");
        var failures = new List<string>();
        foreach (var c in cases.Cast<JObject>())
        {
            var logger = Substitute.For<IModLogger>();
            var plan = LocationsReader.Read((JObject)c["shop"]!.DeepClone(), "shops.test", new LongDefeatConfigScope(logger, "shop.json"), () => classIds);
            var warnings = Warnings(logger);
            var expected = c["expected"]!;
            var ok = expected.Type == JTokenType.Null ? plan == null && warnings == 0 : plan != null && Same(expected, Normalized(plan, warnings));
            if (!ok)
                failures.Add($"{(string)c["name"]!}: expected {expected.ToString(Newtonsoft.Json.Formatting.None)}, got {(plan == null ? "null" : Normalized(plan, warnings).ToString(Newtonsoft.Json.Formatting.None))}");
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void TheCaseFile_CoversEveryRuleRowOfSection5()
    {
        var names = ((JArray)CaseFile()["cases"]!).Select(c => (string)c["name"]!).ToList();

        foreach (var word in new[] { "reach", "id", "kind", "slots", "price", "classes", "engine", "marshal", "horse", "ballista" })
            Assert.IsTrue(names.Any(n => n.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0), "no case names '" + word + "'");
    }

    [TestMethod]
    public void AWarning_NamesTheFileAndTheKey()
    {
        var logger = Substitute.For<IModLogger>();
        var shop = JObject.Parse("{'armoury':{'reach':99,'sets':[{'id':'a','kind':'armour','price':5,'slots':{'Head':'h'}}]}}");

        LocationsReader.Read(shop, "shops.rohan", new LongDefeatConfigScope(logger, "shop.json"), null);

        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shop.json") && m.Contains("shops.rohan.armoury.reach")));
    }

    [TestMethod]
    public void ABadSet_NamesItsPlaceInTheList()
    {
        var logger = Substitute.For<IModLogger>();
        var shop = JObject.Parse("{'armoury':{'sets':[{'id':'a','kind':'armour','price':5,'slots':{'Head':'h'}},{'id':'b','kind':'armour','price':0,'slots':{'Head':'h'}}]}}");

        LocationsReader.Read(shop, "shops.rohan", new LongDefeatConfigScope(logger, "shop.json"), null);

        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shops.rohan.armoury.sets[1].price") && m.Contains("'b'")));
    }

    [TestMethod]
    public void WithNoClassIdsToCheckAgainst_AListedClassIsTakenAsItIs()
    {
        var logger = Substitute.For<IModLogger>();
        var shop = JObject.Parse("{'armoury':{'sets':[{'id':'w','kind':'weapons','classes':['anything'],'price':5,'slots':{'Item0':'bow'}}]}}");

        var plan = LocationsReader.Read(shop, "shops.test", new LongDefeatConfigScope(logger, "shop.json"), null)!;

        CollectionAssert.AreEqual(new[] { "anything" }, plan.Armoury!.Sets.Single().Classes!.ToArray());
        Assert.AreEqual(0, Warnings(logger));
    }

    [TestMethod]
    public void TheClassIds_AreAskedForOnlyWhenASetListsAClass_AndOnlyOnce()
    {
        var asked = 0;
        Func<System.Collections.Generic.IReadOnlyCollection<string>> ids = () =>
        {
            asked++;
            return new[] { "archer" };
        };
        var plain = JObject.Parse("{'armoury':{'sets':[{'id':'a','kind':'armour','price':5,'slots':{'Head':'h'}}]},'yard':{'engines':[{'kind':'mangonel','price':5}]}}");
        var listing = JObject.Parse("{'armoury':{'sets':[{'id':'a','kind':'weapons','classes':['archer'],'price':5,'slots':{'Item0':'b'}},{'id':'b','kind':'weapons','classes':['archer'],'price':5,'slots':{'Item0':'c'}}]}}");

        LocationsReader.Read(plain, "shops.t", new LongDefeatConfigScope(Substitute.For<IModLogger>(), "shop.json"), ids);
        Assert.AreEqual(0, asked, "classes.json is not read early for a shop that lists no class");

        LocationsReader.Read(listing, "shops.t", new LongDefeatConfigScope(Substitute.For<IModLogger>(), "shop.json"), ids);
        Assert.AreEqual(1, asked);
    }

    [TestMethod]
    public void AShopWithNoListedClass_LoadsWithoutClassesJson_AndWithoutItsWarning()
    {
        var logger = Substitute.For<IModLogger>();
        const string mix = "{'troop':'a_inf','class':'Infantry','weight':3}";
        var waves = "{'rules':{},'bounties':{'a_inf':4},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300},'waves':[{'count':110,'mix':[" + mix + "]}]}}}";
        var shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','troop':'d_inf','class':'Infantry','packSize':12,'price':180}],"
                   + "'armoury':{'sets':[{'id':'a','kind':'armour','price':5,'slots':{'Head':'h'}}]}}}}";
        var fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard','playerTroop':'lord_4_1','waves':'r','shop':'sh',"
                       + "'garrison':[{'troop':'d_inf','class':'Infantry','count':100}],'keep':null}";

        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + fortress + "]}", waves, shop, logger);

        Assert.AreEqual("a", catalog.Fortresses.Single().Shop.Locations!.Armoury!.Sets.Single().Id);
        logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("classes.json")));
    }

    [TestMethod]
    public void TheShopReader_HandsTheBlocksToThisReader_AndAShopWithoutThemHasNoLocationsPlan()
    {
        var logger = Substitute.For<IModLogger>();
        var shop = "{'shops':{'rohan':{'startingGold':600,'offers':[{'id':'m','troop':'t','class':'Infantry','packSize':12,'price':180}],"
                   + "'yard':{'engines':[{'kind':'mangonel','price':450}]},'marshal':{'troop':'lord_4_7','price':500}},'plain':{'startingGold':1,'offers':[]}}}";

        var shops = ShopReader.Read(shop, new LongDefeatConfigScope(logger, "shop.json"));

        Assert.AreEqual(450, shops["rohan"].Locations!.Yard!.Engines.Single().Price);
        Assert.AreEqual("lord_4_7", shops["rohan"].Locations!.Marshal!.Troop);
        Assert.IsNull(shops["rohan"].Locations!.Armoury);
        Assert.IsNull(shops["plain"].Locations, "no key, no plan, and no warning for it");
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TheCatalog_ChecksAListedClassAgainstClassesJson()
    {
        var logger = Substitute.For<IModLogger>();
        const string mix = "{'troop':'a_inf','class':'Infantry','weight':3}";
        var waves = "{'rules':{},'bounties':{'a_inf':4},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300},'waves':[{'count':110,'mix':[" + mix + "]}]}}}";
        var shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','troop':'d_inf','class':'Infantry','packSize':12,'price':180}],"
                   + "'armoury':{'sets':[{'id':'w','kind':'weapons','classes':['archer','wizard'],'price':5,'slots':{'Item0':'bow'}}]}}}}";
        var fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard','playerTroop':'lord_4_1','waves':'r','shop':'sh',"
                       + "'garrison':[{'troop':'d_inf','class':'Infantry','count':100}],'keep':null}";
        var classes = "{'classes':[{'id':'archer','name':'{=A}Archer','text':'{=B}Bow','kits':{'vlandia':{'Item0':'bow'}}}]}";

        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + fortress + "]}", waves, shop, logger, null, "{'boons':[]}", classes);

        CollectionAssert.AreEqual(new[] { "archer" }, catalog.Fortresses.Single().Shop.Locations!.Armoury!.Sets.Single().Classes!.ToArray());
        logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shop.json") && m.Contains("wizard")));
    }
}

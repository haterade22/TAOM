using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The three long_defeat JSON files (phase1-plan.md section 3): each bad value costs one warning naming the file and the key, a
/// bad row or wave is skipped, a bad number keeps the default, and a file that is missing or not JSON loads nothing without
/// throwing. Single quotes keep the fixtures readable; Newtonsoft accepts them, and also the literals NaN and Infinity.
/// </summary>
[TestClass]
public class LongDefeatConfigCatalogTests
{
    private const string Mix = "{'troop':'a_inf','class':'Infantry','weight':3},{'troop':'a_arc','class':'Ranged','weight':1}";
    private const string Wave = "{'count':110,'mix':[" + Mix + "]}";
    private const string Garrison = "{'troop':'d_inf','class':'Infantry','count':100},{'troop':'d_arc','class':'Ranged','count':50}";
    private const string Offer = "{'id':'militia','name':'Militia','troop':'d_inf','class':'Infantry','packSize':12,'price':180}";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static string Fortress(string garrison = Garrison, string replaceKey = "", string replaceWith = "", string keep = "null", string muster = "")
    {
        var text = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard'," +
                   "'playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[" + garrison + "],'keep':" + keep + (muster.Length == 0 ? "" : ",'muster':" + muster) + "}";
        return replaceKey.Length == 0 ? text : text.Replace(replaceKey, replaceWith);
    }

    private static string Fortresses(params string[] entries) => "{'fortresses':[" + string.Join(",", entries) + "]}";

    private static string Waves(string rules = "", string waves = Wave, string scaling = "'countPerWave':10,'maxCount':300",
        string bounties = "'a_inf':4,'a_arc':6") =>
        "{'rules':{" + rules + "},'bounties':{" + bounties + "},'rosters':{'r':{'scaling':{" + scaling + "},'waves':[" + waves + "]}}}";

    private static string Shop(string offers = Offer, string gold = "600") =>
        "{'shops':{'sh':{'startingGold':" + gold + ",'offers':[" + offers + "]}}}";

    private LongDefeatConfigCatalog Load(string? fortresses = null, string? waves = null, string? shop = null) =>
        LongDefeatConfigCatalog.FromTexts(fortresses ?? Fortresses(Fortress()), waves ?? Waves(), shop ?? Shop(), _logger);

    private void AssertOneWarningNaming(string file, string key) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(file) && m.Contains(key)));

    [TestMethod]
    public void ValidFiles_LoadTheFortressWithItsPlanAndLogNoWarning()
    {
        var catalog = Load();

        var fortress = catalog.Fortresses.Single();
        Assert.AreEqual("helms", fortress.Id);
        Assert.AreEqual("{=K}Helm", fortress.Name);
        Assert.AreEqual("s", fortress.Scene);
        Assert.AreEqual("vlandia", fortress.DefenderCulture);
        Assert.AreEqual("isengard", fortress.AttackerCulture);
        Assert.AreEqual("lord_4_1", fortress.PlayerTroop);
        Assert.AreEqual(2, fortress.Garrison.Count);
        Assert.AreEqual(new GarrisonRow("d_arc", FormationSlot.Ranged, 50), fortress.Garrison[1]);
        Assert.IsNull(fortress.Keep);
        Assert.AreEqual(110, fortress.Waves.ForWave(1).Count);
        Assert.AreEqual(600, fortress.Shop.StartingGold);
        Assert.AreEqual(12, fortress.Shop.Offers.Single().PackSize);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Fortress_ASceneNameNativeCodeCouldNotLoad_IsSkippedWithOneWarning()
    {
        foreach (var scene in new[] { "a b", "../x", new string('a', 129), "caf\u00e9" })
        {
            _logger = Substitute.For<IModLogger>();
            var catalog = Load(Fortresses(Fortress(replaceKey: "'scene':'s'", replaceWith: "'scene':'" + scene + "'")));

            Assert.AreEqual(0, catalog.Fortresses.Count, scene);
            AssertOneWarningNaming("fortresses.json", "scene");
        }

        _logger = Substitute.For<IModLogger>();
        Assert.AreEqual(1, Load(Fortresses(Fortress(replaceKey: "'scene':'s'", replaceWith: "'scene':'" + new string('a', 128) + "'"))).Fortresses.Count);
    }

    [TestMethod]
    public void AFileThatReverted_EndsWithOneSummaryWarning_AndACleanFileDoesNot()
    {
        Assert.AreEqual(1, Load(waves: Waves(rules: "'liveCap':'many'")).Fortresses.Count);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("waves.json") && m.Contains("loaded") && m.Contains("1 value(s)")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") || m.Contains("shop.json")));
    }

    [TestMethod]
    public void AFileWhoseOnlyFaultIsALowKeepFloor_StillEndsWithTheSummaryWarning()
    {
        Assert.AreEqual(1, Load(waves: Waves(rules: "'keepMinAttackers':1")).Fortresses.Count);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("keepMinAttackers")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("waves.json") && m.Contains("loaded") && m.Contains("1 value(s)")));
    }

    [TestMethod]
    public void BalanceHash_IgnoresLineEndings_AndFollowsAnyNumber()
    {
        var one = Waves();
        var hash = Load(waves: one).BalanceHash;

        Assert.AreEqual(16, hash.Length);
        Assert.AreEqual(hash, Load(waves: one.Replace("\n", "\r\n")).BalanceHash);
        Assert.AreEqual(hash, Load(waves: one).BuildPlan(Load(waves: one).Fortresses[0], RunOptions.Default).Provenance.Balance);
        Assert.AreNotEqual(hash, Load(waves: Waves(rules: "'liveCap':200")).BalanceHash);
        Assert.AreNotEqual(hash, Load(waves: one, shop: Shop().Replace("600", "601")).BalanceHash);
        Assert.AreNotEqual(hash, Load(Fortresses(Fortress(replaceKey: "'scene':'s'", replaceWith: "'scene':'t'")), one).BalanceHash);
    }

    [TestMethod]
    public void BuildPlan_CarriesRulesBountiesAndOptions()
    {
        var catalog = Load(waves: Waves(rules: "'liveCap':200"));
        var options = new RunOptions { StopAfterWaves = 2 };

        var plan = catalog.BuildPlan(catalog.Fortresses[0], options);

        Assert.AreEqual("helms", plan.FortressId);
        Assert.AreEqual(200, plan.Rules.LiveCap);
        Assert.AreEqual(4, plan.Bounties["a_inf"]);
        Assert.AreSame(options, plan.Options);
        Assert.AreEqual(150, plan.Garrison.Sum(g => g.Count));
    }

    [TestMethod]
    public void TryGetFortress_IsCaseInsensitive()
    {
        Assert.IsTrue(Load().TryGetFortress("HELMS", out var fortress));
        Assert.AreEqual("helms", fortress.Id);
        Assert.IsFalse(Load().TryGetFortress("rivendell", out _));
        Assert.IsFalse(Load().TryGetFortress("", out _));
    }

    [TestMethod]
    public void WaveScaling_ReadsBothNumbers()
    {
        var catalog = Load(waves: Waves(scaling: "'countPerWave':25,'maxCount':400"));

        Assert.AreEqual(135, catalog.Fortresses[0].Waves.ForWave(2).Count);
        Assert.AreEqual(400, catalog.Fortresses[0].Waves.ForWave(50).Count);
    }

    [DataTestMethod]
    [DataRow("'countPerWave':101,'maxCount':300", "countPerWave")]
    [DataRow("'countPerWave':NaN,'maxCount':300", "countPerWave")]
    [DataRow("'countPerWave':1.5,'maxCount':300", "countPerWave")]
    [DataRow("'countPerWave':10,'maxCount':100", "maxCount")]
    [DataRow("'countPerWave':10,'maxCount':1001", "maxCount")]
    public void WaveScaling_BadNumber_KeepsTheDefaultWithOneWarning(string scaling, string key)
    {
        var catalog = Load(waves: Waves(scaling: scaling));

        Assert.AreEqual(1, catalog.Fortresses.Count);
        var waves = catalog.Fortresses[0].Waves;
        if (key == "countPerWave")
            Assert.AreEqual(120, waves.ForWave(2).Count, "default countPerWave 10 on a 110 wave");
        else
            Assert.AreEqual(300, waves.ForWave(100).Count, "default maxCount 300");
        AssertOneWarningNaming("waves.json", key);
    }

    [TestMethod]
    public void Rules_EveryKey_ReadsItsValue()
    {
        var rules = "'preparationSeconds':91,'intermissionSeconds':61,'earlyGoldPerSecond':3.5,'earlyGoldCap':121,'waveBonusBase':151," +
                    "'waveBonusPerWave':51,'spawnsPerTick':9,'liveCap':160,'garrisonCap':230,'musterBatch':21,'musterBatchSeconds':5.5," +
                    "'quietSeconds':61,'quietAnySeconds':181,'strandedSeconds':91,'stragglerShare':0.2,'keepRadius':13.5," +
                    "'keepHeightBand':5.5,'keepHoldSeconds':46,'keepDecaySeconds':91,'keepMinAttackers':23,'keepDominance':2.5,'restShare':0.25,'stragglerCount':6,'stragglerSeconds':61";

        var r = Load(waves: Waves(rules)).BuildPlan(Load().Fortresses[0], RunOptions.Default).Rules;

        Assert.AreEqual(
            new RunRules
            {
                PreparationSeconds = 91f, IntermissionSeconds = 61f, EarlyGoldPerSecond = 3.5f, EarlyGoldCap = 121, WaveBonusBase = 151,
                WaveBonusPerWave = 51, SpawnsPerTick = 9, LiveCap = 160, GarrisonCap = 230, MusterBatch = 21, MusterBatchSeconds = 5.5f,
                QuietSeconds = 61f, QuietAnySeconds = 181f, StrandedSeconds = 91f, StragglerShare = 0.2f, KeepRadius = 13.5f,
                KeepHeightBand = 5.5f, KeepHoldSeconds = 46f, KeepDecaySeconds = 91f, KeepMinAttackers = 23, KeepDominance = 2.5f, RestShare = 0.25f, StragglerCount = 6, StragglerSeconds = 61f,
            },
            r);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [DataTestMethod]
    [DataRow("preparationSeconds", "0.5")]
    [DataRow("preparationSeconds", "3601")]
    [DataRow("preparationSeconds", "NaN")]
    [DataRow("preparationSeconds", "Infinity")]
    [DataRow("preparationSeconds", "'90'")]
    [DataRow("intermissionSeconds", "NaN")]
    [DataRow("musterBatchSeconds", "NaN")]
    [DataRow("quietSeconds", "0")]
    [DataRow("quietAnySeconds", "NaN")]
    [DataRow("strandedSeconds", "NaN")]
    [DataRow("keepHoldSeconds", "NaN")]
    [DataRow("keepDecaySeconds", "3601")]
    [DataRow("earlyGoldPerSecond", "-1")]
    [DataRow("earlyGoldPerSecond", "101")]
    [DataRow("earlyGoldPerSecond", "NaN")]
    [DataRow("earlyGoldCap", "-1")]
    [DataRow("earlyGoldCap", "1000001")]
    [DataRow("earlyGoldCap", "1.5")]
    [DataRow("waveBonusBase", "1000001")]
    [DataRow("waveBonusPerWave", "-5")]
    [DataRow("spawnsPerTick", "0")]
    [DataRow("spawnsPerTick", "51")]
    [DataRow("liveCap", "100")]
    [DataRow("liveCap", "401")]
    [DataRow("garrisonCap", "0")]
    [DataRow("garrisonCap", "401")]
    [DataRow("musterBatch", "0")]
    [DataRow("musterBatch", "51")]
    [DataRow("stragglerShare", "0.51")]
    [DataRow("stragglerShare", "-0.1")]
    [DataRow("stragglerShare", "NaN")]
    [DataRow("keepRadius", "1.9")]
    [DataRow("keepRadius", "51")]
    [DataRow("keepRadius", "NaN")]
    [DataRow("keepHeightBand", "0.9")]
    [DataRow("keepHeightBand", "21")]
    [DataRow("keepHeightBand", "NaN")]
    [DataRow("keepDominance", "0.9")]
    [DataRow("keepDominance", "11")]
    [DataRow("keepDominance", "NaN")]
    [DataRow("restShare", "-0.1")]
    [DataRow("restShare", "1.1")]
    [DataRow("restShare", "NaN")]
    [DataRow("stragglerSeconds", "0.5")]
    [DataRow("stragglerSeconds", "3601")]
    [DataRow("stragglerSeconds", "NaN")]
    [DataRow("stragglerCount", "-1")]
    [DataRow("stragglerCount", "101")]
    [DataRow("keepMinAttackers", "0")]
    [DataRow("keepMinAttackers", "101")]
    public void Rules_BadValue_KeepsTheDefaultWithOneWarningNamingTheKey(string key, string value)
    {
        var catalog = Load(waves: Waves($"'{key}':{value}"));

        Assert.AreEqual(RunRules.Default, catalog.BuildPlan(catalog.Fortresses[0], RunOptions.Default).Rules);
        AssertOneWarningNaming("waves.json", key);
    }

    [DataTestMethod]
    [DataRow(5, 12)]
    [DataRow(11, 12)]
    [DataRow(12, 12)]
    [DataRow(40, 40)]
    public void KeepMinAttackers_AtOrBelowTheStragglerShareOfTheSmallestWave_RisesAboveIt(int written, int expected)
    {
        var catalog = Load(waves: Waves($"'keepMinAttackers':{written},'stragglerShare':0.1"));

        Assert.AreEqual(expected, catalog.Rules.KeepMinAttackers);
        _logger.Received(written == expected ? 0 : 1).LogWarning(Arg.Is<string>(m => m.Contains("waves.json") && m.Contains("keepMinAttackers")));
    }

    [DataTestMethod]
    [DataRow("liveCap")]
    [DataRow("keepMinAttackers")]
    [DataRow("earlyGoldPerSecond")]
    public void Rules_HugeInteger_KeepsTheDefaultWithOneWarningAndTheFortress(string key)
    {
        var catalog = Load(waves: Waves($"'{key}':9999999999999999999999999"));

        Assert.AreEqual(1, catalog.Fortresses.Count);
        Assert.AreEqual(RunRules.Default, catalog.BuildPlan(catalog.Fortresses[0], RunOptions.Default).Rules);
        AssertOneWarningNaming("waves.json", key);
    }

    [TestMethod]
    public void Garrison_HugeInteger_SkipsTheRowAndKeepsTheFortress()
    {
        var rows = "{'troop':'d_inf','class':'Infantry','count':9999999999999999999999999},{'troop':'d_arc','class':'Ranged','count':50}";

        var catalog = Load(Fortresses(Fortress(rows)));

        Assert.AreEqual("d_arc", catalog.Fortresses.Single().Garrison.Single().TroopId);
        AssertOneWarningNaming("fortresses.json", "count");
    }

    [TestMethod]
    public void Keep_HugeCoordinate_IsSkippedButTheFortressStays()
    {
        var catalog = Load(Fortresses(Fortress(keep: "{'x':99999999999999999999999999,'y':2,'z':3}")));

        Assert.IsNull(catalog.Fortresses.Single().Keep);
        AssertOneWarningNaming("fortresses.json", "keep");
    }

    [TestMethod]
    public void Rules_NotAnObject_KeepsAllDefaultsWithOneWarning()
    {
        var catalog = Load(waves: "{'rules':5,'bounties':{'a_inf':4,'a_arc':6},'rosters':{'r':{'waves':[" + Wave + "]}}}");

        Assert.AreEqual(RunRules.Default, catalog.BuildPlan(catalog.Fortresses[0], RunOptions.Default).Rules);
        AssertOneWarningNaming("waves.json", "rules");
    }

    [DataTestMethod]
    [DataRow(100)]
    [DataRow(1001)]
    public void Wave_CountOutOfRange_SkipsThatWaveOnly(int count)
    {
        var bad = $"{{'count':{count},'mix':[{Mix}]}}";

        var catalog = Load(waves: Waves(waves: bad + "," + Wave));

        Assert.AreEqual(1, catalog.Fortresses[0].Waves.Authored.Count);
        AssertOneWarningNaming("waves.json", "count");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("-2")]
    [DataRow("1.5")]
    [DataRow("'3'")]
    public void Wave_BadWeight_SkipsThatWave(string weight)
    {
        var bad = $"{{'count':110,'mix':[{{'troop':'a_inf','class':'Infantry','weight':{weight}}}]}}";

        var catalog = Load(waves: Waves(waves: bad + "," + Wave));

        Assert.AreEqual(1, catalog.Fortresses[0].Waves.Authored.Count);
        AssertOneWarningNaming("waves.json", "weight");
    }

    [TestMethod]
    public void Wave_BadClass_SkipsThatRowAndKeepsTheWave()
    {
        var wave = "{'count':110,'mix':[{'troop':'a_inf','class':'Cavalry','weight':1}," + Mix + "]}";

        var catalog = Load(waves: Waves(waves: wave));

        Assert.AreEqual(1, catalog.Fortresses[0].Waves.Authored.Count);
        Assert.AreEqual(2, catalog.Fortresses[0].Waves.ForWave(1).Mix.Count);
        AssertOneWarningNaming("waves.json", "class");
    }

    [TestMethod]
    public void Roster_WithNoValidWave_SkipsTheFortress()
    {
        var catalog = Load(waves: Waves(waves: "{'count':50,'mix':[" + Mix + "]}"));

        Assert.AreEqual(0, catalog.Fortresses.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("helms")));
    }

    [DataTestMethod]
    [DataRow("-1")]
    [DataRow("10001")]
    [DataRow("1.5")]
    [DataRow("'4'")]
    [DataRow("NaN")]
    public void Bounty_BadValue_PaysNothingWithOneWarning(string value)
    {
        var catalog = Load(waves: Waves(bounties: $"'a_inf':{value},'a_arc':6"));

        Assert.AreEqual(0, catalog.BuildPlan(catalog.Fortresses[0], RunOptions.Default).Bounties["a_inf"]);
        AssertOneWarningNaming("waves.json", "a_inf");
    }

    [TestMethod]
    public void Bounty_MissingForAWaveTroop_IsZeroWithOneWarning()
    {
        var catalog = Load(waves: Waves(bounties: "'a_arc':6"));

        var bounties = catalog.BuildPlan(catalog.Fortresses[0], RunOptions.Default).Bounties;
        Assert.AreEqual(0, bounties.TryGetValue("a_inf", out var pay) ? pay : 0);
        AssertOneWarningNaming("waves.json", "a_inf");
    }

    [TestMethod]
    public void Bounty_ForATroopNoWaveUses_WarnsNothing()
    {
        Load(waves: Waves(bounties: "'a_inf':4,'a_arc':6,'unused':9"));

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("51")]
    [DataRow("1.5")]
    public void Offer_BadPackSize_SkipsTheOffer(string size)
    {
        var bad = Offer.Replace("'packSize':12", $"'packSize':{size}");

        var catalog = Load(shop: Shop(bad + "," + Offer.Replace("militia", "other")));

        Assert.AreEqual("other", catalog.Fortresses[0].Shop.Offers.Single().Id);
        AssertOneWarningNaming("shop.json", "packSize");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("1000001")]
    [DataRow("NaN")]
    public void Offer_BadPrice_SkipsTheOffer(string price)
    {
        var bad = Offer.Replace("'price':180", $"'price':{price}");

        var catalog = Load(shop: Shop(bad + "," + Offer.Replace("militia", "other")));

        Assert.AreEqual("other", catalog.Fortresses[0].Shop.Offers.Single().Id);
        AssertOneWarningNaming("shop.json", "price");
    }

    [TestMethod]
    public void Offer_InvalidFirstThenValidWithTheSameId_KeepsTheValidOne()
    {
        var bad = Offer.Replace("'packSize':12", "'packSize':0");

        var catalog = Load(shop: Shop(bad + "," + Offer));

        Assert.AreEqual(12, catalog.Fortresses.Single().Shop.Offers.Single().PackSize);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("repeats")));
    }

    [TestMethod]
    public void Fortress_InvalidFirstThenValidWithTheSameId_KeepsTheValidOne()
    {
        var bad = Fortress(replaceKey: "'scene':'s',", replaceWith: "");

        var catalog = Load(Fortresses(bad, Fortress()));

        Assert.AreEqual("s", catalog.Fortresses.Single().Scene);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("duplicate")));
    }

    [TestMethod]
    public void Waves_NotAnArray_WarnsOnceForTheRoster()
    {
        _ = Load(waves: "{'rules':{},'bounties':{},'rosters':{'r':{'waves':5}}}").Fortresses.Count;

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("waves.json") && m.Contains("rosters.r")));
    }

    [TestMethod]
    public void Shops_NotAnObject_WarnsOnce()
    {
        _ = Load(shop: "{'shops':5}").Fortresses.Count;

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shop.json") && m.Contains("shops")));
    }

    [DataTestMethod]
    [DataRow("infantry")]
    [DataRow("RANGED")]
    public void Class_WrongCasing_IsRefused(string cls)
    {
        var catalog = Load(shop: Shop(Offer.Replace("'Infantry'", $"'{cls}'") + "," + Offer.Replace("militia", "other")));

        Assert.AreEqual("other", catalog.Fortresses[0].Shop.Offers.Single().Id);
        AssertOneWarningNaming("shop.json", "class");
    }

    [TestMethod]
    public void Offer_BadClass_SkipsTheOffer()
    {
        var catalog = Load(shop: Shop(Offer.Replace("'Infantry'", "'Cavalry'") + "," + Offer.Replace("militia", "other")));

        Assert.AreEqual("other", catalog.Fortresses[0].Shop.Offers.Single().Id);
        AssertOneWarningNaming("shop.json", "class");
    }

    [TestMethod]
    public void Offer_DuplicateId_SkipsTheLaterOne()
    {
        var later = Offer.Replace("'price':180", "'price':999");

        var catalog = Load(shop: Shop(Offer + "," + later));

        var offer = catalog.Fortresses[0].Shop.Offers.Single();
        Assert.AreEqual(180, offer.Price);
        AssertOneWarningNaming("shop.json", "militia");
    }

    [DataTestMethod]
    [DataRow("-1")]
    [DataRow("1000001")]
    [DataRow("NaN")]
    public void StartingGold_Bad_FallsBackWithOneWarning(string gold)
    {
        var catalog = Load(shop: Shop(gold: gold));

        Assert.AreEqual(0, catalog.Fortresses[0].Shop.StartingGold);
        AssertOneWarningNaming("shop.json", "startingGold");
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("401")]
    [DataRow("2.5")]
    public void Garrison_BadCount_SkipsTheRow(string count)
    {
        var rows = "{'troop':'d_inf','class':'Infantry','count':" + count + "},{'troop':'d_arc','class':'Ranged','count':50}";

        var catalog = Load(Fortresses(Fortress(rows)));

        Assert.AreEqual("d_arc", catalog.Fortresses.Single().Garrison.Single().TroopId);
        AssertOneWarningNaming("fortresses.json", "count");
    }

    [TestMethod]
    public void Garrison_BadClass_SkipsTheRow()
    {
        var rows = "{'troop':'d_inf','class':'Archer','count':10},{'troop':'d_arc','class':'Ranged','count':50}";

        var catalog = Load(Fortresses(Fortress(rows)));

        Assert.AreEqual(1, catalog.Fortresses.Single().Garrison.Count);
        AssertOneWarningNaming("fortresses.json", "class");
    }

    [TestMethod]
    public void Garrison_NoValidRow_SkipsTheFortress()
    {
        var catalog = Load(Fortresses(Fortress("{'troop':'d_inf','class':'Infantry','count':0}")));

        Assert.AreEqual(0, catalog.Fortresses.Count);
    }

    [TestMethod]
    public void Fortress_UnknownWavesReference_IsSkippedWithAWarningNamingIt()
    {
        var catalog = Load(Fortresses(Fortress(replaceKey: "'waves':'r'", replaceWith: "'waves':'nope'")));

        Assert.AreEqual(0, catalog.Fortresses.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("nope")));
    }

    [TestMethod]
    public void Fortress_UnknownShopReference_IsSkippedWithAWarningNamingIt()
    {
        var catalog = Load(Fortresses(Fortress(replaceKey: "'shop':'sh'", replaceWith: "'shop':'nope'")));

        Assert.AreEqual(0, catalog.Fortresses.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("nope")));
    }

    [DataTestMethod]
    [DataRow("'scene':'s',")]
    [DataRow("'defenderCulture':'vlandia',")]
    [DataRow("'attackerCulture':'isengard',")]
    [DataRow("'playerTroop':'lord_4_1',")]
    [DataRow("'id':'helms',")]
    public void Fortress_MissingRequiredText_IsSkipped(string removed)
    {
        var catalog = Load(Fortresses(Fortress(replaceKey: removed, replaceWith: "")));

        Assert.AreEqual(0, catalog.Fortresses.Count);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Fortress_DuplicateId_KeepsTheFirst()
    {
        var catalog = Load(Fortresses(Fortress(), Fortress(replaceKey: "'scene':'s'", replaceWith: "'scene':'other'")));

        Assert.AreEqual("s", catalog.Fortresses.Single().Scene);
        AssertOneWarningNaming("fortresses.json", "helms");
    }

    [TestMethod]
    public void Fortress_NoName_UsesTheId()
    {
        var catalog = Load(Fortresses(Fortress(replaceKey: "'name':'{=K}Helm',", replaceWith: "")));

        Assert.AreEqual("helms", catalog.Fortresses.Single().Name);
    }

    [TestMethod]
    public void Keep_ARecordedBlock_IsRead()
    {
        var keep = "{'x':138.4,'y':159.5,'z':32.5,'radius':10}";

        var point = Load(Fortresses(Fortress(keep: keep))).Fortresses.Single().Keep!;

        Assert.AreEqual(138.4f, point.X);
        Assert.AreEqual(159.5f, point.Y);
        Assert.AreEqual(32.5f, point.Z);
        Assert.AreEqual(10f, point.Radius);
    }

    [TestMethod]
    public void Keep_NoRadius_UsesTheRulesRadius()
    {
        var catalog = Load(Fortresses(Fortress(keep: "{'x':1,'y':2,'z':3}")), Waves("'keepRadius':15"));

        Assert.AreEqual(15f, catalog.Fortresses.Single().Keep!.Radius);
    }

    [DataTestMethod]
    [DataRow("{'x':NaN,'y':2,'z':3,'radius':12}")]
    [DataRow("{'x':1,'y':Infinity,'z':3,'radius':12}")]
    [DataRow("{'x':100001,'y':2,'z':3,'radius':12}")]
    [DataRow("{'x':1,'y':-100001,'z':3,'radius':12}")]
    [DataRow("{'x':1,'y':2,'radius':12}")]
    [DataRow("{'x':1,'y':2,'z':3,'radius':1}")]
    [DataRow("{'x':1,'y':2,'z':3,'radius':NaN}")]
    [DataRow("7")]
    public void Keep_BadBlock_IsSkippedButTheFortressStays(string keep)
    {
        var catalog = Load(Fortresses(Fortress(keep: keep)));

        Assert.IsNull(catalog.Fortresses.Single().Keep);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("keep")));
    }

    private const string Foot = "{'x':10,'y':20,'z':30,'fx':0,'fy':1}";
    private const string Archers = "{'x':40.5,'y':50.5,'z':60.5,'fx':1,'fy':0}";

    [TestMethod]
    public void Muster_ARecordedBlock_IsRead()
    {
        var muster = Load(Fortresses(Fortress(muster: "{'foot':" + Foot + ",'archers':" + Archers + "}"))).Fortresses.Single().Muster!;

        Assert.AreEqual(new MusterFrame(10f, 20f, 30f, 0f, 1f), muster.Foot);
        Assert.AreEqual(new MusterFrame(40.5f, 50.5f, 60.5f, 1f, 0f), muster.Archers);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Muster_TheLookIsNormalized()
    {
        var muster = Load(Fortresses(Fortress(muster: "{'foot':{'x':1,'y':2,'z':3,'fx':0,'fy':0.6}}"))).Fortresses.Single().Muster!;

        Assert.AreEqual(0f, muster.Foot!.Fx);
        Assert.AreEqual(1f, muster.Foot.Fy);
    }

    [TestMethod]
    public void Muster_OnePointRecorded_LeavesTheOtherOnTheSceneFrame()
    {
        var muster = Load(Fortresses(Fortress(muster: "{'foot':" + Foot + "}"))).Fortresses.Single().Muster!;

        Assert.IsNotNull(muster.Foot);
        Assert.IsNull(muster.Archers);
        Assert.IsNull(muster.For(FormationSlot.Ranged));
        Assert.AreEqual(muster.Foot, muster.For(FormationSlot.Infantry));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    [DataRow("{}")]
    public void Muster_MissingOrEmpty_IsNullWithNoWarning(string muster)
    {
        var catalog = Load(Fortresses(Fortress(muster: muster)));

        Assert.IsNull(catalog.Fortresses.Single().Muster);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("{'foot':{'x':NaN,'y':2,'z':3,'fx':0,'fy':1}}")]
    [DataRow("{'foot':{'x':1,'y':Infinity,'z':3,'fx':0,'fy':1}}")]
    [DataRow("{'foot':{'x':1,'y':2,'z':3,'fx':NaN,'fy':1}}")]
    [DataRow("{'foot':{'x':100001,'y':2,'z':3,'fx':0,'fy':1}}")]
    [DataRow("{'foot':{'x':1,'y':-100001,'z':3,'fx':0,'fy':1}}")]
    [DataRow("{'foot':{'x':1,'y':2,'fx':0,'fy':1}}")]
    [DataRow("{'foot':{'x':1,'y':2,'z':3,'fx':0}}")]
    [DataRow("{'foot':{'x':1,'y':2,'z':3,'fx':0,'fy':0}}")]
    [DataRow("{'foot':{'x':1,'y':2,'z':3,'fx':3,'fy':4}}")]
    [DataRow("{'foot':{'x':99999999999999999999999999,'y':2,'z':3,'fx':0,'fy':1}}")]
    [DataRow("{'foot':7}")]
    [DataRow("7")]
    public void Muster_BadBlock_IsSkippedWithOneWarning_AndTheFortressStays(string muster)
    {
        var catalog = Load(Fortresses(Fortress(muster: muster)));

        Assert.IsNull(catalog.Fortresses.Single().Muster);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("muster")));
    }

    [TestMethod]
    public void Muster_ABadArchersPoint_RevertsTheWholeBlock()
    {
        var catalog = Load(Fortresses(Fortress(muster: "{'foot':" + Foot + ",'archers':{'x':1,'y':2,'z':3,'fx':0,'fy':0}}")));

        Assert.IsNull(catalog.Fortresses.Single().Muster);
        AssertOneWarningNaming("fortresses.json", "muster.archers");
    }

    [TestMethod]
    public void Muster_ReachesTheRunPlan()
    {
        var catalog = Load(Fortresses(Fortress(muster: "{'foot':" + Foot + "}")));

        var plan = catalog.BuildPlan(catalog.Fortresses.Single(), RunOptions.Default);

        Assert.AreEqual(catalog.Fortresses.Single().Muster, plan.Muster);
    }

    [TestMethod]
    public void Muster_NoBlock_LeavesThePlanWithNone()
    {
        var catalog = Load();

        Assert.IsNull(catalog.BuildPlan(catalog.Fortresses.Single(), RunOptions.Default).Muster);
    }

    [TestMethod]
    public void NoValidFortress_LeavesTheListEmpty()
    {
        Assert.AreEqual(0, Load(Fortresses()).Fortresses.Count);
    }

    [DataTestMethod]
    [DataRow("fortresses")]
    [DataRow("waves")]
    [DataRow("shop")]
    public void FileMissing_LoadsNothingWithAWarningAndDoesNotThrow(string which)
    {
        var catalog = LongDefeatConfigCatalog.FromTexts(
            which == "fortresses" ? null : Fortresses(Fortress()), which == "waves" ? null : Waves(), which == "shop" ? null : Shop(), _logger);

        Assert.AreEqual(0, catalog.Fortresses.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(which + ".json")));
    }

    [DataTestMethod]
    [DataRow("{ this is not json")]
    [DataRow("[1,2]")]
    [DataRow("")]
    public void FileNotJsonOrNotAnObject_LoadsNothingWithoutThrowing(string text)
    {
        Assert.AreEqual(0, Load(fortresses: text).Fortresses.Count);
        Assert.AreEqual(0, Load(waves: text).Fortresses.Count);
        Assert.AreEqual(0, Load(shop: text).Fortresses.Count);
    }

    [TestMethod]
    public void MissingWavesFile_StillGivesTheDefaultRules()
    {
        var catalog = LongDefeatConfigCatalog.FromTexts(Fortresses(Fortress()), null, Shop(), _logger);

        Assert.AreEqual(RunRules.Default, catalog.Rules);
    }
}

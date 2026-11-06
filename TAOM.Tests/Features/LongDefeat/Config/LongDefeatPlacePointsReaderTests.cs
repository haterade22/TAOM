using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The <c>armoury</c> and <c>yard</c> blocks of a fortress row in fortresses.json (phase4-plan.md section 5), the muster-shaped blocks <c>taom.ld_export</c> writes, and how the catalog hands them and the shop's
/// places to a run's plan. An absent or null block is no place and no warning; a bad one is one warning naming the file and the key, and the fortress and its other places stay.
/// </summary>
[TestClass]
public class LongDefeatPlacePointsReaderTests
{
    private const string Mix = "{'troop':'a_inf','class':'Infantry','weight':3}";
    private const string Wave = "{'count':110,'mix':[" + Mix + "]}";
    private const string Garrison = "{'troop':'d_inf','class':'Infantry','count':100}";
    private const string Offer = "{'id':'militia','name':'Militia','troop':'d_inf','class':'Infantry','packSize':12,'price':180}";
    private const string Affixes = "{'names':{'isengard':{'lash':'{=A1}L','drums':'{=A2}D','blades':'{=A3}B','eye':'{=A4}E','discipline':'{=A5}I','hide':'{=A6}H','terror':'{=A7}T','hammer':'{=A8}M'}}}";
    private const string Place = "{'x':1,'y':2,'z':3,'fx':0,'fy':1}";
    private const string Classes = "{'classes':[]}";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static string Fortress(string extra = "") =>
        "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard','playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[" + Garrison + "],'keep':null"
        + (extra.Length == 0 ? "" : "," + extra) + "}";

    private LongDefeatConfigCatalog Catalog(string fortress, string shopExtra = "")
    {
        var waves = "{'rules':{},'bounties':{'a_inf':4},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300},'waves':[" + Wave + "]}}}";
        var shop = "{'shops':{'sh':{'startingGold':600,'offers':[" + Offer + "]" + (shopExtra.Length == 0 ? "" : "," + shopExtra) + "}}}";
        return LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + fortress + "]}", waves, shop, _logger, Affixes, "{'boons':[]}", Classes);
    }

    private FortressEntry Load(string extra = "") => Catalog(Fortress(extra)).Fortresses.Single();

    private void AssertOneWarningNaming(params string[] words) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && words.All(w => m.Contains(w))));

    [TestMethod]
    public void NoPlaceBlocks_ReadsExactlyAsBefore_NoArmouryNoYardNoWarning()
    {
        var fortress = Load();

        Assert.IsNull(fortress.ArmouryPoint);
        Assert.IsNull(fortress.YardPoint);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void NullBlocks_AreNoPlaces_WithoutAWarning()
    {
        var fortress = Load("'armoury':null,'yard':null");

        Assert.IsNull(fortress.ArmouryPoint);
        Assert.IsNull(fortress.YardPoint);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void TheArmouryAndTheYard_AreReadInTheMusterShape_WithTheLookNormalized()
    {
        var fortress = Load("'armoury':{'x':10,'y':20,'z':30,'fx':0.6,'fy':0},'yard':" + Place);

        Assert.AreEqual(new MusterFrame(10f, 20f, 30f, 1f, 0f), fortress.ArmouryPoint, "the look is normalized as a muster point's is");
        Assert.AreEqual(new MusterFrame(1f, 2f, 3f, 0f, 1f), fortress.YardPoint);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("{'x':1,'y':2,'z':3,'fx':0,'fy':0}")]
    [DataRow("{'x':1,'y':2,'z':3,'fx':3,'fy':4}")]
    [DataRow("{'x':NaN,'y':2,'z':3,'fx':0,'fy':1}")]
    [DataRow("{'x':1,'y':2,'z':3,'fx':0}")]
    [DataRow("{'x':1,'y':'two','z':3,'fx':0,'fy':1}")]
    [DataRow("{'x':200000,'y':2,'z':3,'fx':0,'fy':1}")]
    [DataRow("{}")]
    [DataRow("7")]
    [DataRow("[]")]
    public void ABadArmoury_IsNullAndWarnedOnce_AndTheYardAndTheFortressStay(string bad)
    {
        var fortress = Load("'armoury':" + bad + ",'yard':" + Place);

        Assert.IsNull(fortress.ArmouryPoint);
        Assert.IsNotNull(fortress.YardPoint, "a bad armoury must not take the yard with it");
        AssertOneWarningNaming("armoury");
    }

    [DataTestMethod]
    [DataRow("{'x':1,'y':2,'z':3,'fx':0,'fy':0}")]
    [DataRow("{'x':1,'y':2,'z':3}")]
    [DataRow("true")]
    public void ABadYard_IsNullAndWarnedOnce_AndTheArmouryStays(string bad)
    {
        var fortress = Load("'yard':" + bad + ",'armoury':" + Place);

        Assert.IsNull(fortress.YardPoint);
        Assert.IsNotNull(fortress.ArmouryPoint);
        AssertOneWarningNaming("yard");
    }

    [TestMethod]
    public void TheShippedFortressesFile_HoldsNoArmouryAndNoYard_UntilTheyAreRecorded()
    {
        var dir = new[] { "Main", "_Module", "ModuleData", "long_defeat" };
        string Shipped(string file) => File.ReadAllText(RepoPaths.RepoPath(dir.Concat(new[] { file }).ToArray()));
        var catalog = LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), _logger);

        Assert.IsTrue(catalog.Fortresses.Count > 0);
        Assert.IsTrue(catalog.Fortresses.All(f => f.ArmouryPoint == null && f.YardPoint == null), "no coordinate ships until taom.ld_export produces it");
        Assert.IsTrue(catalog.Fortresses.All(f => f.Shop.Locations == null), "no set, engine or Marshal ships in the first slice, so every location row stays hidden");
    }

    [TestMethod]
    public void BuildPlan_CarriesTheRecordedPlaces_AndTheShopsBlocks()
    {
        var catalog = Catalog(Fortress("'armoury':" + Place + ",'yard':{'x':4,'y':5,'z':6,'fx':1,'fy':0}"),
            "'armoury':{'sets':[{'id':'a','kind':'armour','price':5,'slots':{'Head':'h'}}]},'yard':{'engines':[{'kind':'mangonel','price':450}]},'marshal':{'troop':'lord_4_7','price':500}");

        var plan = catalog.BuildPlan(catalog.Fortresses[0], RunOptions.Default);

        Assert.AreEqual(new MusterFrame(1f, 2f, 3f, 0f, 1f), plan.ArmouryPoint);
        Assert.AreEqual(new MusterFrame(4f, 5f, 6f, 1f, 0f), plan.YardPoint);
        Assert.AreEqual("a", plan.Armoury!.Sets.Single().Id);
        Assert.AreEqual("mangonel", plan.Yard!.Engines.Single().Kind);
        Assert.AreEqual("lord_4_7", plan.Marshal!.Troop);
    }

    [TestMethod]
    public void ThePlansThreeBlocks_AreTheShopsOwn_SoNoFixtureCanSetOneCopyAndNotTheOther()
    {
        var armoury = new ArmouryPlan(4f, new[] { new GearSet("a", "a", "", GearKind.Armour, null, new[] { new LdKitSlot("Head", "h") }, 5) });
        var yard = new YardPlan(6f, new[] { new EngineOffer("mangonel", 450) });
        var marshal = new MarshalPlan("lord_4_7", 500, "m", "");
        var plan = new RunPlan("helms", new[] { new GarrisonRow("d", FormationSlot.Infantry, 1) }, new WavePlan(new[] { new WaveSpec(1, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(1, 1)),
            new ShopPlan(0, new ShopOffer[0], null, new LocationsPlan(armoury, yard, marshal)), RunRules.Default, RunOptions.Default);

        Assert.AreSame(armoury, plan.Armoury);
        Assert.AreSame(yard, plan.Yard);
        Assert.AreSame(marshal, plan.Marshal);
    }

    [TestMethod]
    public void BuildPlan_WithNoPlaceBlocks_HasNoPlaces_AndOffersNothing()
    {
        var catalog = Catalog(Fortress());

        var plan = catalog.BuildPlan(catalog.Fortresses[0], RunOptions.Default);

        Assert.IsNull(plan.ArmouryPoint);
        Assert.IsNull(plan.YardPoint);
        Assert.IsNull(plan.Armoury);
        Assert.IsNull(plan.Yard);
        Assert.IsNull(plan.Marshal);
        Assert.AreEqual(0, plan.HiddenGearSets.Count);
        Assert.AreEqual(LocationOffer.None, plan.Locations, "Slice 1 delivers no location, so the plan offers none");
    }
}

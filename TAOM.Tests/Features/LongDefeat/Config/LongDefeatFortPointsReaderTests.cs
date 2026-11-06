using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The <c>towers</c> and <c>volley</c> blocks of a fortress row in fortresses.json (phase3-plan.md section 5), the blocks <c>taom.ld_export</c> writes.
/// A bad point costs one warning naming the file and the key and is dropped on its own; a bad tower is skipped; a missing block is no warning at all.
/// </summary>
[TestClass]
public class LongDefeatFortPointsReaderTests
{
    private const string Mix = "{'troop':'a_inf','class':'Infantry','weight':3},{'troop':'a_arc','class':'Ranged','weight':1}";
    private const string Wave = "{'count':110,'mix':[" + Mix + "]}";
    private const string Garrison = "{'troop':'d_inf','class':'Infantry','count':100},{'troop':'d_arc','class':'Ranged','count':50}";
    private const string Offer = "{'id':'militia','name':'Militia','troop':'d_inf','class':'Infantry','packSize':12,'price':180}";
    private const string Affixes = "{'names':{'isengard':{'lash':'{=A1}L','drums':'{=A2}D','blades':'{=A3}B','eye':'{=A4}E','discipline':'{=A5}I','hide':'{=A6}H','terror':'{=A7}T','hammer':'{=A8}M'}}}";

    private const string Post = "{'x':1,'y':2,'z':3,'fx':0,'fy':1}";
    private const string Spot = "{'x':4,'y':5,'z':6}";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static string Fortress(string towers = "", string volley = "") =>
        "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard'," +
        "'playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[" + Garrison + "],'keep':null"
        + (towers.Length == 0 ? "" : ",'towers':" + towers) + (volley.Length == 0 ? "" : ",'volley':" + volley) + "}";

    private static string Tower(string id = "'hornburg'", string posts = Post, string volley = Spot) =>
        "{'id':" + id + (posts.Length == 0 ? "" : ",'posts':[" + posts + "]") + (volley.Length == 0 ? "" : ",'volley':[" + volley + "]") + "}";

    private FortressEntry Load(string fortress)
    {
        var waves = "{'rules':{},'bounties':{'a_inf':4,'a_arc':6},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300},'waves':[" + Wave + "]}}}";
        var shop = "{'shops':{'sh':{'startingGold':600,'offers':[" + Offer + "]}}}";
        return LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + fortress + "]}", waves, shop, _logger, Affixes, "{'boons':[]}", "{'classes':[]}")
            .Fortresses.Single();
    }

    private void AssertOneWarningNaming(params string[] words) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && words.All(w => m.Contains(w))));

    private void AssertNoWarning() => _logger.DidNotReceive().LogWarning(Arg.Any<string>());

    [TestMethod]
    public void NoNewBlocks_ReadsExactlyAsBefore_NoTowersNoVolleyNoWarning()
    {
        var fortress = Load(Fortress());

        Assert.IsNull(fortress.Towers);
        Assert.IsNull(fortress.Volley);
        AssertNoWarning();
    }

    [DataTestMethod]
    [DataRow("null", "null")]
    [DataRow("[]", "{}")]
    [DataRow("[]", "{'origins':[]}")]
    public void NullOrEmptyBlocks_AreNoTowersAndNoVolley_WithoutAWarning(string towers, string volley)
    {
        var fortress = Load(Fortress(towers, volley));

        Assert.IsNull(fortress.Towers);
        Assert.IsNull(fortress.Volley);
        AssertNoWarning();
    }

    [TestMethod]
    public void TheShippedFortressesFile_HoldsNoNewBlocks_AndTheReaderTakesItAsToday()
    {
        var dir = new[] { "Main", "_Module", "ModuleData", "long_defeat" };
        var catalog = LongDefeatConfigCatalog.FromTexts(File.ReadAllText(RepoPaths.RepoPath(dir.Concat(new[] { "fortresses.json" }).ToArray())),
            File.ReadAllText(RepoPaths.RepoPath(dir.Concat(new[] { "waves.json" }).ToArray())),
            File.ReadAllText(RepoPaths.RepoPath(dir.Concat(new[] { "shop.json" }).ToArray())), _logger);

        Assert.IsTrue(catalog.Fortresses.Count > 0);
        Assert.IsTrue(catalog.Fortresses.All(f => f.Towers == null && f.Volley == null), "no coordinate ships until taom.ld_export produces it");
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("towers") || m.Contains("volley")));
    }

    [TestMethod]
    public void ASecondTowersKeyInOneRow_ReplacesTheFirst_AsTheExportReplyTellsThePlayer()
    {
        var fortress = Load(Fortress("[" + Tower("'old'") + "],'towers':[" + Tower("'newer'") + "]"));

        Assert.AreEqual("newer", fortress.Towers!.Single().Id);
    }

    [TestMethod]
    public void ATower_IsReadWithItsPostsInTheMusterShape_AndItsVolleySpotsAsPoints()
    {
        var fortress = Load(Fortress(
            "[" + Tower(posts: Post + ",{'x':7,'y':8,'z':9,'fx':0.6,'fy':0}", volley: Spot + ",{'x':10,'y':11,'z':12}")
            + "," + Tower("'wall'", posts: "") + "]"));

        Assert.AreEqual(2, fortress.Towers!.Count);
        var hornburg = fortress.Towers[0];
        Assert.AreEqual("hornburg", hornburg.Id);
        Assert.AreEqual(2, hornburg.Posts.Count);
        Assert.AreEqual(new TAOM.Features.LongDefeat.Domain.MusterFrame(1f, 2f, 3f, 0f, 1f), hornburg.Posts[0]);
        Assert.AreEqual(1f, hornburg.Posts[1].Fx, "the look is normalized as a muster point's is");
        Assert.AreEqual(0f, hornburg.Posts[1].Fy);
        Assert.AreEqual(2, hornburg.VolleyOrigins.Count);
        Assert.AreEqual(new TAOM.Features.LongDefeat.Domain.LdPoint(10f, 11f, 12f), hornburg.VolleyOrigins[1]);
        Assert.AreEqual(0, fortress.Towers[1].Posts.Count, "a tower with only volley spots keeps them");
        Assert.AreEqual(1, fortress.Towers[1].VolleyOrigins.Count);
        AssertNoWarning();
    }

    [DataTestMethod]
    [DataRow("{'x':1,'y':2,'z':3,'fx':0,'fy':0}")]
    [DataRow("{'x':1,'y':2,'z':3,'fx':0.2,'fy':0.2}")]
    [DataRow("{'x':1,'y':2,'z':3,'fx':3,'fy':4}")]
    [DataRow("{'x':1,'y':2,'z':3,'fx':NaN,'fy':1}")]
    [DataRow("{'x':NaN,'y':2,'z':3,'fx':0,'fy':1}")]
    [DataRow("{'x':1,'y':2,'z':Infinity,'fx':0,'fy':1}")]
    [DataRow("{'x':1,'y':2,'z':3,'fx':0}")]
    [DataRow("{'x':1,'y':'two','z':3,'fx':0,'fy':1}")]
    [DataRow("{'x':200000,'y':2,'z':3,'fx':0,'fy':1}")]
    [DataRow("{}")]
    [DataRow("7")]
    [DataRow("null")]
    public void ABadPost_IsDroppedWithOneWarning_AndTheTowerAndItsGoodPostsStay(string bad)
    {
        var fortress = Load(Fortress("[" + Tower(posts: Post + "," + bad + ",{'x':9,'y':9,'z':9,'fx':1,'fy':0}") + "]"));

        var tower = fortress.Towers!.Single();
        Assert.AreEqual(2, tower.Posts.Count);
        Assert.AreEqual(9f, tower.Posts[1].X);
        Assert.AreEqual(1, tower.VolleyOrigins.Count);
        AssertOneWarningNaming("towers", "hornburg", "post");
    }

    [DataTestMethod]
    [DataRow("{'x':1,'y':2}")]
    [DataRow("{'x':NaN,'y':2,'z':3}")]
    [DataRow("{'x':1,'y':2,'z':'high'}")]
    [DataRow("{'x':1,'y':2,'z':-100001}")]
    [DataRow("[]")]
    [DataRow("null")]
    public void ABadTowerVolleySpot_IsDroppedWithOneWarning_AndTheOthersStay(string bad)
    {
        var fortress = Load(Fortress("[" + Tower(volley: Spot + "," + bad) + "]"));

        Assert.AreEqual(1, fortress.Towers!.Single().VolleyOrigins.Count);
        Assert.AreEqual(1, fortress.Towers!.Single().Posts.Count);
        AssertOneWarningNaming("towers", "hornburg", "volley");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("null")]
    [DataRow("'Hornburg'")]
    [DataRow("'tower1'")]
    [DataRow("'a b'")]
    [DataRow("7")]
    public void ATowerWithNoIdOrABadOne_IsSkippedWithOneWarning_AndTheNextTowerStays(string id)
    {
        var broken = id.Length == 0 ? "{'posts':[" + Post + "]}" : Tower(id);
        var fortress = Load(Fortress("[" + broken + "," + Tower("'wall'") + "]"));

        Assert.AreEqual("wall", fortress.Towers!.Single().Id);
        AssertOneWarningNaming("towers", "id");
    }

    [TestMethod]
    public void ADuplicateTowerId_SkipsTheLaterOne_WithOneWarning()
    {
        var fortress = Load(Fortress("[" + Tower(posts: Post) + "," + Tower(posts: "{'x':50,'y':50,'z':50,'fx':0,'fy':1}") + "]"));

        var tower = fortress.Towers!.Single();
        Assert.AreEqual(1f, tower.Posts.Single().X, "the first one is kept");
        AssertOneWarningNaming("towers", "hornburg", "duplicate");
    }

    [TestMethod]
    public void ADuplicateId_OfATowerThatWasSkipped_IsNotADuplicate()
    {
        var fortress = Load(Fortress("[" + Tower(posts: "{'x':1}", volley: "") + "," + Tower() + "]"));

        Assert.AreEqual("hornburg", fortress.Towers!.Single().Id, "the first one never loaded, so the second is the tower");
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("duplicate")));
    }

    [TestMethod]
    public void ATowerOrAnEntryThatIsNotAnObject_IsSkippedWithOneWarning()
    {
        var fortress = Load(Fortress("[7," + Tower() + "]"));

        Assert.AreEqual("hornburg", fortress.Towers!.Single().Id);
        AssertOneWarningNaming("towers");
    }

    [TestMethod]
    public void ATowerWithNoUsablePoint_IsSkipped_AndTheBlockIsNullWhenNoTowerIsLeft()
    {
        var fortress = Load(Fortress("[" + Tower(posts: "{'x':1}", volley: "{'x':1}") + "]"));

        Assert.IsNull(fortress.Towers);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("hornburg") && m.Contains("no usable")));
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("7")]
    [DataRow("'towers'")]
    public void TowersThatIsNotAnArray_IsDroppedWithOneWarning_AndTheFortressAndItsVolleyStay(string towers)
    {
        var fortress = Load(Fortress(towers, "{'origins':[" + Spot + "]}"));

        Assert.IsNull(fortress.Towers);
        Assert.IsNotNull(fortress.Volley);
        AssertOneWarningNaming("towers");
    }

    [TestMethod]
    public void APostsOrVolleyListThatIsNotAnArray_IsDroppedWithOneWarning()
    {
        var fortress = Load(Fortress("[{'id':'hornburg','posts':7,'volley':[" + Spot + "]}]"));

        var tower = fortress.Towers!.Single();
        Assert.AreEqual(0, tower.Posts.Count);
        Assert.AreEqual(1, tower.VolleyOrigins.Count);
        AssertOneWarningNaming("hornburg", "posts");
    }

    [TestMethod]
    public void VolleyOrigins_AreReadAsPoints_AndAnExtraFacingIsIgnored()
    {
        var fortress = Load(Fortress(volley: "{'origins':[" + Spot + ",{'x':7,'y':8,'z':9,'fx':0,'fy':1}]}"));

        Assert.AreEqual(2, fortress.Volley!.Origins.Count);
        Assert.AreEqual(new TAOM.Features.LongDefeat.Domain.LdPoint(7f, 8f, 9f), fortress.Volley.Origins[1]);
        AssertNoWarning();
    }

    [TestMethod]
    public void ABadVolleyOrigin_IsDroppedWithOneWarning_AndTheGoodOnesStay()
    {
        var fortress = Load(Fortress(volley: "{'origins':[" + Spot + ",{'x':NaN,'y':1,'z':1},{'y':1,'z':1}]}"));

        Assert.AreEqual(1, fortress.Volley!.Origins.Count);
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("volley")));
    }

    [DataTestMethod]
    [DataRow("7")]
    [DataRow("[]")]
    [DataRow("{'origins':7}")]
    [DataRow("{'origins':[{'x':1}]}")]
    public void AVolleyBlockThatIsNotUsable_IsNullAndWarned_AndTheFortressStays(string volley)
    {
        var fortress = Load(Fortress("[" + Tower() + "]", volley));

        Assert.IsNull(fortress.Volley);
        Assert.AreEqual("hornburg", fortress.Towers!.Single().Id, "a bad volley block must not take the towers with it");
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("fortresses.json") && m.Contains("volley")));
    }

    [TestMethod]
    public void AnEmptyVolleyBlock_IsNullWithNoWarning()
    {
        var fortress = Load(Fortress(volley: "{}"));

        Assert.IsNull(fortress.Volley);
        AssertNoWarning();
    }
}

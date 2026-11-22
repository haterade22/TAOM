using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// boons.json (phase2-plan.md section 3): rows in file order, a closed vocabulary of effect keys, one warning per bad value naming file and key, a row left
/// with no effect skipped, and a drawback that always carries its own line of text. Single quotes keep the fixtures readable.
/// </summary>
[TestClass]
public class LongDefeatBoonsReaderTests
{
    private const string Fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard'," +
        "'playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[{'troop':'d_inf','class':'Infantry','count':100}],'keep':null}";
    private const string Waves = "{'rules':{},'bounties':{'a':1},'rosters':{'r':{'scaling':{},'waves':[{'count':110,'mix':[{'troop':'a','class':'Infantry','weight':1}]}]}}}";
    private const string Shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','name':'M','troop':'d_inf','class':'Infantry','packSize':12,'price':180}]}}}";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private BoonPlan Plan(string? boons)
    {
        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + Fortress + "]}", Waves, Shop, _logger, boons: boons);
        return catalog.BuildPlan(catalog.Fortresses.Single(), RunOptions.Default).Boons;
    }

    private static string Row(string id = "a", string category = "warchest", string effects = "[{'key':'bountyPercent','value':25}]", string extra = "") =>
        "{'id':'" + id + "','category':'" + category + "','name':'{=N_" + id + "}Name','text':'{=T_" + id + "}Text','effects':" + effects + extra + "}";

    private static string File(params string[] rows) => "{'legendWeight':1,'boons':[" + string.Join(",", rows) + "]}";

    private void AssertOneWarningNaming(string text) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("boons.json") && m.Contains(text)));

    [TestMethod]
    public void AValidFile_GivesTheRowsInFileOrder_WithNoWarning()
    {
        var plan = Plan(File(
            Row("a"),
            Row("b", "fortress", "[{'key':'garrisonCap','value':30},{'key':'restShare','value':0.25}]"),
            Row("c", "legend", "[{'key':'affixCount','value':-1}]")));

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, plan.Rows.Select(r => r.Id).ToArray());
        Assert.AreEqual(BoonCategory.WarChest, plan.Rows[0].Category);
        Assert.AreEqual(BoonCategory.Legend, plan.Rows[2].Category);
        Assert.AreEqual(new BoonEffect("bountyPercent", 25f), plan.Rows[0].Effects.Single());
        Assert.AreEqual(2, plan.Rows[1].Effects.Count);
        Assert.AreEqual("{=N_a}Name", plan.Rows[0].Name);
        Assert.AreEqual("{=T_a}Text", plan.Rows[0].Text);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("boons.json")));
    }

    [TestMethod]
    public void ALegendRow_WeighsTheFilesLegendWeight_AndTheDefaultIsOne()
    {
        Assert.AreEqual(1, Plan(File(Row())).LegendWeight);
        Assert.AreEqual(4, Plan("{'legendWeight':4,'boons':[" + Row() + "]}").LegendWeight);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("101")]
    [DataRow("2.5")]
    [DataRow("'many'")]
    public void ABadLegendWeight_KeepsTheDefault_WithOneWarning(string value)
    {
        var plan = Plan("{'legendWeight':" + value + ",'boons':[" + Row() + "]}");

        Assert.AreEqual(1, plan.LegendWeight);
        AssertOneWarningNaming("legendWeight");
    }

    [TestMethod]
    public void AMissingFile_IsOneWarning_AndAnEmptyPool()
    {
        var plan = Plan(null);

        Assert.AreEqual(0, plan.Rows.Count);
        AssertOneWarningNaming("not found");
    }

    [TestMethod]
    public void ADuplicateId_SkipsTheLaterRow()
    {
        var plan = Plan(File(Row("a"), Row("a", "fortress", "[{'key':'garrisonCap','value':5}]")));

        Assert.AreEqual(1, plan.Rows.Count);
        Assert.AreEqual(BoonCategory.WarChest, plan.Rows[0].Category);
        AssertOneWarningNaming("repeats the id 'a'");
    }

    [TestMethod]
    public void AnUnknownCategory_SkipsTheRow()
    {
        var plan = Plan(File(Row("a", "mystery"), Row("b")));

        CollectionAssert.AreEqual(new[] { "b" }, plan.Rows.Select(r => r.Id).ToArray());
        AssertOneWarningNaming("category");
    }

    [TestMethod]
    public void AnUnknownKey_SkipsTheEffect_AndARowLeftWithNoneIsSkipped()
    {
        var plan = Plan(File(
            Row("a", effects: "[{'key':'swingSpeedPercent','value':5},{'key':'bountyPercent','value':5}]"),
            Row("b", effects: "[{'key':'swingSpeedPercent','value':5}]")));

        CollectionAssert.AreEqual(new[] { "a" }, plan.Rows.Select(r => r.Id).ToArray());
        Assert.AreEqual("bountyPercent", plan.Rows[0].Effects.Single().Key);
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("swingSpeedPercent")));
    }

    [TestMethod]
    [DataRow("'bountyPercent','value':301")]
    [DataRow("'bountyPercent','value':-91")]
    [DataRow("'bountyPercent','value':NaN")]
    [DataRow("'bountyPercent','value':'lots'")]
    [DataRow("'gold','value':1.5")]
    [DataRow("'gold','value':0")]
    [DataRow("'packBonus','value':2.5")]
    [DataRow("'restShare','value':5")]
    [DataRow("'affixCount','value':1")]
    [DataRow("'affixCount','value':-9")]
    public void ABadValue_SkipsThatEffect_WithOneWarning(string keyAndValue)
    {
        var plan = Plan(File(Row("a", effects: "[{'key':" + keyAndValue + "},{'key':'pricePercent','value':-10}]")));

        Assert.AreEqual("pricePercent", plan.Rows.Single().Effects.Single().Key);
        AssertOneWarningNaming("effects[0]");
    }

    [TestMethod]
    public void ADrawback_NeedsItsOwnText_OrTheBoonIsSkipped()
    {
        var withText = Row("a", extra: ",'drawback':[{'key':'waveBonusPercent','value':-15}],'drawbackText':'{=D_a}Smaller wave bonuses.'");
        var without = Row("b", extra: ",'drawback':[{'key':'waveBonusPercent','value':-15}]");

        var plan = Plan(File(withText, without));

        CollectionAssert.AreEqual(new[] { "a" }, plan.Rows.Select(r => r.Id).ToArray());
        Assert.IsTrue(plan.Rows[0].HasDrawback);
        Assert.AreEqual("{=D_a}Smaller wave bonuses.", plan.Rows[0].DrawbackText);
        AssertOneWarningNaming("drawbackText");
    }

    [TestMethod]
    public void AffixCountAndGold_AreNeverADrawback()
    {
        var plan = Plan(File(Row("a", extra: ",'drawback':[{'key':'gold','value':100},{'key':'affixCount','value':-1},{'key':'pricePercent','value':10}],'drawbackText':'{=D_a}Dearer.'")));

        Assert.AreEqual("pricePercent", plan.Rows.Single().Drawback.Single().Key);
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("drawback")));
    }

    [TestMethod]
    public void AClassRow_NeedsItsClass_AndOtherRowsIgnoreOne()
    {
        var plan = Plan(File(
            Row("a", "class", extra: ",'class':'archer'"),
            Row("b", "class"),
            Row("c", extra: ",'class':'archer'")));

        CollectionAssert.AreEqual(new[] { "a", "c" }, plan.Rows.Select(r => r.Id).ToArray());
        Assert.AreEqual("archer", plan.Rows[0].ClassId);
        Assert.AreEqual("", plan.Rows[1].ClassId, "a class on a war chest row means nothing");
        AssertOneWarningNaming("'b'");
    }

    [TestMethod]
    public void ARowWithoutNameOrText_IsSkipped()
    {
        var plan = Plan(File(
            "{'id':'a','category':'warchest','text':'{=T}x','effects':[{'key':'gold','value':5}]}",
            "{'id':'b','category':'warchest','name':'{=N}x','effects':[{'key':'gold','value':5}]}",
            Row("c")));

        CollectionAssert.AreEqual(new[] { "c" }, plan.Rows.Select(r => r.Id).ToArray());
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("needs")));
    }

    // Phase 5 (D63): a boon may carry a name and a text for one defender culture, so "Oath of Eorl" at Helm's Deep is "Oath of Cirion" at Minas Tirith. The fixture's fortress is vlandia.
    [TestMethod]
    public void ARowWithWordsForTheFortressesCulture_ShowsThem_AndKeepsItsIdAndEffects_WithNoWarning()
    {
        var plan = Plan(File(Row("a", extra: ",'cultures':{'Vlandia':{'name':'{=N_a_V}Mark name','text':'{=T_a_V}Mark text'},'gondor':{'name':'{=N_a_G}City name'}}")));

        var row = plan.Rows.Single();
        Assert.AreEqual(("a", "{=N_a_V}Mark name", "{=T_a_V}Mark text"), (row.Id, row.Name, row.Text), "the culture is matched without case");
        Assert.AreEqual(new BoonEffect("bountyPercent", 25f), row.Effects.Single());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("boons.json")));
    }

    [TestMethod]
    public void WordsWithNoText_KeepTheRowsOwnText_AndWordsForAnotherCulture_ChangeNothing()
    {
        var plan = Plan(File(
            Row("a", extra: ",'cultures':{'vlandia':{'name':'{=N_a_V}Mark name'}}"),
            Row("b", extra: ",'cultures':{'gondor':{'name':'{=N_b_G}City name','text':'{=T_b_G}City text'}}")));

        Assert.AreEqual(("{=N_a_V}Mark name", "{=T_a}Text"), (plan.Rows[0].Name, plan.Rows[0].Text));
        Assert.AreEqual(("{=N_b}Name", "{=T_b}Text"), (plan.Rows[1].Name, plan.Rows[1].Text));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("boons.json")));
    }

    [TestMethod]
    public void WordsWithNoName_AreSkippedWithOneWarning_AndTheBoonKeepsItsOwnWords()
    {
        var plan = Plan(File(Row("a", extra: ",'cultures':{'vlandia':{'text':'{=T_a_V}Mark text'}}")));

        Assert.AreEqual(("{=N_a}Name", "{=T_a}Text"), (plan.Rows.Single().Name, plan.Rows.Single().Text));
        AssertOneWarningNaming("boons[0].cultures.vlandia");
    }

    [TestMethod]
    public void WordsWhoseTextIsNoText_KeepTheirName_AndTheRowsOwnText_WithOneWarning()
    {
        var plan = Plan(File(Row("a", extra: ",'cultures':{'vlandia':{'name':'{=N_a_V}Mark name','text':7}}")));

        Assert.AreEqual(("{=N_a_V}Mark name", "{=T_a}Text"), (plan.Rows.Single().Name, plan.Rows.Single().Text));
        AssertOneWarningNaming("boons[0].cultures.vlandia.text");
    }

    [TestMethod]
    public void ACulturesBlockThatIsNoObject_IsOneWarning_AndTheBoonStays()
    {
        var plan = Plan(File(Row("a", extra: ",'cultures':7")));

        Assert.AreEqual("{=N_a}Name", plan.Rows.Single().Name);
        AssertOneWarningNaming("boons[0].cultures");
    }

    // The deck draws by id, file order and weight. Words change none of them, so a seed gives the same cards at every fortress.
    [TestMethod]
    public void For_ChangesWordsOnly_SoIdsOrderWeightsAndDrawbacksAreTheSameForEveryCulture()
    {
        var words = new System.Collections.Generic.Dictionary<string, BoonWords>(System.StringComparer.OrdinalIgnoreCase) { ["gondor"] = new BoonWords("{=G}City", "{=GT}City text") };
        var cut = new BoonRow("cut", BoonCategory.Legend, "{=N}Name", "{=T}Text", new[] { new BoonEffect("affixCount", -1f) }) { Cultures = words };
        var cost = new BoonRow("cost", BoonCategory.WarChest, "{=N2}Name", "{=T2}Text", new[] { new BoonEffect("pricePercent", -20f) })
            { Drawback = new[] { new BoonEffect("earlyGoldPercent", -50f) }, DrawbackText = "{=D}Half the horn" };
        var plan = new BoonPlan(new[] { cut, cost }, 3);

        var gondor = plan.For("GONDOR");

        CollectionAssert.AreEqual(new[] { "cut", "cost" }, gondor.Rows.Select(r => r.Id).ToArray());
        Assert.AreEqual(("{=G}City", "{=GT}City text"), (gondor.Rows[0].Name, gondor.Rows[0].Text));
        Assert.AreEqual(plan.WeightOf(cut), gondor.WeightOf(gondor.Rows[0]));
        Assert.AreEqual(cut.Effects.Single(), gondor.Rows[0].Effects.Single());
        Assert.AreEqual(cost, gondor.Rows[1], "a row with no words for the culture is the same row");
        Assert.AreEqual("{=N}Name", plan.For("vlandia").Rows[0].Name);
    }
}

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>boons.json, the troop and class keys (phase2-plan.md section 2): a stat, damage or health key names its <c>target</c>, which is one of four words, in an effect and in a drawback.</summary>
[TestClass]
public class LongDefeatBoonsTargetReaderTests
{
    private const string Fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard'," +
        "'playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[{'troop':'d_inf','class':'Infantry','count':100}],'keep':null}";
    private const string Waves = "{'rules':{},'bounties':{'a':1},'rosters':{'r':{'scaling':{},'waves':[{'count':110,'mix':[{'troop':'a','class':'Infantry','weight':1}]}]}}}";
    private const string Shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','name':'M','troop':'d_inf','class':'Infantry','packSize':12,'price':180}]}}}";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private BoonPlan Plan(string effects, string extra = "", string category = "infantry")
    {
        var row = "{'id':'a','category':'" + category + "','name':'{=N_a}Name','text':'{=T_a}Text','effects':" + effects + extra + "}";
        var boons = "{'legendWeight':1,'boons':[" + row + ",{'id':'z','category':'warchest','name':'{=N_z}Z','text':'{=T_z}Z','effects':[{'key':'gold','value':5}]}]}";
        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + Fortress + "]}", Waves, Shop, _logger, boons: boons);
        return catalog.BuildPlan(catalog.Fortresses.Single(), RunOptions.Default).Boons;
    }

    [DataTestMethod]
    [DataRow("swingSpeedPercent")]
    [DataRow("moveSpeedPercent")]
    [DataRow("drawSpeedPercent")]
    [DataRow("reloadSpeedPercent")]
    [DataRow("blockPercent")]
    [DataRow("parryPercent")]
    [DataRow("aimErrorPercent")]
    [DataRow("damagePercent")]
    [DataRow("damageTakenPercent")]
    [DataRow("healthPercent")]
    public void EveryTroopAndClassKey_IsInTheVocabulary_AndTakesItsTarget(string key)
    {
        var plan = Plan("[{'key':'" + key + "','value':10,'target':'cavalry'}]");

        Assert.AreEqual(new BoonEffect(key, 10f, BoonTarget.Cavalry), plan.Find("a")!.Effects.Single());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("boons.json")));
    }

    [DataTestMethod]
    [DataRow("infantry", "Infantry")]
    [DataRow("archers", "Archers")]
    [DataRow("cavalry", "Cavalry")]
    [DataRow("player", "Player")]
    public void TheFourTargetWords_ParseToTheirTarget(string word, string expected)
    {
        var plan = Plan("[{'key':'healthPercent','value':20,'target':'" + word + "'}]");

        Assert.AreEqual(expected, plan.Find("a")!.Effects.Single().Target.ToString());
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(",'target':'horses'")]
    [DataRow(",'target':'Infantry'")]
    [DataRow(",'target':7")]
    public void ATargetedKey_WithNoTargetOrABadOne_SkipsThatEffect_WithOneWarning(string target)
    {
        var plan = Plan("[{'key':'healthPercent','value':20" + target + "},{'key':'damagePercent','value':5,'target':'archers'}]");

        Assert.AreEqual("damagePercent", plan.Find("a")!.Effects.Single().Key);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("boons.json") && m.Contains("effects[0]") && m.Contains("target")));
    }

    [TestMethod]
    public void AKeyThatNamesNoWhom_IgnoresATarget_AndKeepsNone()
    {
        var plan = Plan("[{'key':'bountyPercent','value':10,'target':'infantry'}]", category: "warchest");

        Assert.IsNull(plan.Find("a")!.Effects.Single().Target);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("boons.json")));
    }

    [TestMethod]
    public void ADrawback_MayAimAtATarget_AndNeedsOneToo()
    {
        var plan = Plan("[{'key':'damagePercent','value':20,'target':'infantry'}]",
            ",'drawback':[{'key':'damageTakenPercent','value':10,'target':'infantry'},{'key':'moveSpeedPercent','value':-5}],'drawbackText':'{=D_a}Costs.'");

        var drawback = plan.Find("a")!.Drawback;
        Assert.AreEqual(new BoonEffect("damageTakenPercent", 10f, BoonTarget.Infantry), drawback.Single());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("drawback[1]") && m.Contains("target")));
    }

    [TestMethod]
    public void AValueOutOfRange_StillSkipsTheEffect_BeforeTheTargetIsRead()
    {
        var plan = Plan("[{'key':'healthPercent','value':301,'target':'player'},{'key':'healthPercent','value':-91,'target':'player'},{'key':'healthPercent','value':50,'target':'player'}]");

        Assert.AreEqual(50f, plan.Find("a")!.Effects.Single().Value);
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("boons.json") && m.Contains(".value'")));
    }
}

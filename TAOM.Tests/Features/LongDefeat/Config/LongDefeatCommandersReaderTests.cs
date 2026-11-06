using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// The <c>commanders</c> block of a waves.json roster (phase2-plan.md section 3): every bad value costs one warning naming the file and the key; a bad rank or ability is skipped, a bad
/// number keeps its default, and a block whose <c>every</c> is bad, or that keeps no rank, gives no commanders at all.
/// </summary>
[TestClass]
public class LongDefeatCommandersReaderTests
{
    private const string Mix = "{'troop':'a_inf','class':'Infantry','weight':3},{'troop':'a_arc','class':'Ranged','weight':1}";
    private const string Wave = "{'count':110,'mix':[" + Mix + "]}";
    private const string Garrison = "{'troop':'d_inf','class':'Infantry','count':100}";
    private const string Offer = "{'id':'militia','name':'Militia','troop':'d_inf','class':'Infantry','packSize':12,'price':180}";

    private const string Ranks =
        "[{'rank':'captain','name':'{=R1}Captain','troop':'lord_a','bounty':150,'health':2.0,'abilities':['war_cry']}," +
        "{'rank':'lieutenant','name':'{=R2}Lieutenant','troop':'lord_b','bounty':250,'health':3.0,'abilities':['war_cry','summon']}]";

    private const string Abilities =
        "{'war_cry':{'kind':'WarCry','name':'{=A1}Cry','firstSeconds':20,'cooldownSeconds':60,'durationSeconds':15,'swingSpeedPercent':15,'moveSpeedPercent':10}," +
        "'summon':{'kind':'Summon','name':'{=A2}Summon','firstSeconds':30,'cooldownSeconds':120,'count':20,'mix':[{'troop':'s_inf','class':'Infantry','weight':3},{'troop':'s_arc','class':'Ranged','weight':1}]}}";

    private const string Beyond = "{'healthPerStep':0.25,'cooldownFactorPerStep':0.9,'summonPerStep':5,'maxSteps':10}";

    private const string Bounties = "'a_inf':4,'a_arc':6,'s_inf':20,'s_arc':25";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static string Commanders(string every = "4", string ranks = Ranks, string abilities = Abilities, string beyond = Beyond) =>
        "{'every':" + every + ",'ranks':" + ranks + ",'abilities':" + abilities + (beyond.Length == 0 ? "" : ",'beyond':" + beyond) + "}";

    private WavePlan Load(string? commanders, string bounties = Bounties)
    {
        var fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard','playerTroop':'lord_4_1','waves':'r','shop':'sh'," +
                       "'garrison':[" + Garrison + "],'keep':null}";
        var waves = "{'rules':{},'bounties':{" + bounties + "},'rosters':{'r':{'scaling':{'countPerWave':10,'maxCount':300}," +
                    (commanders == null ? "" : "'commanders':" + commanders + ",") + "'waves':[" + Wave + "]}}}";
        var shop = "{'shops':{'sh':{'startingGold':600,'offers':[" + Offer + "]}}}";
        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + fortress + "]}", waves, shop, _logger, "{}", "{'boons':[]}", "{'classes':[]}");
        return catalog.Fortresses.Single().Waves;
    }

    private void AssertOneWarningNaming(string key) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("waves.json") && m.Contains(key)));

    [TestMethod]
    public void AValidBlock_LoadsTheRanksTheAbilitiesAndTheStepNumbers_WithNoWarning()
    {
        var plan = Load(Commanders()).Commanders;

        Assert.AreEqual(4, plan.Every);
        CollectionAssert.AreEqual(new[] { "captain", "lieutenant" }, plan.Ranks.Select(r => r.Id).ToArray());
        var captain = plan.Ranks[0];
        Assert.AreEqual(("{=R1}Captain", "lord_a", 150, 2f), (captain.Name, captain.TroopId, captain.Bounty, captain.Health));
        CollectionAssert.AreEqual(new[] { "war_cry", "summon" }, plan.Ranks[1].AbilityIds.ToArray());
        var cry = plan.Abilities["war_cry"];
        Assert.AreEqual((CommanderAbilityKind.WarCry, 20f, 60f, 15f, 15f, 10f), (cry.Kind, cry.FirstSeconds, cry.CooldownSeconds, cry.DurationSeconds, cry.SwingSpeedPercent, cry.MoveSpeedPercent));
        var summon = plan.Abilities["summon"];
        Assert.AreEqual((CommanderAbilityKind.Summon, 20, 2), (summon.Kind, summon.Count, summon.Mix.Count));
        Assert.AreEqual(new CommanderBeyond(0.25f, 0.9f, 5, 10), plan.Beyond);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void NoBlock_MeansNoCommanders_WithNoWarning()
    {
        var plan = Load(null).Commanders;

        Assert.IsNull(plan.ForWave(4));
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("1")]
    [DataRow("21")]
    [DataRow("4.5")]
    [DataRow("'four'")]
    public void AnEveryOutsideTwoToTwenty_SkipsTheBlock_WithOneWarning(string every)
    {
        var plan = Load(Commanders(every: every)).Commanders;

        Assert.IsNull(plan.ForWave(4));
        Assert.IsNull(plan.ForWave(8));
        AssertOneWarningNaming("commanders.every");
    }

    [TestMethod]
    public void ARankWithNoTroop_ABadBounty_OrABadHealth_IsSkippedWithOneWarning()
    {
        var ranks = "[{'rank':'a','name':'{=R}A','bounty':1,'health':2,'abilities':[]}," +
                    "{'rank':'b','name':'{=R}B','troop':'lord_b','bounty':-1,'health':2,'abilities':[]}," +
                    "{'rank':'c','name':'{=R}C','troop':'lord_c','bounty':10,'health':0.5,'abilities':[]}," +
                    "{'rank':'d','name':'{=R}D','troop':'lord_d','bounty':10,'health':2,'abilities':['war_cry']}]";

        var plan = Load(Commanders(ranks: ranks)).Commanders;

        CollectionAssert.AreEqual(new[] { "d" }, plan.Ranks.Select(r => r.Id).ToArray());
        AssertOneWarningNaming("ranks[0].troop");
        AssertOneWarningNaming("ranks[1].bounty");
        AssertOneWarningNaming("ranks[2].health");
    }

    [TestMethod]
    public void ARankNamingAnAbilityTheBlockLacks_KeepsTheRankWithoutIt_WithOneWarning()
    {
        var ranks = "[{'rank':'a','name':'{=R}A','troop':'lord_a','bounty':1,'health':2,'abilities':['war_cry','nonsense']}]";

        var plan = Load(Commanders(ranks: ranks)).Commanders;

        CollectionAssert.AreEqual(new[] { "war_cry" }, plan.ForWave(4)!.Abilities.Select(a => a.Id).ToArray());
        AssertOneWarningNaming("ranks[0].abilities");
    }

    [TestMethod]
    public void AllRanksBad_SkipsTheBlock()
    {
        var ranks = "[{'rank':'a','name':'{=R}A','bounty':1,'health':2,'abilities':[]}]";

        var plan = Load(Commanders(ranks: ranks)).Commanders;

        Assert.IsNull(plan.ForWave(4));
    }

    [DataTestMethod]
    [DataRow("{'kind':'Heal','name':'{=A}X','firstSeconds':20,'cooldownSeconds':60}", "abilities.war_cry.kind")]
    [DataRow("{'kind':'WarCry','name':'{=A}X','firstSeconds':0,'cooldownSeconds':60,'durationSeconds':15}", "abilities.war_cry.firstSeconds")]
    [DataRow("{'kind':'WarCry','name':'{=A}X','firstSeconds':20,'cooldownSeconds':3601,'durationSeconds':15}", "abilities.war_cry.cooldownSeconds")]
    [DataRow("{'kind':'WarCry','name':'{=A}X','firstSeconds':NaN,'cooldownSeconds':60,'durationSeconds':15}", "abilities.war_cry.firstSeconds")]
    [DataRow("{'kind':'WarCry','name':'{=A}X','firstSeconds':20,'cooldownSeconds':60,'durationSeconds':Infinity}", "abilities.war_cry.durationSeconds")]
    [DataRow("{'kind':'WarCry','name':'{=A}X','firstSeconds':20,'cooldownSeconds':60,'durationSeconds':60}", "abilities.war_cry.durationSeconds")]
    [DataRow("{'kind':'WarCry','firstSeconds':20,'cooldownSeconds':60,'durationSeconds':15}", "abilities.war_cry.name")]
    public void ABadWarCry_IsSkipped_WithOneWarning(string cry, string key)
    {
        var abilities = "{'war_cry':" + cry + "}";
        var ranks = "[{'rank':'a','name':'{=R}A','troop':'lord_a','bounty':1,'health':2,'abilities':['war_cry']}]";

        var plan = Load(Commanders(ranks: ranks, abilities: abilities)).Commanders;

        Assert.AreEqual(0, plan.ForWave(4)!.Abilities.Count);
        AssertOneWarningNaming(key);
    }

    [DataTestMethod]
    [DataRow("0")]
    [DataRow("51")]
    [DataRow("2.5")]
    public void ASummonCountOutsideOneToFifty_SkipsTheAbility_WithOneWarning(string count)
    {
        var abilities = "{'summon':{'kind':'Summon','name':'{=A}S','firstSeconds':30,'cooldownSeconds':120,'count':" + count + ",'mix':[{'troop':'s_inf','class':'Infantry','weight':1}]}}";
        var ranks = "[{'rank':'a','name':'{=R}A','troop':'lord_a','bounty':1,'health':2,'abilities':['summon']}]";

        var plan = Load(Commanders(ranks: ranks, abilities: abilities)).Commanders;

        Assert.AreEqual(0, plan.ForWave(4)!.Abilities.Count);
        AssertOneWarningNaming("abilities.summon.count");
    }

    [TestMethod]
    public void ASummonMix_SkipsItsBadRows_AndAnAbilityWithNoRowLeft()
    {
        var abilities = "{'summon':{'kind':'Summon','name':'{=A}S','firstSeconds':30,'cooldownSeconds':120,'count':10," +
                        "'mix':[{'troop':'s_inf','class':'Cavalry','weight':1},{'class':'Infantry','weight':1},{'troop':'s_arc','class':'Ranged','weight':1}]}," +
                        "'empty':{'kind':'Summon','name':'{=A}E','firstSeconds':30,'cooldownSeconds':120,'count':10,'mix':[]}}";
        var ranks = "[{'rank':'a','name':'{=R}A','troop':'lord_a','bounty':1,'health':2,'abilities':['summon','empty']}]";

        var plan = Load(Commanders(ranks: ranks, abilities: abilities)).Commanders;

        var abilitiesLeft = plan.ForWave(4)!.Abilities;
        Assert.AreEqual("summon", abilitiesLeft.Single().Id);
        CollectionAssert.AreEqual(new[] { "s_arc" }, abilitiesLeft.Single().Mix.Select(m => m.TroopId).ToArray());
        AssertOneWarningNaming("abilities.summon.mix[0].class");
        AssertOneWarningNaming("abilities.summon.mix[1].troop");
        AssertOneWarningNaming("abilities.empty.mix");
    }

    [TestMethod]
    public void AWarCryPercentOutOfRange_IsDroppedWithOneWarning_AndTheCryStillLoads()
    {
        var abilities = "{'war_cry':{'kind':'WarCry','name':'{=A}X','firstSeconds':20,'cooldownSeconds':60,'durationSeconds':15,'swingSpeedPercent':301,'moveSpeedPercent':10}}";
        var ranks = "[{'rank':'a','name':'{=R}A','troop':'lord_a','bounty':1,'health':2,'abilities':['war_cry']}]";

        var cry = Load(Commanders(ranks: ranks, abilities: abilities)).Commanders.Abilities["war_cry"];

        Assert.AreEqual((0f, 10f), (cry.SwingSpeedPercent, cry.MoveSpeedPercent));
        AssertOneWarningNaming("abilities.war_cry.swingSpeedPercent");
    }

    [TestMethod]
    public void BadStepNumbers_KeepTheirDefaults_WithOneWarningEach()
    {
        var beyond = "{'healthPerStep':-1,'cooldownFactorPerStep':'fast','summonPerStep':51,'maxSteps':101}";

        var plan = Load(Commanders(beyond: beyond)).Commanders;

        Assert.AreEqual(new CommanderBeyond(0.25f, 0.9f, 5, 10), plan.Beyond);
        AssertOneWarningNaming("beyond.healthPerStep");
        AssertOneWarningNaming("beyond.cooldownFactorPerStep");
        AssertOneWarningNaming("beyond.summonPerStep");
        AssertOneWarningNaming("beyond.maxSteps");
    }

    [TestMethod]
    public void NoBeyondBlock_TakesTheDefaults_WithNoWarning()
    {
        var plan = Load(Commanders(beyond: "")).Commanders;

        Assert.AreEqual(new CommanderBeyond(0.25f, 0.9f, 5, 10), plan.Beyond);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ASummonTroopWithNoBounty_PaysNothingAndIsWarnedAboutOnce()
    {
        var plan = Load(Commanders(), bounties: "'a_inf':4,'a_arc':6,'s_inf':20");

        Assert.IsNotNull(plan.Commanders.ForWave(8));
        AssertOneWarningNaming("'s_arc'");
    }
}

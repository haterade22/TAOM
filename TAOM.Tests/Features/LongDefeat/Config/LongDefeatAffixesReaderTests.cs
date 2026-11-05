using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// affixes.json (phase2-plan.md section 3): a bad schedule keeps the shipped one, a bad number keeps its default, a name table that lacks powers
/// costs one warning naming them, and the plan a fortress gets carries the names of its attacker culture. Single quotes keep the fixtures readable.
/// </summary>
[TestClass]
public class LongDefeatAffixesReaderTests
{
    private const string Fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard'," +
        "'playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[{'troop':'d_inf','class':'Infantry','count':100}],'keep':null}";
    private const string Waves = "{'rules':{},'bounties':{'a':1},'rosters':{'r':{'scaling':{},'waves':[{'count':110,'mix':[{'troop':'a','class':'Infantry','weight':1}]}]}}}";
    private const string Shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','name':'M','troop':'d_inf','class':'Infantry','packSize':12,'price':180}]}}}";

    private const string AllNames = "'lash':'{=A1}L','drums':'{=A2}D','blades':'{=A3}B','eye':'{=A4}E','discipline':'{=A5}I','hide':'{=A6}H','terror':'{=A7}T','hammer':'{=A8}M'";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private RunPlan Plan(string? affixes, string culture = "isengard")
    {
        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + Fortress.Replace("isengard", culture) + "]}", Waves, Shop, _logger, affixes);
        return catalog.BuildPlan(catalog.Fortresses.Single(), RunOptions.Default);
    }

    private static string File(string schedule = "'schedule':[{'fromWave':5,'count':1},{'fromWave':10,'count':2}]", string powers = "'powers':{}",
        string names = "'names':{'isengard':{" + AllNames + "}}") => "{" + schedule + "," + powers + "," + names + "}";

    private void AssertOneWarningNaming(string key) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("affixes.json") && m.Contains(key)));

    [TestMethod]
    public void ValidFile_GivesTheScheduleThePowersAndTheCulturesNames_WithNoWarning()
    {
        var plan = Plan(File(powers: "'powers':{'lash':{'swingSpeedPercent':15,'attackEagernessPercent':25},'terror':{'radius':8.0,'pulseSeconds':2.0,'amount':3.0,'floor':35.0}}")).Affixes;

        Assert.AreEqual(1, plan.CountFor(5));
        Assert.AreEqual(2, plan.CountFor(10));
        Assert.AreEqual(2, plan.CountFor(40), "the last row holds");
        Assert.AreEqual(15f, plan.Value("lash", "swingSpeedPercent"));
        Assert.AreEqual(25f, plan.Value("lash", "attackEagernessPercent"));
        Assert.AreEqual(35f, plan.Value("terror", "floor"));
        Assert.AreEqual("{=A6}H", plan.NameOf("hide"));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void TheNamesAreTheFortressesAttackerCulturesTable_NotAnotherCulturesNor_ACaseVariantsMiss()
    {
        var text = File(names: "'names':{'isengard':{" + AllNames + "},'mordor':{" + AllNames.Replace("'{=A", "'{=Z") + "}}");

        Assert.AreEqual("{=A6}H", Plan(text).Affixes.NameOf("hide"));
        Assert.AreEqual("{=Z6}H", Plan(text, culture: "mordor").Affixes.NameOf("hide"));
        Assert.AreEqual("{=A6}H", Plan(text, culture: "Isengard").Affixes.NameOf("hide"), "culture ids compare without case, as the fortress reader does");
    }

    [TestMethod]
    public void ACultureWithNoTable_ShowsIds_WithOneWarningPerCulture()
    {
        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + Fortress.Replace("isengard", "gondor") + "]}", Waves, Shop, _logger, File());
        var plan = catalog.BuildPlan(catalog.Fortresses.Single(), RunOptions.Default);
        catalog.BuildPlan(catalog.Fortresses.Single(), RunOptions.Default);

        Assert.IsNull(plan.Affixes.NameOf("hide"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("affixes.json") && m.Contains("names.gondor")));
    }

    [TestMethod]
    public void ANameTableThatLacksPowers_CostsOneWarningNamingThem()
    {
        var plan = Plan(File(names: "'names':{'isengard':{'lash':'{=A1}L','hide':'{=A6}H'}}")).Affixes;

        Assert.AreEqual("{=A1}L", plan.NameOf("lash"));
        Assert.IsNull(plan.NameOf("drums"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("names.isengard") && m.Contains("drums") && m.Contains("blades")));
    }

    [TestMethod]
    public void AMissingFile_MeansNoAffixAtAll_WithOneWarning()
    {
        var plan = Plan(null).Affixes;

        Assert.AreEqual(0, plan.CountFor(50));
        Assert.AreEqual(0, plan.Roll(1, 50).Count);
        AssertOneWarningNaming("was not found");
    }

    [TestMethod]
    public void AnAbsentSchedule_IsTheShippedOne_WithNoWarning()
    {
        var plan = Plan("{'powers':{},'names':{'isengard':{" + AllNames + "}}}").Affixes;

        Assert.AreEqual(1, plan.CountFor(5));
        Assert.AreEqual(2, plan.CountFor(10));
        Assert.AreEqual(3, plan.CountFor(15));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [DataTestMethod]
    [DataRow("'schedule':{}")]
    [DataRow("'schedule':[{'fromWave':5,'count':9}]")]
    [DataRow("'schedule':[{'fromWave':0,'count':1}]")]
    [DataRow("'schedule':[{'fromWave':5,'count':1.5}]")]
    [DataRow("'schedule':[{'fromWave':5,'count':1},{'fromWave':5,'count':2}]")]
    [DataRow("'schedule':[{'fromWave':10,'count':1},{'fromWave':5,'count':2}]")]
    [DataRow("'schedule':[{'fromWave':5}]")]
    [DataRow("'schedule':[7]")]
    public void ABadSchedule_KeepsTheShippedOne_WithOneWarning(string schedule)
    {
        var plan = Plan(File(schedule: schedule)).Affixes;

        Assert.AreEqual(1, plan.CountFor(5), schedule);
        Assert.AreEqual(3, plan.CountFor(15), schedule);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("affixes.json") && m.Contains("schedule")));
    }

    [TestMethod]
    public void AScheduleWithAZeroCount_IsValid()
    {
        var plan = Plan(File(schedule: "'schedule':[{'fromWave':5,'count':0},{'fromWave':8,'count':1}]")).Affixes;

        Assert.AreEqual(0, plan.CountFor(5));
        Assert.AreEqual(1, plan.CountFor(8));
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [DataTestMethod]
    [DataRow("'lash':{'swingSpeedPercent':400}", "swingSpeedPercent")]
    [DataRow("'lash':{'swingSpeedPercent':-95}", "swingSpeedPercent")]
    [DataRow("'lash':{'swingSpeedPercent':'fast'}", "swingSpeedPercent")]
    [DataRow("'lash':{'swingSpeedPercent':NaN}", "swingSpeedPercent")]
    [DataRow("'lash':{'swingSpeedPercent':Infinity}", "swingSpeedPercent")]
    [DataRow("'hide':{'armourPercent':301}", "armourPercent")]
    [DataRow("'terror':{'floor':120}", "floor")]
    [DataRow("'terror':{'radius':0}", "radius")]
    [DataRow("'blades':{'poisonSeconds':0}", "poisonSeconds")]
    public void ABadPowerNumber_IsRefused_WithOneWarning_AndTheDefaultHolds(string power, string key)
    {
        var plan = Plan(File(powers: "'powers':{" + power + "}")).Affixes;

        var power1 = power.Split(':')[0].Trim('\'');
        var defaults = new System.Collections.Generic.Dictionary<string, float> { ["floor"] = 35f, ["radius"] = 8f, ["poisonSeconds"] = 6f };
        Assert.AreEqual(defaults.TryGetValue(key, out var d) ? d : 0f, plan.Value(power1, key), key);
        AssertOneWarningNaming(key);
    }

    [TestMethod]
    public void APowerThatIsNotOneOfTheEight_IsIgnored_WithOneWarning()
    {
        Plan(File(powers: "'powers':{'frost':{'swingSpeedPercent':15}}"));

        AssertOneWarningNaming("frost");
    }

    [TestMethod]
    public void APowerLeftOutOfTheFile_ReadsAsItsDefaults_AndANonObjectPowerCostsOneWarning()
    {
        var plan = Plan(File(powers: "'powers':{'lash':7}")).Affixes;

        Assert.AreEqual(0f, plan.Value("lash", "swingSpeedPercent"));
        Assert.AreEqual(35f, plan.Value("terror", "floor"), "terror and blades default to the shipped numbers when left out");
        AssertOneWarningNaming("powers.lash");
    }

    [TestMethod]
    public void ARunPlan_BuiltFromAFortress_CarriesTheAffixPlan()
    {
        var plan = Plan(File());

        Assert.AreEqual(1, plan.Affixes.CountFor(5));
        Assert.AreNotSame(AffixPlan.None, plan.Affixes);
    }

    [TestMethod]
    public void AnAffixesFileThatIsNotJson_LoadsNothing_WithoutThrowing()
    {
        var plan = Plan("not json").Affixes;

        Assert.AreEqual(0, plan.CountFor(20));
        AssertOneWarningNaming("affixes.json");
    }
}

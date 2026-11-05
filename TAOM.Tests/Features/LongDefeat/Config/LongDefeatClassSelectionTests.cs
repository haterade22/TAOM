using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// How the catalog turns <c>RunOptions.ClassId</c> into the plan's loadout: the class asked for, else the bot's seeded draw or the first class on offer, a class the fortress's
/// culture has no kit for left off, and the resolved id written back into the options so the report names the class that was played.
/// </summary>
[TestClass]
public class LongDefeatClassSelectionTests
{
    private const string Fortress = "{'id':'helms','name':'{=K}Helm','scene':'s','defenderCulture':'vlandia','attackerCulture':'isengard'," +
        "'playerTroop':'lord_4_1','waves':'r','shop':'sh','garrison':[{'troop':'d_inf','class':'Infantry','count':100}],'keep':null}";
    private const string Waves = "{'rules':{},'bounties':{'a':1},'rosters':{'r':{'scaling':{},'waves':[{'count':110,'mix':[{'troop':'a','class':'Infantry','weight':1}]}]}}}";
    private const string Shop = "{'shops':{'sh':{'startingGold':600,'offers':[{'id':'m','name':'M','troop':'d_inf','class':'Infantry','packSize':12,'price':180}]}}}";
    private const string Affixes = "{'names':{'isengard':{'lash':'{=A1}L','drums':'{=A2}D','blades':'{=A3}B','eye':'{=A4}E','discipline':'{=A5}I','hide':'{=A6}H','terror':'{=A7}T','hammer':'{=A8}M'}}}";

    private const string Classes = "{'classes':[" +
        "{'id':'sword','name':'{=N1}Sword','text':'{=T1}t','mounted':false,'kits':{'vlandia':{'Item0':'sword_a','Item1':'shield_a'}}}," +
        "{'id':'axe','name':'{=N2}Axe','text':'{=T2}t','mounted':false,'kits':{'vlandia':{'Item0':'axe_a'}}}," +
        "{'id':'bow','name':'{=N3}Bow','text':'{=T3}t','mounted':false,'kits':{'vlandia':{'Item0':'bow_a'}}}," +
        "{'id':'gondor_only','name':'{=N4}G','text':'{=T4}t','mounted':false,'kits':{'gondor':{'Item0':'g'}}}]}";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private RunPlan Plan(RunOptions options, string? classes = Classes)
    {
        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + Fortress + "]}", Waves, Shop, _logger, Affixes, "{'boons':[]}", classes);
        return catalog.BuildPlan(catalog.Fortresses.Single(), options);
    }

    [TestMethod]
    public void ClassesFor_ListsTheClassesWithAKitForTheFortressesDefenderCulture_InFileOrder()
    {
        var catalog = LongDefeatConfigCatalog.FromTexts("{'fortresses':[" + Fortress + "]}", Waves, Shop, _logger, Affixes, "{'boons':[]}", Classes);

        CollectionAssert.AreEqual(new[] { "sword", "axe", "bow" }, catalog.ClassesFor(catalog.Fortresses.Single()).Select(c => c.Id).ToArray());
    }

    [TestMethod]
    public void AnAskedClass_BecomesThePlansLoadout_AndIsWrittenBackIntoTheOptions()
    {
        var plan = Plan(RunOptions.Default with { ClassId = "axe" });

        Assert.AreEqual("axe", plan.Loadout!.ClassId);
        Assert.AreEqual("axe", plan.Options.ClassId);
        CollectionAssert.AreEqual(new[] { "Item0=axe_a", "Horse=", "HorseHarness=" }, plan.Loadout.Slots.Select(s => s.Slot + "=" + s.ItemId).ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("classes.json") || m.Contains("class '")));
    }

    [TestMethod]
    public void NoClassAsked_ForAPerson_TakesTheFirstClassOnOffer()
    {
        var plan = Plan(RunOptions.Default);

        Assert.AreEqual("sword", plan.Loadout!.ClassId);
        Assert.AreEqual("sword", plan.Options.ClassId);
    }

    [TestMethod]
    public void NoClassAsked_ForTheBot_TakesTheSeedsDraw()
    {
        // seed 2 draws index 2 of three classes on offer (the vectors pinned in ClassPlanTests), and gondor_only is not on offer.
        var plan = Plan(RunOptions.Default with { Autoplay = true, Seed = 2UL });

        Assert.AreEqual("bow", plan.Loadout!.ClassId);
    }

    [TestMethod]
    public void AClassTheFortressCultureHasNoKitFor_IsRefusedWithOneWarning_AndTheDefaultTaken()
    {
        var plan = Plan(RunOptions.Default with { ClassId = "gondor_only" });

        Assert.AreEqual("sword", plan.Loadout!.ClassId);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("gondor_only") && m.Contains("vlandia")));
    }

    [TestMethod]
    public void AnUnknownClass_Cavalry_BeforeItIsShipped_IsRefusedWithOneWarning_AndTheDefaultTaken()
    {
        var plan = Plan(RunOptions.Default with { ClassId = "cavalry", Autoplay = true, Seed = 0UL });

        Assert.AreEqual("axe", plan.Loadout!.ClassId, "seed 0 draws index 1");
        Assert.AreEqual("axe", plan.Options.ClassId, "the report names the class that was played, not the one asked for");
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("cavalry")));
    }

    [TestMethod]
    public void NoClassesFile_LeavesTheLordInHisOwnGear_AndTheOptionsWithoutAClass()
    {
        var plan = Plan(RunOptions.Default with { ClassId = "axe" }, classes: "{'classes':[]}");

        Assert.IsNull(plan.Loadout);
        Assert.IsNull(plan.Options.ClassId);
    }
}

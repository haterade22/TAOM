using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Config;

/// <summary>
/// classes.json (phase2-plan.md section 3): classes in file order, a kit per defender culture keyed by the XML slot names, one warning per bad value naming
/// file and key, and the rules a kit must meet (a foot class never sets a horse slot, a mounted class sets both). Single quotes keep the fixtures readable.
/// </summary>
[TestClass]
public class LongDefeatClassesReaderTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private ClassPlan Plan(string? classes) => LongDefeatConfigCatalog.FromTexts(null, null, null, _logger, classes: classes).Classes;

    private static string Row(string id = "sword", string kits = "{'vlandia':{'Item0':'sword_a','Item1':'shield_a'}}", string mounted = "false", string extra = "") =>
        "{'id':'" + id + "','name':'{=N_" + id + "}Name','text':'{=T_" + id + "}Text','mounted':" + mounted + ",'kits':" + kits + extra + "}";

    private static string File(params string[] rows) => "{'classes':[" + string.Join(",", rows) + "]}";

    private void AssertOneWarningNaming(string text) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("classes.json") && m.Contains(text)));

    [TestMethod]
    public void AValidFile_GivesTheClassesInFileOrder_WithTheirKits_AndNoWarning()
    {
        var plan = Plan(File(Row("sword"), Row("axe", "{'vlandia':{}}"), Row("rider", "{'vlandia':{'Item0':'spear','Horse':'horse_a','HorseHarness':'harness_a'}}", "true")));

        CollectionAssert.AreEqual(new[] { "sword", "axe", "rider" }, plan.Classes.Select(c => c.Id).ToArray());
        Assert.AreEqual("{=N_sword}Name", plan.Classes[0].Name);
        Assert.AreEqual("{=T_sword}Text", plan.Classes[0].Text);
        Assert.IsFalse(plan.Classes[0].Mounted);
        Assert.IsTrue(plan.Classes[2].Mounted);
        CollectionAssert.AreEqual(new[] { "Item0=sword_a", "Item1=shield_a" }, plan.Classes[0].Kits["vlandia"].Slots.Select(s => s.Slot + "=" + s.ItemId).ToArray());
        Assert.AreEqual(0, plan.Classes[1].Kits["vlandia"].Slots.Count, "an empty kit is the lord's own gear");
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("classes.json")));
    }

    [TestMethod]
    public void AnEmptyStringClearsASlot_AndIsKept()
    {
        var plan = Plan(File(Row(kits: "{'vlandia':{'Item2':'','Item3':''}}")));

        CollectionAssert.AreEqual(new[] { "Item2=", "Item3=" }, plan.Classes[0].Kits["vlandia"].Slots.Select(s => s.Slot + "=" + s.ItemId).ToArray());
    }

    [TestMethod]
    public void AMissingFile_LoadsNothing_WithOneWarning()
    {
        var plan = Plan(null);

        Assert.AreEqual(0, plan.Classes.Count);
        AssertOneWarningNaming("not found");
    }

    [DataTestMethod]
    [DataRow("not json at all")]
    [DataRow("[1,2]")]
    public void AFileThatIsNotAJsonObject_LoadsNothing_WithoutThrowing(string text)
    {
        Assert.AreEqual(0, Plan(text).Classes.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("classes.json")));
    }

    [TestMethod]
    public void ClassesThatIsNotAnArray_LoadsNothing_WithOneWarningNamingTheKey()
    {
        Assert.AreEqual(0, Plan("{'classes':{}}").Classes.Count);
        AssertOneWarningNaming("'classes'");
    }

    [TestMethod]
    public void AClassWithNoIdNameOrText_IsSkipped_AndTheRestKept()
    {
        var plan = Plan(File("{'id':'a','name':'{=N}n','kits':{'vlandia':{}}}", "{'name':'{=N}n','text':'{=T}t','kits':{'vlandia':{}}}", Row("ok")));

        CollectionAssert.AreEqual(new[] { "ok" }, plan.Classes.Select(c => c.Id).ToArray());
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("classes.json") && m.Contains("'id', a 'name' and a 'text'")));
    }

    [TestMethod]
    public void ARepeatedId_KeepsTheFirst_AndWarnsOnce()
    {
        var plan = Plan(File(Row("a", "{'vlandia':{'Item0':'first'}}"), Row("a", "{'vlandia':{'Item0':'second'}}")));

        Assert.AreEqual("first", plan.Classes.Single().Kits["vlandia"].Slots.Single().ItemId);
        AssertOneWarningNaming("repeats the id 'a'");
    }

    [TestMethod]
    public void AnUnknownSlot_IsSkipped_WithOneWarningNamingIt_AndTheKitStaysOtherwise()
    {
        var plan = Plan(File(Row(kits: "{'vlandia':{'Item0':'a','Weapon0':'b','Item9':'c'}}")));

        CollectionAssert.AreEqual(new[] { "Item0" }, plan.Classes.Single().Kits["vlandia"].Slots.Select(s => s.Slot).ToArray());
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("classes.json") && (m.Contains("Weapon0") || m.Contains("Item9"))));
    }

    [TestMethod]
    public void ASlotValueThatIsNotAString_IsSkipped_WithOneWarning()
    {
        var plan = Plan(File(Row(kits: "{'vlandia':{'Item0':5,'Item1':'shield_a'}}")));

        CollectionAssert.AreEqual(new[] { "Item1" }, plan.Classes.Single().Kits["vlandia"].Slots.Select(s => s.Slot).ToArray());
        AssertOneWarningNaming("Item0");
    }

    [TestMethod]
    public void AFootClassThatSetsAHorseSlot_LosesThatSlot_WithOneWarning()
    {
        var plan = Plan(File(Row(kits: "{'vlandia':{'Item0':'a','Horse':'horse_a'}}")));

        CollectionAssert.AreEqual(new[] { "Item0" }, plan.Classes.Single().Kits["vlandia"].Slots.Select(s => s.Slot).ToArray());
        AssertOneWarningNaming("Horse");
    }

    [DataTestMethod]
    [DataRow("{'vlandia':{'Item0':'a','Horse':'horse_a'}}")]
    [DataRow("{'vlandia':{'Item0':'a','HorseHarness':'harness_a'}}")]
    [DataRow("{'vlandia':{'Item0':'a','Horse':'','HorseHarness':'harness_a'}}")]
    public void AMountedClassMissingAHorseOrAHarness_IsSkippedWhole_WithOneWarning(string kits)
    {
        var plan = Plan(File(Row("rider", kits, "true"), Row("ok")));

        CollectionAssert.AreEqual(new[] { "ok" }, plan.Classes.Select(c => c.Id).ToArray());
        AssertOneWarningNaming("'rider'");
    }

    [TestMethod]
    public void AClassWithNoKitObject_Or_NoValidKit_IsSkipped_WithOneWarning()
    {
        var plan = Plan(File("{'id':'a','name':'{=N}n','text':'{=T}t'}", Row("b", "{}"), Row("ok")));

        CollectionAssert.AreEqual(new[] { "ok" }, plan.Classes.Select(c => c.Id).ToArray());
        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("classes.json") && m.Contains("kit")));
    }

    [TestMethod]
    public void MountedThatIsNotABoolean_KeepsFalse_WithOneWarning()
    {
        var plan = Plan(File(Row(mounted: "'yes'")));

        Assert.IsFalse(plan.Classes.Single().Mounted);
        AssertOneWarningNaming("mounted");
    }

    [TestMethod]
    public void TwoCultures_EachKeepTheirOwnKit_AndTheClassIsOnOfferForBoth()
    {
        var plan = Plan(File(Row(kits: "{'vlandia':{'Item0':'a'},'gondor':{'Item0':'b'}}")));

        Assert.AreEqual("a", plan.OnOffer("vlandia").Single().LoadoutFor("vlandia")!.Slots[0].ItemId);
        Assert.AreEqual("b", plan.OnOffer("gondor").Single().LoadoutFor("gondor")!.Slots[0].ItemId);
    }
}

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.CoopCompat.LoadOrder;

namespace TAOM.CoopCompat.Tests.LoadOrder;

/// <summary>Which modules a load order check compares, and where two orders first differ. No game state.</summary>
[TestClass]
public class LoadOrderRulesTests
{
    private static ModuleEntry Module(string id, bool official = false, bool data = false) => new ModuleEntry(id, id + " name", official, data);

    private static readonly ModuleEntry[] Host =
    {
        Module("TAOM.Dependencies"), Module("Native", official: true, data: true), Module("SandBoxCore", official: true, data: true),
        Module("TAOM_Map"), Module("LOTRLOME_Armory"), Module("TAOM"), Module("Coop"), Module("RBM", data: true), Module("UIOnly"),
    };

    [TestMethod]
    public void Compared_TheTaomBlockAndNonOfficialDataModules_Only()
    {
        var compared = LoadOrderRules.Compared(Host);

        Assert.IsTrue(compared.Contains("TAOM"));
        Assert.IsTrue(compared.Contains("TAOM_Map"));
        Assert.IsTrue(compared.Contains("TAOM_AIO_Compat"), "the TAOM block is compared even when the host lacks a member");
        Assert.IsTrue(compared.Contains("RBM"), "a community module with object data");
        Assert.IsFalse(compared.Contains("Native"), "an official module keeps the game's own order");
        Assert.IsFalse(compared.Contains("UIOnly"), "a module without object data cannot change items or troops");
        Assert.IsFalse(compared.Contains("Coop"));
    }

    [TestMethod]
    public void TryFindDifference_SameOrder_IsFalse()
    {
        var order = new[] { "TAOM_Map", "LOTRLOME_Armory", "TAOM", "RBM" };
        var compared = LoadOrderRules.Compared(Host);

        Assert.IsFalse(LoadOrderRules.TryFindDifference(order, order, compared, out var first, out var second));
        Assert.IsNull(first);
        Assert.IsNull(second);
    }

    [TestMethod]
    public void TryFindDifference_SwappedPair_NamesTheHostFirstThenSecond()
    {
        var host = new[] { "TAOM_Map", "LOTRLOME_Armory", "TAOM", "RBM" };
        var player = new[] { "TAOM_Map", "RBM", "LOTRLOME_Armory", "TAOM" };

        Assert.IsTrue(LoadOrderRules.TryFindDifference(host, player, LoadOrderRules.Compared(Host), out var first, out var second));
        Assert.AreEqual("LOTRLOME_Armory", first);
        Assert.AreEqual("RBM", second);
    }

    [TestMethod]
    public void TryFindDifference_ModuleOnOneSideOnly_IsIgnored()
    {
        var host = new[] { "TAOM_Map", "TAOM", "RBM" };
        var player = new[] { "PlayerOnly", "TAOM_Map", "TAOM" };
        var compared = LoadOrderRules.Compared(Host);
        compared.Add("PlayerOnly");

        Assert.IsFalse(LoadOrderRules.TryFindDifference(host, player, compared, out _, out _));
    }

    [TestMethod]
    public void TryFindDifference_OfficialOrDatalessModuleMoved_IsIgnored()
    {
        var host = new[] { "Native", "UIOnly", "TAOM_Map", "TAOM" };
        var player = new[] { "UIOnly", "TAOM_Map", "TAOM", "Native" };

        Assert.IsFalse(LoadOrderRules.TryFindDifference(host, player, LoadOrderRules.Compared(Host), out _, out _));
    }

    [TestMethod]
    public void DeclaresObjectData_ItemsList_IsTrue()
    {
        const string xml = "<Module><Xmls><XmlNode><XmlName id=\"GameText\" path=\"a\"/></XmlNode><XmlNode><XmlName id=\"Items\" path=\"b\"/></XmlNode></Xmls></Module>";
        Assert.IsTrue(LoadOrderRules.DeclaresObjectData(xml));
    }

    [TestMethod]
    public void DeclaresObjectData_TextOnly_IsFalse()
    {
        const string xml = "<Module><Xmls><XmlNode><XmlName id=\"GameText\" path=\"a\"/></XmlNode><XmlNode><XmlName id=\"Languages\" path=\"b\"/></XmlNode></Xmls></Module>";
        Assert.IsFalse(LoadOrderRules.DeclaresObjectData(xml));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("<Module><Xmls>")]
    [DataRow("<Module/>")]
    public void DeclaresObjectData_MissingOrMalformed_IsFalse(string? xml)
    {
        Assert.IsFalse(LoadOrderRules.DeclaresObjectData(xml));
    }

    [TestMethod]
    public void LocalOrder_SkipsEmptyAndOverlongIds_AndKeepsTheOrder()
    {
        var modules = new[] { Module("A"), new ModuleEntry("", "x", false, false), Module(new string('x', LoadOrderRules.MaxIdLength + 1)), Module("B") };

        CollectionAssert.AreEqual(new[] { "A", "B" }, LoadOrderRules.LocalOrder(modules));
    }

    [TestMethod]
    public void LocalOrder_CapsTheCount()
    {
        var modules = Enumerable.Range(0, LoadOrderRules.MaxModules + 10).Select(i => Module("M" + i)).ToArray();

        Assert.AreEqual(LoadOrderRules.MaxModules, LoadOrderRules.LocalOrder(modules).Length);
    }

    [TestMethod]
    public void Problem_AnAcceptableList_IsNull()
    {
        Assert.IsNull(LoadOrderRules.Problem(new[] { "TAOM", "TAOM_Map" }));
    }

    [TestMethod]
    public void Problem_TooManyEmptyOrOverlongIds_IsReported()
    {
        Assert.IsNotNull(LoadOrderRules.Problem(Enumerable.Repeat("M", LoadOrderRules.MaxModules + 1).ToArray()));
        Assert.IsNotNull(LoadOrderRules.Problem(new[] { "TAOM", "" }));
        Assert.IsNotNull(LoadOrderRules.Problem(new[] { "TAOM", null! }));
        Assert.IsNotNull(LoadOrderRules.Problem(new[] { new string('x', LoadOrderRules.MaxIdLength + 1) }));
    }

    [TestMethod]
    public void Describe_NameWithItsId_ClippedToTheLimit()
    {
        Assert.AreEqual("Realistic Battle (RBM)", LoadOrderRules.Describe("RBM", "Realistic Battle"));
        Assert.AreEqual("TAOM", LoadOrderRules.Describe("TAOM", "TAOM"));
        Assert.AreEqual("TAOM", LoadOrderRules.Describe("TAOM", " "));
        Assert.AreEqual(LoadOrderRules.MaxIdLength, LoadOrderRules.Describe("X", new string('n', 300)).Length);
    }
}

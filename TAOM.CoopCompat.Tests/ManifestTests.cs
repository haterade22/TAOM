using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.CoopCompat.Tests;

[TestClass]
public class ManifestTests
{
    private static XElement Root(string path) => XDocument.Load(RepoFiles.Path(path)).Root!;

    private static string? Value(XElement parent, string element) =>
        (string?)parent.Element(element)?.Attribute("value");

    [TestMethod]
    public void IdAndName_AreTaomCoopCompat()
    {
        var root = Root("CoopCompat/_Module/SubModule.xml");

        Assert.AreEqual("TAOM.CoopCompat", Value(root, "Id"));
        Assert.AreEqual("TAOM.CoopCompat", Value(root, "Name"));
    }

    [TestMethod]
    public void SubModule_HasNoTagsAndNamesItsClassAndDll()
    {
        var subs = Root("CoopCompat/_Module/SubModule.xml").Element("SubModules")!.Elements("SubModule").ToList();

        Assert.AreEqual(1, subs.Count);
        Assert.IsNull(subs[0].Element("Tags"), "An untagged submodule loads on a client and on a dedicated server.");
        var type = typeof(CoopCompatSubModule);
        Assert.AreEqual(type.FullName, Value(subs[0], "SubModuleClassType"));
        Assert.AreEqual(type.Assembly.GetName().Name + ".dll", Value(subs[0], "DLLName"));
    }

    [TestMethod]
    public void DependedModules_NameTaomAndCoop()
    {
        var root = Root("CoopCompat/_Module/SubModule.xml");

        var ids = root.Element("DependedModules")!.Elements("DependedModule")
            .Select(e => (string?)e.Attribute("Id")).ToList();
        CollectionAssert.AreEqual(
            new[] { "TAOM.Dependencies", "Native", "SandBoxCore", "Sandbox", "TAOM", "Coop" }, ids);

        var metadata = root.Element("DependedModuleMetadatas")!.Elements("DependedModuleMetadata")
            .ToDictionary(e => (string)e.Attribute("id")!, e => (string?)e.Attribute("order"));
        Assert.AreEqual("LoadBeforeThis", metadata["TAOM"]);
        Assert.AreEqual("LoadBeforeThis", metadata["Coop"]);
    }
}

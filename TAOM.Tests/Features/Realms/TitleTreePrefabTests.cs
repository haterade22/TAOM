using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Client.Features.Realms.Popups;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// RealmsTitleTreeNode.xml against its view models. Every <c>@</c> binding in a card or row template must name a
/// property of <see cref="TitleCardVM"/>: a nested <c>DataSource="{...}"</c> rescopes all bindings on its element, which
/// once made the pennant's <c>Brush.GlobalAlphaFactor="@ContentAlpha"</c> resolve against the banner identifier and
/// draw every banner empty. DataSourceProperty is an engine attribute.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TitleTreePrefabTests
{
    private static readonly string Gui = Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "GUI"));

    private static XDocument Node() =>
        XDocument.Load(Path.Combine(Gui, "PreFabs", "Realms", "RealmsTitleTreeNode.xml"));

    private static bool IsBound(Type type, string property) =>
        type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
            ?.GetCustomAttribute<DataSourceProperty>() != null;

    private static XElement Template(XDocument node, string list) =>
        node.Descendants("ListPanel")
            .Single(p => (string?)p.Attribute("DataSource") == "{" + list + "}")
            .Element("ItemTemplate")!;

    [DataTestMethod]
    [DataRow("Members")]
    [DataRow("Rows")]
    public void CardTemplate_BindsOnlyTitleCardProperties_WithoutRescoping(string list)
    {
        var template = Template(Node(), list);
        var elements = template.Descendants().ToList();

        var scoped = elements.Where(e => e.Attribute("DataSource") != null).Select(e => e.Name.LocalName).ToList();
        Assert.AreEqual(0, scoped.Count, $"{list}: nested DataSource rescopes the bindings on {string.Join(", ", scoped)}");

        var bindings = elements.SelectMany(e => e.Attributes())
            .Where(a => a.Value.StartsWith("@", StringComparison.Ordinal))
            .Select(a => (Attribute: a.Name.LocalName, Property: a.Value.Substring(1)))
            .ToList();
        Assert.IsTrue(bindings.Any(b => b.Attribute == "ImageId" && b.Property == nameof(TitleCardVM.BannerId)), $"{list}: banner id");
        Assert.IsTrue(bindings.Any(b => b.Attribute == "Brush.GlobalAlphaFactor"), $"{list}: fade binding");

        var missing = bindings.Where(b => !IsBound(typeof(TitleCardVM), b.Property))
            .Select(b => $"{b.Attribute}=@{b.Property}").ToList();
        Assert.AreEqual(0, missing.Count, $"{list}: not on TitleCardVM: {string.Join(", ", missing)}");
    }

    [DataTestMethod]
    [DataRow("Members")]
    [DataRow("Rows")]
    public void CardTemplate_FadesTextThroughTheBrush(string list)
    {
        var texts = Template(Node(), list).Descendants("TextWidget").ToList();

        Assert.IsTrue(texts.Count > 0);
        foreach (var text in texts)
        {
            Assert.IsNull(text.Attribute("AlphaFactor"), $"{list}: AlphaFactor never reaches a TextWidget's text");
            Assert.AreEqual("@ContentAlpha", (string?)text.Attribute("Brush.GlobalAlphaFactor"), $"{list}: {text.Attribute("Text")}");
        }
    }

    [TestMethod]
    public void NodeLists_AreTitleTreeNodeProperties()
    {
        var node = Node();
        foreach (var list in node.Descendants("ListPanel").Select(p => (string?)p.Attribute("DataSource")).Where(d => d != null))
            Assert.IsTrue(IsBound(typeof(TitleTreeNodeVM), list!.Trim('{', '}')), list);
        Assert.IsTrue(IsBound(typeof(TitleTreeNodeVM), nameof(TitleTreeNodeVM.IsStack)));
    }
}

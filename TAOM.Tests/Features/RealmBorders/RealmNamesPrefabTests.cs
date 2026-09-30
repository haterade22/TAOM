using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.RealmBorders.UI;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins the realm-names prefab against its view models and brushes. A Gauntlet binding typo or an
/// attribute the engine ignores fails silently in game, and one shipped here: AlphaFactor on a
/// TextWidget, which the v1.5.3 text renderer never reads, so the names could not fade.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RealmNamesPrefabTests
{
    private static string Gui => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "GUI"));

    private static XElement Prefab() =>
        XDocument.Load(Path.Combine(Gui, "PreFabs", "RealmBorders", "TaomRealmNames.xml")).Root!;

    private static bool IsBound(Type type, string property) =>
        type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.GetCustomAttribute<DataSourceProperty>() != null;

    [TestMethod]
    public void TextWidgets_FadeThroughTheirBrush_NeverAlphaFactor()
    {
        var texts = Prefab().Descendants("TextWidget").ToList();

        Assert.AreEqual(2, texts.Count);
        foreach (var text in texts)
        {
            Assert.IsNull(text.Attribute("AlphaFactor"), "AlphaFactor does not reach the text of a TextWidget");
            Assert.AreEqual("@Alpha", (string?)text.Attribute("Brush.GlobalAlphaFactor"));
        }
    }

    [TestMethod]
    public void EveryBinding_IsADataSourceProperty()
    {
        var root = Prefab();
        Assert.IsTrue(IsBound(typeof(RealmNamesVM), "Items"));
        Assert.AreEqual("{Items}", root.Descendants().Select(e => (string?)e.Attribute("DataSource")).Single(d => d != null));

        var bindings = root.Descendants("ItemTemplate").Descendants()
            .SelectMany(e => e.Attributes())
            .Select(a => a.Value)
            .Where(v => v.StartsWith("@", StringComparison.Ordinal))
            .Select(v => v.Substring(1))
            .Distinct()
            .ToList();

        Assert.AreNotEqual(0, bindings.Count);
        foreach (var binding in bindings)
            Assert.IsTrue(IsBound(typeof(RealmNameItemVM), binding), $"RealmNameItemVM has no [DataSourceProperty] {binding}");
    }

    [TestMethod]
    public void EveryBrush_IsDeclaredInTheFeaturesBrushFile()
    {
        var declared = XDocument.Load(Path.Combine(Gui, "Brushes", "TaomRealmBorders.xml")).Descendants("Brush")
            .Select(b => (string)b.Attribute("Name")!).ToList();

        foreach (var brush in Prefab().Descendants().Select(e => (string?)e.Attribute("Brush")).Where(b => b != null))
            CollectionAssert.Contains(declared, brush);
    }

    [TestMethod]
    public void EveryWidget_IgnoresEvents_SoTheMapKeepsItsClicks()
    {
        var widgets = Prefab().Descendants().Where(e => Regex.IsMatch(e.Name.LocalName, "Widget$")).ToList();

        Assert.AreNotEqual(0, widgets.Count);
        foreach (var widget in widgets)
            Assert.AreEqual("true", (string?)widget.Attribute("DoNotAcceptEvents"), $"<{widget.Name}> would take clicks from the map");
    }
}

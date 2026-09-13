using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SettlementNameplateRelation;

namespace TAOM.Tests.Features.SettlementNameplateRelation;

/// <summary>
/// Drift guard for the three settlement nameplate prefab clones (#591). Every seam here fails
/// SILENTLY in game: a renamed Id leaves the custom widget's path attribute null (no tint, no
/// alpha), a dropped <c>RelationType="@Relation"</c> binding never marks the palette dirty, a
/// 7-character colour attribute throws inside the attribute loader, which catches it with a failed
/// assert and keeps the compiled default (so the override never applies), and a re-parented child
/// breaks the item widget's own paths so vanilla's per-frame update asserts and returns.
/// Assertions walk the ACTUAL XML and run inside the per-size loop, so each size can fail on its own.
/// </summary>
[TestClass]
public class NameplateRelationPrefabTests
{
    private static readonly string[] Sizes = { "Large", "Medium", "Small" };

    private static readonly Regex Rrggbbaa = new Regex("^#[0-9A-Fa-f]{8}$");

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string PrefabPath(string size) => Path.Combine(
        RepoRoot, @"Main\_Module\GUI\Prefabs\Nameplate\SettlementNameplateItem" + size + ".xml");

    private static XDocument Load(string size)
    {
        var path = PrefabPath(size);
        Assert.IsTrue(File.Exists(path), "Prefab missing: " + path);
        return XDocument.Load(path);
    }

    private static XElement RequirePlateWidget(XDocument doc, string size)
    {
        var plates = doc.Descendants("TaomSettlementPlateWidget").ToList();
        Assert.AreEqual(1, plates.Count, size + ": expected exactly one TaomSettlementPlateWidget");
        return plates[0];
    }

    /// <summary>Mirrors Widget.FindChild(BindingPath): each segment is matched by Id among the
    /// element's direct Children entries only.</summary>
    private static XElement? ResolvePath(XElement from, string path)
    {
        var current = from;
        foreach (var segment in path.Split('\\'))
        {
            var children = current.Element("Children");
            if (children == null) return null;
            current = children.Elements().FirstOrDefault(e => (string?)e.Attribute("Id") == segment);
            if (current == null) return null;
        }
        return current;
    }

    [TestMethod]
    public void PlateWidget_AllSizes_ReplacesTheLayoutContainerAndBindsRelation()
    {
        foreach (var size in Sizes)
        {
            var plate = RequirePlateWidget(Load(size), size);

            Assert.AreEqual("SettlementNameplateLayout", (string?)plate.Attribute("Id"),
                size + ": the plate widget must keep the Id the item widget's paths go through");
            Assert.AreEqual("@Relation", (string?)plate.Attribute("RelationType"),
                size + ": RelationType must bind the VM's Relation int");
        }
    }

    [TestMethod]
    public void PlateWidget_AllSizes_WidgetPathsResolveToDescendantsById()
    {
        var expected = new Dictionary<string, string>
        {
            ["BarBackgroundWidget"] = "Widget",
            ["NameTextWidget"] = "TextWidget",
            ["FrameWidget"] = "Widget",
            ["BannerWidget"] = "MaskedTextureWidget",
            ["TrackedRingWidget"] = "Widget",
        };

        foreach (var size in Sizes)
        {
            var plate = RequirePlateWidget(Load(size), size);
            foreach (var pair in expected)
            {
                var path = (string?)plate.Attribute(pair.Key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(path), size + ": " + pair.Key + " attribute missing");

                var target = ResolvePath(plate, path!);
                Assert.IsNotNull(target, size + ": " + pair.Key + "='" + path + "' resolves to nothing");
                Assert.AreEqual(pair.Value, target!.Name.LocalName,
                    size + ": " + pair.Key + " must point at a " + pair.Value);
            }
        }
    }

    [TestMethod]
    public void PlateWidget_AllSizes_ColorAttributesIfPresentAreRrggbbaa()
    {
        foreach (var size in Sizes)
        {
            var plate = RequirePlateWidget(Load(size), size);
            foreach (var attribute in plate.Attributes().Where(a => a.Name.LocalName.EndsWith("Color", StringComparison.Ordinal)))
            {
                Assert.IsTrue(Rrggbbaa.IsMatch(attribute.Value),
                    size + ": " + attribute.Name.LocalName + "='" + attribute.Value + "' is not #RRGGBBAA");
            }
        }
    }

    [TestMethod]
    public void ItemWidget_AllSizes_ExistingPathsStillResolve()
    {
        var pathAttributes = new[]
        {
            "SettlementBannerWidget", "SettlementNameplateCapsuleWidget", "InspectedIconWidget",
            "PortIconWidget", "SettlementNameTextWidget", "SettlementPartiesGridWidget",
            "WidgetToShow", "MapEventVisualWidget", "ParleyIconWidget",
        };

        foreach (var size in Sizes)
        {
            var doc = Load(size);
            var items = doc.Descendants("SettlementNameplateItemWidget").ToList();
            Assert.AreEqual(1, items.Count, size + ": expected exactly one SettlementNameplateItemWidget");

            foreach (var name in pathAttributes)
            {
                var path = (string?)items[0].Attribute(name);
                Assert.IsFalse(string.IsNullOrWhiteSpace(path), size + ": item widget lost " + name);
                Assert.IsNotNull(ResolvePath(items[0], path!),
                    size + ": item widget path " + name + "='" + path + "' no longer resolves");
            }
        }
    }

    [TestMethod]
    public void PlateWidget_AllSizes_ItemAncestorWithinProductionDepth()
    {
        // The widget walks ParentWidget at most MaxAncestorDepth times for the item widget vanilla
        // writes alpha to; a re-nesting that pushes it further silently stops the mirroring while
        // every path test still passes (Codex review, #591). Widget levels alternate with the
        // <Children> wrapper element, so one hop is two XML parents.
        foreach (var size in Sizes)
        {
            var plate = RequirePlateWidget(Load(size), size);
            var hops = 0;
            var current = plate.Parent?.Parent;
            while (current != null && current.Name.LocalName != "SettlementNameplateItemWidget")
            {
                hops++;
                current = current.Parent?.Parent;
            }

            Assert.IsNotNull(current, size + ": the plate is not under a SettlementNameplateItemWidget");
            hops++;
            Assert.IsTrue(hops <= TaomSettlementPlateWidget.MaxAncestorDepth,
                size + ": item widget is " + hops + " parents up, beyond MaxAncestorDepth " + TaomSettlementPlateWidget.MaxAncestorDepth);
        }
    }

    [TestMethod]
    public void CapsuleButton_AllSizes_DoesNotCascadeStatesToThePlate()
    {
        // A ButtonWidget with UpdateChildrenStates would push Hovered/Pressed/Default into the
        // plate's children and reset the text brush the widget colours.
        foreach (var size in Sizes)
        {
            var capsule = Load(size).Descendants("ButtonWidget")
                .FirstOrDefault(e => (string?)e.Attribute("Id") == "SettlementNameplateCapsuleWidget");
            Assert.IsNotNull(capsule, size + ": capsule button missing");
            Assert.IsNull(capsule!.Attribute("UpdateChildrenStates"), size + ": capsule must not cascade states");
        }
    }
}

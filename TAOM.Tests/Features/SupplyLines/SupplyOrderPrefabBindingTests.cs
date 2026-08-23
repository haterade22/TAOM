using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SupplyLines.UI;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// Pins the shipped TaomSupplyOrderScreen.xml prefab against the VM types. A Gauntlet binding
/// typo fails SILENTLY in game (blank widget, dead button, no crash, no log), which is exactly
/// the class of bug gui-ui.md documents; this makes it a red test instead. Both directions are
/// checked: every binding in the XML must exist on the right VM, and every [DataSourceProperty]
/// on the VMs must be bound somewhere (unused properties are dead code per gui-ui.md).
/// </summary>
[TestClass]
public class SupplyOrderPrefabBindingTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string PrefabPath => Path.Combine(
        RepoRoot, @"Main\_Module\GUI\PreFabs\SupplyLines\TaomSupplyOrderScreen.xml");

    private static string SpriteDataPath => Path.Combine(
        RepoRoot, @"Main\_Module\GUI\TAOMSpriteData.xml");

    private static string BrushesDir => Path.Combine(RepoRoot, @"Main\_Module\GUI\Brushes");

    // The prefab's DataSource collections and the item VM each template binds against.
    private static readonly Dictionary<string, Type> CollectionItemTypes = new Dictionary<string, Type>
    {
        ["Settlements"] = typeof(SupplySourceRowVM),
        ["Goods"] = typeof(SupplyGoodRowVM),
        ["Troops"] = typeof(SupplyTroopRowVM),
    };

    // Vanilla sprites and brushes the prefab deliberately uses. Anything referenced beyond this
    // set must be registered in TAOMSpriteData.xml / Main/_Module/GUI/Brushes or it renders
    // blank / default-styled with no error.
    //
    // EVERY entry here must be grep-verified against the installed game's Modules/*/GUI/Brushes
    // before it is added: this allowlist previously vouched for Popup.Text.Medium/.Small, which
    // exist in NO brush file anywhere (review round B critic), so the gate green-lit 22 widgets
    // silently rendering with the engine default brush. The current text brushes are defined in
    // Native/GUI/Brushes/Popup.xml (verified on the installed 1.4.8, 2026-08-23).
    private static readonly HashSet<string> KnownVanillaSprites = new HashSet<string>
    {
        "BlankWhiteSquare_9",
        "horizontal_gradient_divider",
    };

    private static readonly HashSet<string> KnownVanillaBrushes = new HashSet<string>
    {
        "Frame1.Broken",
        "InventoryHeaderFontBrush",
        "ButtonBrush2",
        "Popup.Description.Text",
        "Popup.Button.Text",
        "TownManagement.Item.Title.Text",
    };

    private static XElement LoadPrefabRoot()
    {
        Assert.IsTrue(File.Exists(PrefabPath), $"prefab missing at {PrefabPath}");
        var root = XDocument.Load(PrefabPath).Root;
        Assert.IsNotNull(root, "prefab has no root element");
        return root!;
    }

    [TestMethod]
    public void Prefab_EveryBinding_ExistsOnTheRightVmType()
    {
        var failures = new List<string>();
        Walk(LoadPrefabRoot(), typeof(SupplyOrderScreenVM), failures);
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    private static void Walk(XElement element, Type vmType, List<string> failures)
    {
        var childType = vmType;

        // DataSource first: it decides which VM the subtree binds against.
        var dataSource = element.Attribute("DataSource");
        if (dataSource != null)
        {
            var match = Regex.Match(dataSource.Value, @"^\{(\w+)\}$");
            if (!match.Success)
            {
                failures.Add($"unparseable DataSource '{dataSource.Value}' on <{element.Name}>");
            }
            else
            {
                var collection = match.Groups[1].Value;
                AssertBoundProperty(vmType, collection, failures);
                if (!CollectionItemTypes.TryGetValue(collection, out childType))
                {
                    failures.Add($"collection '{collection}' has no mapped item VM type in this test");
                    childType = vmType;
                }
            }
        }

        foreach (var attr in element.Attributes())
        {
            var name = attr.Name.LocalName;
            if (name == "DataSource")
                continue;

            if (name.StartsWith("Command.", StringComparison.Ordinal))
            {
                var method = vmType.GetMethod(attr.Value, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method == null)
                    failures.Add($"{vmType.Name} has no public parameterless method '{attr.Value}' for {name}");
            }
            else if (attr.Value.StartsWith("@", StringComparison.Ordinal))
            {
                AssertBoundProperty(vmType, attr.Value.Substring(1), failures);
            }
        }

        foreach (var child in element.Elements())
            Walk(child, childType, failures);
    }

    private static void AssertBoundProperty(Type vmType, string propertyName, List<string> failures)
    {
        var property = vmType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
        {
            failures.Add($"{vmType.Name} has no public property '{propertyName}'");
            return;
        }

        // Gauntlet only refreshes properties carrying [DataSourceProperty]; a bare property
        // binds once and then never updates.
        var hasAttribute = property.GetCustomAttributes(true)
            .Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));
        if (!hasAttribute)
            failures.Add($"{vmType.Name}.{propertyName} is bound in the prefab but lacks [DataSourceProperty]");
    }

    [TestMethod]
    public void VmTypes_EveryDataSourceProperty_IsBoundInThePrefab()
    {
        var boundNames = new HashSet<string>(StringComparer.Ordinal);
        CollectBoundNames(LoadPrefabRoot(), boundNames);

        var failures = new List<string>();
        var vmTypes = new[]
        {
            typeof(SupplyOrderScreenVM),
            typeof(SupplySourceRowVM),
            typeof(SupplyGoodRowVM),
            typeof(SupplyTroopRowVM),
        };
        foreach (var vmType in vmTypes)
        {
            foreach (var property in vmType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var hasAttribute = property.GetCustomAttributes(true)
                    .Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));
                if (hasAttribute && !boundNames.Contains(property.Name))
                    failures.Add($"{vmType.Name}.{property.Name} is [DataSourceProperty] but nothing in the prefab binds it (dead code per gui-ui.md)");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    private static void CollectBoundNames(XElement element, HashSet<string> names)
    {
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.LocalName == "DataSource")
            {
                var match = Regex.Match(attr.Value, @"^\{(\w+)\}$");
                if (match.Success)
                    names.Add(match.Groups[1].Value);
            }
            else if (attr.Value.StartsWith("@", StringComparison.Ordinal))
            {
                names.Add(attr.Value.Substring(1));
            }
        }

        foreach (var child in element.Elements())
            CollectBoundNames(child, names);
    }

    [TestMethod]
    public void Prefab_EverySpriteAndBrush_IsVanillaKnownOrRegistered()
    {
        var allowedSprites = new HashSet<string>(KnownVanillaSprites, StringComparer.Ordinal);
        Assert.IsTrue(File.Exists(SpriteDataPath), $"TAOMSpriteData.xml missing at {SpriteDataPath}");
        foreach (var nameElement in XDocument.Load(SpriteDataPath).Descendants("Name"))
            allowedSprites.Add(nameElement.Value);

        var allowedBrushes = new HashSet<string>(KnownVanillaBrushes, StringComparer.Ordinal);
        if (Directory.Exists(BrushesDir))
        {
            foreach (var brushFile in Directory.GetFiles(BrushesDir, "*.xml"))
            {
                foreach (var brush in XDocument.Load(brushFile).Descendants("Brush"))
                {
                    var name = brush.Attribute("Name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                        allowedBrushes.Add(name!);
                }
            }
        }

        var failures = new List<string>();
        foreach (var element in LoadPrefabRoot().DescendantsAndSelf())
        {
            var sprite = element.Attribute("Sprite")?.Value;
            if (sprite != null && !allowedSprites.Contains(sprite))
                failures.Add($"Sprite '{sprite}' is neither known-vanilla nor registered in TAOMSpriteData.xml (renders blank silently)");

            var brush = element.Attribute("Brush")?.Value;
            if (brush != null && !allowedBrushes.Contains(brush))
                failures.Add($"Brush '{brush}' is neither known-vanilla nor declared in Main/_Module/GUI/Brushes (renders default-styled silently)");
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }
}

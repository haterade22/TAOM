using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.UI;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Pins the shipped CareerScreen.xml prefab against its VM types. A Gauntlet binding typo fails
/// SILENTLY in game (a blank widget or a dead button, no crash, no log), which is how a prefab and
/// its VM drift apart across a redesign. Every <c>@Property</c> and <c>Command.*</c> in the prefab
/// must resolve on the VM that scope binds to, and a bound property must carry
/// <c>[DataSourceProperty]</c> or it binds once and never refreshes. Modelled on
/// SupplyOrderPrefabBindingTests.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CareerScreenPrefabBindingTests
{
    // The item VM each DataSource collection hands its ItemTemplate. Names are unique across the
    // prefab, so a flat map is enough; an unmapped collection fails the test rather than guessing.
    private static readonly Dictionary<string, Type> CollectionItemTypes = new Dictionary<string, Type>
    {
        ["AbilityEffects"] = typeof(CareerAbilityEffectVM),
        ["KeystoneEffectLines"] = typeof(CareerAbilityEffectVM),
        ["PassiveEffectLines"] = typeof(CareerAbilityEffectVM),
        ["ChoiceGroupsTier1"] = typeof(CareerChoiceGroupObjectVM),
        ["ChoiceGroupsTier2"] = typeof(CareerChoiceGroupObjectVM),
        ["ChoiceGroupsTier3"] = typeof(CareerChoiceGroupObjectVM),
        ["Choices"] = typeof(CareerChoiceObjectVM),
        ["EligibleSwitchTargets"] = typeof(CareerSwitchTargetVM),
    };

    private static XElement LoadPrefabRoot()
    {
        var path = RepoPath("Main", "_Module", "GUI", "PreFabs", "CareerSystem", "CareerScreen.xml");
        Assert.IsTrue(File.Exists(path), $"prefab missing at {path}");
        var root = XDocument.Load(path).Root;
        Assert.IsNotNull(root, "prefab has no root element");
        return root!;
    }

    [TestMethod]
    public void Prefab_EveryBindingAndCommand_ResolvesOnTheVmItsScopeBindsTo()
    {
        var failures = new List<string>();
        Walk(LoadPrefabRoot(), typeof(CareerScreenVM), failures);
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void Prefab_BindsEachOfTheFourScopes()
    {
        // Guards the walk itself: if the prefab were restructured so a scope vanished, the test above
        // would still pass while checking nothing for that VM.
        var reached = new HashSet<Type>();
        CollectScopes(LoadPrefabRoot(), typeof(CareerScreenVM), reached);

        CollectionAssert.IsSubsetOf(
            new[] { typeof(CareerScreenVM), typeof(CareerChoiceGroupObjectVM), typeof(CareerChoiceObjectVM), typeof(CareerSwitchTargetVM) },
            reached.ToList());
    }

    [TestMethod]
    public void Prefab_ShowsTheRootPerkInTheHeaderGatedOnHasRootPerk()
    {
        // #769: the root perk text is bound under the career description in the header bar, and an
        // empty root hides its widget instead of leaving a blank line. The generic walk above proves
        // both properties resolve.
        var widgets = LoadPrefabRoot().Descendants()
            .Where(e => (string?)e.Attribute("Text") == "@RootPerkDescription")
            .ToList();

        Assert.AreEqual(1, widgets.Count, "exactly one widget shows RootPerkDescription");
        Assert.AreEqual("@HasRootPerk", (string?)widgets[0].Attribute("IsVisible"));
        Assert.AreEqual(
            "@CareerDescription",
            (string?)widgets[0].ElementsBeforeSelf().LastOrDefault()?.Attribute("Text"),
            "the root perk sits directly under the career description");
    }

    private static void CollectScopes(XElement element, Type vmType, HashSet<Type> reached)
    {
        reached.Add(vmType);
        var childType = vmType;
        var dataSource = element.Attribute("DataSource");
        if (dataSource != null)
        {
            var match = Regex.Match(dataSource.Value, @"^\{(\w+)\}$");
            if (match.Success && CollectionItemTypes.TryGetValue(match.Groups[1].Value, out var mapped))
                childType = mapped;
        }
        foreach (var child in element.Elements())
            CollectScopes(child, childType, reached);
    }

    private static void Walk(XElement element, Type vmType, List<string> failures)
    {
        var childType = vmType;

        // DataSource first: it names a collection on THIS scope and decides the subtree's scope.
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
                if (!CollectionItemTypes.TryGetValue(collection, out childType!))
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

        var hasAttribute = property.GetCustomAttributes(true)
            .Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));
        if (!hasAttribute)
            failures.Add($"{vmType.Name}.{propertyName} is bound in the prefab but lacks [DataSourceProperty]");
    }
}

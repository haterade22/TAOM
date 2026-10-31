using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FordsOfWar.Client;

/// <summary>
/// Pins the command panel's movie to its view models. A binding that names nothing is a silent no-op in Gauntlet, and a
/// <c>DataSource</c> on a movie root is never read, so the root carries none. The walk follows a scope per <c>DataSource</c> and
/// hands each <c>ItemTemplate</c> the item type of its list, as Gauntlet does. It reads attribute data and constructs no
/// TaleWorlds widget, so it runs without the game.
/// </summary>
[TestClass]
public class FordsCommandPrefabTests
{
    private static readonly string[] Grammar = { "Prefab", "Window", "Children", "ItemTemplate" };

    private static readonly Type[] ViewModels =
    {
        typeof(FordsCommandVM), typeof(FordsLaneVM), typeof(FordsOfferVM), typeof(FordsHeroOfferVM), typeof(FordsOrderVM),
        typeof(FordsLaneBarVM), typeof(FordsTowerBarVM), typeof(FordsFeedLineVM), typeof(FordsPointVM),
    };

    private static XElement Document() =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "GUI", "PreFabs", "FordsOfWar", "FordsCommand.xml")).Root!;

    private static XElement Root() => Document().Element("Window")!.Elements().Single();

    [TestMethod]
    public void ThePrefab_ParsesWithOneRootWidgetInsideWindow()
    {
        Assert.AreEqual("Prefab", Document().Name.LocalName);
        Assert.AreEqual(1, Document().Element("Window")!.Elements().Count());
    }

    [TestMethod]
    public void EveryBinding_ResolvesToAMemberOfTheViewModelInScope()
    {
        var failures = new List<string>();
        var bound = new Dictionary<Type, HashSet<string>>();

        Walk(Root(), typeof(FordsCommandVM), failures, bound);

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(bound.Values.Sum(v => v.Count) > 30, "the walk found too few bindings to be walking the prefab");
    }

    [TestMethod]
    public void EveryDataSourceProperty_IsBoundByThePrefabSomewhere()
    {
        var failures = new List<string>();
        var bound = new Dictionary<Type, HashSet<string>>();
        Walk(Root(), typeof(FordsCommandVM), failures, bound);

        foreach (var vmType in ViewModels)
        {
            if (!bound.TryGetValue(vmType, out var names))
            {
                failures.Add($"{vmType.Name} is never bound by the prefab");
                continue;
            }

            foreach (var property in vmType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsDataSourceProperty(property) && !names.Contains(property.Name))
                    failures.Add($"{vmType.Name}.{property.Name} is [DataSourceProperty] but the prefab never binds it");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void TheMovieRoot_CarriesNoDataSource()
    {
        Assert.IsNull(Root().Attribute("DataSource"), "GauntletMovie.LoadMovie reads no DataSource on the root view");
    }

    [TestMethod]
    public void EveryText_IsABinding_SoNoEnglishIsHardCodedInTheMovie()
    {
        var literal = Root().DescendantsAndSelf()
            .Where(e => e.Attribute("Text") != null && !e.Attribute("Text")!.Value.StartsWith("@", StringComparison.Ordinal))
            .Select(e => e.Attribute("Text")!.Value)
            .ToList();

        Assert.AreEqual(0, literal.Count, "static text belongs in a TAOM_FOW_ key: " + string.Join(", ", literal));
    }

    [TestMethod]
    public void EveryButton_PassesNoEventsToItsChildren_AndNamesAClickCommand()
    {
        var buttons = Root().Descendants().Where(e => e.Name.LocalName == "ButtonWidget").ToList();

        Assert.IsTrue(buttons.Count >= 5);
        foreach (var button in buttons)
        {
            Assert.AreEqual("true", (string?)button.Attribute("DoNotPassEventsToChildren"), "a button with content children needs it or its text swallows the click");
            Assert.IsNotNull(button.Attribute("Command.Click"), "a button without a click command does nothing");
        }
    }

    [TestMethod]
    public void OnlyButtonsAndTheResultBackdrop_TakeEvents()
    {
        var offenders = new List<string>();
        foreach (var element in Root().DescendantsAndSelf().Where(e => !Grammar.Contains(e.Name.LocalName)))
        {
            var isButton = element.Name.LocalName == "ButtonWidget";
            var isBackdrop = (string?)element.Attribute("IsVisible") == "@IsResultVisible";
            var ignores = (string?)element.Attribute("DoNotAcceptEvents") == "true";
            if (!isButton && !isBackdrop && !ignores)
                offenders.Add($"<{element.Name.LocalName}> {(string?)element.Attribute("Text") ?? (string?)element.Attribute("Sprite") ?? ""}");
        }

        Assert.AreEqual(0, offenders.Count,
            "the cursor stays visible, so a widget that takes events steals the camera's right drag and wheel: " + string.Join("; ", offenders));
    }

    [TestMethod]
    public void TheCapturePointRow_IsAReadOnlyListBoundToPoints_WithItsTitleAndTheThreeFields()
    {
        var list = Root().Descendants().Single(e => (string?)e.Attribute("DataSource") == "{Points}");

        var bindings = list.Descendants().Attributes().Where(a => a.Value.StartsWith("@", StringComparison.Ordinal)).Select(a => a.Value).ToList();
        CollectionAssert.IsSubsetOf(new[] { "@Title", "@StatusText", "@ContestedText", "@OwnerColor" }, bindings);
        Assert.IsTrue(Root().Descendants().Any(e => (string?)e.Attribute("Text") == "@PointsTitle"));
        Assert.AreEqual(0, list.DescendantsAndSelf().Count(e => e.Name.LocalName == "ButtonWidget"), "the row only reports; it gives no command");
    }

    [TestMethod]
    public void TheCapturePointRow_UsesOnlySpritesAndBrushesTheRestOfThePrefabAlreadyUses()
    {
        var list = Root().Descendants().Single(e => (string?)e.Attribute("DataSource") == "{Points}");
        var title = Root().Descendants().Single(e => (string?)e.Attribute("Text") == "@PointsTitle");
        var inRow = list.DescendantsAndSelf().Concat(new[] { title }).ToList();
        var outside = Root().DescendantsAndSelf().Except(inRow).ToList();

        foreach (var attribute in new[] { "Sprite", "Brush" })
        {
            var used = inRow.Select(e => (string?)e.Attribute(attribute)).Where(v => v != null).Distinct().ToList();
            var known = outside.Select(e => (string?)e.Attribute(attribute)).Where(v => v != null).ToHashSet();
            Assert.IsTrue(used.Count > 0, "the row sets a " + attribute);
            Assert.IsTrue(used.All(known.Contains), attribute + " values new to the prefab: " + string.Join(", ", used.Where(v => !known.Contains(v))));
        }
    }

    [TestMethod]
    public void EveryCommand_IsAPublicParameterlessMethodOfItsViewModel()
    {
        var commands = typeof(FordsCommandVM).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => m.Name.StartsWith("Execute", StringComparison.Ordinal)).Select(m => m.Name)
            .Concat(new[] { "ExecuteSelect", "ExecuteBuy", "ExecuteAct", "ExecuteSet" });
        var used = Root().DescendantsAndSelf().Attributes().Where(a => a.Name.LocalName.StartsWith("Command.", StringComparison.Ordinal))
            .Select(a => a.Value).Distinct().ToList();

        CollectionAssert.AreEquivalent(commands.Distinct().OrderBy(n => n).ToList(), used.OrderBy(n => n).ToList());
    }

    private static void Walk(XElement element, Type? scope, List<string> failures, Dictionary<Type, HashSet<string>> bound)
    {
        Type? itemType = null;
        var dataSource = element.Attribute("DataSource");
        if (dataSource != null && scope != null)
        {
            var match = Regex.Match(dataSource.Value, @"^\{(\w+)\}$");
            var property = match.Success ? Bind(scope, match.Groups[1].Value, failures, bound) : null;
            if (!match.Success)
                failures.Add($"unparseable DataSource '{dataSource.Value}' on <{element.Name}>");
            itemType = property == null ? null : ListItemType(property.PropertyType);
            scope = property == null || itemType != null ? null : property.PropertyType;
        }

        foreach (var attribute in element.Attributes())
        {
            var name = attribute.Name.LocalName;
            if (name == "DataSource" || scope == null)
                continue;
            if (name.StartsWith("Command.", StringComparison.Ordinal))
            {
                if (scope.GetMethod(attribute.Value, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) == null)
                    failures.Add($"{scope.Name} has no public parameterless '{attribute.Value}' for {name}");
            }
            else if (attribute.Value.StartsWith("@", StringComparison.Ordinal))
            {
                Bind(scope, attribute.Value.Substring(1), failures, bound);
            }
        }

        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName != "ItemTemplate")
            {
                Walk(child, scope, failures, bound);
                continue;
            }

            if (itemType == null)
                failures.Add($"<ItemTemplate> under <{element.Name}>, which binds no list");
            foreach (var template in child.Elements())
                Walk(template, itemType, failures, bound);
        }
    }

    private static PropertyInfo? Bind(Type scope, string propertyName, List<string> failures, Dictionary<Type, HashSet<string>> bound)
    {
        if (!bound.TryGetValue(scope, out var names))
            bound[scope] = names = new HashSet<string>(StringComparer.Ordinal);
        names.Add(propertyName);

        var property = scope.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
            failures.Add($"{scope.Name} has no public property '{propertyName}'");
        else if (!IsDataSourceProperty(property))
            failures.Add($"{scope.Name}.{propertyName} is bound but lacks [DataSourceProperty], so it never refreshes");
        return property;
    }

    private static Type? ListItemType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("MBBindingList", StringComparison.Ordinal)
            ? type.GetGenericArguments()[0]
            : null;

    private static bool IsDataSourceProperty(PropertyInfo property) =>
        property.GetCustomAttributes(true).Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));
}

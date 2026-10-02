using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.UI.FactionScreen;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Issue #704. Pins Kysaro's faction screen prefab against its view models, both directions (the
/// SupplyOrderPrefabBindingTests precedent). A Gauntlet binding that names nothing fails silently: a
/// blank widget or a dead button, no log. The review found one (<c>ExecuteHoverDiag</c>, bound to a
/// debug method the port had dropped). The walk follows Gauntlet's scoping: a <c>DataSource</c>
/// rebinds the element's own attributes and commands as well as its children (the prefab's own
/// MinimapPopupMap comment records the bug that taught this), and an <c>ItemTemplate</c> binds to the
/// list's item type.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FactionScreenPrefabBindingTests
{
    private static readonly Type[] TaomVmTypes =
    {
        typeof(FactionScreenVM), typeof(FactionItemVM), typeof(FactionDetailVM),
        typeof(HeroItemVM), typeof(TextItemVM), typeof(BenefitItemVM),
    };

    private static XElement LoadPrefab()
    {
        var root = XDocument.Parse(ReadSource("Main/_Module/GUI/Prefabs/FactionUI/TAOMFactionScreen.xml")).Root;
        Assert.IsNotNull(root, "the prefab has no root element");
        return root!;
    }

    private static Dictionary<Type, HashSet<string>> WalkPrefab(List<string> failures)
    {
        var bound = new Dictionary<Type, HashSet<string>>();
        Walk(LoadPrefab(), typeof(FactionScreenVM), failures, bound);
        return bound;
    }

    [TestMethod]
    public void Prefab_EveryBinding_ExistsOnTheVmItBindsAgainst()
    {
        var failures = new List<string>();
        WalkPrefab(failures);

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void VmTypes_EveryDataSourceProperty_IsBoundOnItsOwnType()
    {
        // Per type, not per name: HeroItemVM.Name being bound says nothing about FactionItemVM.Name.
        var bound = WalkPrefab(new List<string>());
        var failures = new List<string>();

        foreach (var vmType in TaomVmTypes)
        {
            if (!bound.TryGetValue(vmType, out var names))
            {
                failures.Add($"{vmType.Name} is never bound by the prefab");
                continue;
            }
            foreach (var property in vmType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsDataSourceProperty(property) && !names.Contains(property.Name))
                    failures.Add($"{vmType.Name}.{property.Name} is [DataSourceProperty] but the prefab never binds it there");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
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

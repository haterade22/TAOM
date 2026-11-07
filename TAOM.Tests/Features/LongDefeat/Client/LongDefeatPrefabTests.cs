using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.LongDefeat;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>
/// Pins the movies to their view models, as <c>FordsCommandPrefabTests</c> does: a binding that names nothing is a silent no-op in
/// Gauntlet, and a root <c>DataSource</c> is never read. The two LongDefeat movies and their FactionUI themes, which load in their place
/// unless the theme fails, all bind the one <see cref="LongDefeatHudVM"/>. Reads attribute data only, so it runs without the game.
/// </summary>
[TestClass]
public class LongDefeatPrefabTests
{
    private static readonly string[] Grammar = { "Prefab", "Window", "Children", "ItemTemplate" };

    private static XElement Root(string name, string folder = "LongDefeat") =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "GUI", "PreFabs", folder, name + ".xml")).Root!.Element("Window")!.Elements().Single();

    private static readonly string[] Movies = { "LongDefeatHud", "LongDefeatPanel" };

    // The FactionUI theme loads these in place of the two movies over the same LongDefeatHudVM (FrontEndMovieService), so a property only a
    // themed movie binds is live, and a themed binding must resolve like any other.
    private static readonly string[] ThemedMovies = { "TAOMLongDefeatHud", "TAOMLongDefeatPanel" };

    private static IEnumerable<XElement> EveryMovieRoot() =>
        Movies.Select(m => Root(m)).Concat(ThemedMovies.Select(m => Root(m, "FactionUI")));

    private static IEnumerable<XElement> HudRoots() => new[] { Root("LongDefeatHud"), Root("TAOMLongDefeatHud", "FactionUI") };

    private static IEnumerable<XElement> PanelRoots() => new[] { Root("LongDefeatPanel"), Root("TAOMLongDefeatPanel", "FactionUI") };

    [TestMethod]
    public void EveryMovie_ParsesWithOneRootWidget_AndNoRootDataSource()
    {
        foreach (var root in EveryMovieRoot())
            Assert.IsNull(root.Attribute("DataSource"), root.Name.LocalName + ": GauntletMovie.LoadMovie reads no DataSource on the root view");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryBinding_ResolvesToADataSourcePropertyOrCommandInScope()
    {
        var failures = new List<string>();
        var bound = new Dictionary<Type, HashSet<string>>();

        foreach (var root in EveryMovieRoot())
            Walk(root, typeof(LongDefeatHudVM), failures, bound);

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(bound.Values.Sum(v => v.Count) > 15, "the walk found too few bindings to be walking the prefabs");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EveryDataSourceProperty_IsBoundByOneOfTheMovies()
    {
        var failures = new List<string>();
        var bound = new Dictionary<Type, HashSet<string>>();
        foreach (var root in EveryMovieRoot())
            Walk(root, typeof(LongDefeatHudVM), failures, bound);

        foreach (var vmType in new[] { typeof(LongDefeatHudVM), typeof(LongDefeatOfferVM), typeof(LongDefeatSpeedVM), typeof(LongDefeatBoonVM), typeof(LongDefeatFortVM) })
        {
            var names = bound.TryGetValue(vmType, out var found) ? found : new HashSet<string>();
            foreach (var property in vmType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsDataSourceProperty(property) && !names.Contains(property.Name))
                    failures.Add($"{vmType.Name}.{property.Name} is [DataSourceProperty] but no movie binds it");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void EveryText_IsABinding_SoNoEnglishIsHardCodedInAMovie()
    {
        foreach (var movie in Movies)
        {
            var literal = Root(movie).DescendantsAndSelf()
                .Where(e => e.Attribute("Text") != null && !e.Attribute("Text")!.Value.StartsWith("@", StringComparison.Ordinal))
                .Select(e => e.Attribute("Text")!.Value)
                .ToList();

            Assert.AreEqual(0, literal.Count, movie + ": static text belongs in a TAOM_LD_ key: " + string.Join(", ", literal));
        }
    }

    [TestMethod]
    public void TheHud_TakesNoEvents_SoTheAgentKeepsTheMouseAndTheOrderBar()
    {
        var offenders = HudRoots().SelectMany(r => r.DescendantsAndSelf())
            .Where(e => !Grammar.Contains(e.Name.LocalName) && (string?)e.Attribute("DoNotAcceptEvents") != "true")
            .Select(e => e.Name.LocalName + " " + ((string?)e.Attribute("Text") ?? ""))
            .ToList();

        Assert.AreEqual(0, offenders.Count, "a HUD widget that takes events steals clicks from the battle: " + string.Join("; ", offenders));
    }

    [TestMethod]
    public void EveryPanelButton_PassesNoEventsToItsChildren_AndNamesAClickCommand()
    {
        foreach (var panel in PanelRoots())
        {
            var buttons = panel.Descendants().Where(e => e.Name.LocalName == "ButtonWidget").ToList();

            Assert.IsTrue(buttons.Count >= 6, "pick, reroll, buy, horn, close, leave");
            foreach (var button in buttons)
            {
                Assert.AreEqual("true", (string?)button.Attribute("DoNotPassEventsToChildren"), "a button with content children needs it or its text swallows the click");
                Assert.IsNotNull(button.Attribute("Command.Click"));
            }
        }
    }

    [TestMethod]
    public void EveryColourLiteral_ReadsAsRgba_SoNoTextIsNearlyTransparent()
    {
        // The engine reads #RRGGBBAA (TaleWorlds.Library.Color), so a literal written ARGB has its blue-green bytes as alpha.
        foreach (var movie in Movies)
        {
            var text = System.IO.File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "GUI", "PreFabs", "LongDefeat", movie + ".xml"));
            foreach (Match m in Regex.Matches(text, "\"#([0-9A-Fa-f]{8})\""))
                Assert.IsTrue(Convert.ToInt32(m.Groups[1].Value.Substring(6), 16) >= 0x80, movie + ": #" + m.Groups[1].Value + " has alpha below 0x80 read as RGBA");
        }
    }

    [TestMethod]
    public void TheShopPanel_HasTheFourSpeedButtons_WithAMarkOnTheCurrentOne()
    {
        var list = Root("LongDefeatPanel").Descendants().Single(e => (string?)e.Attribute("DataSource") == "{Speeds}");
        var button = list.Descendants("ButtonWidget").Single();

        Assert.AreEqual("ExecuteSelect", (string?)button.Attribute("Command.Click"));
        Assert.IsTrue(button.Descendants().Any(e => (string?)e.Attribute("IsVisible") == "@IsCurrent"), "the current speed is marked");
        Assert.IsTrue(Root("LongDefeatPanel").Descendants().Any(e => (string?)e.Attribute("Text") == "@SpeedTitle"));
    }

    [TestMethod]
    public void TheHudStrip_ShowsTheSpeed()
    {
        Assert.IsTrue(Root("LongDefeatHud").Descendants().Any(e => (string?)e.Attribute("Text") == "@SpeedText"));
    }

    [TestMethod]
    public void TheHudShowsTheVolleyLine_AndItsPanelRowOnlyBuys()
    {
        Assert.IsTrue(Root("LongDefeatHud").Descendants().Any(e => (string?)e.Attribute("Text") == "@VolleyText"));
        foreach (var panel in PanelRoots())
        {
            var commands = panel.Descendants().Select(e => (string?)e.Attribute("Command.Click")).Where(c => c != null).ToList();
            CollectionAssert.Contains(commands, "ExecuteBuyVolley");
            Assert.IsFalse(commands.Any(c => c!.Contains("FireVolley")), "N looses the volley; the panel never does (D24)");
        }
    }

    [TestMethod]
    public void ThePanelGroups_AreGatedByTheirOwnVisibility()
    {
        var gates = Root("LongDefeatPanel").Descendants().Select(e => (string?)e.Attribute("IsVisible")).Where(v => v != null).ToList();

        CollectionAssert.IsSubsetOf(new[] { "@IsShopVisible", "@IsEndVisible", "@IsPickVisible" }, gates);
    }

    [TestMethod]
    public void TheFortificationsGroup_BindsTheFortVM_ShowsOnItsOwnFlag_AndItsRowBuysOnClick()
    {
        var group = Root("LongDefeatPanel").Descendants().Single(e => (string?)e.Attribute("DataSource") == "{Fort}");

        Assert.AreEqual("@IsVisible", (string?)group.Attribute("IsVisible"));
        Assert.IsTrue(group.Descendants().Any(e => (string?)e.Attribute("Text") == "@Title"));
        var buttons = group.Descendants("ButtonWidget").ToList();
        Assert.AreEqual(5, buttons.Count, "field care, the gate level, the gate repair, the high towers and the Elven Volley");

        // Each row: its click, its enabled flag, its own visible flag, and its three lines.
        foreach (var (command, can, visible, texts) in new[]
                 {
                     ("ExecuteBuyCare", "@CanBuyCare", "@IsCareVisible", new[] { "@CareName", "@CareDetail", "@CarePrice" }),
                     ("ExecuteBuyGate", "@CanBuyGate", "@IsGateVisible", new[] { "@GateName", "@GateDetail", "@GatePrice" }),
                     ("ExecuteRepair", "@CanRepair", "@IsRepairVisible", new[] { "@RepairName", "@RepairDetail", "@RepairPrice" }),
                     ("ExecuteBuyTowers", "@CanBuyTowers", "@IsTowersVisible", new[] { "@TowersName", "@TowersDetail", "@TowersPrice" }),
                     ("ExecuteBuyVolley", "@CanBuyVolley", "@IsVolleyVisible", new[] { "@VolleyName", "@VolleyDetail", "@VolleyPrice" }),
                 })
        {
            var row = buttons.Single(b => (string?)b.Attribute("Command.Click") == command);
            Assert.AreEqual(can, (string?)row.Attribute("IsEnabled"), command);
            Assert.AreEqual(visible, (string?)row.Attribute("IsVisible"), command);
            foreach (var text in texts)
                Assert.IsTrue(row.Descendants().Any(e => (string?)e.Attribute("Text") == text), command + " " + text);
        }
    }

    [TestMethod]
    public void EverySpriteAndBrush_IsOneTheFordsPanelAlreadyUses_SoNoneIsInvented()
    {
        var fords = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "GUI", "PreFabs", "FordsOfWar", "FordsCommand.xml")).Root!.DescendantsAndSelf().ToList();

        foreach (var attribute in new[] { "Sprite", "Brush" })
        {
            var known = fords.Select(e => (string?)e.Attribute(attribute)).Where(v => v != null).ToHashSet();
            foreach (var movie in Movies)
            {
                var used = Root(movie).DescendantsAndSelf().Select(e => (string?)e.Attribute(attribute)).Where(v => v != null).Distinct().ToList();
                Assert.IsTrue(used.All(known.Contains), movie + " " + attribute + " values new to the repo: " + string.Join(", ", used.Where(v => !known.Contains(v))));
            }
        }
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

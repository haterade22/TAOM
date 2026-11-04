using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// A brush-typed attribute (<c>Brush</c>, <c>IconBrush</c>, ...) in a PrefabExtension's injected XML or in a TAOM GUI/PreFabs
/// file, and a GUI prefab's <c>Constant BrushName</c>, must name a brush the engine loads for TAOM's module set. Gauntlet
/// resolves an unknown name to the default brush without an error, so the widget just draws unstyled (#989: the Messengers
/// button named two brushes no module defines). PrefabExtension names are read from every C# source under Main/ and Client/
/// that declares one; GUI prefabs are parsed as XML (comments ignored, any quoting). Bindings (<c>{</c>), <c>@</c> values and
/// Gauntlet <c>!Constant</c> / <c>*Parameter</c> references are skipped.
/// The engine loads the top-level GUI/Brushes/*.xml of each module in launcher order, keyed by file name so a later module's
/// file of the same name replaces an earlier one, and TAOM's own folder last. The test models that with TAOM's DependedModule
/// list followed by TAOM, a safe subset of the launcher order (UIResourceManager.cs:117-123). A brush from a module TAOM does
/// not depend on (NavalDLC) is therefore not declared. Inconclusive without the install.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class PrefabExtensionBrushTests
{
    // The XML sits in C# string literals: plain ("Brush=\"X\""), verbatim ("Brush=""X""") or raw ("Brush="X"").
    private static readonly Regex BrushLiteral = new Regex(
        "\\b[A-Za-z]*Brush\\s*=\\s*(?:\\\\\"|\"\"|\")([^\"\\\\{@!*][^\"\\\\]*)", RegexOptions.Compiled);

    internal static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    /// <summary>
    /// The existing <c>under</c> folder of each module TAOM depends on, in <c>SubModule.xml</c> order, then TAOM's own, which
    /// the engine loads last. Both the brush and the sprite gate read their module set from here. Inconclusive without the install.
    /// </summary>
    internal static IEnumerable<string> ModuleFolders(params string[] under)
    {
        var game = GameDir();
        var modules = Path.Combine(game, "Modules");
        if (!Directory.Exists(modules))
            Assert.Inconclusive($"No Modules folder under {game}; the install is missing.");

        return XDocument.Load(RepoPath("Main", "_Module", "SubModule.xml"))
            .Descendants("DependedModule")
            .Select(e => (string?)e.Attribute("Id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => Path.Combine(new[] { modules, id! }.Concat(under).ToArray()))
            .Append(RepoPath(new[] { "Main", "_Module" }.Concat(under).ToArray()))
            .Where(Directory.Exists)
            .ToList();
    }

    private static HashSet<string> DeclaredBrushes()
    {
        // File name -> path: a later module's top-level file of the same name replaces an earlier one.
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in ModuleFolders("GUI", "Brushes"))
            foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.TopDirectoryOnly))
                files[Path.GetFileName(file)] = file;

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files.Values)
            foreach (var brush in XDocument.Load(file).Descendants("Brushes").Elements("Brush"))
            {
                var name = (string?)brush.Attribute("Name");
                if (!string.IsNullOrEmpty(name))
                    names.Add(name!);
            }

        if (names.Count < 100)
            Assert.Inconclusive($"Only {names.Count} brushes found under {GameDir()}; the install is missing or partial.");
        return names;
    }

    [TestMethod]
    public void TheDeclaredSet_ExcludesBrushesOfModulesTaomDoesNotDependOn()
    {
        var declared = DeclaredBrushes();
        Assert.IsTrue(declared.Contains("Popup.Button.Base"), "a Native brush must be declared");
        Assert.IsFalse(declared.Contains("Naval.Map.Party.Nameplate.Ship.Banner.Frame"),
            "a NavalDLC-only brush must not count: TAOM does not depend on NavalDLC");
    }

    // Values a GUI prefab may carry that are not a brush defined by a module.
    private static readonly HashSet<string> AllowedNonDeclaredNames = new HashSet<string>(StringComparer.Ordinal)
    {
        // A NavalDLC brush that vanilla prefabs name; TAOM does not load NavalDLC.
        "Naval.Map.Party.Nameplate.Ship.Banner.Frame",
    };

    // PartyUpgradeButtonWidget.MarinerTroopBrush is a BrushWidget that takes the Id of a child widget
    // (PartyUpgradeButtonWidget.cs:58), so that attribute holds a widget Id, not a brush name.
    private const string WidgetIdAttribute = "MarinerTroopBrush";

    private static bool IsLiteral(string value) =>
        value.Length > 0 && "{@!*".IndexOf(value[0]) < 0;

    private static IEnumerable<string> PrefabBrushValues(XDocument doc)
    {
        foreach (var element in doc.Descendants())
        {
            foreach (var attribute in element.Attributes())
                if (attribute.Name.LocalName.EndsWith("Brush", StringComparison.Ordinal)
                    && attribute.Name.LocalName != WidgetIdAttribute && IsLiteral(attribute.Value))
                    yield return attribute.Value;
            if (element.Name.LocalName == "Constant")
            {
                var brushName = (string?)element.Attribute("BrushName");
                if (brushName != null && IsLiteral(brushName))
                    yield return brushName;
            }
        }
    }

    [TestMethod]
    public void EveryGuiPrefabBrush_IsDeclaredByAModule()
    {
        var declared = DeclaredBrushes();
        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        var prefabs = RepoPath("Main", "_Module", "GUI", "PreFabs");
        var repoDir = RepoPath();

        var used = 0;
        var constantBrushNames = 0;
        foreach (var file in Directory.EnumerateFiles(prefabs, "*.xml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(file);
            constantBrushNames += doc.Descendants()
                .Count(e => e.Name.LocalName == "Constant" && e.Attribute("BrushName") != null);
            foreach (var name in PrefabBrushValues(doc))
            {
                used++;
                if (!declared.Contains(name) && !AllowedNonDeclaredNames.Contains(name))
                    unknown.Add($"{name}  ({file.Substring(repoDir.Length).TrimStart(Path.DirectorySeparatorChar)})");
            }
        }

        Assert.IsTrue(used > 0, "No brush value found in any GUI prefab; the scan is broken.");
        Assert.IsTrue(constantBrushNames > 0,
            "No Constant BrushName found in any GUI prefab; the Constant scan is broken or the prefabs no longer use them.");
        Console.WriteLine($"GUI prefab brush values checked: {used}; Constant BrushName elements: {constantBrushNames}");
        Assert.AreEqual(0, unknown.Count,
            "These GUI prefab brushes are not loaded by the engine for TAOM's module set, so Gauntlet draws the "
            + "widget with the default brush: " + Environment.NewLine + string.Join(Environment.NewLine, unknown));
    }

    [TestMethod]
    public void EveryPrefabExtensionBrush_IsDeclaredByAModule()
    {
        var declared = DeclaredBrushes();
        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        var repoDir = RepoPath();

        var used = 0;
        foreach (var file in CSharpFiles())
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("[PrefabExtension("))
                continue;
            foreach (Match m in BrushLiteral.Matches(text))
            {
                used++;
                var name = m.Groups[1].Value;
                if (!declared.Contains(name))
                    unknown.Add($"{name}  ({file.Substring(repoDir.Length).TrimStart(Path.DirectorySeparatorChar)})");
            }
        }

        Assert.IsTrue(used > 0, "No brush literal found in any PrefabExtension source; the scan is broken.");
        Assert.AreEqual(0, unknown.Count,
            "These PrefabExtension brushes are not loaded by the engine for TAOM's module set, so Gauntlet draws the "
            + "widget with the default brush: " + Environment.NewLine + string.Join(Environment.NewLine, unknown));
    }
}

/// <summary>
/// A <c>Brush.&lt;Property&gt;</c> attribute, in a GUI prefab or in the XML a PrefabExtension source injects, must name a
/// public property of <c>TaleWorlds.GauntletUI.Brush</c>. It reads only repo files and reflects on the referenced assembly,
/// so it needs no game install.
/// </summary>
[TestClass]
public class PrefabBrushPropertyTests
{
    // Brush.<Property>="..." inside the XML held by a C# string literal; the first segment is the property.
    private static readonly Regex BrushPropertyLiteral = new Regex(@"\bBrush\.([A-Za-z]+)(?:\.[A-Za-z.]+)?\s*=", RegexOptions.Compiled);

    [TestMethod]
    public void BrushPropertyLiteral_MatchesTheFirstSegmentCaseSensitively()
    {
        var names = BrushPropertyLiteral.Matches("<X Brush.FontSize=\"3\" Brush.fontsize=\"3\" IconBrush.Foo=\"1\" Brush.Color.Red=\"1\" />")
            .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        CollectionAssert.AreEqual(new[] { "FontSize", "fontsize", "Color" }, names);
    }

    [TestMethod]
    public void EveryGuiPrefabBrushProperty_NamesABrushProperty()
    {
        // WidgetExtensions.GetObjectAndProperty looks the segment up exactly and drops an unknown one without an error.
        var properties = new HashSet<string>(
            typeof(TaleWorlds.GauntletUI.Brush)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name),
            StringComparer.Ordinal);
        Assert.IsTrue(properties.Contains("FontSize"), "Brush reflection is broken: FontSize is missing.");

        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        var repoDir = RepoPath();
        var used = 0;
        foreach (var file in Directory.EnumerateFiles(RepoPath("Main", "_Module", "GUI", "PreFabs"), "*.xml", SearchOption.AllDirectories))
            foreach (var attribute in XDocument.Load(file).Descendants().SelectMany(e => e.Attributes()))
            {
                var name = attribute.Name.LocalName;
                if (!name.StartsWith("Brush.", StringComparison.Ordinal))
                    continue;
                used++;
                var property = name.Substring("Brush.".Length).Split('.')[0];
                if (!properties.Contains(property))
                    unknown.Add($"{name}  ({file.Substring(repoDir.Length).TrimStart(Path.DirectorySeparatorChar)})");
            }

        Assert.IsTrue(used > 0, "No Brush.<Property> attribute found in any GUI prefab; the scan is broken.");

        var usedInCSharp = 0;
        foreach (var file in CSharpFiles())
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("[PrefabExtension("))
                continue;
            foreach (Match m in BrushPropertyLiteral.Matches(text))
            {
                usedInCSharp++;
                if (!properties.Contains(m.Groups[1].Value))
                    unknown.Add($"Brush.{m.Groups[1].Value}  ({file.Substring(repoDir.Length).TrimStart(Path.DirectorySeparatorChar)})");
            }
        }

        Assert.IsTrue(usedInCSharp > 0, "No Brush.<Property> literal found in any PrefabExtension source; the C# scan is broken.");
        Assert.AreEqual(0, unknown.Count,
            "These GUI prefab or PrefabExtension attributes name no public property of TaleWorlds.GauntletUI.Brush "
            + "(compared case-sensitively), so Gauntlet silently drops them: "
            + Environment.NewLine + string.Join(Environment.NewLine, unknown));
    }
}

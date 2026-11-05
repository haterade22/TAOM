using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// A brush-typed attribute (<c>Brush</c>, <c>IconBrush</c>, ...) in a PrefabExtension's injected XML must name a brush the
/// engine loads for TAOM's module set. Gauntlet resolves an unknown name to the default brush without an error, so the
/// widget just draws unstyled (#989: the Messengers button named two brushes no module defines). The names are read from
/// every C# source under Main/ (RepoPaths.CodeRoots) that declares a PrefabExtension; bindings (<c>{</c>), <c>@</c> values and
/// Gauntlet <c>!Constant</c> / <c>*Parameter</c> references are skipped.
/// The engine loads the top-level GUI/Brushes/*.xml of each module in the SubModule.xml DependedModule order, keyed by file
/// name so a later module's file of the same name replaces an earlier one, and TAOM's own folder last. A brush from a module
/// TAOM does not depend on (NavalDLC) is therefore not declared. Inconclusive without the install.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class PrefabExtensionBrushTests
{
    // The XML sits in C# string literals: plain ("Brush=\"X\""), verbatim ("Brush=""X""") or raw ("Brush="X"").
    private static readonly Regex BrushLiteral = new Regex(
        "\\b[A-Za-z]*Brush\\s*=\\s*(?:\\\\\"|\"\"|\")([^\"\\\\{@!*][^\"\\\\]*)", RegexOptions.Compiled);

    private static HashSet<string> DeclaredBrushes()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var modules = Path.Combine(game, "Modules");
        if (!Directory.Exists(modules))
            Assert.Inconclusive($"No Modules folder under {game}; the install is missing.");

        var dependedIds = XDocument.Load(RepoPath("Main", "_Module", "SubModule.xml"))
            .Descendants("DependedModule")
            .Select(e => (string?)e.Attribute("Id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!);

        // File name -> path: a later module's top-level file of the same name replaces an earlier one.
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dirs = dependedIds.Select(id => Path.Combine(modules, id, "GUI", "Brushes"))
            .Append(RepoPath("Main", "_Module", "GUI", "Brushes"))
            .Where(Directory.Exists);
        foreach (var dir in dirs)
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
            Assert.Inconclusive($"Only {names.Count} brushes found under {game}; the install is missing or partial.");
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

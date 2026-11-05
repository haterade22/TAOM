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
/// A literal <c>Sprite="X"</c> in a TAOM GUI prefab or in the XML a PrefabExtension source injects must name a sprite the
/// engine knows, or Gauntlet draws nothing: no crash, no log (the brush twin of this gate is
/// <see cref="PrefabExtensionBrushTests"/>). A name resolves when it is a <c>GenericSprite</c> or <c>NineRegionSprite</c>
/// <c>Name</c> in the top-level <c>GUI/*SpriteData.xml</c> of a module TAOM depends on or of TAOM itself.
/// Vanilla sprites are legal, so a TAOM-only check would be wrong. Bindings (<c>{</c>), <c>@</c> values and Gauntlet
/// <c>!Constant</c> / <c>*Parameter</c> references are skipped. The module list is <see cref="PrefabExtensionBrushTests.ModuleFolders"/>, the one the brush gate uses. Inconclusive
/// without the install.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PrefabSpriteTests
{
    // The XML sits in C# string literals: plain ("Sprite=\"X\""), verbatim ("Sprite=""X""") or raw ("Sprite="X""). A plain
    // literal writes a path backslash as \\; a verbatim or raw literal writes it as a single \.
    private static readonly Regex SpriteLiteral = new Regex(
        "\\bSprite\\s*=\\s*(?:\\\\\"(?<v>[^\"\\\\{@!*](?:[^\"\\\\]|\\\\\\\\)*)|(?:\"\"|\")(?<v>[^\"{@!*][^\"]*))", RegexOptions.Compiled);

    // Sprites a prefab names that no module TAOM depends on defines.
    private static readonly HashSet<string> AllowedNonDeclaredNames = new HashSet<string>(StringComparer.Ordinal)
    {
        // NavalDLC defines it; the settlement nameplates carry it on a hidden PortIconWidget. TAOM does not load NavalDLC.
        @"Map\anchor_stroked",
    };

    private static HashSet<string> KnownSprites()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dir in PrefabExtensionBrushTests.ModuleFolders("GUI"))
            foreach (var file in Directory.EnumerateFiles(dir, "*SpriteData.xml", SearchOption.TopDirectoryOnly))
                foreach (var sprite in XDocument.Load(file).Descendants().Where(e => e.Name.LocalName == "GenericSprite" || e.Name.LocalName == "NineRegionSprite"))
                {
                    var name = (string?)sprite.Element("Name");
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name!.Trim());
                }

        if (names.Count < 1000)
            Assert.Inconclusive($"Only {names.Count} sprites found under {PrefabExtensionBrushTests.GameDir()}; the install is missing or partial.");

        return names;
    }

    private static bool IsLiteral(string value) =>
        value.Length > 0 && "{@!*".IndexOf(value[0]) < 0;

    internal static IEnumerable<string> PrefabSpriteValues(XDocument doc) =>
        doc.Descendants().SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName == "Sprite" && IsLiteral(a.Value))
            .Select(a => a.Value);

    internal static IEnumerable<string> ExtensionSpriteValues(string source) =>
        SpriteLiteral.Matches(source).Cast<Match>().Select(m => m.Groups["v"].Value.Replace(@"\\", @"\"));

    [TestMethod]
    public void TheKnownSet_HoldsVanillaSpritesButNotNavalOnes()
    {
        var known = KnownSprites();

        Assert.IsTrue(known.Contains("BlankWhiteSquare_9"), "a Native nine-region sprite must be known");
        Assert.IsFalse(known.Contains(@"Map\anchor_stroked"), "a NavalDLC-only sprite must not count: TAOM does not depend on NavalDLC");
    }

    [TestMethod]
    public void SpriteLiteral_ReadsPlainVerbatimAndEscapedBackslashForms()
    {
        CollectionAssert.AreEqual(new[] { "A_9" }, ExtensionSpriteValues("\"<W Sprite=\\\"A_9\\\" />\"").ToList());
        CollectionAssert.AreEqual(new[] { "B" }, ExtensionSpriteValues("@\"<W Sprite=\"\"B\"\" />\"").ToList());
        CollectionAssert.AreEqual(new[] { @"Dir\C" }, ExtensionSpriteValues("\"<W Sprite=\\\"Dir\\\\C\\\" />\"").ToList());
        CollectionAssert.AreEqual(new[] { @"Dir\C" }, ExtensionSpriteValues("@\"<W Sprite=\"\"Dir\\C\"\" />\"").ToList());
        CollectionAssert.AreEqual(new[] { @"Dir\C" }, ExtensionSpriteValues("\"\"\"<W Sprite=\"Dir\\C\" />\"\"\"").ToList());
        Assert.AreEqual(0, ExtensionSpriteValues("\"<W Sprite=\\\"@Bound\\\" Sprite=\\\"{Binding}\\\" />\"").Count());
    }

    [TestMethod]
    public void EveryGuiPrefabSprite_IsRegistered()
    {
        var known = KnownSprites();
        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        var repoDir = RepoPath();

        var used = 0;
        foreach (var file in Directory.EnumerateFiles(RepoPath("Main", "_Module", "GUI", "PreFabs"), "*.xml", SearchOption.AllDirectories))
            foreach (var name in PrefabSpriteValues(XDocument.Load(file)))
            {
                used++;
                if (!known.Contains(name) && !AllowedNonDeclaredNames.Contains(name))
                    unknown.Add($"{name}  ({file.Substring(repoDir.Length).TrimStart(Path.DirectorySeparatorChar)})");
            }

        var usedInCSharp = 0;
        foreach (var file in CSharpFiles())
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("[PrefabExtension("))
                continue;
            foreach (var name in ExtensionSpriteValues(text))
            {
                usedInCSharp++;
                if (!known.Contains(name) && !AllowedNonDeclaredNames.Contains(name))
                    unknown.Add($"{name}  ({file.Substring(repoDir.Length).TrimStart(Path.DirectorySeparatorChar)})");
            }
        }

        Assert.IsTrue(used > 100, "Too few Sprite values in the GUI prefabs; the scan is broken.");
        Assert.IsTrue(usedInCSharp > 0, "No Sprite literal found in any PrefabExtension source; the C# scan is broken.");
        Console.WriteLine($"GUI prefab Sprite values checked: {used}; PrefabExtension Sprite literals: {usedInCSharp}");
        Assert.AreEqual(0, unknown.Count,
            "These Sprite names are in no SpriteData of TAOM's module set, so Gauntlet draws "
            + "nothing and logs nothing. A TAOM sprite is its path under SpriteParts/ui_taom without the module prefix "
            + "(CareerSystem\\foo, not TAOM\\CareerSystem\\foo): " + Environment.NewLine + string.Join(Environment.NewLine, unknown));
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MenuLinkColors;

namespace TAOM.Tests.Features.MenuLinkColors;

/// <summary>
/// Pins the C# culture set, the shipped brush XML, and the shipped culture data to each other.
///
/// This exists because the failure mode is silent: <c>Brush.GetStyleOrDefault</c> returns the
/// brush's <c>Default</c> style for an unknown name with no error, so a style name emitted by C#
/// but missing from the XML renders the link in black body text — indistinguishable from a
/// deliberate design choice. These tests turn that into a red build.
/// </summary>
[TestClass]
public class MenuLinkBrushCoverageTests
{
    /// <summary>
    /// Relative-luminance window every menu link colour must sit inside.
    /// Upper bound: legible against the pale parchment background (~L 0.78).
    /// Lower bound: distinguishable from the BLACK body text, so a link still reads as a link.
    /// </summary>
    private const double MinLuminance = 0.035;
    private const double MaxLuminance = 0.10;

    private static string BrushFilePath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "GUI", "Brushes", "GameMenu.xml");
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static string CulturesFilePath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData", "taom_spcultures.xml");
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    /// <summary>Every &lt;Style&gt; under the GameMenu.InfoText brush, as name -&gt; the whole element.</summary>
    private static Dictionary<string, XElement> MenuInfoTextStyleElements()
    {
        var path = BrushFilePath();
        Assert.IsNotNull(path, "Could not locate Main/_Module/GUI/Brushes/GameMenu.xml from the test run directory.");

        var brush = XDocument.Load(path)
            .Descendants("Brush")
            .FirstOrDefault(b => (string)b.Attribute("Name") == "GameMenu.InfoText");
        Assert.IsNotNull(brush, "Brush 'GameMenu.InfoText' is missing from the shipped GameMenu.xml.");

        var styles = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var style in brush.Descendants("Style"))
        {
            var name = (string)style.Attribute("Name");
            if (name != null)
                styles[name] = style;
        }

        return styles;
    }

    /// <summary>Every &lt;Style&gt; under the GameMenu.InfoText brush, as name -&gt; FontColor.</summary>
    private static Dictionary<string, string> MenuInfoTextStyles()
    {
        var path = BrushFilePath();
        Assert.IsNotNull(path, "Could not locate Main/_Module/GUI/Brushes/GameMenu.xml from the test run directory.");

        var brush = XDocument.Load(path)
            .Descendants("Brush")
            .FirstOrDefault(b => (string)b.Attribute("Name") == "GameMenu.InfoText");
        Assert.IsNotNull(brush, "Brush 'GameMenu.InfoText' is missing from the shipped GameMenu.xml.");

        var styles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var style in brush.Descendants("Style"))
        {
            var name = (string)style.Attribute("Name");
            if (name != null)
                styles[name] = (string)style.Attribute("FontColor");
        }

        return styles;
    }

    /// <summary>WCAG relative luminance of an "#RRGGBBAA" brush colour. Alpha is ignored.</summary>
    private static double RelativeLuminance(string hex)
    {
        var rgb = hex.TrimStart('#');
        double Channel(int offset)
        {
            var c = Convert.ToInt32(rgb.Substring(offset, 2), 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }

    [TestMethod]
    public void EverySupportedCulture_HasAllThreeBrushStyles()
    {
        var styles = MenuInfoTextStyles();
        var missing = new List<string>();

        foreach (var cultureId in TaomCultureLinkStyles.SupportedCultureIds)
        {
            var baseName = TaomCultureLinkStyles.StyleNameFor(cultureId);
            // RichText appends ".MouseOver" / ".MouseDown" at render time; a missing variant makes
            // the link flash black on hover.
            foreach (var name in new[] { baseName, baseName + ".MouseOver", baseName + ".MouseDown" })
                if (!styles.ContainsKey(name))
                    missing.Add(name);
        }

        Assert.AreEqual(0, missing.Count,
            "Brush styles emitted by C# but absent from GameMenu.xml (these render as black body text): "
            + string.Join(", ", missing));
    }

    [TestMethod]
    public void EveryTaomLinkStyle_HasAnExplicitFontColor()
    {
        // An unset FontColor falls through to the brush's Default style, which is black.
        var offenders = MenuInfoTextStyles()
            .Where(s => s.Key.StartsWith(TaomCultureLinkStyles.StylePrefix, StringComparison.Ordinal))
            .Where(s => string.IsNullOrWhiteSpace(s.Value))
            .Select(s => s.Key)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Link styles with no FontColor (they inherit the black Default): " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void NoOrphanTaomLinkStyle_ExistsInTheBrush()
    {
        var known = new HashSet<string>(
            TaomCultureLinkStyles.SupportedCultureIds.Select(TaomCultureLinkStyles.StyleNameFor),
            StringComparer.Ordinal);

        var orphans = MenuInfoTextStyles().Keys
            .Where(n => n.StartsWith(TaomCultureLinkStyles.StylePrefix, StringComparison.Ordinal))
            .Select(n => n.EndsWith(".MouseOver", StringComparison.Ordinal)
                      || n.EndsWith(".MouseDown", StringComparison.Ordinal)
                ? n.Substring(0, n.LastIndexOf('.'))
                : n)
            .Distinct(StringComparer.Ordinal)
            .Where(n => !known.Contains(n))
            .ToList();

        Assert.AreEqual(0, orphans.Count,
            "Brush styles no culture can emit — dead weight, delete them: " + string.Join(", ", orphans));
    }

    [TestMethod]
    public void EveryMenuLinkColor_SitsInsideTheParchmentContrastWindow()
    {
        var offenders = new List<string>();

        foreach (var style in MenuInfoTextStyles())
        {
            if (!style.Key.StartsWith("Link", StringComparison.Ordinal)) continue;
            if (string.IsNullOrWhiteSpace(style.Value)) continue;

            var luminance = RelativeLuminance(style.Value);
            if (luminance < MinLuminance || luminance > MaxLuminance)
                offenders.Add($"{style.Key} ({style.Value}) L={luminance:F4}");
        }

        Assert.AreEqual(0, offenders.Count,
            $"Link colours outside the L {MinLuminance}–{MaxLuminance} window — too pale to read on the "
            + "parchment, or too dark to tell apart from the black body text: " + string.Join("; ", offenders));
    }

    [TestMethod]
    public void EverySupportedCultureId_ExistsInShippedCultureData()
    {
        // A style keyed on a culture that no longer exists would never render. Vanilla-remapped
        // ids (empire, aserai, vlandia, khuzait, sturgia, battania) live in spcultures.xslt rather
        // than taom_spcultures.xml, so they are excluded from this direction of the check.
        var vanillaRemapped = new HashSet<string>(
            new[] { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" }, StringComparer.Ordinal);

        var path = CulturesFilePath();
        Assert.IsNotNull(path, "Could not locate Main/_Module/ModuleData/taom_spcultures.xml.");

        var shipped = new HashSet<string>(
            XDocument.Load(path).Descendants("Culture")
                .Select(c => (string)c.Attribute("id"))
                .Where(id => id != null),
            StringComparer.Ordinal);

        var unknown = TaomCultureLinkStyles.SupportedCultureIds
            .Where(id => !vanillaRemapped.Contains(id) && !shipped.Contains(id))
            .ToList();

        Assert.AreEqual(0, unknown.Count,
            "Culture ids with a link style but no culture in taom_spcultures.xml: " + string.Join(", ", unknown));
    }

    [TestMethod]
    public void EveryLinkStyle_StatesItsGlowExplicitly()
    {
        // Inheritance differs between the two groups of styles, so neither can rely on the brush
        // Default. A style name NOT on the inherited Info.Text brush becomes a fresh Style whose
        // unset attributes DO fall back to Default. But a name that IS inherited was cloned by
        // Style.FillFrom, which assigns through the property setters and latches
        // _isTextGlowColorChanged = true — and vanilla sets TextGlowColor="#111111FF" on every
        // Info.Text Link style. So the retinted fallback group keeps a dark halo behind dark text
        // on the parchment unless it restates the value. Requiring it everywhere removes the trap.
        var offenders = new List<string>();

        foreach (var style in MenuInfoTextStyleElements())
        {
            if (!style.Key.StartsWith("Link", StringComparison.Ordinal)) continue;

            var glow = (string)style.Value.Attribute("TextGlowColor");
            if (!string.Equals(glow, "#00000000", StringComparison.OrdinalIgnoreCase))
                offenders.Add($"{style.Key} (TextGlowColor={glow ?? "unset"})");
        }

        Assert.AreEqual(0, offenders.Count,
            "Link styles that do not zero their glow explicitly — the inherited ones silently keep "
            + "vanilla's #111111FF dark halo: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void FallbackLinkStyles_AreOverriddenForTheParchment()
    {
        // Links the rewriter deliberately leaves alone (bandit cultures, unresolvable objects,
        // concept/unit links) fall through to these. Vanilla's inherited values were tuned for a
        // dark panel; without an override here they wash out on the parchment.
        var styles = MenuInfoTextStyles();

        foreach (var name in new[] { "Link", "Link.Settlement", "Link.Hero", "Link.Kingdom", "Link.Clan", "Link.Concept", "Link.Unit" })
        {
            Assert.IsTrue(styles.ContainsKey(name), $"Fallback style '{name}' is not overridden for the parchment.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(styles[name]), $"Fallback style '{name}' has no FontColor.");
        }
    }
}

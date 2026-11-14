using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Core;

/// <summary>
/// Variag and dwarf hero looks gate over the repo's own ModuleData (lords.xml, named companions and the
/// lords.xslt rebuilds, which replace a lord's face). The Variags of Khand (culture battania) are swarthy,
/// dark-haired men of the south-east; dwarves are fair to ruddy. Skin is bits 48-53 and hair colour bits 30-35
/// of the face key's first 64-bit part (value / 63; vanilla Aserai lords average about 37 skin). A Variag hero's
/// skin stays at 28 or above and hair at 44 or above. Dwarf women use the full human skin scale (dwarf men have
/// their own pale-to-ruddy one), so a dwarf woman's skin stays at 9 or below.
/// </summary>
[TestClass]
public class VariagDwarfAppearanceDataTests
{
    private sealed record HeroFace(string Id, string File, string Race, string Culture, bool IsFemale, string Key);

    private static string ModuleData => RepoPath("Main", "_Module", "ModuleData");

    internal static int Field(string key, int part, int shift)
    {
        var value = ulong.Parse(key.Substring(part * 16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (int)((value >> shift) & 63);
    }

    private static int Skin(string key) => Field(key, 0, 48);
    private static int Hair(string key) => Field(key, 0, 30);
    private static int Height(string key) => Field(key, 7, 19);

    /// <summary>Every hero face key in ModuleData, with race and culture merged over all of the hero's
    /// definitions (an xslt rebuild wins, since the game uses it).</summary>
    private static List<HeroFace> HeroFaces()
    {
        var defs = new List<(string Id, string File, bool Xslt, Dictionary<string, string> Attrs, string Key)>();
        foreach (var file in Directory.EnumerateFiles(ModuleData, "*.*", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)) continue;
            var ext = Path.GetExtension(file);
            if (ext != ".xml" && ext != ".xslt") continue;
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }
            var xslt = ext == ".xslt";
            foreach (var e in doc.Descendants().Where(d => d.Name.LocalName == "NPCCharacter"))
            {
                var id = (string)e.Attribute("id");
                if (id == null) continue;
                var attrs = e.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);
                defs.Add((id, Path.GetFileName(file), xslt, attrs, FaceKey(e)));
            }
            if (!xslt) continue;
            foreach (var t in doc.Descendants().Where(d => d.Name.LocalName == "template"))
            {
                var match = (string)t.Attribute("match") ?? "";
                const string prefix = "NPCCharacter[@id='";
                if (!match.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var id = match.Substring(prefix.Length).TrimEnd(']', '\'');
                var attrs = t.Descendants().Where(d => d.Name.LocalName == "attribute")
                    .GroupBy(d => (string)d.Attribute("name") ?? "")
                    .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
                defs.Add((id, Path.GetFileName(file), true, attrs, FaceKey(t)));
            }
        }

        var merged = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var d in defs.OrderBy(d => d.Xslt))
        {
            if (!merged.TryGetValue(d.Id, out var m)) merged[d.Id] = m = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var name in new[] { "race", "culture", "is_female", "is_hero" })
                if (d.Attrs.TryGetValue(name, out var v) && v.Length > 0 && (d.Xslt || !m.ContainsKey(name))) m[name] = v;
        }

        return defs
            .Where(d => d.Key != null)
            .Select(d => (d, m: merged[d.Id]))
            .Where(x => string.Equals(Get(x.m, "is_hero"), "true", StringComparison.OrdinalIgnoreCase))
            .Select(x => new HeroFace(x.d.Id, x.d.File, Get(x.m, "race") ?? "human",
                (Get(x.m, "culture") ?? "").Replace("Culture.", ""),
                string.Equals(Get(x.m, "is_female"), "true", StringComparison.OrdinalIgnoreCase), x.d.Key))
            .ToList();
    }

    private static string Get(Dictionary<string, string> m, string name) => m.TryGetValue(name, out var v) ? v : null;

    private static string FaceKey(XElement e)
    {
        var key = (string)e.Descendants().FirstOrDefault(d => d.Name.LocalName == "BodyProperties")?.Attribute("key");
        return key != null && key.Length == 128 ? key : null;
    }

    private static void AssertNone(IEnumerable<string> bad, string what)
    {
        var list = bad.ToList();
        Assert.AreEqual(0, list.Count, what + ": " + string.Join(", ", list));
    }

    private static List<HeroFace> Of(Func<HeroFace, bool> who, int atLeast)
    {
        var heroes = HeroFaces().Where(who).ToList();
        Assert.IsTrue(heroes.Count >= atLeast, $"Expected at least {atLeast} heroes to load, found {heroes.Count}.");
        return heroes;
    }

    private const int MinVariagSkin = 28;
    private const int MinVariagHair = 44;
    private const int MaxDwarfWomanSkin = 9;

    [TestMethod]
    public void VariagHeroes_AreSwarthyAndDarkHaired() =>
        AssertNone(Of(h => h.Race == "human" && h.Culture == "battania", 15)
            .Where(h => Skin(h.Key) < MinVariagSkin || Hair(h.Key) < MinVariagHair)
            .Select(h => $"{h.Id} ({h.File}) skin {Skin(h.Key)}/63 hair {Hair(h.Key)}/63"), "Variag heroes outside skin >= " + MinVariagSkin + ", hair >= " + MinVariagHair);

    [TestMethod]
    public void DwarfWomen_SkinStaysFair() =>
        AssertNone(Of(h => h.Race == "dwarf" && h.IsFemale, 5).Where(h => Skin(h.Key) > MaxDwarfWomanSkin)
            .Select(h => $"{h.Id} ({h.File}) skin {Skin(h.Key)}/63"), "Dwarf women above skin " + MaxDwarfWomanSkin);
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Infrastructure;

/// <summary>One definition of a hero's face: every 128-digit BodyProperties and BodyPropertiesMax key in it.</summary>
internal sealed record HeroFace(string Id, string File, string Race, string Culture, bool IsFemale, IReadOnlyList<string> Keys)
{
    /// <summary>The value of a key field for each of this definition's keys, for a failure message.</summary>
    public string Values(Func<string, int> field) => string.Join("/", Keys.Select(field));
}

/// <summary>
/// Reads the hero face keys of the repo's own ModuleData for the appearance data gates. A hero is a
/// definition in an xml file or an lords.xslt rebuild (which replaces a lord's face), with race, culture and
/// sex merged over all of the hero's definitions (an xslt rebuild wins, since the game uses it). A file that
/// does not parse throws, so a broken file cannot shrink the hero set silently. Fields are value / 63.
/// </summary>
internal static class HeroFaceKeys
{
    private static string ModuleData => RepoPath("Main", "_Module", "ModuleData");

    /// <summary>One 6-bit field of a key: the part (0 to 7, 16 hex digits each) and the bit shift in it.</summary>
    internal static int Field(string key, int part, int shift)
    {
        var value = ulong.Parse(key.Substring(part * 16, 16), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (int)((value >> shift) & 63);
    }

    internal static int Skin(string key) => Field(key, 0, 48);
    internal static int Hair(string key) => Field(key, 0, 30);
    internal static int Height(string key) => Field(key, 7, 19);

    internal static List<HeroFace> HeroFaces()
    {
        var defs = new List<(string Id, string File, bool Xslt, Dictionary<string, string> Attrs, List<string> Keys)>();
        foreach (var file in Directory.EnumerateFiles(ModuleData, "*.*", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar)) continue;
            var ext = Path.GetExtension(file);
            if (ext != ".xml" && ext != ".xslt") continue;
            var doc = XDocument.Load(file);
            var xslt = ext == ".xslt";
            foreach (var e in doc.Descendants().Where(d => d.Name.LocalName == "NPCCharacter"))
            {
                var id = (string)e.Attribute("id");
                if (id == null) continue;
                var attrs = e.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);
                defs.Add((id, Path.GetFileName(file), xslt, attrs, FaceKeys(e)));
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
                defs.Add((id, Path.GetFileName(file), true, attrs, FaceKeys(t)));
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
            .Where(d => d.Keys.Count > 0)
            .Select(d => (d, m: merged[d.Id]))
            .Where(x => string.Equals(Get(x.m, "is_hero"), "true", StringComparison.OrdinalIgnoreCase))
            .Select(x => new HeroFace(x.d.Id, x.d.File, Get(x.m, "race") ?? "human",
                (Get(x.m, "culture") ?? "").Replace("Culture.", ""),
                string.Equals(Get(x.m, "is_female"), "true", StringComparison.OrdinalIgnoreCase), x.d.Keys))
            .ToList();
    }

    private static string Get(Dictionary<string, string> m, string name) => m.TryGetValue(name, out var v) ? v : null;

    private static List<string> FaceKeys(XElement e) =>
        e.Descendants()
            .Where(d => d.Name.LocalName == "BodyProperties" || d.Name.LocalName == "BodyPropertiesMax")
            .Select(d => (string)d.Attribute("key"))
            .Where(k => k != null && k.Length == 128)
            .ToList();
}

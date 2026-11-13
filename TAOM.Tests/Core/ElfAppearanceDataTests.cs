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
/// Elf hero looks gate over the repo's own ModuleData (lords.xml, named companions and the lords.xslt
/// rebuilds, which replace a lord's face). The elf race in LOTRLOME_Armory uses the full human skin colour
/// scale, white to near black, so the face key decides how an elf looks. Fields (value / 63): skin is bits
/// 48-53 and hair colour bits 30-35 of the key's first 64-bit part; height is bits 19-24 of the eighth part.
/// Elves are fair (skin 0-5, the range of the elf troop templates fighter_rivendell and fighter_mirkwood),
/// Galadhrim are golden or fair haired (hair colour at most 22, light brown), and no elf is shorter than an
/// average man (height at least 32). Chaegermeister (lord_R3_1) is a Patreon tribute hero kept as authored.
/// </summary>
[TestClass]
public class ElfAppearanceDataTests
{
    private const int MaxFairSkin = 5;
    private const int MaxGaladhrimHair = 22;
    private const int MinElfHeight = 32;

    private static readonly HashSet<string> Exceptions = new(StringComparer.Ordinal) { "lord_R3_1" };

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

    private static List<HeroFace> Elves()
    {
        var elves = HeroFaces().Where(h => h.Race == "elf").ToList();
        Assert.IsTrue(elves.Count > 50, $"Expected the elf heroes to load, found {elves.Count}.");
        return elves;
    }

    private static void AssertNone(IEnumerable<string> bad, string what)
    {
        var list = bad.ToList();
        Assert.AreEqual(0, list.Count, what + ": " + string.Join(", ", list));
    }

    [TestMethod]
    public void Field_ReadsTheKeyBits()
    {
        var key = "0028280FD7FC23C2" + new string('0', 96) + "0000000000780000";
        Assert.AreEqual(40, Skin(key));
        Assert.AreEqual(63, Hair("0000000FC0000000" + new string('0', 112)));
        Assert.AreEqual(15, Height(key));
    }

    [TestMethod]
    public void ElfHeroes_SkinStaysFair() =>
        AssertNone(Elves().Where(h => !Exceptions.Contains(h.Id) && Skin(h.Key) > MaxFairSkin)
            .Select(h => $"{h.Id} ({h.File}) skin {Skin(h.Key)}/63"), "Elf heroes outside the fair skin range 0-" + MaxFairSkin);

    [TestMethod]
    public void GaladhrimHeroes_HairStaysGoldenOrFair() =>
        AssertNone(Elves().Where(h => h.Culture == "lothlorien" && Hair(h.Key) > MaxGaladhrimHair)
            .Select(h => $"{h.Id} ({h.File}) hair {Hair(h.Key)}/63"), "Galadhrim heroes with hair darker than " + MaxGaladhrimHair);

    [TestMethod]
    public void ElfHeroes_AreNotShorterThanAnAverageMan() =>
        AssertNone(Elves().Where(h => Height(h.Key) < MinElfHeight)
            .Select(h => $"{h.Id} ({h.File}) height {Height(h.Key)}/63"), "Elf heroes below height " + MinElfHeight);

    [TestMethod]
    public void Exceptions_StillExistAsElfHeroes()
    {
        var ids = new HashSet<string>(Elves().Select(h => h.Id), StringComparer.Ordinal);
        foreach (var id in Exceptions)
            Assert.IsTrue(ids.Contains(id), $"Exception {id} is no longer an elf hero; remove it from the list.");
    }
}

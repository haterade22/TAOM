using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// A culture cloned from another must not keep the source's identity in text the player reads.
///
/// This has now shipped twice. The first time (rca-new-factions-2026-06-02) the orc cultures went
/// out describing themselves as Gundabad. The second time, `lindon` — carved out of `rivendell` to
/// be Círdan's Falathrim at the Grey Havens — shipped with 62 contaminated strings: its culture
/// blurb read "Lindon, the Last Homely House ... led by Lord Elrond. Nestled in the Misty
/// Mountains", its clan names were the Ñoldorin royal houses (Fëanor, Fingolfin, Finarfin, Turgon),
/// its male-name pool ended with Fëanor and all seven of his sons, and it fielded a troop called
/// "[Lindon] Nõldorin Lancer". Blue Craig, in the Ered Luin, hunted "through the Misty Mountains".
///
/// Both clone scripts DID carry a contamination gate, and neither fired. The reason is the lesson:
/// each gate's forbidden-word list was derived from that script's own substitution table, so it
/// could only detect words somebody had already thought to remap. "Elrond" was in neither list, so
/// it was invisible to both halves at once. A check built from the same assumption as the thing it
/// checks is not a check.
///
/// This test is therefore deliberately INDEPENDENT of the tooling: the word lists below come from
/// what the source culture IS in Tolkien, not from any substitution table. Adding a new promoted
/// culture means adding its source's identity words here, by hand, from the lore.
/// </summary>
[TestClass]
public class ClonedCultureFlavorTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    /// <summary>
    /// Promoted culture -> words belonging to the culture it was carved out of. A hit in any
    /// player-facing string scoped to the promoted culture is contamination.
    /// </summary>
    private static readonly Dictionary<string, string[]> ForbiddenBySource = new()
    {
        // Lindon is Círdan's realm on the Gulf of Lune: Falathrim Sindar, shipwrights, the Grey
        // Havens. Everything Ñoldorin, Eregionic or valley-bound belongs to Rivendell.
        ["lindon"] = new[]
        {
            "Elrond", "Imladris", "Rivendell", "Last Homely House", "hidden valley", "Hidden Valley",
            "Trollshaws", "Noldor", "Nõldor", "Ñoldor", "Feanor", "Fëanor", "Fingolfin", "Finarfin",
            "Turgon", "Celebrimbor", "Gwaith-i-Mirdain", "Lambengolmor", "Glorfindel", "Erestor",
            "Maedhros", "Maglor", "Curufin", "Caranthir", "Celegorm", "Amras", "Amrod", "Finwe",
            "Himring", "Gondolin", "Arwen", "Lindir",
        },
        // Blue Craig is in the western Ered Luin above the Gulf of Lune — a continent away from
        // Goblin-town and the High Pass.
        ["bluecraig"] = new[]
        {
            "Goblin-town", "High Pass", "Misty Mountains", "Moria", "Gundabad", "Bolg",
            "Cirith Ungol", "Angmar", "Carn Dûm", "Iron Hills",
        },
    };

    private static void Scan(List<string> hits, string culture, string where, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        foreach (var word in ForbiddenBySource[culture].Where(value.Contains))
            hits.Add($"{where}: '{word}' in \"{Trim(value)}\"");
    }

    private static string Trim(string s) => s.Length <= 90 ? s : s.Substring(0, 90) + "…";

    [TestMethod]
    public void PromotedCultures_DoNotInheritTheSourceCulturesIdentity_InPlayerFacingText()
    {
        var hits = new List<string>();

        // 1. Registered strings, scoped by the culture named in the string id.
        foreach (var file in new[] { "taom_module_strings.xml", "taom_cc_strings.xml" })
        {
            var path = Path.Combine(ModuleDataPath, file);
            Assert.IsTrue(File.Exists(path), $"{file} not found at {path}");
            foreach (var e in XDocument.Load(path).Descendants("string"))
            {
                var id = (string?)e.Attribute("id") ?? "";
                var text = (string?)e.Attribute("text") ?? "";
                foreach (var culture in ForbiddenBySource.Keys)
                    if (id.IndexOf(culture, StringComparison.OrdinalIgnoreCase) >= 0)
                        Scan(hits, culture, $"{file}:{id}", text);
            }
        }

        // 2. The four culture-scoped narrative menus, scoped by each entry's own culture_id.
        foreach (var menu in new[] { "parents_menu.json", "youth_menu.json", "education_menu.json",
                                     "adulthood_menu.json" })
        {
            var path = Path.Combine(ModuleDataPath, "charactercreation", menu);
            Assert.IsTrue(File.Exists(path), $"{menu} not found at {path}");
            foreach (var o in JArray.Parse(File.ReadAllText(path)))
            {
                var culture = o.Value<string>("culture_id");
                if (culture == null || !ForbiddenBySource.ContainsKey(culture)) continue;
                var id = o.Value<string>("string_id") ?? "?";
                Scan(hits, culture, $"{menu}:{id}", o.Value<string>("text") ?? "");
                Scan(hits, culture, $"{menu}:{id}", o.Value<string>("description") ?? "");
            }
        }

        // 3. The culture blocks themselves — names, clan names, descriptions.
        var cultures = File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));
        foreach (var culture in ForbiddenBySource.Keys)
        {
            var block = Regex.Match(cultures, $"<Culture\\b[^>]*\\bid=\"{culture}\".*?</Culture>",
                RegexOptions.Singleline);
            Assert.IsTrue(block.Success, $"<Culture id=\"{culture}\"> not found");
            foreach (Match m in Regex.Matches(block.Value, "\\}([^\"]{2,200})\""))
                Scan(hits, culture, $"taom_spcultures.xml:{culture}", m.Groups[1].Value);
        }

        // 4. Each promoted culture's own troop display names.
        foreach (var culture in ForbiddenBySource.Keys)
        {
            var path = Path.Combine(ModuleDataPath, "troops", $"troops_{culture}.xml");
            if (!File.Exists(path)) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(path), "name=\"\\{=[^}]*\\}([^\"]+)\""))
                Scan(hits, culture, $"troops_{culture}.xml", m.Groups[1].Value);
        }

        Assert.AreEqual(0, hits.Count,
            "A promoted culture is still describing itself as the culture it was cloned from. This "
            + "is player-facing text — the culture-selection blurb, clan and character names, "
            + "narrative options, troop names. It has shipped twice before, both times because the "
            + "clone script's contamination gate was built from the same substitution table it was "
            + "meant to audit. Fix the text (tools/fix_promoted_culture_flavor.py) and widen that "
            + "script's tables:\n  " + string.Join("\n  ", hits.Take(25))
            + (hits.Count > 25 ? $"\n  ... and {hits.Count - 25} more" : ""));
    }
}

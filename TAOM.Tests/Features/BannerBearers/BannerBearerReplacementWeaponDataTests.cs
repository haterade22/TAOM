using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BannerBearers;

/// <summary>
/// Pins the engine's undeclared 1H invariant on <c>&lt;banner_bearer_replacement_weapons&gt;</c>.
///
/// Every vanilla culture ships only one-handed swords there, and
/// <c>SandboxBattleBannerBearersModel.GetBannerBearerReplacementWeapon</c> tier-matches with NO
/// weapon-class filter — the engine trusts the data. The banner itself sits in ExtraWeaponSlot as
/// a <c>HeldInOffHand + HasToBeHeldUp + DropOnWeaponChange</c> weapon, so a two-handed mainhand
/// can force a native drop of the banner during the reinforcement spawn's wieldInitialWeapons,
/// after which <c>BannerBearerLogic.SpawnBannerBearer</c>'s unguarded native slot-4 read
/// access-violates (issue #360, siege CTD at Glad Thaw,
/// docs/reviews/rca-banner-bearers-reinforcement-av-2026-07-25.md).
/// </summary>
[TestClass]
public class BannerBearerReplacementWeaponDataTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    // 1H crafting templates ship in both vanilla (empire_sword_1_t2 is a CraftedItem) and the
    // Armory. Anything two-handed or polearm-class re-opens the native banner-drop window.
    private static readonly Regex ForbiddenCraftingTemplate =
        new Regex("TwoHanded|Polearm|Pike", RegexOptions.IgnoreCase);

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ShippedCultures_EveryBannerBearerReplacementWeaponIsOneHanded()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game dir unresolved: " + string.Join("; ", GameAssemblies.Diagnostics));

        var references = CollectReplacementWeaponReferences();
        Assert.AreNotEqual(0, references.Count, "no <banner_bearer_replacement_weapons> entries found — parser broken?");

        var definitions = IndexWeaponDefinitions(GameAssemblies.GameDir);
        var failures = new List<string>();

        foreach (var (source, culture, itemId) in references)
        {
            if (!definitions.TryGetValue(itemId, out var def))
            {
                failures.Add($"{culture} ({source}): '{itemId}' is not defined in the Armory or vanilla item XML — broken reference");
                continue;
            }

            if (def.CraftingTemplate != null && ForbiddenCraftingTemplate.IsMatch(def.CraftingTemplate))
                failures.Add($"{culture} ({source}): '{itemId}' is a CraftedItem with crafting_template='{def.CraftingTemplate}' — replacement weapons MUST be one-handed");
            else if (def.CraftingTemplate == null && !string.Equals(def.ItemType, "OneHandedWeapon", StringComparison.OrdinalIgnoreCase))
                failures.Add($"{culture} ({source}): '{itemId}' has Type='{def.ItemType}' — replacement weapons MUST be Type=\"OneHandedWeapon\"");
        }

        Assert.AreEqual(0, failures.Count,
            "banner-bearer replacement weapons violate the engine's 1H invariant (issue #360):\n"
            + string.Join("\n", failures));
    }

    private static List<(string Source, string Culture, string ItemId)> CollectReplacementWeaponReferences()
    {
        var results = new List<(string, string, string)>();
        foreach (var file in new[] { "taom_spcultures.xml", "spcultures.xslt" })
        {
            var path = Path.Combine(ModuleDataPath, file);
            Assert.IsTrue(File.Exists(path), $"expected culture data file missing: {path}");
            var xml = File.ReadAllText(path);

            // Attribute a block to the nearest preceding Culture id (xml) / template match (xslt).
            foreach (Match block in Regex.Matches(xml,
                "<banner_bearer_replacement_weapons>(.*?)</banner_bearer_replacement_weapons>",
                RegexOptions.Singleline))
            {
                var preceding = xml.Substring(0, block.Index);
                var culture = Regex.Matches(preceding, "Culture\\[@id='([^']+)'\\]|<Culture\\b[^>]*?\\bid\\s*=\\s*\"([^\"]+)\"",
                        RegexOptions.Singleline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                    .LastOrDefault() ?? "<unknown>";

                foreach (Match item in Regex.Matches(block.Groups[1].Value, "id\\s*=\\s*\"(?:Item\\.)?([^\"]+)\""))
                    results.Add((file, culture, item.Groups[1].Value));
            }
        }
        return results;
    }

    private sealed class WeaponDefinition
    {
        public string? CraftingTemplate;
        public string? ItemType;
    }

    private static Dictionary<string, WeaponDefinition> IndexWeaponDefinitions(string gameDir)
    {
        var sources = new List<string>();
        foreach (var dir in new[]
        {
            Path.Combine(gameDir, "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items"),
            Path.Combine(gameDir, "Modules", "SandBoxCore", "ModuleData", "items"),
            Path.Combine(gameDir, "Modules", "Native", "ModuleData", "items"),
        })
        {
            if (Directory.Exists(dir))
                sources.AddRange(Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories));
        }
        Assert.AreNotEqual(0, sources.Count, $"no item XML sources found under '{gameDir}' — is the Armory installed?");

        var map = new Dictionary<string, WeaponDefinition>(StringComparer.Ordinal);
        foreach (var file in sources)
        {
            var xml = File.ReadAllText(file);
            foreach (Match tag in Regex.Matches(xml, "<(CraftedItem|Item)\\b(.*?)>", RegexOptions.Singleline))
            {
                var attrs = tag.Groups[2].Value;
                var id = Regex.Match(attrs, "\\bid\\s*=\\s*\"([^\"]+)\"");
                if (!id.Success || map.ContainsKey(id.Groups[1].Value)) continue;

                var def = new WeaponDefinition();
                if (tag.Groups[1].Value == "CraftedItem")
                {
                    var template = Regex.Match(attrs, "crafting_template\\s*=\\s*\"([^\"]+)\"");
                    def.CraftingTemplate = template.Success ? template.Groups[1].Value : "<none>";
                }
                else
                {
                    var type = Regex.Match(attrs, "\\bType\\s*=\\s*\"([^\"]+)\"");
                    def.ItemType = type.Success ? type.Groups[1].Value : "<none>";
                }
                map[id.Groups[1].Value] = def;
            }
        }
        return map;
    }
}

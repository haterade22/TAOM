using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Cross-references CC-selectable cultures against the two things the player receives at the moment
/// character creation finalizes: starting equipment and starting denars.
///
/// The third sibling of <see cref="NarrativeCultureCoverageTests"/> (narrative menus) and
/// <see cref="CareerCultureCoverageTests"/> (careers), and it exists for the same reason: both of
/// these coverage tables were hand-maintained and written before `goblin` / `mistymountainorcs`
/// existed, so both silently skipped them. Neither failure raises an exception — the player simply
/// walks out of character creation naked and broke:
///
///   - Equipment: <c>PlayerEquipmentRosterIds</c> builds the id unconditionally as
///     <c>player_char_creation_{culture}_{title}_{m|f}</c>. A missing roster makes
///     <c>PlayerEquipmentService</c> log <c>RosterNotFound</c> and apply nothing.
///   - Gold: <c>startup_resources_config.xml</c> defaults a missing culture to 0 denars, and
///     documents that as intentional ("Default 0 (no warning when missing)") — so an absent row is
///     indistinguishable from a deliberate zero.
///
/// Drive the invariant off the culture list, never off a hand-written list of cultures.
/// </summary>
[TestClass]
public class PlayerStartCoverageTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    /// <summary>
    /// TAOM repurposes the six vanilla cultures (vlandia=Rohan, empire=Dunland, and so on) rather
    /// than redefining them, so their CC rosters still ship in the game's own
    /// <c>SandBox/ModuleData/sandbox_equipment_sets.xml</c> — outside this repository and not
    /// present on a clean checkout. Verified 2026-08-10: that file carries exactly these six.
    /// They are excluded from the equipment check only; the gold check still covers them, because
    /// <c>startup_resources_config.xml</c> is ours.
    /// </summary>
    private static readonly HashSet<string> CulturesWithVanillaRosters =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "empire", "vlandia", "sturgia", "aserai", "battania", "khuzait" };

    private static List<string> LoadSelectableCultures()
    {
        var path = Path.Combine(ModuleDataPath, "charactercreation", "cultures.json");
        Assert.IsTrue(File.Exists(path), $"cultures.json not found at {path}");

        return JArray.Parse(File.ReadAllText(path))
            .Select(c => c.Value<string>("culture_id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList()!;
    }

    /// <summary>
    /// The title types a culture actually offers, read from its own youth_menu options. A culture
    /// needs a roster for every title it can hand out and no others — hardcoding the full title set
    /// would demand rosters nobody can reach (Gondor offers no `bard`, Erebor no `retainer`).
    /// </summary>
    private static Dictionary<string, List<string>> LoadTitleTypesByCulture()
    {
        var path = Path.Combine(ModuleDataPath, "charactercreation", "youth_menu.json");
        Assert.IsTrue(File.Exists(path), $"youth_menu.json not found at {path}");

        return JArray.Parse(File.ReadAllText(path))
            .Where(o => !string.IsNullOrEmpty(o.Value<string>("culture_id"))
                        && !string.IsNullOrEmpty(o.Value<string>("title_type")))
            .GroupBy(o => o.Value<string>("culture_id")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(o => o.Value<string>("title_type")!).Distinct().ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> LoadTaomRosterIds()
    {
        var path = Path.Combine(ModuleDataPath, "equipmentsets", "taom_char_creation_equipment.xml");
        Assert.IsTrue(File.Exists(path), $"taom_char_creation_equipment.xml not found at {path}");

        return new HashSet<string>(
            XDocument.Load(path).Descendants("EquipmentRoster")
                .Select(e => (string?)e.Attribute("id"))
                .Where(id => !string.IsNullOrEmpty(id))!,
            StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void EverySelectableCulture_HasCharacterCreationEquipment_ForEveryTitleTypeItOffers()
    {
        var titlesByCulture = LoadTitleTypesByCulture();
        var rosterIds = LoadTaomRosterIds();

        var gaps = new List<string>();
        foreach (var culture in LoadSelectableCultures())
        {
            if (CulturesWithVanillaRosters.Contains(culture)) continue;
            if (!titlesByCulture.TryGetValue(culture, out var titles)) continue;

            foreach (var title in titles)
            foreach (var gender in new[] { "m", "f" })
            {
                var id = $"player_char_creation_{culture}_{title}_{gender}";
                if (!rosterIds.Contains(id)) gaps.Add(id);
            }
        }

        Assert.AreEqual(0, gaps.Count,
            "A CC-selectable culture is missing the starting-equipment roster for a title type its "
            + "own youth_menu offers. PlayerEquipmentRosterIds builds this id unconditionally, so "
            + "the player finishes character creation with no equipment at all — silently, with "
            + "only a RosterNotFound log line. Author these rosters in "
            + "equipmentsets/taom_char_creation_equipment.xml:\n  " + string.Join("\n  ", gaps));
    }

    [TestMethod]
    public void EverySelectableCulture_HasNonZeroStartingGold()
    {
        var path = Path.Combine(ModuleDataPath, "startup_resources", "startup_resources_config.xml");
        Assert.IsTrue(File.Exists(path), $"startup_resources_config.xml not found at {path}");

        var playerGoldByCulture = XDocument.Load(path).Descendants("Culture")
            .Where(e => !string.IsNullOrEmpty((string?)e.Attribute("id")))
            .ToDictionary(
                e => (string)e.Attribute("id")!,
                e => (string?)e.Attribute("playerGold") ?? "",
                StringComparer.OrdinalIgnoreCase);

        var gaps = new List<string>();
        foreach (var culture in LoadSelectableCultures())
        {
            if (!playerGoldByCulture.TryGetValue(culture, out var gold))
            {
                gaps.Add($"{culture}: no <Culture> row at all");
                continue;
            }

            if (!int.TryParse(gold, out var parsed) || parsed <= 0)
                gaps.Add($"{culture}: playerGold=\"{gold}\"");
        }

        Assert.AreEqual(0, gaps.Count,
            "A CC-selectable culture has no starting denars. A missing row is indistinguishable "
            + "from a deliberate zero — the config documents 'Default 0 (no warning when missing)' "
            + "— so the player starts broke with nothing logged. Add the row to "
            + "startup_resources/startup_resources_config.xml:\n  " + string.Join("\n  ", gaps));
    }
}

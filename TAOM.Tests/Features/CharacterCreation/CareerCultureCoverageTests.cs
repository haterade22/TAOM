using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Config validation tests that cross-reference CC cultures against career
/// eligible cultures. Catches the class of bug where a selectable CC culture
/// has no eligible careers, which would produce an empty career menu.
///
/// Root cause: Review #24 found that shaghana/abanissa had zero careers,
/// causing KeyNotFoundException in vanilla TrySwitchToNextMenu.
/// </summary>
[TestClass]
public class CareerCultureCoverageTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    [TestMethod]
    public void EveryCCCulture_HasAtLeastOneEligibleCareer_OrIsDocumentedException()
    {
        // Arrange — load CC cultures from cultures.json
        var culturesPath = Path.Combine(ModuleDataPath, "charactercreation", "cultures.json");
        Assert.IsTrue(File.Exists(culturesPath), $"cultures.json not found at {culturesPath}");

        var culturesJson = JArray.Parse(File.ReadAllText(culturesPath));
        var ccCultures = culturesJson
            .Select(c => c.Value<string>("culture_id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        // Arrange — load career eligible cultures from taom_careers.xml
        var careersPath = Path.Combine(ModuleDataPath, "career_system", "taom_careers.xml");
        Assert.IsTrue(File.Exists(careersPath), $"taom_careers.xml not found at {careersPath}");

        var careersXml = XDocument.Load(careersPath);
        var coveredCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var career in careersXml.Descendants("Career"))
        {
            var eligibleCultures = career.Descendants("Culture")
                .Select(c => c.Attribute("id")?.Value)
                .Where(id => !string.IsNullOrEmpty(id));
            foreach (var cultureId in eligibleCultures)
                coveredCultures.Add(cultureId);
        }

        // Cultures that are known to not have careers yet.
        // When careers are added for these, remove them from this list.
        var documentedExceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Empty, and worth keeping empty. `goblin` and `mistymountainorcs` were here until
            // 2026-08-10, and `shaghana` and `abanissa` — the two this test was originally written
            // for, after Review #24 — until 2026-08-11, when each was given three careers cloned
            // from its nearest peer by tools/insert_new_faction_careers.py. Removing an entry is
            // what makes this test start guarding the culture — leaving a fixed culture parked
            // here is a permanent blind spot, not a harmless leftover.
        };

        // Act + Assert — every CC culture must have career coverage or be documented
        var uncoveredCultures = new List<string>();
        foreach (var cultureId in ccCultures)
        {
            if (!coveredCultures.Contains(cultureId) && !documentedExceptions.Contains(cultureId))
                uncoveredCultures.Add(cultureId);
        }

        Assert.AreEqual(0, uncoveredCultures.Count,
            $"CC cultures with no eligible careers and no documented exception: {string.Join(", ", uncoveredCultures)}. " +
            "Either add careers for these cultures in taom_careers.xml, or add them to documentedExceptions in this test.");
    }

    [TestMethod]
    public void EveryCareerInXml_HasMatchingJsonEntry()
    {
        // Arrange — load career IDs from XML
        var careersPath = Path.Combine(ModuleDataPath, "career_system", "taom_careers.xml");
        Assert.IsTrue(File.Exists(careersPath), $"taom_careers.xml not found at {careersPath}");

        var careersXml = XDocument.Load(careersPath);
        var xmlCareerIds = careersXml.Descendants("Career")
            .Select(c => c.Attribute("id")?.Value)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        // Arrange — load career IDs from JSON
        var jsonPath = Path.Combine(ModuleDataPath, "charactercreation", "career_menu.json");
        Assert.IsTrue(File.Exists(jsonPath), $"career_menu.json not found at {jsonPath}");

        var jsonArray = JArray.Parse(File.ReadAllText(jsonPath));
        var jsonCareerIds = new HashSet<string>(
            jsonArray.Select(j => j.Value<string>("career_string_id")).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.OrdinalIgnoreCase);

        // Act + Assert — every XML career must have a JSON entry
        var missingFromJson = xmlCareerIds.Where(id => !jsonCareerIds.Contains(id)).ToList();

        Assert.AreEqual(0, missingFromJson.Count,
            $"Careers in taom_careers.xml with no matching career_menu.json entry: {string.Join(", ", missingFromJson)}. " +
            "Add entries to career_menu.json for these careers.");
    }

    [TestMethod]
    public void EveryJsonEntry_HasMatchingCareerInXml()
    {
        // Arrange — load career IDs from JSON
        var jsonPath = Path.Combine(ModuleDataPath, "charactercreation", "career_menu.json");
        Assert.IsTrue(File.Exists(jsonPath), $"career_menu.json not found at {jsonPath}");

        var jsonArray = JArray.Parse(File.ReadAllText(jsonPath));
        var jsonCareerIds = jsonArray
            .Select(j => j.Value<string>("career_string_id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        // Arrange — load career IDs from XML
        var careersPath = Path.Combine(ModuleDataPath, "career_system", "taom_careers.xml");
        Assert.IsTrue(File.Exists(careersPath), $"taom_careers.xml not found at {careersPath}");

        var careersXml = XDocument.Load(careersPath);
        var xmlCareerIds = new HashSet<string>(
            careersXml.Descendants("Career").Select(c => c.Attribute("id")?.Value).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.OrdinalIgnoreCase);

        // Act + Assert — every JSON entry must have a matching XML career
        var orphanedJson = jsonCareerIds.Where(id => !xmlCareerIds.Contains(id)).ToList();

        Assert.AreEqual(0, orphanedJson.Count,
            $"career_menu.json entries with no matching Career in taom_careers.xml: {string.Join(", ", orphanedJson)}. " +
            "Remove orphaned entries or add the missing Career to taom_careers.xml.");
    }

    [TestMethod]
    public void CareerMenuJson_AllSkillNamesAreValid()
    {
        var validSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing",
            "Riding", "Athletics", "Crafting", "Scouting", "Tactics", "Roguery",
            "Charm", "Leadership", "Trade", "Steward", "Medicine", "Engineering"
        };

        var jsonPath = Path.Combine(ModuleDataPath, "charactercreation", "career_menu.json");
        var jsonArray = JArray.Parse(File.ReadAllText(jsonPath));

        var invalidSkills = new List<string>();
        foreach (var entry in jsonArray)
        {
            var careerId = entry.Value<string>("career_string_id") ?? "unknown";
            var skills = entry["skills"]?.ToObject<string[]>() ?? Array.Empty<string>();
            foreach (var skill in skills)
            {
                if (!validSkills.Contains(skill))
                    invalidSkills.Add($"{careerId}: '{skill}'");
            }
        }

        Assert.AreEqual(0, invalidSkills.Count,
            $"Invalid skill names in career_menu.json: {string.Join(", ", invalidSkills)}");
    }

    [TestMethod]
    public void CareerMenuJson_AllAttributeNamesAreValid()
    {
        var validAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Vigor", "Control", "Endurance", "Cunning", "Social", "Intelligence"
        };

        var jsonPath = Path.Combine(ModuleDataPath, "charactercreation", "career_menu.json");
        var jsonArray = JArray.Parse(File.ReadAllText(jsonPath));

        var invalidAttributes = new List<string>();
        foreach (var entry in jsonArray)
        {
            var careerId = entry.Value<string>("career_string_id") ?? "unknown";
            var attribute = entry.Value<string>("attribute") ?? "";
            if (!string.IsNullOrEmpty(attribute) && !validAttributes.Contains(attribute))
                invalidAttributes.Add($"{careerId}: '{attribute}'");
        }

        Assert.AreEqual(0, invalidAttributes.Count,
            $"Invalid attribute names in career_menu.json: {string.Join(", ", invalidAttributes)}");
    }
    [TestMethod]
    public void EreborCareerStartingRosters_EquipOnlyWarRams()
    {
        // The ram_rider career is the ONLY Erebor Cavalry career, and its starting roster used to
        // equip Item.saddle_horse: a VANILLA HORSE on a dwarf, which is exactly the case the
        // MOUNTED_DWARF validator exists to forbid. No gate caught it, because these rosters are
        // applied at runtime by CareerStartingEquipmentService rather than sitting on an
        // NPCCharacter, so the schema sweep never sees them. Issue #515.
        var path = Path.Combine(ModuleDataPath, "equipmentsets", "taom_career_starting_equipment.xml");
        Assert.IsTrue(File.Exists(path), $"taom_career_starting_equipment.xml not found at {path}");

        var allowedMounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Item.taom_war_ram_a",
            "Item.taom_war_ram_b",
        };

        var offenders = new List<string>();
        foreach (var roster in XDocument.Load(path).Descendants("EquipmentRoster"))
        {
            var rosterId = roster.Attribute("id")?.Value ?? string.Empty;
            if (!rosterId.StartsWith("player_career_erebor_", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var equipment in roster.Descendants("Equipment"))
            {
                if (!string.Equals(equipment.Attribute("slot")?.Value, "Horse", StringComparison.OrdinalIgnoreCase))
                    continue;
                var itemId = equipment.Attribute("id")?.Value ?? string.Empty;
                if (!allowedMounts.Contains(itemId))
                    offenders.Add($"{rosterId} equips {itemId}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "A dwarf career starting roster equips a mount that is not a war ram. Dwarves ride rams "
            + "and nothing else: the dwarf skeleton's rider bone is misaligned on a horse. Offenders: "
            + string.Join(", ", offenders));
    }

    [TestMethod]
    public void EreborCavalryCareerRoster_ActuallyGrantsAWarRam()
    {
        // The negative test above passes trivially if the mount is simply deleted. This is the
        // positive half: selecting the ram_rider career must actually spawn the player on a ram.
        var path = Path.Combine(ModuleDataPath, "equipmentsets", "taom_career_starting_equipment.xml");
        var doc = XDocument.Load(path);

        foreach (var suffix in new[] { "m", "f" })
        {
            var rosterId = $"player_career_erebor_cavalry_{suffix}";
            var roster = doc.Descendants("EquipmentRoster")
                .FirstOrDefault(r => string.Equals(r.Attribute("id")?.Value, rosterId, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(roster, $"{rosterId} is missing; the ram_rider career would fall back to culture-default gear");

            var mount = roster.Descendants("Equipment")
                .FirstOrDefault(e => string.Equals(e.Attribute("slot")?.Value, "Horse", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(mount, $"{rosterId} has no Horse slot; a ram_rider player would start on foot");
            StringAssert.StartsWith(mount.Attribute("id")?.Value ?? string.Empty, "Item.taom_war_ram",
                $"{rosterId} must mount the player on a war ram");
        }
    }

}

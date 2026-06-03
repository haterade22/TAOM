using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

[TestClass]
public class ConfigIdValidationTests
{
    private static readonly HashSet<string> ValidCultureIds = new HashSet<string>
    {
        // Custom cultures (LOTR names as StringIds)
        "gondor", "mordor", "erebor", "rivendell", "lothlorien",
        "mirkwood", "isengard", "gundabad", "dolguldur", "umbar",
        // New orc cultures (Misty Mountains expansion)
        "goblin", "mistymountainorcs",
        // XSLT cultures (vanilla engine StringIds)
        "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania"
    };

    private static readonly HashSet<string> ValidKingdomIds = new HashSet<string>
    {
        "empire_w", "empire_s", "empire", "vlandia", "battania",
        "aserai", "khuzait", "sturgia", "erebor", "rivendell",
        "lothlorien", "mirkwood", "isengard", "gundabad", "dolguldur",
        "umbar", "shaghana", "abanissa",
        // New kingdoms (Misty Mountains expansion): goblin + mistymountainorcs = new cultures;
        // lindon reuses Culture.rivendell; bluecraig reuses Culture.goblin.
        "goblin", "mistymountainorcs", "lindon", "bluecraig"
    };

    private static string FindModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static HashSet<string> CulturesWithFlaggedRoster(string filePath, string flagAttribute)
    {
        var cultures = new HashSet<string>();
        if (!File.Exists(filePath))
            return cultures;
        foreach (var roster in XDocument.Load(filePath).Descendants("EquipmentRoster"))
        {
            if (roster.Element("Flags")?.Attribute(flagAttribute)?.Value != "true")
                continue;
            var culture = roster.Attribute("culture")?.Value?.Replace("Culture.", "");
            if (!string.IsNullOrEmpty(culture))
                cultures.Add(culture);
        }
        return cultures;
    }

    // --- Child-generation equipment templates (RCA 2026-06-02) ---
    // A custom culture whose clans get lords triggers InitialChildGeneration -> HeroCreator.CreateChild
    // -> EquipmentSelectionModel.GetEquipmentForInitialChildrenGeneration. That model searches for a
    // culture-matching equipment roster flagged IsChildEquipmentTemplate (young) or
    // IsTeenagerEquipmentTemplate (teen), both also IsLordTemplate; if NONE exists it returns null and
    // vanilla NREs on new-game in EquipmentHelper.AssignHeroEquipmentFromEquipment. goblin +
    // mistymountainorcs shipped without these rosters and crashed every new game.
    [TestMethod]
    public void ChildGenerationCultures_HaveChildTeenAndLordEquipmentTemplates()
    {
        var md = FindModuleDataPath();
        if (md == null) { Assert.Inconclusive("ModuleData path not found — run from repo root"); return; }

        var childFile = Path.Combine(md, "equipmentsets", "taom_child_equipment_templates.xml");
        var lordFile = Path.Combine(md, "equipmentsets", "taom_lord_template_equipment.xml");

        var childCultures = CulturesWithFlaggedRoster(childFile, "IsChildEquipmentTemplate");
        var teenCultures = CulturesWithFlaggedRoster(lordFile, "IsTeenagerEquipmentTemplate");
        var lordCultures = CulturesWithFlaggedRoster(lordFile, "IsLordTemplate");

        // The new orc cultures were the regression — pin them so a future clone can't drop them again.
        foreach (var c in new[] { "goblin", "mistymountainorcs" })
            Assert.IsTrue(childCultures.Contains(c),
                $"Culture '{c}' has no IsChildEquipmentTemplate roster in taom_child_equipment_templates.xml — " +
                "InitialChildGeneration NREs in HeroCreator.CreateChild for this culture's lords.");

        // Consistency: every culture with child templates must also have teenager + adult lord
        // templates (a child grows into a teen then adult; child-gen draws teen equipment by age).
        var missingTeen = childCultures.Where(c => !teenCultures.Contains(c)).OrderBy(c => c).ToList();
        var missingLord = childCultures.Where(c => !lordCultures.Contains(c)).OrderBy(c => c).ToList();
        Assert.AreEqual(0, missingTeen.Count,
            $"Cultures with child templates but no IsTeenagerEquipmentTemplate lord roster: {string.Join(", ", missingTeen)}");
        Assert.AreEqual(0, missingLord.Count,
            $"Cultures with child templates but no IsLordTemplate roster: {string.Join(", ", missingLord)}");
    }

    [TestMethod]
    public void NewOrcCultures_HaveChildEducationEquipmentRosters()
    {
        var md = FindModuleDataPath();
        if (md == null) { Assert.Inconclusive("ModuleData path not found"); return; }

        var eduFile = Path.Combine(md, "equipmentsets", "taom_education_equipment_templates.xml");
        Assert.IsTrue(File.Exists(eduFile), "taom_education_equipment_templates.xml missing");
        var ids = XDocument.Load(eduFile).Descendants("EquipmentRoster")
            .Select(r => r.Attribute("id")?.Value ?? "").ToList();
        foreach (var c in new[] { "goblin", "mistymountainorcs" })
            Assert.IsTrue(ids.Any(id => id.StartsWith("child_education_equipments") && id.EndsWith("_" + c)),
                $"Culture '{c}' has no child_education_equipments_*_{c} rosters — childhood education events NRE.");
    }

    // --- Settlement Guards: Spear culture IDs ---

    [TestMethod]
    public void SettlementGuards_SpearCultureIds_AllValid()
    {
        var moduleDataPath = FindModuleDataPath();
        if (moduleDataPath == null)
            Assert.Inconclusive("ModuleData path not found — run from repo root");

        var path = Path.Combine(moduleDataPath, "settlement_guards", "settlement_guards_config.xml");
        if (!File.Exists(path))
            Assert.Inconclusive($"Config file not found: {path}");

        var doc = XDocument.Load(path);
        var spearsEl = doc.Root.Element("Spears");
        if (spearsEl == null)
            Assert.Fail("No <Spears> element in settlement_guards_config.xml");

        var invalid = new List<string>();
        foreach (var el in spearsEl.Elements("Spear"))
        {
            var culture = el.Attribute("culture")?.Value;
            if (!string.IsNullOrEmpty(culture) && !ValidCultureIds.Contains(culture))
                invalid.Add(culture);
        }

        Assert.AreEqual(0, invalid.Count,
            $"Invalid culture IDs in Spears config: {string.Join(", ", invalid)}. " +
            "XSLT cultures must use engine IDs (vlandia, empire, aserai, khuzait, sturgia, battania), not lore names.");
    }

    // --- Settlement Guards: Culture fallback IDs ---

    [TestMethod]
    public void SettlementGuards_CultureFallbackIds_AllValid()
    {
        var moduleDataPath = FindModuleDataPath();
        if (moduleDataPath == null)
            Assert.Inconclusive("ModuleData path not found");

        var path = Path.Combine(moduleDataPath, "settlement_guards", "settlement_guards_config.xml");
        if (!File.Exists(path))
            Assert.Inconclusive($"Config file not found: {path}");

        var doc = XDocument.Load(path);
        var invalid = new List<string>();

        foreach (var el in doc.Root.Elements("Culture"))
        {
            var id = el.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(id) && !ValidCultureIds.Contains(id))
                invalid.Add(id);
        }

        Assert.AreEqual(0, invalid.Count,
            $"Invalid culture IDs in Culture fallback config: {string.Join(", ", invalid)}");
    }

    // --- Lore name guard: catch common mistakes ---

    [TestMethod]
    [DataRow("rohan", "vlandia")]
    [DataRow("dunland", "empire")]
    [DataRow("harad", "aserai")]
    [DataRow("rhun", "khuzait")]
    [DataRow("dale", "sturgia")]
    [DataRow("khand", "battania")]
    [DataRow("dol_guldur", "dolguldur")]
    public void LoreNames_AreNotValidCultureIds(string loreName, string correctId)
    {
        Assert.IsFalse(ValidCultureIds.Contains(loreName),
            $"'{loreName}' should NOT be a valid culture ID. Use '{correctId}' instead.");
        Assert.IsTrue(ValidCultureIds.Contains(correctId),
            $"'{correctId}' should be a valid culture ID.");
    }

    // --- ValidCultureIds set is complete ---

    [TestMethod]
    public void ValidCultureIds_Contains18Cultures()
    {
        Assert.AreEqual(18, ValidCultureIds.Count,
            "Expected 18 valid culture IDs (12 custom incl. goblin + mistymountainorcs, + 6 XSLT)");
    }

    [TestMethod]
    public void ValidKingdomIds_Contains22Kingdoms()
    {
        Assert.AreEqual(22, ValidKingdomIds.Count,
            "Expected 22 valid kingdom IDs (incl. goblin, mistymountainorcs, lindon, bluecraig)");
    }
}

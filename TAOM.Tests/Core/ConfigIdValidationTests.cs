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

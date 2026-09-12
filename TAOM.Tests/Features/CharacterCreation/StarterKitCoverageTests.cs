using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Pins the starter-kit invariant (#569): every item a player-start roster hands out is a
/// <c>starter_</c> twin, in every weapon and armour slot, in both the battle and the civilian
/// set, across all three player roster files. There is no allowlist on purpose: the generator
/// (<c>tools/generate_starter_kit.py</c>) clones arrows and shields too, so an exception here would
/// only ever be a roster someone repointed at a real item by hand.
///
/// The rosters are applied at runtime by <c>PlayerEquipmentService</c> / <c>CareerStartingEquipmentService</c>
/// and never named by an <c>NPCCharacter</c>, so <c>validate_moduledata.py</c>'s troop sweeps cannot
/// see them; this is the same blind spot <see cref="CareerCultureCoverageTests"/> covers for mounts.
///
/// The six vanilla-mapped cultures (vlandia, empire, sturgia, aserai, battania, khuzait) get their
/// culture-default rosters from <c>taom_player_start_vanilla_override.xml</c>, which carries the same
/// ids as vanilla's rosters plus <c>_replaceWhileMerging="true"</c> on each roster element. Without
/// that attribute the engine's XML merge (<c>MBObjectManager.MergeElements</c>) APPENDS a second
/// battle set to vanilla's roster and the adapter keeps taking vanilla's first, so the whole
/// override is invisible in game while every file-level check passes.
/// </summary>
[TestClass]
public class StarterKitCoverageTests
{
    private static readonly string ModuleDataPath = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..",
            "Main", "_Module", "ModuleData"));

    private const string CultureDefaultFile = "taom_char_creation_equipment.xml";
    private const string CareerFile = "taom_career_starting_equipment.xml";
    private const string VanillaOverrideFile = "taom_player_start_vanilla_override.xml";

    private static readonly string[] PlayerRosterFiles = { CultureDefaultFile, CareerFile, VanillaOverrideFile };

    private static readonly HashSet<string> PlayerSlots = new HashSet<string>(StringComparer.Ordinal)
        { "Item0", "Item1", "Item2", "Item3", "Body", "Leg", "Cape", "Head", "Gloves" };

    private static readonly string[] NonPlayerMarkers = { "_childhood_age_", "_education_age_", "_show_" };

    private const string StarterPrefix = "Item.starter_";

    private static bool IsPlayerRoster(string id) =>
        (id.StartsWith("player_char_creation_", StringComparison.Ordinal)
         || id.StartsWith("player_career_", StringComparison.Ordinal))
        && !NonPlayerMarkers.Any(m => id.Contains(m));

    private static XDocument LoadRosters(string file)
    {
        var path = Path.Combine(ModuleDataPath, "equipmentsets", file);
        Assert.IsTrue(File.Exists(path), $"{file} not found at {path}");
        return XDocument.Load(path);
    }

    private static IEnumerable<XElement> PlayerRosters(XDocument doc) =>
        doc.Descendants("EquipmentRoster")
            .Where(r => IsPlayerRoster((string?)r.Attribute("id") ?? ""));

    [TestMethod]
    public void EveryPlayerStartRoster_UsesOnlyStarterItems_InEveryWeaponAndArmourSlot()
    {
        var violations = new List<string>();
        foreach (var file in PlayerRosterFiles)
        {
            foreach (var roster in PlayerRosters(LoadRosters(file)))
            foreach (var eq in roster.Descendants("Equipment"))
            {
                var slot = (string?)eq.Attribute("slot") ?? "";
                var id = (string?)eq.Attribute("id") ?? "";
                if (!PlayerSlots.Contains(slot)) continue;
                if (!id.StartsWith(StarterPrefix, StringComparison.Ordinal))
                    violations.Add($"{file}: {roster.Attribute("id")?.Value} {slot} {id}");
            }
        }

        Assert.AreEqual(0, violations.Count,
            "A player-start roster hands out a real item instead of its starter_ twin. Run "
            + "tools/generate_starter_kit.py --apply (to author the twin) and "
            + "tools/wire_starter_kit_rosters.py --apply (to repoint the roster):\n  "
            + string.Join("\n  ", violations));
    }

    [TestMethod]
    public void EveryPlayerStartRoster_RewiresBothBattleAndCivilianSets()
    {
        // Equipment.FillFrom copies the civilian set independently of the battle set, and the
        // career layer carries no civilian set, so the culture-default civilian set is the
        // civilian kit for every culture. A roster that lost it, or kept a real item in it,
        // hands the player the strong sword in every town.
        var gaps = new List<string>();
        foreach (var roster in PlayerRosters(LoadRosters(CultureDefaultFile)))
        {
            var id = roster.Attribute("id")?.Value ?? "";
            var sets = roster.Elements("EquipmentSet").ToList();
            var battle = sets.Where(s => s.Attribute("equipmentType") == null).ToList();
            var civilian = sets.Where(s => (string?)s.Attribute("equipmentType") == "Civilian").ToList();
            if (battle.Count != 1 || civilian.Count != 1)
            {
                gaps.Add($"{id}: {battle.Count} battle set(s), {civilian.Count} civilian set(s)");
                continue;
            }
            foreach (var set in sets)
            {
                var starters = set.Elements("Equipment")
                    .Count(e => PlayerSlots.Contains((string?)e.Attribute("slot") ?? "")
                                && ((string?)e.Attribute("id") ?? "").StartsWith(StarterPrefix, StringComparison.Ordinal));
                if (starters == 0)
                    gaps.Add($"{id}: {(set.Attribute("equipmentType") == null ? "battle" : "civilian")} set has no starter item");
            }
        }

        Assert.AreEqual(0, gaps.Count,
            "Every culture-default player roster needs one battle and one civilian set, both rewired:\n  "
            + string.Join("\n  ", gaps));
    }

    [TestMethod]
    public void PlayerStartRosters_DoNotRewire_ChildhoodEducationOrShowRosters()
    {
        // The inverse guard: the childhood/education/show stages and the parents' rosters share
        // the id prefix but are not the kit the player walks out with. A rewiring tool that
        // matched on the prefix alone would silently downgrade what those screens render.
        var doc = LoadRosters(CultureDefaultFile);
        var others = doc.Descendants("EquipmentRoster")
            .Where(r => !IsPlayerRoster((string?)r.Attribute("id") ?? ""))
            .ToList();
        Assert.IsTrue(others.Count > 0, "the culture-default file should still carry the non-player rosters");

        var touched = others
            .Where(r => r.Descendants("Equipment").Any(e =>
                ((string?)e.Attribute("id") ?? "").StartsWith(StarterPrefix, StringComparison.Ordinal)))
            .Select(r => r.Attribute("id")?.Value)
            .ToList();

        Assert.AreEqual(0, touched.Count,
            "A non-player roster references a starter_ item; the rewiring tool matched too widely:\n  "
            + string.Join("\n  ", touched));
    }

    [TestMethod]
    public void EveryVanillaMappedCulture_HasATaomOverrideRoster_ForEveryTitleItOffers()
    {
        var cultures = JArray.Parse(File.ReadAllText(Path.Combine(ModuleDataPath, "charactercreation", "cultures.json")))
            .Select(c => c.Value<string>("culture_id"))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        var titlesByCulture = JArray.Parse(File.ReadAllText(Path.Combine(ModuleDataPath, "charactercreation", "youth_menu.json")))
            .Where(o => !string.IsNullOrEmpty(o.Value<string>("culture_id")) && !string.IsNullOrEmpty(o.Value<string>("title_type")))
            .GroupBy(o => o.Value<string>("culture_id")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(o => o.Value<string>("title_type")!).Distinct().ToList(),
                StringComparer.OrdinalIgnoreCase);

        var cultureDefaultIds = new HashSet<string>(
            PlayerRosters(LoadRosters(CultureDefaultFile)).Select(r => r.Attribute("id")!.Value),
            StringComparer.OrdinalIgnoreCase);
        var overrideIds = new HashSet<string>(
            PlayerRosters(LoadRosters(VanillaOverrideFile)).Select(r => r.Attribute("id")!.Value),
            StringComparer.OrdinalIgnoreCase);

        // A vanilla-mapped culture is one whose player rosters TAOM does not author in its own
        // culture-default file; derived, so a new culture cannot slip into either bucket silently.
        // Known blind spot: the expectation comes from youth_menu.json, not from vanilla's roster
        // set. Vanilla also ships `mercenary` (and battania `kern`) rosters for these cultures that
        // TAOM's youth menu never offers; if a title is added to the menu without re-running
        // tools/wire_starter_kit_rosters.py, this test fails, which is the intended signal.
        var gaps = new List<string>();
        var covered = 0;
        foreach (var culture in cultures)
        {
            if (!titlesByCulture.TryGetValue(culture!, out var titles)) continue;
            if (cultureDefaultIds.Any(id => id.StartsWith($"player_char_creation_{culture}_", StringComparison.OrdinalIgnoreCase)))
                continue;
            covered++;
            foreach (var title in titles)
            foreach (var sex in new[] { "m", "f" })
            {
                var id = $"player_char_creation_{culture}_{title}_{sex}";
                if (!overrideIds.Contains(id)) gaps.Add(id);
            }
        }

        Assert.IsTrue(covered >= 6, $"expected the six vanilla-mapped cultures to be derived, found {covered}");
        Assert.AreEqual(0, gaps.Count,
            "A vanilla-mapped culture offers a youth title with no TAOM override roster, so that start "
            + "still reads vanilla's Calradian kit. Re-run tools/wire_starter_kit_rosters.py --apply:\n  "
            + string.Join("\n  ", gaps));
    }

    [TestMethod]
    public void EveryVanillaOverrideRoster_CarriesReplaceWhileMerging()
    {
        var doc = LoadRosters(VanillaOverrideFile);
        var rosters = doc.Descendants("EquipmentRoster").ToList();
        Assert.IsTrue(rosters.Count > 0, "the override file carries no rosters");

        var bad = rosters
            .Where(r => (string?)r.Attribute("_replaceWhileMerging") != "true"
                        || !IsPlayerRoster((string?)r.Attribute("id") ?? ""))
            .Select(r => r.Attribute("id")?.Value)
            .ToList();

        Assert.AreEqual(0, bad.Count,
            "Every override roster must carry _replaceWhileMerging=\"true\" on the roster element and a "
            + "player_char_creation_ id. Without the attribute MBObjectManager.MergeElements appends a "
            + "second battle set to vanilla's roster and the adapter keeps taking vanilla's first:\n  "
            + string.Join("\n  ", bad));
    }
}

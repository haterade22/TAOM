using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.Elk;

/// <summary>
/// Pins who rides the great elk (#636) and that every elk carries the elk saddle. The two items live in the
/// unversioned LOTRLOME_Armory, so these tests read only the ids the repo names: the two Mirkwood cavalry
/// troops, Thranduil and the five shared Mirkwood lord templates, the generated lord and ruler templates, and
/// the elk_rider career start. validate_moduledata.py resolves the ids against the installed Armory.
///
/// The saddle pin matters for the same reason the ram's barding pin does: elk_001 is the bare animal and the
/// rider's seat is the separate elk_saddle_001 mesh. Swapping only the Horse slot would leave a horse harness
/// (family_type 1, so the engine accepts it) draped over an elk.
/// </summary>
[TestClass]
public class ElkMountWiringTests
{
    private const string Elk = "Item.taom_elk_a";
    private const string ElkSaddle = "Item.taom_elk_saddle_a";

    // A property, not a cached field: the helper Assert.Fails when the folder is missing, which a static
    // initializer would turn into a TypeInitializationException for every test in the class.
    private static string ModuleDataPath => CultureDataFixture.ModuleDataPath();

    private static XDocument Load(params string[] relative)
    {
        var path = Path.Combine(new[] { ModuleDataPath }.Concat(relative).ToArray());
        Assert.IsTrue(File.Exists(path), $"{path} not found");
        return XDocument.Load(path);
    }

    /// <summary>The Horse or HorseHarness id among an element's direct equipment children, else null.
    /// troops_*.xml writes &lt;equipment&gt;, the equipmentsets files write &lt;Equipment&gt;.</summary>
    private static string? Slot(XElement scope, string slot) =>
        scope.Elements()
            .Where(e => string.Equals(e.Name.LocalName, "equipment", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(e => string.Equals((string?)e.Attribute("slot"), slot, StringComparison.Ordinal))
            ?.Attribute("id")?.Value;

    private static bool IsBattleSet(XElement set) => set.Attribute("equipmentType") == null;

    [TestMethod]
    public void MirkwoodCavalryTroops_RideTheElkWithItsSaddle()
    {
        var doc = Load("troops", "troops_mirkwood.xml");
        foreach (var troopId in new[] { "mirkwood_rochenlas", "mirkwood_beleglas" })
        {
            var troop = doc.Descendants("NPCCharacter").SingleOrDefault(n => (string?)n.Attribute("id") == troopId);
            Assert.IsNotNull(troop, $"{troopId} is missing from troops_mirkwood.xml");
            var equipments = troop.Element("Equipments");
            Assert.IsNotNull(equipments, $"{troopId} has no <Equipments>");

            // Pinned on the troop-level override on purpose: BasicCharacterObject adds the rosters first, then
            // MBEquipmentRoster.AddOverriddenEquipments deserializes each direct <equipment> child into EVERY set
            // (v1.5.3), civilian included. elk.md's "the troops ride the elk into town" rests on this form.
            Assert.AreEqual(Elk, Slot(equipments, "Horse"),
                $"{troopId} must ride the elk through the troop-level override, which reaches every set");
            Assert.AreEqual(ElkSaddle, Slot(equipments, "HorseHarness"), $"{troopId} must carry the elk saddle");
        }
    }

    [TestMethod]
    public void ThranduilAndTheMirkwoodLordTemplates_RideTheElkWithItsSaddle()
    {
        // thranduil_bat_equipment is his own set; the five templates are shared by the 28 other mounted-by-default
        // Mirkwood lords and are the culture's default battle roster. Legolas keeps his horseless set.
        var doc = Load("equipmentsets", "taom_equipment_sets_mirkwood.xml");
        var ids = new[] { "thranduil_bat_equipment" }
            .Concat("abcde".Select(c => $"mirkwood_bat_template_medium_{c}"));
        foreach (var rosterId in ids)
        {
            var roster = doc.Descendants("EquipmentRoster").SingleOrDefault(r => (string?)r.Attribute("id") == rosterId);
            Assert.IsNotNull(roster, $"{rosterId} is missing");
            var sets = roster.Elements("EquipmentSet").Where(IsBattleSet).ToList();
            Assert.IsTrue(sets.Count > 0, $"{rosterId} has no battle set");
            foreach (var set in sets)
            {
                Assert.AreEqual(Elk, Slot(set, "Horse"), $"{rosterId} must ride the elk");
                Assert.AreEqual(ElkSaddle, Slot(set, "HorseHarness"), $"{rosterId} must carry the elk saddle");
            }
        }
    }

    [TestMethod]
    public void GeneratedMirkwoodLordAndRulerTemplates_RideTheElkWithItsSaddle()
    {
        // taom_lord_template_equipment.xml copies each culture's FIRST bat_template roster
        // (mirkwood_bat_template_medium_a) into its IsLordTemplate / IsKingdomRulerTemplate battle rosters. The
        // engine hands those to every Mirkwood hero who comes of age, to a new Mirkwood ruler after Thranduil and to
        // spc_mirkwood_lord_1/2 (lord and rebel templates), so a stale copy puts all of them back on horses.
        var doc = Load("equipmentsets", "taom_lord_template_equipment.xml");
        var ids = new[] { "lord", "ruler" }
            .SelectMany(rank => new[] { "male", "female" }.Select(sex => $"taom_mirkwood_{rank}_battle_{sex}"));
        foreach (var rosterId in ids)
        {
            var roster = doc.Descendants("EquipmentRoster").SingleOrDefault(r => (string?)r.Attribute("id") == rosterId);
            Assert.IsNotNull(roster, $"{rosterId} is missing");
            var set = roster.Elements("EquipmentSet").SingleOrDefault(IsBattleSet);
            Assert.IsNotNull(set, $"{rosterId} has no single battle set");
            Assert.AreEqual(Elk, Slot(set, "Horse"), $"{rosterId} must ride the elk, as mirkwood_bat_template_medium_a does");
            Assert.AreEqual(ElkSaddle, Slot(set, "HorseHarness"), $"{rosterId} must carry the elk saddle");
        }
    }

    [TestMethod]
    public void ElkRiderCareerStart_MountsThePlayerOnTheElkWithItsSaddle()
    {
        // elk_rider is Mirkwood's only Cavalry career (CareerSystemIoC), so player_career_mirkwood_cavalry_m/_f
        // belong to it alone. They handed out saddle_horse + light_harness before #636.
        var doc = Load("equipmentsets", "taom_career_starting_equipment.xml");
        foreach (var suffix in new[] { "m", "f" })
        {
            var rosterId = $"player_career_mirkwood_cavalry_{suffix}";
            var roster = doc.Descendants("EquipmentRoster").SingleOrDefault(r => (string?)r.Attribute("id") == rosterId);
            Assert.IsNotNull(roster, $"{rosterId} is missing; the elk_rider career would fall back to culture-default gear");
            var set = roster.Elements("EquipmentSet").SingleOrDefault(IsBattleSet);
            Assert.IsNotNull(set, $"{rosterId} has no single battle set");
            Assert.AreEqual(Elk, Slot(set, "Horse"), $"{rosterId} must mount the elk_rider player on the elk");
            Assert.AreEqual(ElkSaddle, Slot(set, "HorseHarness"), $"{rosterId} must start the player on the elk saddle");
        }
    }

    [TestMethod]
    public void EveryElkInRepoData_CarriesTheElkSaddle()
    {
        // The general form of the three pins above, so a set added later cannot mount an elk on a horse harness.
        var offenders = new List<string>();
        var elksSeen = 0;
        foreach (var folder in new[] { "troops", "equipmentsets" })
        foreach (var path in Directory.GetFiles(Path.Combine(ModuleDataPath, folder), "*.xml"))
        {
            var file = Path.GetFileName(path);
            foreach (var scope in XDocument.Load(path).Descendants()
                         .Where(e => e.Name.LocalName is "EquipmentSet" or "EquipmentRoster" or "Equipments"))
            {
                // A troop roster inherits its <Equipments>-level overrides, which win over its own slots (see above).
                var parent = scope.Name.LocalName == "EquipmentRoster" && scope.Parent?.Name.LocalName == "Equipments"
                    ? scope.Parent
                    : null;
                var horse = (parent == null ? null : Slot(parent, "Horse")) ?? Slot(scope, "Horse");
                if (!string.Equals(horse, Elk, StringComparison.Ordinal)) continue;
                elksSeen++;
                var harness = (parent == null ? null : Slot(parent, "HorseHarness")) ?? Slot(scope, "HorseHarness");
                if (!string.Equals(harness, ElkSaddle, StringComparison.Ordinal))
                {
                    var owner = scope.AncestorsAndSelf()
                        .Select(e => (string?)e.Attribute("id"))
                        .FirstOrDefault(id => !string.IsNullOrEmpty(id)) ?? "(unnamed)";
                    offenders.Add($"{file}: {owner} pairs the elk with {harness ?? "no harness"}");
                }
            }
        }

        Assert.IsTrue(elksSeen > 0, "no elk found in troops/ or equipmentsets/; the pairing check would pass vacuously");
        Assert.AreEqual(0, offenders.Count,
            "An elk is harnessed with something other than the elk saddle. The seat is the elk_saddle_001 mesh, "
            + "not part of the elk body:\n  " + string.Join("\n  ", offenders));
    }
}

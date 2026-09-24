using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.Elk;

/// <summary>
/// Pins who rides the great elk (#636) and that every elk or moose carries the elk saddle. The items live in the
/// unversioned LOTRLOME_Armory, so these tests read only the ids the repo names; validate_moduledata.py resolves
/// them against the installed Armory. Since 2026-09-23 (Mike, #646) the great elk carries Mirkwood's top cavalry
/// troop only: the moose took Thranduil and the lords, the Animalia elk the lower cavalry and the elk_rider career
/// start (pinned in AnimaliaMountWiringTests).
///
/// The saddle pin matters for the same reason the ram's barding pin does: elk_001 is the bare animal and the
/// rider's seat is the separate elk_saddle_001 mesh, and the Animalia elk and moose are bare animals too.
/// Swapping only the Horse slot would leave a horse harness (family_type 1, so the engine accepts it) draped
/// over an elk.
/// </summary>
[TestClass]
public class ElkMountWiringTests
{
    private const string Elk = "Item.taom_elk_a";
    private const string ElkSaddle = "Item.taom_elk_saddle_a";

    /// <summary>Every mount that is a bare elk-family body and takes the elk saddle as its seat (#636, #646).</summary>
    private static readonly HashSet<string> ElkBodies = new(StringComparer.Ordinal)
    {
        Elk, "Item.taom_animalia_elk_a", "Item.taom_animalia_moose_a",
    };

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

    [TestMethod]
    public void MirkwoodTopCavalry_RidesTheGreatElkWithItsSaddle()
    {
        // mirkwood_beleglas is the top of Mirkwood's cavalry line (mirkwood_rochenlas upgrades into it and rides the
        // Animalia elk). Pinned on the troop-level override on purpose: BasicCharacterObject adds the rosters first,
        // then MBEquipmentRoster.AddOverriddenEquipments deserializes each direct <equipment> child into EVERY set
        // (v1.5.3), civilian included. elk.md's "the troops ride the elk into town" rests on this form.
        const string troopId = "mirkwood_beleglas";
        var troop = Load("troops", "troops_mirkwood.xml").Descendants("NPCCharacter")
            .SingleOrDefault(n => (string?)n.Attribute("id") == troopId);
        Assert.IsNotNull(troop, $"{troopId} is missing from troops_mirkwood.xml");
        Assert.IsFalse(troop.Descendants("upgrade_target").Any(), $"{troopId} upgrades further, so it is no longer the top cavalry");
        var equipments = troop.Element("Equipments");
        Assert.IsNotNull(equipments, $"{troopId} has no <Equipments>");
        Assert.AreEqual(Elk, Slot(equipments, "Horse"),
            $"{troopId} must ride the great elk through the troop-level override, which reaches every set");
        Assert.AreEqual(ElkSaddle, Slot(equipments, "HorseHarness"), $"{troopId} must carry the elk saddle");
    }

    [TestMethod]
    public void EveryElkOrMooseInRepoData_CarriesTheElkSaddle()
    {
        // The general form of the rider pins, so a set added later cannot mount an elk or the moose on a horse
        // harness.
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
                if (horse == null || !ElkBodies.Contains(horse)) continue;
                elksSeen++;
                var harness = (parent == null ? null : Slot(parent, "HorseHarness")) ?? Slot(scope, "HorseHarness");
                if (!string.Equals(harness, ElkSaddle, StringComparison.Ordinal))
                {
                    var owner = scope.AncestorsAndSelf()
                        .Select(e => (string?)e.Attribute("id"))
                        .FirstOrDefault(id => !string.IsNullOrEmpty(id)) ?? "(unnamed)";
                    offenders.Add($"{file}: {owner} pairs {horse} with {harness ?? "no harness"}");
                }
            }
        }

        Assert.IsTrue(elksSeen > 0, "no elk or moose found in troops/ or equipmentsets/; the pairing check would pass vacuously");
        Assert.AreEqual(0, offenders.Count,
            "An elk or the moose is harnessed with something other than the elk saddle. The seat is the elk_saddle_001 mesh, "
            + "not part of the elk body:\n  " + string.Join("\n  ", offenders));
    }
}

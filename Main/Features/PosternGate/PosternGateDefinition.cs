using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Features.TaomBuildings;

namespace TAOM.Features.PosternGate;

/// <summary>
/// The Postern Gate family (docs/features/postern-gate.md): one building, six names. Every fief holds exactly one, chosen
/// by its culture; a culture no variant lists gets the plain Postern Gate. Culture ids are the data's own ids (the
/// definition test reads them from taom_spcultures.xml): <c>empire</c> is Dunland and <c>battania</c> Khand there, so
/// neither is listed. Costs are the config's, and the effect line shows the configured percent.
/// </summary>
internal static class PosternGateDefinition
{
    internal const string FamilyId = "postern_gate";

    // Every variant id below is saved with each town that holds it: add new ids, never rename or remove one. A save
    // holding an id TAOM no longer registers loses that building and the town's whole build queue (Town.AfterLoad).
    // PosternGateDefinitionTests.Variants_AreTheSixSavedIds pins them.
    internal const string DefaultVariantId = "building_taom_postern_gate";

    // The town screen resolves sprites by their registered name, which includes the sprite folder (docs/features/gui-sprite-system.md).
    // The small variant adds "_t". The PNGs live in GUI/SpriteParts/ui_taom/TAOM/TownManagement/.
    internal const string SpriteName = "TAOM\\TownManagement\\taom_building_postern_gate";

    internal static TaomBuildingDefinition Create(IReadOnlyList<int> costs, IReadOnlyList<float> reductions,
        IPosternGateSettingsProvider settings) => new(
        FamilyId,
        new[]
        {
            new TaomBuildingVariant(DefaultVariantId,
                "{=taom_postern_name}Postern Gate",
                "{=taom_postern_desc}A small gate in the curtain wall, kept shut and known to few. In a siege, parties slip into or out of this fief with fewer losses.",
                Array.Empty<string>()),
            new TaomBuildingVariant("building_taom_postern_gate_dwarf",
                "{=taom_postern_dwarf_name}Hidden Door",
                "{=taom_postern_dwarf_desc}A door cut into the living stone, its seam hidden from any eye that does not know the rune. In a siege, parties slip into or out of this fief with fewer losses.",
                new[] { "erebor", "eredluin" }),
            new TaomBuildingVariant("building_taom_postern_gate_rohan",
                "{=taom_postern_rohan_name}Culvert",
                "{=taom_postern_rohan_desc}A drain under the earthen wall, wide enough for a rider who leads his horse. In a siege, parties slip into or out of this fief with fewer losses.",
                new[] { "vlandia", "southrhovanion" }),
            new TaomBuildingVariant("building_taom_postern_gate_gondor",
                "{=taom_postern_gondor_name}Sally Port",
                "{=taom_postern_gondor_desc}A stout gate low in the wall, where a company can march out under the eyes of the watch. In a siege, parties slip into or out of this fief with fewer losses.",
                new[] { "gondor", "arthedain", "cardolan", "rhudaur" }),
            new TaomBuildingVariant("building_taom_postern_gate_elf",
                "{=taom_postern_elf_name}Hidden Way",
                "{=taom_postern_elf_desc}A path through roots and rock that only the Elves can follow. In a siege, parties slip into or out of this fief with fewer losses.",
                new[] { "rivendell", "mirkwood", "lothlorien", "lindon" }),
            new TaomBuildingVariant("building_taom_postern_gate_orc",
                "{=taom_postern_orc_name}Bolt-Hole",
                "{=taom_postern_orc_desc}A crawl-tunnel dug in the dark and shored with scrap, used by whoever is quick enough to squeeze through. In a siege, parties slip into or out of this fief with fewer losses.",
                new[] { "mordor", "isengard", "gundabad", "dolguldur", "goblin", "mistymountainorcs", "bluecraig" }),
        },
        costs,
        true,
        SpriteName,
        // Read at display time: with the switch off the building cuts nothing, so its screen shows no effect line.
        level => level is >= 1 and <= 3 && level <= reductions.Count && settings.CutEnabled
            ? new BuildingEffectText(
                "{=taom_postern_effect}{PERCENT}% fewer troops lost breaking into or out of a siege here",
                "PERCENT",
                Math.Round(reductions[level - 1] * 100.0).ToString(CultureInfo.InvariantCulture))
            : null);
}

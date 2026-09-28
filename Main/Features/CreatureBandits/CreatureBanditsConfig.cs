using System.Collections.Generic;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// Creature Bandits (#692): hostile bandit-only creatures that fight with no rider. Each creature is a
/// hidden troop (characters/creature_bandits.xml) whose Horse slot holds the creature's mount item; in a
/// field battle the spawn patch swaps that troop for a riderless creature agent. Design and evidence:
/// docs/research/creature-bandits-roadmap.md.
/// </summary>
public static class CreatureBanditsConfig
{
    /// <summary>
    /// Every creature troop. These troops appear only in the spider brood's party templates, never in a volunteer
    /// pool or a lord's template, which is what keeps creatures bandit-only. <see cref="CreatureBanditRules"/> holds
    /// the one lookup set (ordinal: troop ids are case-sensitive in the object manager).
    /// </summary>
    public static readonly IReadOnlyList<string> CreatureTroopIds = new[]
    {
        "taom_spider_brood_pale",
        "taom_spider_brood_forest",
        "taom_spider_brood_brown",
    };

    /// <summary>
    /// The spider brood's bandit clan (characters/clans.xml). A looter faction (its culture has no settlements),
    /// so vanilla would spawn it anywhere on the map; <c>TaomBanditDensityModel</c> caps that at zero and
    /// <c>CreatureBroodSpawnBehavior</c> spawns it around Mirkwood.
    /// </summary>
    public const string BroodClanId = "mirkwood_spiders";

    /// <summary>
    /// Where broods spawn and patrol: the Mirkwood towns, villages and castles and Dol Guldur, in the live
    /// TAOM_Map settlements.xml (checked 2026-09-27). Villages are listed so the broods prey on village roads.
    /// </summary>
    public static readonly IReadOnlyList<string> BroodAnchorSettlementIds = new[]
    {
        "village_M1_1", "village_M1_2", "village_M1_3", "village_M1_4",
        "village_M2_1", "village_M2_2", "village_M2_3",
        "castle_M1", "castle_M2", "castle_M3", "castle_M4", "castle_M5",
        "town_DG1",
    };

    /// <summary>Most broods alive at once. First pass; tune from play.</summary>
    public const int MaxBroods = 4;

    /// <summary>
    /// The MCM "Spawn Spider Broods" default. On: players get broods in a new campaign, and one who hits a problem can
    /// stop new spawns without a patch. A later change of default needs a property rename (persisted MCM defaults).
    /// </summary>
    public const bool DefaultSpawnBroods = true;

    /// <summary>How far from its anchor a brood spawns, in days of average bandit travel (vanilla looters: 0.75).</summary>
    public const float SpawnRadiusDays = 0.25f;

    /// <summary>Harmony category for the battle patches; the feature module applies it at process load.</summary>
    public const string PatchCategory = "Patch93_CreatureBandits";

    /// <summary>The creature bandit's behaviour tree, registered by CreatureBanditMissionBehavior.</summary>
    public const string TreeName = "CreatureBanditTree";

    /// <summary>Harmony category for the campaign-map patches (map icon, encounter); applied at game init.</summary>
    public const string CampaignPatchCategory = "Patch94_CreatureBroodCampaign";
}

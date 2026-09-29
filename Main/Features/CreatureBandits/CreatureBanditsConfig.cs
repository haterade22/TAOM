using System.Collections.Generic;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// Creature Bandits (#692): hostile bandit-only creatures that fight with no rider. Each creature is a
/// hidden troop (characters/creature_bandits.xml) whose Horse slot holds the creature's mount item; in a
/// field battle the spawn patch swaps that troop for a riderless creature agent. Design and evidence:
/// docs/research/creature-bandits-roadmap.md. The Wild Trolls (#694) share the campaign side (spawning, no parley, no
/// prisoners) but are humanoid troops that fight as themselves: see <see cref="TrollBanditTroopIds"/>.
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
    /// Where broods spawn and patrol: every town, castle and village of Mirkwood and Dol Guldur (the 47 settlements of
    /// culture mirkwood or dolguldur in the live TAOM_Map settlements.xml, 2026-09-28, #694; the town-bound villages are
    /// village_*, the castle-bound ones castle_village_*). Villages are listed so the broods prey on village roads.
    /// <c>CreatureBanditLiveDataTests</c> keeps the list equal to the live map.
    /// </summary>
    public static readonly IReadOnlyList<string> BroodAnchorSettlementIds = new[]
    {
        "town_M1", "town_M2",
        "village_M1_1", "village_M1_2", "village_M1_3", "village_M1_4",
        "village_M2_1", "village_M2_2", "village_M2_3",
        "castle_M1", "castle_M2", "castle_M3", "castle_M4", "castle_M5",
        "castle_village_M1_1", "castle_village_M1_2", "castle_village_M2_1", "castle_village_M2_2",
        "castle_village_M3_1", "castle_village_M3_2", "castle_village_M4_1", "castle_village_M4_2",
        "castle_village_M5_1", "castle_village_M5_2",
        "town_DG1",
        "village_DG1_1", "village_DG1_2", "village_DG1_3",
        "castle_DG1", "castle_DG2", "castle_DG3", "castle_DG4", "castle_DG5",
        "castle_village_DG1_1", "castle_village_DG1_2", "castle_village_DG1_3",
        "castle_village_DG2_1", "castle_village_DG2_2", "castle_village_DG2_3", "castle_village_DG2_4",
        "castle_village_DG3_1", "castle_village_DG3_2", "castle_village_DG3_3",
        "castle_village_DG4_1", "castle_village_DG4_2", "castle_village_DG5_1", "castle_village_DG5_2",
    };

    /// <summary>Most broods alive at once (#694). One spawns a day, so a new campaign reaches this in about three weeks.</summary>
    public const int MaxBroods = 20;

    /// <summary>
    /// Troll bands (#694): hidden bandit twins of Mordor's <c>cave_troll</c> and <c>hill_troll</c>
    /// (characters/troll_bandits.xml). They are humanoid, so no creature seam takes them; the prisoner rule and their
    /// toughness in place of armour do. They appear only in the troll band template, never in a volunteer pool or a lord's
    /// template, and Mordor's own trolls stay untouched.
    /// </summary>
    public static readonly IReadOnlyList<string> TrollBanditTroopIds = new[]
    {
        "taom_troll_bandit_cave",
        "taom_troll_bandit_hill",
    };

    /// <summary>
    /// The troll bands' bandit clan (characters/clans.xml), a looter faction like the brood's: <c>TaomBanditDensityModel</c>
    /// caps vanilla's map-wide spawn at zero and <c>TrollBandSpawnBehavior</c> keeps one band per kingdom.
    /// </summary>
    public const string TrollClanId = "wild_trolls";

    /// <summary>
    /// A bandit troll's toughness in place of armour (Mike, 2026-09-29: the wild trolls wear none): hit points on top of
    /// the race's (troll races 200, so 300), and the share of every hit it takes. Mordor's trolls keep the race values.
    /// </summary>
    public const int TrollBanditExtraHitPoints = 100;

    public const float TrollBanditDamageTaken = 0.7f;

    /// <summary>The MCM "Spawn Troll Bands" default; the same rename rule as <see cref="DefaultSpawnBroods"/>.</summary>
    public const bool DefaultSpawnTrollBands = true;

    /// <summary>
    /// The MCM "Spawn Spider Broods" default. On: players get broods in a new campaign, and one who hits a problem can
    /// stop new spawns without a patch. A later change of default needs a property rename (persisted MCM defaults).
    /// </summary>
    public const bool DefaultSpawnBroods = true;

    /// <summary>How far from its anchor a brood or a troll band spawns, in days of average bandit travel (vanilla looters: 0.75).</summary>
    public const float SpawnRadiusDays = 0.25f;

    /// <summary>Harmony category for the battle patches; the feature module applies it at process load.</summary>
    public const string PatchCategory = "Patch93_CreatureBandits";

    /// <summary>The creature bandit's behaviour tree, registered by CreatureBanditMissionBehavior.</summary>
    public const string TreeName = "CreatureBanditTree";

    /// <summary>Harmony category for the campaign-map patches (map icon, encounter); applied at game init.</summary>
    public const string CampaignPatchCategory = "Patch94_CreatureBroodCampaign";
}

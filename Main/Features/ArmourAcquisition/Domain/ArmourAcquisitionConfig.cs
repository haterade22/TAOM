using System;
using System.Collections.Generic;

namespace TAOM.Features.ArmourAcquisition.Domain;

/// <summary>One material an armoury upgrade consumes from the party inventory.</summary>
public sealed class UpgradeMaterial
{
    public UpgradeMaterial(string itemId, int count)
    {
        ItemId = itemId;
        Count = count;
    }

    public string ItemId { get; }

    public int Count { get; }
}

/// <summary>
/// What turning a piece into its next class costs, keyed by the class it becomes: flat gold, a share
/// of the value the piece gains, metals, and (for lord kit) the player's kingdom special resource.
/// </summary>
public sealed class UpgradeRecipe
{
    public UpgradeRecipe(ArmourClass target, int gold, float valueShare, float specialResource,
        IReadOnlyList<UpgradeMaterial> materials)
    {
        Target = target;
        Gold = gold;
        ValueShare = valueShare;
        SpecialResource = specialResource;
        Materials = materials;
    }

    public ArmourClass Target { get; }

    public int Gold { get; }

    /// <summary>Share (0 to 1) of the value the piece gains that is added to <see cref="Gold"/>.</summary>
    public float ValueShare { get; }

    public float SpecialResource { get; }

    public IReadOnlyList<UpgradeMaterial> Materials { get; }
}

/// <summary>
/// armour_acquisition_config.xml, validated (docs/features/armour-acquisition.md "Configuration").
/// The provider owns the process lifetime; changes need a full restart.
/// </summary>
public sealed class ArmourAcquisitionConfig
{
    public const int MaxArmouryLevel = 3;

    public ArmourAcquisitionConfig(
        bool enabled,
        int heavyLevel, int eliteLevel, int lordLevel,
        IReadOnlyDictionary<ArmourClass, UpgradeRecipe> recipes,
        IReadOnlyCollection<string> namedWeapons,
        float lordEventChance, int lordEventCooldownDays, int lordEventLeaveRelation,
        float visitChancePerDay, int visitDurationDays, int visitLevelBonus,
        LordsLadderConfig ladder)
    {
        Enabled = enabled;
        HeavyLevel = heavyLevel;
        EliteLevel = eliteLevel;
        LordLevel = lordLevel;
        Recipes = recipes;
        NamedWeapons = namedWeapons;
        LordEventChance = lordEventChance;
        LordEventCooldownDays = lordEventCooldownDays;
        LordEventLeaveRelation = lordEventLeaveRelation;
        VisitChancePerDay = visitChancePerDay;
        VisitDurationDays = visitDurationDays;
        VisitLevelBonus = visitLevelBonus;
        Ladder = ladder;
    }

    public bool Enabled { get; }

    /// <summary>Armoury level a town needs to stock heavy pieces, and to upgrade into heavy.</summary>
    public int HeavyLevel { get; }

    public int EliteLevel { get; }

    /// <summary>Armoury level a town needs to sell and forge lord kit.</summary>
    public int LordLevel { get; }

    public IReadOnlyDictionary<ArmourClass, UpgradeRecipe> Recipes { get; }

    /// <summary>
    /// Weapon and shield ids treated as named: never sold or looted. The ladder's weapon rung awards the named
    /// weapons (Mike, 2026-09-28).
    /// </summary>
    public IReadOnlyCollection<string> NamedWeapons { get; }

    public float LordEventChance { get; }

    public int LordEventCooldownDays { get; }

    public int LordEventLeaveRelation { get; }

    public float VisitChancePerDay { get; }

    public int VisitDurationDays { get; }

    public int VisitLevelBonus { get; }

    /// <summary>The lord's gear ladder (#693): its rungs, the lord's materials and the weapon rung's picks.</summary>
    public LordsLadderConfig Ladder { get; }

    /// <summary>The armoury level a town needs for <paramref name="target"/>: 0 for light and medium.</summary>
    public int RequiredLevel(ArmourClass target) => target switch
    {
        ArmourClass.Heavy => HeavyLevel,
        ArmourClass.Elite => EliteLevel,
        ArmourClass.Lord => LordLevel,
        _ => 0,
    };

    public static ArmourAcquisitionConfig Default { get; } = new(
        enabled: true,
        heavyLevel: 1, eliteLevel: 2, lordLevel: 3,
        recipes: new Dictionary<ArmourClass, UpgradeRecipe>
        {
            [ArmourClass.Medium] = new(ArmourClass.Medium, 150, 0.25f, 0f,
                new[] { new UpgradeMaterial("ironIngot2", 3), new UpgradeMaterial("ironIngot1", 2) }),
            [ArmourClass.Heavy] = new(ArmourClass.Heavy, 500, 0.35f, 0f,
                new[] { new UpgradeMaterial("ironIngot4", 3), new UpgradeMaterial("ironIngot3", 4) }),
            [ArmourClass.Elite] = new(ArmourClass.Elite, 1500, 0.5f, 0f,
                new[] { new UpgradeMaterial("ironIngot5", 4), new UpgradeMaterial("ironIngot4", 3) }),
            [ArmourClass.Lord] = new(ArmourClass.Lord, 3000, 0.5f, 150f,
                new[] { new UpgradeMaterial("ironIngot6", 6) }),
        },
        // The Armory's hero weapons and shields, as the shipped config lists them: seventeen (Mike, 2026-09-27), then
        // Tuor's two heirloom axes, Galadriel's sword and the seven Noldor swords (Mike, 2026-09-28).
        namedWeapons: new HashSet<string>(StringComparer.Ordinal)
        {
            "anduril", "strider_sword", "theoden_sword", "wm_sauron_mace", "glamdring_sword", "eomer_sword",
            "eowyn_sword", "witchking_sword", "nazgul_sword", "wm_gondor_boromir_sword", "wm_gondor_faramir_sword",
            "wm_legolas_sword", "wm_thranduil_sword", "sm_dwarf_dain_hammer_a", "sm_dwarf_dain_axe_a",
            "wm_tuors_axe_1h", "wm_tuors_axe", "wm_galadriel_sword", "wm_fingon_sword", "wm_turin_sword",
            "wm_celegorm_sword", "wm_finwe_sword", "wm_ingwe_sword", "wm_finarin_sword", "wm_voronwe_sword",
            "wm_boromir_shield", "wm_theoden_shield",
        },
        lordEventChance: 0.08f, lordEventCooldownDays: 90, lordEventLeaveRelation: 5,
        visitChancePerDay: 0.04f, visitDurationDays: 7, visitLevelBonus: 1,
        ladder: DefaultLadder());

    // Mike's placeholder numbers (#693, 2026-09-28), as the shipped config lists them.
    private static LordsLadderConfig DefaultLadder()
    {
        var steps = new[]
        {
            new LadderStep(LadderSlot.Hands, "taom_lords_gear_hands", 10),
            new LadderStep(LadderSlot.Legs, "taom_lords_gear_legs", 15),
            new LadderStep(LadderSlot.Shoulders, "taom_lords_gear_shoulders", 20),
            new LadderStep(LadderSlot.Head, "taom_lords_gear_head", 30),
            new LadderStep(LadderSlot.Body, "taom_lords_gear_body", 40),
            new LadderStep(LadderSlot.Weapon, "taom_lords_gear_weapon", 60),
        };
        var materials = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var culture in new[] { "gondor", "vlandia", "erebor", "sturgia", "rivendell", "mirkwood", "mordor",
                     "isengard", "dolguldur", "gundabad", "khuzait", "aserai", "empire" })
            materials[culture] = "taom_lords_material_" + culture;
        var weapons = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["gondor"] = new[] { "anduril", "strider_sword", "glamdring_sword", "wm_gondor_boromir_sword", "wm_gondor_faramir_sword" },
            ["vlandia"] = new[] { "theoden_sword", "eomer_sword", "eowyn_sword" },
            ["mordor"] = new[] { "wm_sauron_mace", "witchking_sword", "nazgul_sword" },
            ["mirkwood"] = new[] { "wm_legolas_sword", "wm_thranduil_sword" },
            ["erebor"] = new[] { "sm_dwarf_dain_hammer_a", "sm_dwarf_dain_axe_a" },
            ["rivendell"] = new[]
            {
                "wm_fingon_sword", "wm_turin_sword", "wm_celegorm_sword", "wm_finwe_sword", "wm_ingwe_sword",
                "wm_finarin_sword", "wm_voronwe_sword", "wm_tuors_axe_1h", "wm_tuors_axe",
            },
            ["lothlorien"] = new[] { "wm_galadriel_sword" },
            ["sturgia"] = new[] { "dale_halberd_b", "dale_war_spear_a" },
            ["isengard"] = new[] { "isengard_berserker_sword_2h", "isengard_2h_axe_c" },
            ["dolguldur"] = new[] { "wm_dol_goldur_halberd_a05", "wm_dol_goldur_2h_mace_a02" },
            ["gundabad"] = new[] { "wm_gundabad_sword_a04", "wm_gundabad_mace_a02" },
            ["khuzait"] = new[] { "sm_rh_drag_sword_2h_a", "sm_rh_loke_sword_2h_a" },
            ["aserai"] = new[] { "wm_harad_sword_a02", "wm_harad_spear_b02" },
            ["empire"] = new[] { "dunland_caerdh_axe_1h_d", "dunland_caerdh_spear_o" },
        };
        return new LordsLadderConfig(steps, countsKnockouts: true, materials,
            new MaterialDrop(baseChance: 0.1f, chancePerTenKills: 0.01f, maxChance: 0.6f, minUnits: 1, maxUnits: 3), weapons);
    }
}

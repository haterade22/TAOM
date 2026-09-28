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
        int harnessOfferCooldownDays)
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
        HarnessOfferCooldownDays = harnessOfferCooldownDays;
    }

    public bool Enabled { get; }

    /// <summary>Armoury level a town needs to stock heavy pieces, and to upgrade into heavy.</summary>
    public int HeavyLevel { get; }

    public int EliteLevel { get; }

    /// <summary>Armoury level a town needs to sell and forge lord kit.</summary>
    public int LordLevel { get; }

    public IReadOnlyDictionary<ArmourClass, UpgradeRecipe> Recipes { get; }

    /// <summary>Weapon ids treated as named: never sold, looted or awarded.</summary>
    public IReadOnlyCollection<string> NamedWeapons { get; }

    public float LordEventChance { get; }

    public int LordEventCooldownDays { get; }

    public int LordEventLeaveRelation { get; }

    public float VisitChancePerDay { get; }

    public int VisitDurationDays { get; }

    public int VisitLevelBonus { get; }

    public int HarnessOfferCooldownDays { get; }

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
        // The seventeen hero weapons and shields of the Armory (Mike, 2026-09-27), as the shipped config lists them.
        namedWeapons: new HashSet<string>(StringComparer.Ordinal)
        {
            "anduril", "strider_sword", "theoden_sword", "wm_sauron_mace", "glamdring_sword", "eomer_sword",
            "eowyn_sword", "witchking_sword", "nazgul_sword", "wm_gondor_boromir_sword", "wm_gondor_faramir_sword",
            "wm_legolas_sword", "wm_thranduil_sword", "sm_dwarf_dain_hammer_a", "sm_dwarf_dain_axe_a",
            "wm_boromir_shield", "wm_theoden_shield",
        },
        lordEventChance: 0.08f, lordEventCooldownDays: 90, lordEventLeaveRelation: 5,
        visitChancePerDay: 0.04f, visitDurationDays: 7, visitLevelBonus: 1,
        harnessOfferCooldownDays: 30);
}

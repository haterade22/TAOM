using System.Collections.Generic;

namespace TAOM.Features.SpecialResources.Domain;

public sealed class SpecialResource
{
    public string Id { get; }
    public IReadOnlyList<string> KingdomIds { get; }
    public IReadOnlyList<string> CultureIds { get; }
    public string DisplayName { get; }
    public string IconSpriteName { get; }
    public float Cap { get; }
    public float StartingAmount { get; }
    public float DailyPerTown { get; }
    public float PerBattleVictoryBase { get; }
    public float PerRaid { get; }
    public float PerSiegeVictory { get; }
    public float PerPrisoner { get; }
    public float PerTournamentWin { get; }
    public float PerHideoutClear { get; }

    /// <summary>Default size, in men, of a battle that pays <see cref="PerBattleVictoryBase"/> in full.</summary>
    public const float DefaultBattleReferenceSize = 100f;
    public const float DefaultBattleMinScale = 0.05f;
    public const float DefaultBattleMaxScale = 3f;

    public const int DefaultEmissaryPriceMultiplier = 1;
    public const int MaxEmissaryPriceMultiplier = 1000;

    /// <summary>
    /// What the Elite Emissary charges, in whatever resource the buyer pays, for each point of the
    /// <c>merchant_cost</c> of a troop whose cost row names THIS resource (the provider folds it into
    /// <see cref="TroopResourceCostEntry.EmissaryPrice"/> at load). It never applies to the gold-only price,
    /// which reads the raw <c>merchant_cost</c>.
    /// </summary>
    public int EmissaryPriceMultiplier { get; }

    /// <summary>Enemy size, in men, at which a victory pays exactly <see cref="PerBattleVictoryBase"/>.</summary>
    public float BattleReferenceSize { get; }

    /// <summary>Floor of the enemy-size scale: a tiny band still pays this share of the base.</summary>
    public float BattleMinScale { get; }

    /// <summary>Ceiling of the enemy-size scale: a huge army pays at most this multiple of the base.</summary>
    public float BattleMaxScale { get; }

    /// <summary>
    /// Ordered list of tier milestones (ascending by <see cref="ResourceTier.Threshold"/>).
    /// Empty for resources that have no tier system.
    /// </summary>
    public IReadOnlyList<ResourceTier> TierThresholds { get; }

    public SpecialResource(
        string id,
        IReadOnlyList<string> kingdomIds,
        IReadOnlyList<string> cultureIds,
        string displayName,
        string iconSpriteName,
        float cap,
        float startingAmount,
        float dailyPerTown,
        float perBattleVictoryBase,
        float perRaid,
        float perSiegeVictory,
        float perPrisoner,
        float perTournamentWin = 0f,
        float perHideoutClear = 0f,
        IReadOnlyList<ResourceTier> tierThresholds = null,
        float battleReferenceSize = DefaultBattleReferenceSize,
        float battleMinScale = DefaultBattleMinScale,
        float battleMaxScale = DefaultBattleMaxScale,
        int emissaryPriceMultiplier = DefaultEmissaryPriceMultiplier)
    {
        Id = id;
        KingdomIds = kingdomIds;
        CultureIds = cultureIds;
        DisplayName = displayName;
        IconSpriteName = iconSpriteName;
        Cap = cap;
        StartingAmount = startingAmount;
        DailyPerTown = dailyPerTown;
        PerBattleVictoryBase = perBattleVictoryBase;
        PerRaid = perRaid;
        PerSiegeVictory = perSiegeVictory;
        PerPrisoner = perPrisoner;
        PerTournamentWin = perTournamentWin;
        PerHideoutClear = perHideoutClear;
        BattleReferenceSize = battleReferenceSize;
        BattleMinScale = battleMinScale;
        BattleMaxScale = battleMaxScale;
        EmissaryPriceMultiplier = emissaryPriceMultiplier;
        TierThresholds = tierThresholds ?? new List<ResourceTier>();
    }
}

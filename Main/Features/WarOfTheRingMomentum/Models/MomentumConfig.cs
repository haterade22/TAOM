using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum.Models;

/// <summary>
/// Momentum tuning knobs. Defaults equal LOTRAOM's SHIPPED momentum_config.xml values
/// (battle max 300 / siege 250 / raid 200 / army 200 / strength max 300) — NOT its
/// compiled fallbacks (500/1000/1000), which disagreed with what actually shipped.
/// </summary>
public class MomentumConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When false (default) the War of the Ring never ends via momentum — endless-war mode.
    /// Victory (threshold win + elimination + the player gate) is fully wired but dormant until
    /// this is enabled; best paired with a future bounded-momentum rebalance so the threshold is
    /// meaningful (see docs/features/war-of-the-ring-momentum.md "Known limitations").
    /// </summary>
    public bool VictoryEnabled { get; set; } = false;

    /// <summary>Internal momentum magnitude at which one side wins the war (only when VictoryEnabled).</summary>
    public int VictoryThreshold { get; set; } = 500;

    public MomentumEventValuesConfig Events { get; set; } = new();
    public MomentumDurationsConfig DurationsHours { get; set; } = new();
    public MomentumPlayerConfig Player { get; set; } = new();

    /// <summary>A side must be this many times stronger to earn the max daily strength award.</summary>
    public float StrengthRatioForMaxMomentum { get; set; } = 4.0f;
}

/// <summary>Event values on the ×100 internal scale (500 threshold ↔ 50000 raw).</summary>
public class MomentumEventValuesConfig
{
    /// <summary>Battle momentum scales with casualties/loser-side strength, capped here.</summary>
    public int MaxBattleMomentum { get; set; } = 300;
    public int SiegeMomentum { get; set; } = 250;
    public int RaidMomentum { get; set; } = 200;
    public int ArmyMomentum { get; set; } = 200;
    /// <summary>Cap for the daily strength-differential award.</summary>
    public int MaxStrengthMomentum { get; set; } = 300;
    /// <summary>Momentum per 100 enemies killed in battle — raw attrition, NOT strength-normalized
    /// like BattleWon (so a war's kill count actually feeds the meter). Displayed = kills × this ÷ 100.
    /// 0 disables the Enemies-Killed source entirely (no award, no breakdown row).</summary>
    public int KillMomentumPerHundred { get; set; } = 10;
}

public class MomentumDurationsConfig
{
    public int BattleWon { get; set; } = 504;        // 21 days
    public int Siege { get; set; } = 504;
    public int Raid { get; set; } = 504;
    public int ArmyGathered { get; set; } = 168;     // 7 days
    public int RelativeStrength { get; set; } = 12;  // 12 hours
    public int EnemiesKilled { get; set; } = 504;    // 21 days, like battles

    public int GetHours(MomentumActionType type)
    {
        switch (type)
        {
            case MomentumActionType.BattleWon: return BattleWon;
            case MomentumActionType.Sieges: return Siege;
            case MomentumActionType.VillageRaided: return Raid;
            case MomentumActionType.ArmyGathered: return ArmyGathered;
            case MomentumActionType.RelativeStrength: return RelativeStrength;
            case MomentumActionType.EnemiesKilled: return EnemiesKilled;
            default: return BattleWon;
        }
    }
}

public class MomentumPlayerConfig
{
    /// <summary>When true, neither side can win until the player has enough recorded events.</summary>
    public bool RequireParticipationForVictory { get; set; } = true;

    /// <summary>Multiplier on momentum gains when the player participates.</summary>
    public float ParticipationMultiplier { get; set; } = 1.5f;

    /// <summary>Recorded player events needed before victory can trigger.</summary>
    public int MinimumPlayerEventsForVictory { get; set; } = 5;
}

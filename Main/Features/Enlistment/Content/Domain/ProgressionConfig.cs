using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>One rank step's promotion thresholds. Loaded from enlistment_config.json; the
/// provider enforces exactly rankCount-1 entries with strictly monotonic day/XP thresholds.</summary>
public sealed class PromotionRequirement
{
    public ServiceRank ToRank { get; set; }
    public int MinDaysServed { get; set; }
    public int MinServiceXp { get; set; }
    public int MinLeadershipSkill { get; set; }
    public int MinDutySuccesses { get; set; }
    public int MinTrust { get; set; }
}

/// <summary>Per-rank daily wage + per-rank daily service XP (indexes follow ServiceRank order).</summary>
public sealed class ProgressionTables
{
    public List<int> DailyWageByRank { get; set; } = new List<int> { 5, 8, 14, 22 };
    public List<int> DailyServiceXpByRank { get; set; } = new List<int> { 10, 12, 16, 20 };

    /// <summary>Donor default was 25 — deliberately halved: career tiers unlock by hero LEVEL, and a fully passive 25/day races the ladder.</summary>
    public int DailyLeadershipXp { get; set; } = 10;

    /// <summary>Daily XP for the chosen assignment's signature skill.</summary>
    public int DailyAssignmentXp { get; set; } = 10;

    /// <summary>Context XP is priority-exclusive: siege > naval > blockade > army (donor stacked all four).</summary>
    public int SiegeContextXp { get; set; } = 8;
    public int NavalContextXp { get; set; } = 7;
    public int BlockadeContextXp { get; set; } = 5;
    public int ArmyContextXp { get; set; } = 6;

    public int TrainingSessionXp { get; set; } = 20;

    /// <summary>Battle-end kill XP line item: kills × per-kill, capped (replaces the donor's mid-mission per-kill path).</summary>
    public int XpPerKill { get; set; } = 25;
    public int KillXpCap { get; set; } = 10;

    public int BattleWinXp { get; set; } = 40;
    public int BattleLossXp { get; set; } = 15;

    /// <summary>Assignment swaps cost a cooldown + trust (donor allowed free swaps).</summary>
    public int AssignmentSwapCooldownDays { get; set; } = 7;
    public int AssignmentSwapTrustCost { get; set; } = 1;
}

/// <summary>
/// Wage policy: the commander pays real gold when solvent; shortfalls defer into arrears
/// released when solvent-and-quiet (replaces the donor's random defer roll + minted gold).
/// </summary>
public sealed class WagePolicyConfig
{
    public bool PayFromCommanderGold { get; set; } = true;

    /// <summary>The commander never pays below this reserve; the difference defers.</summary>
    public int CommanderGoldFloor { get; set; } = 500;

    public int MaxDeferredWages { get; set; } = 60;
}

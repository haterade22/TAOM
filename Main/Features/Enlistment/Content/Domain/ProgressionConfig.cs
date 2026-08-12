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

    /// <summary>
    /// Renown for one battle of service (#443 field report 3). Small on purpose: vanilla's share is
    /// contribution-scaled and an enlisted player is a party of one hero, so his share rounds to
    /// nothing — but renown is the clan-tier currency and a soldier should earn a name slowly.
    /// The merit band adds to this; both at 0 disables the award entirely.
    /// </summary>
    public int BattleWinRenown { get; set; } = 2;
    public int BattleLossRenown { get; set; } = 1;

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

    /// <summary>
    /// How many days of the player's CURRENT daily wage may stand as unpaid arrears. The cap is
    /// therefore rank-relative: 14 days is 70 gold at Recruit (5/day) and 308 at Sergeant (22/day).
    /// The pre-rename key was <c>maxDeferredWages</c>, a flat 60-GOLD cap — a Sergeant reached it in
    /// under three days and every further gold of back pay was destroyed with no log. Anything owed
    /// above the cap is still forfeited, but <c>ServiceRewardService</c> now logs the loss.
    /// Validated to [0, 365] (the contract length) by EnlistmentContentConfigProvider.
    /// </summary>
    public int MaxDeferredWageDays { get; set; } = 14;
}

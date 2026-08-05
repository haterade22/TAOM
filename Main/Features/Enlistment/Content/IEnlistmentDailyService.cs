using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>What one service day produced — the behavior renders this (toast/log), services never touch UI.</summary>
public sealed class DailySummary
{
    public WageDecision Wage { get; set; } = new WageDecision();
    public bool Promoted { get; set; }
    public ServiceRank NewRank { get; set; }
    public bool ContractExpiredToday { get; set; }
}

/// <summary>
/// The daily service tick: day counter, commander-funded wage, daily/context XP
/// (priority-exclusive: siege &gt; naval &gt; blockade &gt; army), and the promotion evaluation —
/// one of exactly two promotion call points (the other is battle end).
/// </summary>
public interface IEnlistmentDailyService
{
    DailySummary RunDailyTick(double nowDays, double hourOfDay);
}

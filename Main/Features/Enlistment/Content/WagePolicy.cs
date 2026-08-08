using System;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>Outcome of one day's wage computation. All values >= 0.</summary>
public sealed class WageDecision
{
    public int PaidFromCommander { get; set; }
    public int Minted { get; set; }
    public int NewlyDeferred { get; set; }
    public int ArrearsReleased { get; set; }

    /// <summary>Gold owed today that the arrears cap destroyed. Non-zero means the player lost pay.</summary>
    public int Forfeited { get; set; }

    public int TotalPaidToPlayer => PaidFromCommander + Minted + ArrearsReleased;
}

/// <summary>
/// Pure daily-wage policy: the commander pays real gold above a solvency floor;
/// shortfalls defer into capped arrears released when solvent again (replaces the donor's
/// random defer roll and minted-from-nothing gold). The MCM escape hatch
/// (PayFromCommanderGold=false) mints instead — arrears then release immediately.
/// </summary>
public static class WagePolicy
{
    /// <summary>
    /// Ceiling on outstanding arrears, in gold: <c>MaxDeferredWageDays × dailyWage</c>. Denominated
    /// in DAYS so the cap tracks the wage it caps — the old flat 60-gold cap was 12 days of a
    /// Recruit's pay but 2.7 days of a Sergeant's, and silently destroyed everything past it.
    /// Widened to <c>long</c> before the multiply so a hand-edited day count cannot overflow into a
    /// negative (and therefore zero) cap.
    /// </summary>
    public static int ArrearsCap(int dailyWage, WagePolicyConfig config)
    {
        if (config == null)
            return 0;

        var cap = (long)Math.Max(0, config.MaxDeferredWageDays) * Math.Max(0, dailyWage);
        return cap > int.MaxValue ? int.MaxValue : (int)cap;
    }

    public static WageDecision ComputeDaily(int dailyWage, int commanderGold, int currentArrears, WagePolicyConfig config)
    {
        var decision = new WageDecision();
        if (config == null)
            return decision;

        var wage = Math.Max(0, dailyWage);
        var arrears = Math.Max(0, currentArrears);

        if (!config.PayFromCommanderGold)
        {
            decision.Minted = wage;
            decision.ArrearsReleased = arrears;
            return decision;
        }

        var available = Math.Max(0, commanderGold - Math.Max(0, config.CommanderGoldFloor));
        decision.PaidFromCommander = Math.Min(wage, available);

        var shortfall = wage - decision.PaidFromCommander;
        var arrearsRoom = Math.Max(0, ArrearsCap(wage, config) - arrears);
        decision.NewlyDeferred = Math.Min(shortfall, arrearsRoom);
        decision.Forfeited = shortfall - decision.NewlyDeferred;

        var remaining = available - decision.PaidFromCommander;
        decision.ArrearsReleased = Math.Min(arrears, remaining);

        return decision;
    }
}

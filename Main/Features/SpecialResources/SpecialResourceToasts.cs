using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.SpecialResources;

/// <summary>
/// The on-screen lines for special-resource earnings, the daily tick and recruit charges. Every line is
/// for the player at this machine: the service marks a remote player's notice non-local, and this class
/// shows only the local ones.
/// </summary>
internal static class SpecialResourceToasts
{
    /// <summary>Unified earning toast (round-4 O1): the positive delta earned across the earn call.</summary>
    public static void Earnings(IReadOnlyList<EarningNotice> notices)
    {
        foreach (var notice in notices)
        {
            if (!notice.IsLocal) continue;
            InformationManager.DisplayMessage(new InformationMessage(
                SpecialResourceMessages.Earned(notice.ResourceName, notice.Earned, notice.Source).ToString(),
                Colors.Green));
        }
    }

    public static void Daily(DailyOutcome outcome)
    {
        if (!outcome.IsLocal) return;

        if (outcome.DailyLine != null)
        {
            var daily = outcome.DailyLine.ToString();
            InformationManager.DisplayMessage(outcome.DailyLineYellow
                ? new InformationMessage(daily, Colors.Yellow)
                : new InformationMessage(daily));
        }

        if (outcome.Overdraft != null)
            InformationManager.DisplayMessage(new InformationMessage(outcome.Overdraft.ToString(), Colors.Red));

        if (outcome.Warning != null)
            InformationManager.DisplayMessage(new InformationMessage(outcome.Warning.ToString(), Colors.Yellow));
    }

    public static void Desertion(int totalDeserted, string resourceName)
    {
        // Slots, not interpolation. Baking the count and the resource name into the
        // DEFAULT text made this string unlocalizable by construction: English reads
        // correctly (GetLocalizedText short-circuits and returns the inline default), but
        // the registered row a translator works from has no place to put a number, so no
        // translation could ever carry it. Found by Codex, 2026-08-09 (#434).
        MBInformationManager.AddQuickInformation(
            new TextObject("{=taom_res_desertion}{COUNT} elite troops deserted — your {RESOURCE} are depleted!")
                .SetTextVariable("COUNT", totalDeserted)
                .SetTextVariable("RESOURCE", resourceName),
            extraTimeInMs: 3000);
    }

    public static void RecruitCharge(RecruitChargeNotice notice, int count, string troopName)
    {
        InformationManager.DisplayMessage(new InformationMessage(
            SpecialResourceMessages.RecruitCharge(notice.ResourceName, notice.Charged, count, troopName).ToString(),
            Colors.Yellow));
    }

    // The one outflow a player triggers by hand, and until #558 the only feedback was a
    // log line: upgrade twenty troops after a battle and the balance was simply gone.
    public static void UpgradeSpend(string resourceName, float spent, float balance)
    {
        InformationManager.DisplayMessage(new InformationMessage(
            SpecialResourceMessages.UpgradeSpend(resourceName, spent, balance).ToString(),
            Colors.Yellow));
    }
}

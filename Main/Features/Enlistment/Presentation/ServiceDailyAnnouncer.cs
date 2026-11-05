using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Presentation;

/// <summary>
/// Everything the daily tick and the battle end say out loud: wages, promotions, the merit toast and
/// the sat-out lines. Split out of <c>EnlistmentContentBehavior</c> for the
/// same reason <see cref="EnlistmentWaitMenuOptions"/> was split out of <c>EnlistmentMenuBehavior</c>
/// (ADR-003 decomposition): the behavior's job is lifecycle — subscribe, forward the tick, unhook —
/// and the wording of player-facing messages is a separate, growing concern that had pushed it past
/// the ADR-002 ceiling.
/// </summary>
public interface IServiceDailyAnnouncer
{
    /// <summary>Say what happened to today's pay (field report 2). Silent when nothing happened.</summary>
    void AnnounceWage(WageDecision decision);

    void AnnouncePromotion(ServiceRank rank);

    /// <summary>The merit toast for the battle's band. Silent when the battle had no sample.</summary>
    void AnnounceMerit(string gradeKey);

    /// <summary>Say that he stayed back from a battle and why (#830). Silent for a null reason.</summary>
    void AnnounceSatOut(SitOutVerdict? reason);
}

/// <inheritdoc />
public sealed class ServiceDailyAnnouncer : IServiceDailyAnnouncer
{
    /// <summary>
    /// Inline rather than a shared constant because it is a rendering detail of these two messages,
    /// not a contract anything else depends on.
    /// </summary>
    private const string GoldIcon = "<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">";

    public void AnnounceWage(WageDecision decision)
    {
        var report = WageReportPolicy.Describe(decision);

        if (report.ShouldAnnouncePayment)
            Say("{=taom_enlist_wage_paid}Wages received: {AMOUNT}{GOLD_ICON}", report.Received);

        // Payment and shortfall are SEPARATE messages on purpose: a partially-paid day is both, and
        // folding them into one line would hide whichever half came second.
        if (report.Forfeited > 0)
        {
            // Distinct from a deferral and worth its own line: this gold is not owed any more, it is
            // gone. Silently destroying earned pay is the one wage outcome the player must never have
            // to infer from a purse that did not move.
            Say("{=taom_enlist_wage_forfeited}The column can no longer account for {AMOUNT}{GOLD_ICON} of your back pay.",
                report.Forfeited);
        }
        else if (report.Deferred > 0)
        {
            Say("{=taom_enlist_wage_deferred}The paychest is short — {AMOUNT}{GOLD_ICON} of your wages is owed against later.",
                report.Deferred);
        }
    }

    public void AnnouncePromotion(ServiceRank rank)
    {
        var text = new TextObject("{=taom_enlist_promoted}You have been promoted to {RANK}.");
        text.SetTextVariable("RANK", ServiceVocabulary.RankName(rank));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }

    public void AnnounceMerit(string gradeKey)
    {
        if (string.IsNullOrEmpty(gradeKey))
            return;
        var text = new TextObject("{=taom_enlist_merit_toast}The sergeants noted your conduct: {GRADE}.");
        text.SetTextVariable("GRADE", ServiceVocabulary.GradeName(gradeKey));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }

    public void AnnounceSatOut(SitOutVerdict? reason)
    {
        var template = SatOutTemplate(reason);
        if (template == null)
            return;
        InformationManager.DisplayMessage(new InformationMessage(new TextObject(template).ToString()));
    }

    /// <summary>The line for a sat-out reason, or null when the reason is not a sit-out. Pure, so it is unit-tested.</summary>
    internal static string SatOutTemplate(SitOutVerdict? reason)
    {
        switch (reason)
        {
            case SitOutVerdict.WoundedFree:
                return "{=taom_enlist_sat_out_wounded_line}You were wounded and stayed back while the column fought.";
            case SitOutVerdict.OverwhelmingCost:
                return "{=taom_enlist_sat_out_line}You stayed back while the column fought, and your share of the battle's due was cut.";
            case SitOutVerdict.ForcedOpen:
                return "{=taom_enlist_sat_out_forced_line}The column fought without you, and you had no way into the line.";
            default:
                return null;
        }
    }

    private static void Say(string template, int amount)
    {
        var text = new TextObject(template);
        text.SetTextVariable("AMOUNT", amount);
        text.SetTextVariable("GOLD_ICON", GoldIcon);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }
}

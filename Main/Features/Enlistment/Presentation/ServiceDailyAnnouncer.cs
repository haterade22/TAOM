using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Presentation;

/// <summary>
/// Everything the daily tick says out loud. Split out of <c>EnlistmentContentBehavior</c> for the
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

    private static void Say(string template, int amount)
    {
        var text = new TextObject(template);
        text.SetTextVariable("AMOUNT", amount);
        text.SetTextVariable("GOLD_ICON", GoldIcon);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }
}

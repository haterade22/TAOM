using TaleWorlds.Localization;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Turns a <see cref="ServiceStatusModel"/> into the wait menu's text. Boundary layer: this is the
/// only place in the status path that touches <c>TextObject</c> / <c>MBTextManager</c>, so the
/// service that decides WHAT to show stays testable without the localization stack.
/// </summary>
public interface IServiceStatusTextWriter
{
    void Write(ServiceStatusModel status);
}

public sealed class ServiceStatusTextWriter : IServiceStatusTextWriter
{
    /// <summary>
    /// The variable the wait menu's registered text (<c>"{TAOM_ENLISTMENT_WAIT_TEXT}"</c>) resolves
    /// against. It is a GLOBAL, and that is fine here — the menu re-renders every frame and
    /// re-resolves it each time (see <see cref="IServiceStatusService"/> for why).
    /// </summary>
    private const string WaitTextVariable = "TAOM_ENLISTMENT_WAIT_TEXT";

    public void Write(ServiceStatusModel status)
    {
        if (status == null)
            return;

        // A fresh TextObject per push, deliberately. These happen only when the status actually
        // CHANGED — a handful of times per settlement stop, not per frame — and reusing one
        // instance would leave stale attributes from the previous shape (the duty line in
        // particular is not always present).
        var text = new TextObject(
            "{=taom_enlist_wait_board}{ACTIVITY}{NEWLINE}{NEWLINE}Rank: {RANK} · {SECTION}{NEWLINE}Days served: {DAYS} · Standing: {TRUST}{ARREARS}{DUTY}");

        text.SetTextVariable("ACTIVITY", ActivityLine(status));
        text.SetTextVariable("RANK", RankName(status.Rank));
        text.SetTextVariable("SECTION", SectionName(status.Assignment));
        text.SetTextVariable("DAYS", status.DaysServed.ToString());
        text.SetTextVariable("TRUST", TrustName(status.Trust));
        text.SetTextVariable("ARREARS", ArrearsLine(status.DeferredWages));
        text.SetTextVariable("DUTY", DutyLine(status.ActiveDutyId));

        MBTextManager.SetTextVariable(WaitTextVariable, text);
    }

    private static TextObject ActivityLine(ServiceStatusModel status)
    {
        switch (status.Activity)
        {
            case CommanderActivity.InBattle:
                return Named("{=taom_enlist_act_battle}{COMMANDER} is engaged. Hold your place in the line.", status.CommanderName);
            case CommanderActivity.Besieging:
                return Named("{=taom_enlist_act_siege}You are camped before the walls with {COMMANDER}'s company.", status.CommanderName);
            case CommanderActivity.InSettlement:
            {
                var line = new TextObject("{=taom_enlist_act_settlement}The column rests inside {SETTLEMENT}.");
                line.SetTextVariable("SETTLEMENT", status.SettlementName ?? "");
                return line;
            }
            case CommanderActivity.Unavailable:
                return new TextObject("{=taom_enlist_act_lost}You have lost the column. Await word of your commander.");
            default:
                return Named("{=taom_enlist_act_march}You march with {COMMANDER}'s company, at your commander's pace.", status.CommanderName);
        }
    }

    private static TextObject Named(string template, string commanderName)
    {
        var line = new TextObject(template);
        line.SetTextVariable("COMMANDER", commanderName ?? "");
        return line;
    }

    /// <summary>Only shown when the commander actually owes you money — a "0 owed" line is noise.</summary>
    private static TextObject ArrearsLine(int deferredWages)
    {
        if (deferredWages <= 0)
            return new TextObject("");

        var line = new TextObject("{=taom_enlist_board_arrears}{NEWLINE}Pay owed to you: {GOLD}{GOLD_ICON}");
        line.SetTextVariable("GOLD", deferredWages.ToString());
        line.SetTextVariable("GOLD_ICON", "<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">");
        return line;
    }

    private static TextObject DutyLine(string activeDutyId)
    {
        if (string.IsNullOrEmpty(activeDutyId))
            return new TextObject("");

        // The duty's own display name is data-driven and registered per row; until those land the
        // line still tells the player they HAVE orders, which is the part that changes behaviour.
        var line = new TextObject("{=taom_enlist_board_duty}{NEWLINE}You have orders: {DUTY_NAME}");
        line.SetTextVariable("DUTY_NAME", new TextObject("{=taom_enlist_duty_" + activeDutyId + "_title}" + activeDutyId));
        return line;
    }

    private static TextObject RankName(ServiceRank rank)
    {
        switch (rank)
        {
            case ServiceRank.Soldier: return new TextObject("{=taom_enlist_rank_soldier}Soldier");
            case ServiceRank.Veteran: return new TextObject("{=taom_enlist_rank_veteran}Veteran");
            case ServiceRank.Sergeant: return new TextObject("{=taom_enlist_rank_sergeant}Sergeant");
            default: return new TextObject("{=taom_enlist_rank_recruit}Recruit");
        }
    }

    private static TextObject SectionName(ServiceAssignment assignment)
    {
        switch (assignment)
        {
            case ServiceAssignment.Archer: return new TextObject("{=taom_enlist_section_arc}bowmen");
            case ServiceAssignment.Cavalry: return new TextObject("{=taom_enlist_section_cav}horse");
            case ServiceAssignment.Support: return new TextObject("{=taom_enlist_section_sup}baggage train");
            default: return new TextObject("{=taom_enlist_section_inf}shield line");
        }
    }

    /// <summary>
    /// Trust as words, not a raw integer. The number is an internal scale ([-10, 20]) that means
    /// nothing to a player, and showing it invites them to optimise a bar instead of playing.
    /// </summary>
    private static TextObject TrustName(int trust)
    {
        if (trust >= 15) return new TextObject("{=taom_enlist_trust_trusted}trusted");
        if (trust >= 8) return new TextObject("{=taom_enlist_trust_solid}well regarded");
        if (trust >= 3) return new TextObject("{=taom_enlist_trust_known}known");
        if (trust >= 0) return new TextObject("{=taom_enlist_trust_new}unproven");
        return new TextObject("{=taom_enlist_trust_poor}in poor odour");
    }
}

using TaleWorlds.Localization;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Presentation;

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

    /// <summary>Inline coin glyph. Shared by the wage and arrears lines so the two cannot diverge.</summary>
    private const string CoinIcon = "<img src=\"General\\Icons\\Coin@2x\" extend=\"8\">";

    public void Write(ServiceStatusModel status)
    {
        if (status == null)
            return;

        // A fresh TextObject per push, deliberately. These happen only when the status actually
        // CHANGED — a handful of times per settlement stop, not per frame — and reusing one
        // instance would leave stale attributes from the previous shape (the duty line in
        // particular is not always present).
        //
        // The key is _v2 because the template's SHAPE changed. `taom_enlist_wait_board` was
        // registered and translated, and for a translated language the registered row wins over
        // this inline default — so reusing the id would have rendered the old three-line sentence
        // to every non-English player and silently dropped the XP / wage / ladder tokens, with no
        // error anywhere. A changed template needs a new id, always.
        //
        // Precision worth keeping, because it is counter-intuitive: this is NOT true for English.
        // `MBTextManager.GetLocalizedText` (v1.4.7, :264-268) short-circuits —
        // `if (_activeTextLanguageId == "English") return RemoveComments(<inline default>);` —
        // and never consults the registered row at all. So for a `{=key}default` TextObject the
        // English XML row is translator source material, not a render path: an English copy edit
        // must be made HERE, in the C#, or it will not appear in game no matter what the XML says.
        // The 11 translated languages are the ones the registration actually feeds.
        var text = new TextObject(
            "{=taom_enlist_wait_board_v2}{ACTIVITY}{NEWLINE}{NEWLINE}Rank: {RANK} · {SECTION}{NEWLINE}Days served: {DAYS} · Standing: {TRUST}{NEWLINE}Service record: {XP} XP · Daily pay: {WAGE}{COIN}{LADDER}{ARREARS}{DUTY}");

        text.SetTextVariable("ACTIVITY", ActivityLine(status));
        text.SetTextVariable("RANK", ServiceVocabulary.RankName(status.Rank));
        text.SetTextVariable("SECTION", ServiceVocabulary.SectionName(status.Assignment));
        text.SetTextVariable("DAYS", status.DaysServed.ToString());
        text.SetTextVariable("TRUST", TrustName(status.Trust));
        text.SetTextVariable("XP", status.ServiceXp.ToString());
        text.SetTextVariable("WAGE", status.DailyWage.ToString());
        text.SetTextVariable("COIN", CoinIcon);
        text.SetTextVariable("LADDER", LadderLine(status));
        text.SetTextVariable("ARREARS", ArrearsLine(status.DeferredWages));
        text.SetTextVariable("DUTY", DutyLine(status.ActiveDutyId));

        MBTextManager.SetTextVariable(WaitTextVariable, text);
    }

    /// <summary>
    /// The one thing standing between the player and their next rank. Promotion needs FIVE
    /// simultaneous gates; listing all five turns a status board into a spreadsheet, and listing
    /// none — which is what shipped — makes the whole ladder read as a broken feature. The service
    /// picks the single most-binding gate; this just renders it.
    ///
    /// Absent when <see cref="ServiceStatusModel.NextRequirementKey"/> is null: top rank, or every
    /// requirement already met and the promotion due on the next daily tick.
    /// </summary>
    private static TextObject LadderLine(ServiceStatusModel status)
    {
        if (string.IsNullOrEmpty(status.NextRequirementKey) || !status.NextRank.HasValue)
            return new TextObject("");

        var line = new TextObject("{=taom_enlist_board_ladder}{NEWLINE}Toward {NEXT_RANK}: {REQUIREMENT}");
        line.SetTextVariable("NEXT_RANK", ServiceVocabulary.RankName(status.NextRank.Value));
        line.SetTextVariable("REQUIREMENT", RequirementPhrase(status.NextRequirementKey, status.NextRequirementTarget));
        return line;
    }

    /// <summary>
    /// A promotion-requirement key as words. Deliberately NOT in ServiceVocabulary: that type
    /// exists because section/rank names were authored twice over one key set and drifted, and
    /// these phrases have exactly one consumer — this board. Moving them there would add a
    /// cross-file hop for no shared use.
    ///
    /// Trust is the one gate rendered WITHOUT its number, on purpose. Standing is shown to the
    /// player as words a few lines above precisely because the raw [-10,20] scale means nothing and
    /// invites bar-optimising; quoting "trust 6" here would contradict that in the same paragraph.
    ///
    /// The default arm is unreachable through PromotionEvaluator's closed key set — it is there so
    /// a future key added to the evaluator degrades into prose instead of printing an internal
    /// identifier at the player, which is exactly how the duty line shipped "recon_sweep".
    /// </summary>
    private static TextObject RequirementPhrase(string key, int target)
    {
        TextObject line;
        switch (key)
        {
            case "days":
                line = new TextObject("{=taom_enlist_req_days}{COUNT} days of service");
                break;
            case "xp":
                line = new TextObject("{=taom_enlist_req_xp}{COUNT} service XP");
                break;
            case "leadership":
                line = new TextObject("{=taom_enlist_req_leadership}Leadership {COUNT}");
                break;
            case "dutySuccesses":
                line = new TextObject("{=taom_enlist_req_duties}{COUNT} duties seen through");
                break;
            case "trust":
                return new TextObject("{=taom_enlist_req_trust}your commander's confidence");
            default:
                return new TextObject("{=taom_enlist_req_other}more service");
        }

        line.SetTextVariable("COUNT", target.ToString());
        return line;
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
        line.SetTextVariable("GOLD_ICON", CoinIcon);
        return line;
    }

    private static TextObject DutyLine(string activeDutyId)
    {
        if (string.IsNullOrEmpty(activeDutyId))
            return new TextObject("");

        // The name is registered per row as taom_enlist_duty_<id>_title (generated by
        // tools/generate_enlistment_duty_strings.py, which hard-fails on an unauthored row).
        //
        // The fallback is deliberately NOT the row id. It used to be, and because the 13 field
        // duties had no registered title the board read "You have orders: recon_sweep" in every
        // language — an internal identifier shown to the player as if it were prose. A generic
        // phrase degrades quietly instead; the loud signal for a missing key belongs in the
        // generator, where a human sees it, not on the player's status board.
        var line = new TextObject("{=taom_enlist_board_duty}{NEWLINE}You have orders: {DUTY_NAME}");
        line.SetTextVariable(
            "DUTY_NAME",
            new TextObject("{=taom_enlist_duty_" + activeDutyId + "_title}a task for the company"));
        return line;
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
        return new TextObject("{=taom_enlist_trust_poor}badly thought of");
    }
}

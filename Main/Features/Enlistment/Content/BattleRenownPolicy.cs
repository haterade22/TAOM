using System;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// How much renown one battle of service is worth (field report 3, "you do not earn enough renown
/// from battles").
///
/// WHY ANY IS OWED. The report is structurally true rather than a tuning miss. TAOM grants no renown
/// of its own, and vanilla's share is proportional to the party's contribution to the battle — the
/// enlisted player is a party of one hero, so whatever he does personally, his party's share of a
/// thousand-man engagement rounds away. Renown for service has to be granted deliberately or it does
/// not arrive at all.
///
/// WHY IT IS SMALL. Renown is the clan-tier currency; a soldier in someone else's army should climb
/// slowly, and the fantasy is earning a name over a career rather than buying a tier with one siege.
///
/// THE BAND FIGURE DOES THE DIFFERENTIATING. <see cref="BattleMeritScorer"/> has already graded the
/// fight from kills, damage taken and survival, and the band it resolves carries a
/// <c>MeritBand.Renown</c> that is added to the flat base here. Shipped defaults: a base of 2 for a
/// win and 1 for a loss, plus 3/2/1/0 by band, so a distinguished win pays 5 against a rough win's
/// 2 — two and a half times, without any one fight moving the tier needle.
///
/// That sentence was FALSE until 2026-08-11: <c>MeritBand.Renown</c> existed but no default band and
/// no shipped config key ever set it, so <c>bandRenown</c> was always 0 and every battle paid the
/// same flat base while this comment claimed otherwise. If you retune the bands, re-read this
/// paragraph and keep the numbers in it honest — a comment asserting behaviour the config no longer
/// produces is worse than no comment.
///
/// Pure so the numbers are testable without a mission; the grant itself goes through
/// <c>IServiceRewardService.Grant</c> like every other payout.
/// </summary>
public static class BattleRenownPolicy
{
    /// <summary>
    /// Renown for one completed battle: the win/loss base plus whatever the merit band awarded.
    /// Never negative — the config is hand-edited JSON, and a negative total would quietly strip
    /// clan renown on every battle, which is a worse bug than the one being fixed.
    /// </summary>
    public static int Compute(bool won, int bandRenown, ProgressionTables tables)
    {
        if (tables == null)
            return 0;

        var baseRenown = won ? tables.BattleWinRenown : tables.BattleLossRenown;
        return Math.Max(0, baseRenown + bandRenown);
    }
}

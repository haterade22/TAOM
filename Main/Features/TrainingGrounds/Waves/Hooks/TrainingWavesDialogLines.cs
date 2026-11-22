using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Features.TrainingGrounds.Waves.Hooks;

/// <summary>
/// The conversation lines of the wave fight, one call each, over vanilla's practice master (ArenaMasterCampaignBehavior, read
/// 2026-11-22). One player option sits on its hub <c>arena_master_talk</c> at priority 101 (vanilla's hub options are 100 and its
/// leave line 80). It leads to the rules, then to a stake choice (three amounts, a way out, and a way to stop between waves), then to
/// the master's send-off. After a wave the master greets the player on the <c>start</c> token at <see cref="DebriefPriority"/>: vanilla's
/// own post-fight greeting (<c>arena_master_meet_start_2</c>) pays the practice prize in its condition and needs
/// <c>AfterPractice</c>, which a wave run never sets, so the higher priority is a second guard that vanilla's line is never asked.
/// Every text is a constant of <see cref="TrainingWavesTexts"/>, each with variables of its own.
/// </summary>
internal static class TrainingWavesDialogLines
{
    /// <summary>The priority of the offer, the rules and the stake lines: above vanilla's 100 for the hub's options.</summary>
    internal const int HubPriority = 101;

    internal const int StakeLowPriority = 104;
    internal const int StakeMidPriority = 103;
    internal const int StakeHighPriority = 102;
    internal const int GoPriority = 102;

    /// <summary>The master's greeting after a wave: above vanilla's 100 for a <c>start</c> line, so vanilla's prize line never runs after a wave.</summary>
    internal const int DebriefPriority = 120;

    private const string Hub = "arena_master_talk";
    private const string PreTalk = "arena_master_pre_talk";
    private const string Start = "start";
    private const string Close = "close_window";
    private const string RulesToken = "taom_tgw_rules";
    private const string StakeToken = "taom_tgw_stake";
    private const string GoToken = "taom_tgw_go";
    private const string WithdrawnToken = "taom_tgw_withdrawn";

    internal static void Register(CampaignGameStarter s, TrainingWavesDialogBehavior b)
    {
        s.AddPlayerLine("taom_tgw_offer", Hub, RulesToken, TrainingWavesTexts.Offer,
            b.CanOffer, null, HubPriority, (out TextObject why) => b.OfferClickable(out why));
        s.AddDialogLine("taom_tgw_rules", RulesToken, StakeToken, TrainingWavesTexts.Rules,
            b.CanRules, null, HubPriority);

        s.AddPlayerLine("taom_tgw_stake_low", StakeToken, GoToken, TrainingWavesTexts.StakeLow,
            () => b.CanStakeLine(2), () => b.Stake(2), StakeLowPriority, (out TextObject why) => b.StakeClickable(2, out why));
        s.AddPlayerLine("taom_tgw_stake_mid", StakeToken, GoToken, TrainingWavesTexts.StakeMid,
            () => b.CanStakeLine(1), () => b.Stake(1), StakeMidPriority, (out TextObject why) => b.StakeClickable(1, out why));
        s.AddPlayerLine("taom_tgw_stake_high", StakeToken, GoToken, TrainingWavesTexts.StakeHigh,
            () => b.CanStakeLine(0), () => b.Stake(0), StakeHighPriority, (out TextObject why) => b.StakeClickable(0, out why));
        s.AddPlayerLine("taom_tgw_not_today", StakeToken, PreTalk, TrainingWavesTexts.NotToday,
            b.CanNotToday, null, HubPriority);
        s.AddPlayerLine("taom_tgw_withdraw", StakeToken, WithdrawnToken, TrainingWavesTexts.Withdraw,
            b.CanWithdraw, b.Withdraw, HubPriority);

        s.AddDialogLine("taom_tgw_go", GoToken, Close, TrainingWavesTexts.Go,
            b.CanGo, null, GoPriority);
        s.AddDialogLine("taom_tgw_go_failed", GoToken, Hub, TrainingWavesTexts.GoFailed,
            null, null, HubPriority);
        s.AddDialogLine("taom_tgw_withdrawn", WithdrawnToken, Close, TrainingWavesTexts.Withdrawn,
            b.CanWithdrawn, null, HubPriority);

        s.AddDialogLine("taom_tgw_cleared", Start, StakeToken, TrainingWavesTexts.Cleared,
            b.CanCleared, b.Debrief, DebriefPriority);
        s.AddDialogLine("taom_tgw_lost", Start, Hub, TrainingWavesTexts.Lost,
            b.CanLost, b.Debrief, DebriefPriority);
        s.AddDialogLine("taom_tgw_done", Start, Hub, TrainingWavesTexts.Done,
            b.CanDone, b.Debrief, DebriefPriority);
    }
}

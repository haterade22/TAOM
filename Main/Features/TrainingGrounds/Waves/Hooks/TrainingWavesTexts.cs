using System;
using System.Globalization;
using TaleWorlds.Localization;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Features.TrainingGrounds.Waves.Hooks;

/// <summary>
/// The English of the wave fight's conversation (docs/features/training-waves.md), one <c>{=KEY}text</c> literal for each line and
/// each refusal. The place is the Training Grounds and its keeper the Training Master (#1527): no line uses an older name
/// (<c>TrainingGroundsTextTests</c>). Each line reads text variables of its own, named for it (<c>TAOM_TGW_CL_</c> for the cleared
/// line): a player option's text resolves after every condition has run, so a variable two lines share shows the last value on both
/// (lessons/adapters-taleworlds-api.md, Provender-master). The literals are plain strings because the engine takes the conversation
/// text that way; <c>tools/harvest_literal_loc_keys.py</c> registers them.
/// </summary>
internal static class TrainingWavesTexts
{
    internal const string Offer = "{=taom_tgw_offer}I want to fight your waves, with a wager on each one.";
    internal const string Rules = "{=taom_tgw_rules}Waves of fighters, one after another, all against you, with practice weapons. Before each wave you wager on yourself. Clear it and I pay back {TAOM_TGW_RU_ODDS} times your wager, more on later waves. Go down, or leave the yard mid-fight, and the wager stays with the house. Between waves you may stop and keep what you won. The first wager is at most {TAOM_TGW_RU_CAP} denars, and the limit rises with each wave you clear.";
    internal const string StakeLow = "{=taom_tgw_stake_low}I wager {TAOM_TGW_ST_LOW_AMOUNT} denars.";
    internal const string StakeMid = "{=taom_tgw_stake_mid}I wager {TAOM_TGW_ST_MID_AMOUNT} denars.";
    internal const string StakeHigh = "{=taom_tgw_stake_high}I wager {TAOM_TGW_ST_HIGH_AMOUNT} denars.";
    internal const string NotToday = "{=taom_tgw_not_today}On second thought, not today.";
    internal const string Withdraw = "{=taom_tgw_withdraw}I will stop here and keep my winnings.";
    internal const string Go = "{=taom_tgw_go}Wave {TAOM_TGW_GO_WAVE}: {TAOM_TGW_GO_COUNT} fighters, no more than {TAOM_TGW_GO_ATONCE} at once. Take a practice weapon and go to it!";
    internal const string GoFailed = "{=taom_tgw_go_failed}Your purse does not hold that much. Come back when it does.";
    internal const string Withdrawn = "{=taom_tgw_withdrawn}A wise choice. You leave the yard {TAOM_TGW_WD_NET} denars richer.";
    internal const string Cleared = "{=taom_tgw_cleared}Well fought! Wave {TAOM_TGW_CL_WAVE} is yours, and so are {TAOM_TGW_CL_PAYOUT} denars: {TAOM_TGW_CL_NET} won so far. The next wave is harder. You may wager up to {TAOM_TGW_CL_CAP} denars on it, and I pay back {TAOM_TGW_CL_ODDS} times the wager. Will you go on?";
    internal const string Lost = "{=taom_tgw_lost}You went down in wave {TAOM_TGW_LO_WAVE}, and your wager of {TAOM_TGW_LO_STAKE} denars stays with the house. Before that you had won {TAOM_TGW_LO_EARLIER} denars.";
    internal const string Done = "{=taom_tgw_done}That was the last wave. Few have stood through them all. You leave the yard {TAOM_TGW_DO_NET} denars richer.";

    internal const string RefusalToday = "{=taom_tgw_ref_today}You have already fought the waves here today.";
    internal const string RefusalTournament = "{=taom_tgw_ref_tournament}The yard is kept for the tournament.";
    internal const string RefusalPoor = "{=taom_tgw_ref_poor}You need at least {TAOM_TGW_RF_MIN} denars to wager.";
    internal const string RefusalShort = "{=taom_tgw_ref_short}Your purse does not hold that much.";

    /// <summary>Vanilla's own keys and words for a wounded or disguised player (ArenaMasterCampaignBehavior), so the two read as vanilla's do.</summary>
    internal const string RefusalWounded = "{=yNMrF2QF}You are wounded";
    internal const string RefusalDisguised = "{=jcEoUPCB}You are in disguise.";

    internal const string RulesOdds = "TAOM_TGW_RU_ODDS";
    internal const string RulesCap = "TAOM_TGW_RU_CAP";
    internal const string StakeLowAmount = "TAOM_TGW_ST_LOW_AMOUNT";
    internal const string StakeMidAmount = "TAOM_TGW_ST_MID_AMOUNT";
    internal const string StakeHighAmount = "TAOM_TGW_ST_HIGH_AMOUNT";
    internal const string GoWave = "TAOM_TGW_GO_WAVE";
    internal const string GoCount = "TAOM_TGW_GO_COUNT";
    internal const string GoAtOnce = "TAOM_TGW_GO_ATONCE";
    internal const string WithdrawnNet = "TAOM_TGW_WD_NET";
    internal const string ClearedWave = "TAOM_TGW_CL_WAVE";
    internal const string ClearedPayout = "TAOM_TGW_CL_PAYOUT";
    internal const string ClearedNet = "TAOM_TGW_CL_NET";
    internal const string ClearedCap = "TAOM_TGW_CL_CAP";
    internal const string ClearedOdds = "TAOM_TGW_CL_ODDS";
    internal const string LostWave = "TAOM_TGW_LO_WAVE";
    internal const string LostStake = "TAOM_TGW_LO_STAKE";
    internal const string LostEarlier = "TAOM_TGW_LO_EARLIER";
    internal const string DoneNet = "TAOM_TGW_DO_NET";
    internal const string RefusalMin = "TAOM_TGW_RF_MIN";

    /// <summary>The odds in tenths as a number of times the wager: 15 is "1.5", 20 is "2". Whole numbers only, so the text never carries a culture's separator.</summary>
    internal static string Odds(int tenths)
    {
        var whole = tenths / 10;
        var tenth = tenths % 10;
        return tenth == 0
            ? whole.ToString(CultureInfo.InvariantCulture)
            : whole.ToString(CultureInfo.InvariantCulture) + "." + tenth.ToString(CultureInfo.InvariantCulture);
    }

    internal static void SetRules(WaveRun run)
    {
        MBTextManager.SetTextVariable(RulesOdds, Odds(run.Row.OddsTenths));
        MBTextManager.SetTextVariable(RulesCap, run.MaxStake);
    }

    /// <summary>The stake lines count from the largest: slot 0 is the full cap, 1 the half, 2 the small one.</summary>
    internal static void SetStake(int slot, int amount)
    {
        var variable = slot switch
        {
            0 => StakeHighAmount,
            1 => StakeMidAmount,
            _ => StakeLowAmount,
        };
        MBTextManager.SetTextVariable(variable, amount);
    }

    internal static void SetGo(WaveRun run)
    {
        MBTextManager.SetTextVariable(GoWave, run.Wave);
        MBTextManager.SetTextVariable(GoCount, run.Row.Count);
        MBTextManager.SetTextVariable(GoAtOnce, run.Row.AtOnce);
    }

    internal static void SetWithdrawn(WaveRun run) => MBTextManager.SetTextVariable(WithdrawnNet, ToInt(run.Net));

    /// <summary>The run has moved on to the next wave, so the wave just cleared is the one before it, and the cap and odds are the next wave's.</summary>
    internal static void SetCleared(WaveRun run)
    {
        MBTextManager.SetTextVariable(ClearedWave, run.Wave - 1);
        MBTextManager.SetTextVariable(ClearedPayout, run.LastPayout);
        MBTextManager.SetTextVariable(ClearedNet, ToInt(run.Net));
        MBTextManager.SetTextVariable(ClearedCap, run.MaxStake);
        MBTextManager.SetTextVariable(ClearedOdds, Odds(run.Row.OddsTenths));
    }

    /// <summary>The wager lost is the last one; what was won before it is the net with that wager added back.</summary>
    internal static void SetLost(WaveRun run)
    {
        MBTextManager.SetTextVariable(LostWave, run.Wave);
        MBTextManager.SetTextVariable(LostStake, run.LastStake);
        MBTextManager.SetTextVariable(LostEarlier, ToInt(run.Net + run.LastStake));
    }

    internal static void SetDone(WaveRun run) => MBTextManager.SetTextVariable(DoneNet, ToInt(run.Net));

    private static int ToInt(long value) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value));

    /// <summary>The reason a refused offer is greyed; empty for an open offer and for the refusals that hide the line.</summary>
    internal static TextObject Explain(OfferRefusal refusal, int smallestStake)
    {
        switch (refusal)
        {
            case OfferRefusal.RanToday:
                return new TextObject(RefusalToday);
            case OfferRefusal.Tournament:
                return new TextObject(RefusalTournament);
            case OfferRefusal.Wounded:
                return new TextObject(RefusalWounded);
            case OfferRefusal.Disguised:
                return new TextObject(RefusalDisguised);
            case OfferRefusal.Poor:
                return new TextObject(RefusalPoor).SetTextVariable(RefusalMin, smallestStake);
            default:
                return TextObject.GetEmpty();
        }
    }
}

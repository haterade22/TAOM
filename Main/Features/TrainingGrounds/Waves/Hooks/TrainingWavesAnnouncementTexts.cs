using System;
using TaleWorlds.Localization;

namespace TAOM.Features.TrainingGrounds.Waves.Hooks;

/// <summary>
/// The English of the four quick notices of a wave fight (docs/features/training-waves.md), one <c>{=KEY}text</c> literal each, with
/// variables of their own: the notice sets them on its own <see cref="TextObject"/>, so no line of the master reads them
/// (<c>TrainingWavesAnnouncementTextsTests</c>). The place is the Training Grounds (#1527). <c>tools/harvest_literal_loc_keys.py</c>
/// registers the literals.
/// </summary>
internal static class TrainingWavesAnnouncementTexts
{
    internal const string Start = "{=taom_tgw_an_start}Wave {TAOM_TGW_AN_START_WAVE}: {TAOM_TGW_AN_START_COUNT} fighters!";
    internal const string Cleared = "{=taom_tgw_an_cleared}Wave {TAOM_TGW_AN_CLEARED_WAVE} cleared! You win {TAOM_TGW_AN_CLEARED_PAYOUT} denars.";
    internal const string Lost = "{=taom_tgw_an_lost}You went down in wave {TAOM_TGW_AN_LOST_WAVE}. The house keeps your wager of {TAOM_TGW_AN_LOST_STAKE} denars.";
    internal const string Refunded = "{=taom_tgw_an_refunded}The wave was called off. Your wager of {TAOM_TGW_AN_REFUNDED_STAKE} denars is returned.";

    /// <summary>The notice for a kind: <paramref name="amount"/> is the fighters of the wave, what it paid, the stake the house keeps, or the stake returned.</summary>
    internal static TextObject For(WaveAnnouncement kind, int wave, int amount)
    {
        switch (kind)
        {
            case WaveAnnouncement.WaveStart:
                return new TextObject(Start).SetTextVariable("TAOM_TGW_AN_START_WAVE", wave).SetTextVariable("TAOM_TGW_AN_START_COUNT", amount);
            case WaveAnnouncement.WaveCleared:
                return new TextObject(Cleared).SetTextVariable("TAOM_TGW_AN_CLEARED_WAVE", wave).SetTextVariable("TAOM_TGW_AN_CLEARED_PAYOUT", amount);
            case WaveAnnouncement.WaveLost:
                return new TextObject(Lost).SetTextVariable("TAOM_TGW_AN_LOST_WAVE", wave).SetTextVariable("TAOM_TGW_AN_LOST_STAKE", amount);
            case WaveAnnouncement.Refunded:
                return new TextObject(Refunded).SetTextVariable("TAOM_TGW_AN_REFUNDED_STAKE", amount);
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "no notice for this kind");
        }
    }
}

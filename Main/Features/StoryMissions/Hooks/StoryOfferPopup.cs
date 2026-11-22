using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.StoryMissions.Hooks;

/// <summary>
/// The story offer's engine text (#1105 slice 4, #1326): a line with the window's {TARGET} filled in, and the two-button popup whose
/// question and accept button follow the sides the player may take (<see cref="StoryOfferText"/>). Escape runs the popup's negative
/// action, so <c>later</c> must be a snooze, never the saved decline.
/// </summary>
internal static class StoryOfferPopup
{
    public static TextObject Line(string text, ChronicleStoryOffer? window)
    {
        var result = new TextObject(text);
        var target = window?.TargetSettlementId == null ? null : Settlement.Find(window.TargetSettlementId);
        result.SetTextVariable("TARGET", target?.Name ?? TextObject.GetEmpty());
        return result;
    }

    public static void Show(StoryOffer offer, Action ride, Action later) => InformationManager.ShowInquiry(new InquiryData(
        Line(offer.Window.Title, offer.Window).ToString(),
        Line(StoryOfferText.PopupBody(offer.Choices, offer.Window.HeroId != null), offer.Window).ToString(),
        true, true,
        Line(StoryOfferText.PopupAccept(offer.Choices, offer.Window.HeroId != null), offer.Window).ToString(),
        new TextObject("{=taom_story_offer_later}Not now").ToString(),
        ride, later), pauseGameActiveState: true, prioritize: false);
}

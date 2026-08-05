using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

public class InquiryPresenterAdapter : IInquiryPresenterAdapter
{
    public void ShowPromotionOffer(string troopName, Action onAccept, Action onDecline)
    {
        var title = new TextObject("{=taom_fc_offer_title}Battlefield Promotion");
        var body = new TextObject("{=taom_fc_offer_body}{TROOP_NAME} has distinguished themselves in battle. Promote them to a companion?");
        body.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);

        InformationManager.ShowInquiry(new InquiryData(
            title.ToString(),
            body.ToString(),
            true,
            true,
            new TextObject("{=taom_fc_accept}Promote").ToString(),
            new TextObject("{=taom_fc_decline}Not Yet").ToString(),
            onAccept,
            onDecline),
            true,
            false);
    }

    public void ShowNoCompanionRoom(string troopName, Action onAcknowledge)
    {
        var title = new TextObject("{=taom_fc_offer_title}Battlefield Promotion");
        var body = new TextObject("{=taom_fc_no_room_body}There is no room for another companion right now. {TROOP_NAME}'s promotion will be offered again later.");
        body.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);

        InformationManager.ShowInquiry(new InquiryData(
            title.ToString(),
            body.ToString(),
            true,
            false,
            new TextObject("{=taom_fc_acknowledge}Understood").ToString(),
            null,
            onAcknowledge,
            null),
            true,
            false);
    }

    public void ShowRenamePrompt(string troopName, Action<string> onConfirm, Action onSkip)
    {
        var title = new TextObject("{=taom_fc_rename_title}Name the New Companion");
        var body = new TextObject("{=taom_fc_rename_body}Give {TROOP_NAME} a name, or keep the one they carried into battle.");
        body.SetTextVariable("TROOP_NAME", troopName ?? string.Empty);

        // Verified 1.4.7 ctor order — see IInquiryPresenterAdapter's doc comment (bug fix (b)).
        InformationManager.ShowTextInquiry(new TextInquiryData(
            title.ToString(),
            body.ToString(),
            true,
            true,
            new TextObject("{=taom_fc_confirm_name}Confirm").ToString(),
            new TextObject("{=taom_fc_keep_name}Keep Default Name").ToString(),
            onConfirm,
            onSkip,
            shouldInputBeObfuscated: false,
            textCondition: null,
            soundEventPath: string.Empty,
            defaultInputText: troopName ?? string.Empty),
            false,
            false);
    }
}

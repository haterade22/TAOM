using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.CampaignStartOptions;

namespace TAOM.Adapters;

public sealed class CampaignStartNoticeAdapter : ICampaignStartNoticeAdapter
{
    public void ShowCampaignKeepsStartChoices() =>
        InformationManager.DisplayMessage(new InformationMessage(new TextObject(
            "{=taom_cso_keeps_choices}This campaign keeps the start choices it was created with, so later changes to them in the TAOM mod options do not apply to it. The console command taom.campaign_option lists and changes them.").ToString()));
}

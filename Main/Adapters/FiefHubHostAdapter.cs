using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

public sealed class FiefHubHostAdapter : IFiefHubHostAdapter
{
    private const string HubMenuId = "fief_hub";

    public bool IsMapClearForMenu => MapMenuGate.IsMapClearForMenu();

    public void ShowNoFiefsMessage()
    {
        InformationManager.DisplayMessage(new InformationMessage(
            new TextObject("{=taom_fief_no_fiefs}You don't own any fiefs yet.").ToString(),
            Colors.Yellow));
    }

    public void OpenHub() => GameMenu.ActivateGameMenu(HubMenuId);
}

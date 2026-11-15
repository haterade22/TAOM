using TaleWorlds.CampaignSystem.GameMenus;
using TAOM.Features.CheatMenu;

namespace TAOM.Adapters;

public sealed class CheatMenuHostAdapter : ICheatMenuHostAdapter
{
    public bool IsMapClearForMenu => MapMenuGate.IsMapClearForMenu();

    public void OpenMenu() => GameMenu.ActivateGameMenu(CheatMenuIds.Root);
}

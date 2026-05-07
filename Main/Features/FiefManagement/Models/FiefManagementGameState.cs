using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.FiefManagement.Models;

public class FiefManagementGameState : GameState
{
    public Settlement Fief { get; private set; }

    public override bool IsMenuState => true;

    // Parameterless ctor required by GameStateManager.CreateState<T>'s new() constraint.
    // The fief is provided via Initialize after construction (vanilla pattern — see InventoryState
    // / CraftingState which use property setters after CreateState<T>()).
    public FiefManagementGameState() { }

    public void Initialize(Settlement fief)
    {
        Fief = fief;
    }
}

using TaleWorlds.Core;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// Game state for the supply order screen. IsMenuState pauses campaign time and makes
/// GameStateManager deactivate map bar input while the screen is up (the attribute-path
/// precedent is CareerScreenGameState; the source module reached the same screen through a
/// Harmony patch on GameStateScreenManager.CreateScreen, which the [GameStateScreen]
/// attribute makes unnecessary).
/// </summary>
public class SupplyOrderGameState : GameState
{
    public override bool IsMenuState => true;

    /// <summary>Set by <see cref="SupplyOrderScreens.Open(bool)"/> before the push: orders
    /// confirmed on this screen are marked camp-placed (cancelled when that camp breaks).</summary>
    public bool FromCamp { get; set; }
}

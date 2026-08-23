using System;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Features.SupplyLines.UI;

/// <summary>
/// Static boundary opener for the supply order screen. The campaign behavior's menu option and
/// the FieldCamp actions call this seam; everything downstream (state, screen, VM) hangs off
/// the pushed <see cref="SupplyOrderGameState"/>.
/// </summary>
public static class SupplyOrderScreens
{
    public static void Open()
    {
        Open(fromCamp: false);
    }

    /// <summary>
    /// <paramref name="fromCamp"/> rides the game state into the VM and onto the placed order
    /// (<c>SupplyOrder.PlacedFromCamp</c>): breaking that camp cancels camp-placed orders and
    /// only those. Town/keep menu callers use the parameterless overload.
    /// </summary>
    public static void Open(bool fromCamp)
    {
        IModLogger? logger = null;
        try
        {
            logger = IoC.Resolve<IModLogger>();
            var manager = Game.Current?.GameStateManager;
            if (manager == null)
            {
                logger?.LogWarning("SupplyLines: cannot open the order screen, no active game state manager");
                return;
            }

            var state = manager.CreateState<SupplyOrderGameState>();
            state.FromCamp = fromCamp;
            manager.PushState(state);
        }
        catch (Exception ex)
        {
            // A failed open must never crash the campaign menu that invoked it.
            logger?.LogError($"SupplyLines: failed to open the supply order screen: {ex}");
        }
    }
}

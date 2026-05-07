using System;
using System.Collections.Generic;
using TAOM.Features.QuickActions.Models;

namespace TAOM.Features.QuickActions;

public interface IQuickActionsService
{
    /// <summary>
    /// Opens the multi-action inquiry. <paramref name="originalSellAll"/> is the vanilla
    /// behavior the patch intercepted; choose-original-sell routes back to it.
    /// </summary>
    void OpenMenu(Action originalSellAll);

    /// <summary>Sells every right-pane item that matches the damaged-threshold filter.</summary>
    QuickActionResult SellAllDamaged();

    /// <summary>Sells every right-pane item with value &lt;= LowValueThreshold (subject to filters).</summary>
    QuickActionResult SellAllLowValue();

    /// <summary>Strips player main hero's battle + civilian equipment to inventory.</summary>
    QuickActionResult UnequipAll();

    /// <summary>Used by the inquiry construction: textual labels for the four actions.</summary>
    IReadOnlyList<(QuickActionType type, string label, string description)> GetMenuOptions();
}

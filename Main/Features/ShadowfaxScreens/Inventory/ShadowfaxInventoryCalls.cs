using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The static helper the IL rewrite calls, and the wiring the Harmony hooks reach (handed over by
/// ShadowfaxScreensModule.InitializeStatics; the helper is vanilla's own call while unwired or switched off).
/// </summary>
internal static class ShadowfaxInventoryCalls
{
    /// <summary>The one category of the Inventory fast open.</summary>
    internal const string PatchCategory = "Patch128_ShadowfaxInventory";

    internal static InventoryOpenFastPathService? Service { get; private set; }

    internal static MethodInfo SortByNameMethod { get; } =
        typeof(ShadowfaxInventoryCalls).GetMethod(nameof(SortByNameOrPassThrough))!;

    internal static void Initialize(InventoryOpenFastPathService? service) => Service = service;

    /// <summary>Starts the timed open (null while "Log Screen Open Times" is off) and the service's totals for it.</summary>
    internal static ScreenOpenWindow? BeginOpen()
    {
        var window = ScreenOpenTiming.Begin("Inventory", "open");
        if (window != null) Service?.BeginOpen();
        return window;
    }

    /// <summary>
    /// Ends the timed open with the mode, the side counts (-1 when unreadable) and the service's phase totals as its
    /// detail. The service closes its open latch before formatting, so no later row is counted into this open.
    /// </summary>
    internal static void EndOpen(ScreenOpenWindow? window, Exception? exception, string mode, int left, int right)
    {
        if (window == null) return;
        ScreenOpenTiming.End(window, exception, Service?.EndOpen(mode, left, right));
    }

    internal static long? BeginRows() => Service?.BeginRows();

    internal static void EndRows(long? start) => Service?.EndRows(start);

    /// <summary>Replaces <c>OrderBy(key)</c> in InitializeInventory. The constructor and AfterReset sort the lists afterwards.</summary>
    public static IEnumerable<ItemRosterElement> SortByNameOrPassThrough(
        IEnumerable<ItemRosterElement> source, Func<ItemRosterElement, string> key)
    {
        var service = Service;
        return service != null && service.ShouldSkipNameSort() ? source : source.OrderBy(key);
    }
}

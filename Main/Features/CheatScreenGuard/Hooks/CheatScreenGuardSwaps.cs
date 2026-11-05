using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Features.MissionPerf;
using TaleWorlds.Core;

namespace TAOM.Features.CheatScreenGuard.Hooks;

/// <summary>The one call site each transpiler swaps (CheatScreenGuardBindingTests reads the real IL). A missing getter makes the swap misfit, so the method stays vanilla.</summary>
internal static class CheatScreenGuardSwaps
{
    /// <summary>Sites the inventory transpiler swapped on its last run; -1 until it ran or after its apply failed.</summary>
    internal static int LastInventorySwapped { get; set; } = -1;

    /// <summary>The same for the party screen transpiler.</summary>
    internal static int LastPartySwapped { get; set; } = -1;

    private static MethodInfo CheatModeGetter => AccessTools.PropertyGetter(typeof(Game), nameof(Game.CheatMode));

    internal static IReadOnlyList<CallSwap> BuildInventory() => new[]
    {
        new CallSwap(CheatModeGetter, AccessTools.Method(typeof(CheatScreenGuardCalls), nameof(CheatScreenGuardCalls.InventoryCheatMode))),
    };

    internal static IReadOnlyList<CallSwap> BuildParty() => new[]
    {
        new CallSwap(CheatModeGetter, AccessTools.Method(typeof(CheatScreenGuardCalls), nameof(CheatScreenGuardCalls.PartyCheatMode))),
    };
}

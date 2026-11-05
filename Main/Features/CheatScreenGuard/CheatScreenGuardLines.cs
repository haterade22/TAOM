using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>The guard's log lines, built in one place so the tests pin their wording.</summary>
internal static class CheatScreenGuardLines
{
    internal const string Tag = "[CheatScreenGuard]";

    internal static string BuildInstallLine() =>
        $"{Tag} ON: with cheat mode on, the own inventory and the party screen open without the every-item and every-troop lists unless MCM 'Cheat Mode Lists All Items And Troops' is on";

    internal static string BuildPartialInstallLine(int inventorySwapped, int partySwapped) =>
        $"{Tag} not fully installed: inventory gate {Describe(inventorySwapped)}, party screen gate {Describe(partySwapped)}; an ungated screen keeps the vanilla cheat list";

    internal static string BuildHiddenLine(CheatScreen screen) =>
        $"{Tag} cheat mode is on: the {(screen == CheatScreen.Inventory ? "inventory" : "party screen")} opened without the cheat list (MCM 'Cheat Mode Lists All Items And Troops' is off); later opens this launch say nothing more";

    private static string Describe(int swapped) => swapped switch
    {
        1 => "on",
        0 => "off (the call site was not found exactly once)",
        _ => "off (the patch did not apply)",
    };
}

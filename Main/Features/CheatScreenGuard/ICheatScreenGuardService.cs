using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Features.CheatScreenGuard;

/// <summary>What the two swapped-in cheat-mode reads ask (docs/features/cheat-screen-guard.md). Neither member throws.</summary>
public interface ICheatScreenGuardService
{
    /// <summary>Asked only while cheat mode is on. True opens the vanilla cheat list; false opens the screen as without cheat mode.</summary>
    bool ShowCheatList(CheatScreen screen);

    /// <summary>The install line from the two swap counts: 1 gated, 0 left vanilla, -1 the transpiler never ran.</summary>
    void LogInstall(int inventorySwapped, int partySwapped);
}

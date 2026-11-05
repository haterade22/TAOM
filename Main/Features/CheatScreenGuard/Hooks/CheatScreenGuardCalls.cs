using TAOM.Features.CheatScreenGuard.Models;
using TaleWorlds.Core;

namespace TAOM.Features.CheatScreenGuard.Hooks;

/// <summary>
/// The helpers the guard's transpilers swap in for the one <c>Game.get_CheatMode</c> read in each screen opener. Each
/// takes the <see cref="Game"/> the engine pushed, reads the real flag, and asks the service only while it is on. With no
/// service, or when the service throws, the answer is the real flag, so the screen behaves as in vanilla. Main thread.
/// </summary>
internal static class CheatScreenGuardCalls
{
    internal static ICheatScreenGuardService? Service { get; private set; }

    internal static void Initialize(ICheatScreenGuardService? service) => Service = service;

    // v1.4.8 InventoryScreenHelper.cs:177, inside OpenInventoryPresentation.
    internal static bool InventoryCheatMode(Game game) => Decide(game.CheatMode, CheatScreen.Inventory);

    // v1.4.8 PartyScreenHelper.cs:187, inside OpenScreenAsNormal.
    internal static bool PartyCheatMode(Game game) => Decide(game.CheatMode, CheatScreen.PartyScreen);

    internal static bool Decide(bool cheatMode, CheatScreen screen)
    {
        if (!cheatMode) return false;
        var service = Service;
        if (service == null) return true;
        try { return service.ShowCheatList(screen); }
        catch { return true; }
    }
}

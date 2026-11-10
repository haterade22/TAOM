using System;
using TaleWorlds.Library;
using TAOM.Client.Features.Realms.Encyclopedia;
using TAOM.Client.Features.Realms.Hooks;

namespace TAOM.Client.Features.Realms;

/// <summary>
/// The Realms hooks ClientSubModule calls (docs/features/realms.md): the client patches at game start, and per frame the
/// queued title windows and page links. The view model mixins and prefab extensions register with the rest of
/// TAOM.Client's UIExtenderEx extensions.
/// </summary>
internal static class RealmsClient
{
    private static bool _tickFaulted;

    /// <summary>
    /// Every game start; each patch applies once and catches its own failure. Runs before SandBox's
    /// OnGameInitializationFinished builds the encyclopedia pages, which the Titles page's patch has to be in place for.
    /// </summary>
    public static void OnGameStart()
    {
        ConversationNames.Patch();
        TitleEncyclopediaPage.Patch();
    }

    /// <summary>Per frame: one queued action at most. Never throws into the engine's tick.</summary>
    public static void Tick()
    {
        try
        {
            UiQueue.Run();
        }
        catch (Exception ex)
        {
            if (_tickFaulted)
                return;
            _tickFaulted = true;
            Debug.Print($"[TAOM.Realms] the titles tick failed (further failures are not logged): {ex}");
        }
    }
}

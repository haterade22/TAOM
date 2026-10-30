using System;
using HarmonyLib;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>
/// Times PartyVM.TransferAllTroops(PartyCommand) as its own "Party transfer all" window. It rebuilds the lists through
/// InitializeTroopLists, and the code cache already spans the session. Observe only. UI main thread.
/// </summary>
[HarmonyPatch(typeof(PartyVM), "TransferAllTroops", new[] { typeof(PartyScreenLogic.PartyCommand) })]
[HarmonyPatchCategory(PartyFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class PartyVM_TransferAllTroops_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static void Prefix(out ScreenOpenWindow? __state)
    {
        __state = null;
        try
        {
            __state = ScreenOpenTiming.Begin("Party", "transfer all");
        }
        catch
        {
            // Untimed.
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(ScreenOpenWindow? __state, Exception? __exception)
    {
        try
        {
            ScreenOpenTiming.End(__state, __exception);
        }
        catch
        {
            // Never break the screen.
        }
    }
}

using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>
/// Times InitializeTroopLists (the constructor, Reset and TransferAllTroops all call it) as the open line's "init"
/// phase. Observe only; the pair links through __state, so a stripped prefix leaves the finalizer a no-op. UI main thread.
/// </summary>
[HarmonyPatch(typeof(PartyVM), "InitializeTroopLists")]
[HarmonyPatchCategory(PartyFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class PartyVM_InitializeTroopLists_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static void Prefix(out long __state)
    {
        __state = 0;
        try
        {
            __state = PartyFastOpenCalls.Service?.BeginPhase() ?? 0;
        }
        catch
        {
            // Untimed.
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(long __state, Exception? __exception)
    {
        try
        {
            PartyFastOpenCalls.Service?.EndPhase(PartyPhase.InitializeTroopLists, __state);
        }
        catch
        {
            // Never break the screen.
        }
    }
}

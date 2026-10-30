using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>Times PartyVM.RefreshValues as the open line's "refresh" phase (the constructor's last step). Observe only. UI main thread.</summary>
[HarmonyPatch(typeof(PartyVM), nameof(PartyVM.RefreshValues))]
[HarmonyPatchCategory(PartyFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class PartyVM_RefreshValues_ShadowfaxParty_Patch
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
            PartyFastOpenCalls.Service?.EndPhase(PartyPhase.RefreshValues, __state);
        }
        catch
        {
            // Never break the screen.
        }
    }
}

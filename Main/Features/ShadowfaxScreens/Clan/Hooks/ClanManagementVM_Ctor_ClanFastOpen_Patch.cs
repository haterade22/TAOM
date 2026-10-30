using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Clan.Hooks;

/// <summary>
/// Clan screen open timer (docs/reference/harmony-patch-registry.md "Patch127_ShadowfaxClan"). The <c>ClanManagementVM</c>
/// constructor is where all three party-list builds happen, so its span is the VM build and nothing else (the movie load
/// that follows in <c>GauntletClanScreen</c> is not in it). A void prefix opens a <c>[ScreenOpen] Clan open</c> window (null
/// while "Log Screen Open Times" is off, which also keeps the service's count window closed); the void finalizer closes both
/// and writes the line with the list-build counts as its detail. Timing only: it changes nothing the screen does.
/// </summary>
[HarmonyPatch(typeof(ClanManagementVM), MethodType.Constructor, new[] { typeof(Action), typeof(Action<Hero>), typeof(Action<Hero>), typeof(Action) })]
[HarmonyPatchCategory(ClanFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class ClanManagementVM_Ctor_ClanFastOpen_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out ScreenOpenWindow? __state)
    {
        __state = null;
        try
        {
            __state = ScreenOpenTiming.Begin("Clan", "open");
            if (__state != null) ClanFastOpenCalls.Service?.BeginOpen();
        }
        catch { /* a diagnostic must never break the screen it measures */ }
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, ScreenOpenWindow? __state)
    {
        try
        {
            if (__state == null) return;
            ScreenOpenTiming.End(__state, __exception, ClanFastOpenCalls.Service?.EndOpen());
        }
        catch { /* observe-only */ }
    }
}

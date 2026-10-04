using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Features.LoadTimeStamps.Hooks;

// The [Lifecycle] stamps (docs/features/load-time-stamps.md): one class per CampaignEventDispatcher
// lifecycle method. Every dispatch gets a dispatch line, whatever "Enable Load-Time Stamps" says.
// With the toggle on, the prefix also swaps every listener of the dispatch's events for a timing
// wrapper and the void finalizer puts every original back, whether or not the dispatch threw.
// Observe only: the prefixes return nothing and the finalizers return nothing, so the original
// always runs and Harmony rethrows its exception with the original stack. Both forward to
// LoadTimeStampsHooks, which never throws.

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnNewGameCreated))]
[HarmonyPatchCategory(LoadTimeStampsModule.LifecycleCategory)]
public static class CampaignEventDispatcher_OnNewGameCreated_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(out LifecycleDispatchScope? __state)
        => __state = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnNewGameCreated);

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LifecycleDispatchScope? __state)
        => LoadTimeStampsHooks.EndDispatch(__state, __exception);
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameEarlyLoaded))]
[HarmonyPatchCategory(LoadTimeStampsModule.LifecycleCategory)]
public static class CampaignEventDispatcher_OnGameEarlyLoaded_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(out LifecycleDispatchScope? __state)
        => __state = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnGameEarlyLoaded);

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LifecycleDispatchScope? __state)
        => LoadTimeStampsHooks.EndDispatch(__state, __exception);
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnGameLoaded))]
[HarmonyPatchCategory(LoadTimeStampsModule.LifecycleCategory)]
public static class CampaignEventDispatcher_OnGameLoaded_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(out LifecycleDispatchScope? __state)
        => __state = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnGameLoaded);

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LifecycleDispatchScope? __state)
        => LoadTimeStampsHooks.EndDispatch(__state, __exception);
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnSessionStart))]
[HarmonyPatchCategory(LoadTimeStampsModule.LifecycleCategory)]
public static class CampaignEventDispatcher_OnSessionStart_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(out LifecycleDispatchScope? __state)
        => __state = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnSessionStart);

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LifecycleDispatchScope? __state)
        => LoadTimeStampsHooks.EndDispatch(__state, __exception);
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnAfterSessionStart))]
[HarmonyPatchCategory(LoadTimeStampsModule.LifecycleCategory)]
public static class CampaignEventDispatcher_OnAfterSessionStart_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(out LifecycleDispatchScope? __state)
        => __state = LoadTimeStampsHooks.BeginDispatch(LifecycleDispatch.OnAfterSessionStart);

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LifecycleDispatchScope? __state)
        => LoadTimeStampsHooks.EndDispatch(__state, __exception);
}

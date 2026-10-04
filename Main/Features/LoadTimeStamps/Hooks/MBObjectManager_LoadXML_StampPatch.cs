using System;
using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.LoadTimeStamps.Hooks;

/// <summary>
/// The always-on [LoadXml] stamp (docs/features/load-time-stamps.md): starts a timed call at
/// LoadXML's entry and closes it in a void finalizer. Observe only: the prefix returns nothing and
/// the finalizer returns nothing, so the original always runs and Harmony rethrows its exception
/// with the original stack. Both forward to LoadTimeStampsHooks, which never throws.
/// </summary>
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.LoadXML),
    new[] { typeof(string), typeof(bool), typeof(string), typeof(bool) })]
[HarmonyPatchCategory(LoadTimeStampsModule.LoadXmlCategory)]
public static class MBObjectManager_LoadXML_StampPatch
{
    [HarmonyPrefix]
    public static void Prefix(string id, string gameType, out LoadXmlCall? __state)
        => __state = LoadTimeStampsHooks.BeginLoadXml(id, gameType);

    // void: observe only, so Harmony rethrows the original exception with its stack.
    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, LoadXmlCall? __state)
        => LoadTimeStampsHooks.EndLoadXml(__state, __exception);
}

using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.LoadTimeStamps.Hooks;

/// <summary>
/// Marks the end of the merge of the LoadXML call in flight on this thread, with its file and XSLT
/// counts (docs/features/load-time-stamps.md). A void finalizer, observe only: it runs whether or
/// not another mod's prefix skipped the original (plan 042's fast path), and it never changes the
/// result or the exception. Never add a prefix here. A merge outside LoadXML (GameText, the native
/// merges) finds no call in flight and is ignored.
/// </summary>
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile))]
[HarmonyPatchCategory(LoadTimeStampsModule.LoadXmlCategory)]
public static class MBObjectManager_CreateMergedXmlFile_StampPatch
{
    // void: observe only. Runs whether or not another prefix skipped the original; never add a prefix here.
    [HarmonyFinalizer]
    public static void Finalizer(List<Tuple<string, string>> toBeMerged, List<string> xsltList)
        => LoadTimeStampsHooks.MergeFinished(toBeMerged, xsltList);
}

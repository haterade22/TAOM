using System;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.XmlMerge.Hooks;

/// <summary>
/// Patch99 (docs/features/xml-merge-fast-path.md): runs the engine's module-XML merge loop through
/// <see cref="XmlMergeService"/>, which keeps one XDocument across the files instead of converting the whole merged
/// document both ways per file. Callers: <c>GetMergedXmlForManaged</c> for every <c>LoadXML</c> (Campaign.OnInitialize
/// and CustomGame.OnInitialize, every game), GameText and BannerIcons, all validated; <c>GetMergedXmlForNative</c>,
/// the custom battle's <c>CustomBattleScenes</c> read and TAOM's two adapters pass skipValidation true and keep the
/// engine's code. About 30 to 40 calls per load, never per frame. The skipped original loads and validates each file,
/// applies each entry's XSLT before that entry's file, merges each file with MergeElements (or appends when the XSD
/// path is ""), round-trips the accumulated document between merges, and returns it. The fast path keeps every one of
/// those calls in the engine's order except the round trip, which XmlMergeLiveEquivalenceTests proves output-neutral
/// for every type of the live install (the one input it can change is in the feature doc, "Known limits").
///
/// <para>Returning false is safe because the result is byte-identical on that module set (that gate) and the service
/// stands aside while any other mod patches a method it bypasses. Returning true on an exception is safe because the
/// fast path writes nothing outside its own documents and caches: the original then runs on untouched arguments, and
/// whatever it does there is the unpatched game's outcome, provided PatchShield stays off the method (below). The
/// finalizer is void, so it never replaces the engine's exception; it only logs. (Harmony keeps <c>rethrow</c> only
/// while every finalizer on the method is void. With plan 040's PatchShield exclusion, the only configuration that
/// ships, PatchShield never attaches here and every finalizer on the method is void, Patch99's and Patch100's, so the
/// engine's exception leaves with its own trace. Without it, PatchShield's second pass attaches its value-returning
/// ShieldFinalizerWithResult: <c>throw</c> plus RethrowStackPreserver, and the swallow below.) Priority.Last lets any
/// other prefix run first.</para>
///
/// <para><b>PatchShield and plan 040.</b> Patching this engine method makes PatchShield's second pass (the end of the
/// first game's initialization) attach its finalizer to it unless <c>PatchShieldPolicy.ExcludedTargetMethods</c> lists
/// it. That finalizer swallows MissingMethodException, MissingFieldException and TypeLoadException and the method then
/// returns null, which <c>MBObjectManager.LoadXML</c> turns into a type that silently loads empty (it passes the null to
/// <c>LoadXml(doc)</c> inside its own try/catch, v1.5.3 MBObjectManager.cs:786-797), where the unpatched game lets the
/// exception propagate. Plan 040 put the method on that list, so this patch adds no entry of its own (a second copy
/// would duplicate 040's), and the two ship together (feature doc, "Shipping order").</para>
/// </summary>
[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.CreateMergedXmlFile))]
[HarmonyPatchCategory(XmlMergeModule.PatchCategory)]
public static class MBObjectManager_CreateMergedXmlFile_Patch
{
    private static XmlMergeService? _service;

    public static void Initialize(XmlMergeService service) => _service = service;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static bool Prefix(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation,
                              ref XmlDocument __result, out XmlMergeCall __state)
    {
        __state = new XmlMergeCall();
        try
        {
            var service = _service;
            if (service == null || !service.TryMergeFast(toBeMerged, xsltList, skipValidation, __state, out var merged))
                return true;
            __result = merged!;
            return false;
        }
        catch
        {
            return true;   // TryMergeFast never throws; this guards the call itself
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, XmlMergeCall? __state)
    {
        try
        {
            if (__state != null)
                _service?.OnOriginalFinished(__state, __exception);
        }
        catch
        {
            // A log line must never change the engine's outcome.
        }
    }

    /// <summary>Called from SubModule.OnGameInitializationFinished at every game init; never throws.</summary>
    public static void LogWindowSummary(string? gameType)
    {
        try { _service?.LogWindowSummary(gameType); }
        catch { /* a summary line must never break game init */ }
    }
}

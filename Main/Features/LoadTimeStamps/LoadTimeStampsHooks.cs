using System;
using System.Collections.Generic;
using TAOM.Features.LoadTimeStamps.Domain;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// The static entry SubModule.cs and the load-time patches call. Its only state is the services the
/// module hands it in InitializeStatics. Every member answers "off" (false or null) when unwired and
/// never throws: a stamp must never break a load.
/// </summary>
internal static class LoadTimeStampsHooks
{
    internal static LoadStampDetailGate? Gate { get; private set; }
    internal static HookStampService? Hooks { get; private set; }
    internal static LoadXmlStampService? LoadXml { get; private set; }
    internal static LifecycleTimingService? Lifecycle { get; private set; }

    // Each stage has its own setter, so a test can wire or clear one part without the others.
    internal static void Initialize(LoadStampDetailGate? gate, HookStampService? hooks)
    {
        Gate = gate;
        Hooks = hooks;
    }

    internal static void InitializeLoadXml(LoadXmlStampService? service) => LoadXml = service;

    internal static void InitializeLifecycle(LifecycleTimingService? service) => Lifecycle = service;

    /// <summary>The toggle, or false when the module is not wired or the read throws.</summary>
    internal static bool DetailEnabled
    {
        get
        {
            try { return Gate?.Enabled ?? false; }
            catch { return false; }
        }
    }

    /// <summary>A timer for one TAOM hook's steps, or null when the detail is off, unwired or failing.</summary>
    internal static HookTimer? StartHook(string hook, string? game)
    {
        try { return Hooks?.Start(hook, game); }
        catch { return null; }
    }

    /// <summary>The LoadXML prefix's call; null when unwired or failing.</summary>
    internal static LoadXmlCall? BeginLoadXml(string? id, string? gameType)
    {
        try { return LoadXml?.Begin(id, gameType); }
        catch { return null; }
    }

    /// <summary>The LoadXML finalizer's call.</summary>
    internal static void EndLoadXml(LoadXmlCall? call, Exception? exception)
    {
        try { LoadXml?.End(call, exception); }
        catch { /* a stamp must never break a load */ }
    }

    /// <summary>The CreateMergedXmlFile finalizer's call.</summary>
    internal static void MergeFinished(IList<Tuple<string, string>>? toBeMerged, IList<string>? xsltList)
    {
        try { LoadXml?.MergeFinished(toBeMerged, xsltList); }
        catch { /* a stamp must never break a load */ }
    }

    /// <summary>SubModule.OnGameInitializationFinished's call: the [LoadXml] summary of this game's loads.</summary>
    internal static void LogLoadXmlSummary()
    {
        try { LoadXml?.LogSummary(); }
        catch { /* a stamp must never break a load */ }
    }

    /// <summary>A CampaignEventDispatcher prefix's call; null when unwired or failing. The toggle only decides
    /// whether the call's handlers are timed: the dispatch line is written either way.</summary>
    internal static LifecycleDispatchScope? BeginDispatch(LifecycleDispatch dispatch)
    {
        try { return Lifecycle?.Begin(dispatch); }
        catch { return null; }
    }

    /// <summary>A CampaignEventDispatcher finalizer's call: the lines, and every original listener put back.</summary>
    internal static void EndDispatch(LifecycleDispatchScope? scope, Exception? exception)
    {
        try { Lifecycle?.End(scope, exception); }
        catch { /* a stamp must never break a load */ }
    }
}

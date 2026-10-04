using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.XmlMerge;

/// <summary>
/// The module-XML merge fast path (docs/features/xml-merge-fast-path.md, plan 042). The engine's
/// <c>MBObjectManager.CreateMergedXmlFile</c> converts the whole document merged so far from <c>XmlDocument</c> to
/// <c>XDocument</c> and back for every file, and compiles every XSLT on every call, so a type's merge grows with the
/// square of its size (NPCCharacters: 13.3 s of a new campaign's 50.2 s loading screen). This service runs the same
/// loop with the same engine helpers in the same order, but keeps the accumulated document as one <c>XDocument</c>
/// between merges and converts it to <c>XmlDocument</c> only before an XSLT and once at the end, with each XSLT
/// compiled once per process (<see cref="XsltTransformCache"/>). XmlMergeLiveEquivalenceTests proves the output
/// identical, character for character, for every type of the live install; the inputs where dropping the round trip
/// can differ are in the feature doc ("Known limits"), and none of them throws, so none reaches the fallback.
///
/// <para>The service never throws to its caller. It runs only for validated merges (skipValidation false), stands
/// aside while another mod patches a method it bypasses, and on any exception lets the engine's own merge run on the
/// untouched arguments, writing the exception's full text at DEBUG; when that engine merge then succeeds, the fault was
/// TAOM's, and the fast path turns itself off for the session. Every merge, fast or not, logs one INFO line (about 30
/// to 40 per load, never per frame), and <see cref="LogWindowSummary"/> logs the totals once per game. One lock
/// serialises the fast path, its caches and its bookkeeping: nothing proves which thread the engine merges on.</para>
/// </summary>
public sealed class XmlMergeService
{
    internal const string ReasonNotInitialized = "not-initialized";
    internal const string ReasonDisabled = "disabled";
    internal const string ReasonBindings = "bindings";
    internal const string ReasonEmptyList = "empty-list";
    internal const string ReasonSkipValidation = "skip-validation";
    internal const string ReasonForeignPatch = "foreign-patch";
    internal const string ReasonFastPathError = "fast-path-error";
    internal const string UnknownType = "unknown";

    private readonly IXmlMergeEngineAdapter _engine;
    private readonly XsltTransformCache _xslt;
    private readonly IModLogger _logger;
    private readonly object _gate = new object();
    private readonly HashSet<string> _standAsideLogged = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _failureTypesLogged = new HashSet<string>(StringComparer.Ordinal);
    private bool _disabled;
    private Window _window = new Window();

    public XmlMergeService(IXmlMergeEngineAdapter engine, XsltTransformCache xslt, IModLogger logger)
    {
        _engine = engine;
        _xslt = xslt;
        _logger = logger;
    }

    /// <summary>Null for the fast path; otherwise why the engine's own code runs.</summary>
    internal string? Decide(bool skipValidation, int entryCount)
    {
        lock (_gate)
            return DecideLocked(skipValidation, entryCount, out _);
    }

    /// <summary>
    /// The engine's <c>CreateMergedXmlFile</c> loop (MBObjectManager.cs:962-978) with the accumulated document kept as
    /// one <c>XDocument</c>. Every branch is the engine's: index 0's XSLT is never applied; an entry's XSLT transforms
    /// the document so far before that entry's file merges; an entry whose file path is "" applies its XSLT and merges
    /// nothing; an entry whose XSD path is "" is appended, not merged; one entry returns the loaded document as is.
    /// The comparisons are the engine's <c>!= ""</c>, so a null path fails the way the engine fails.
    /// </summary>
    internal XmlDocument MergeFast(IReadOnlyList<Tuple<string, string>> toBeMerged, IReadOnlyList<string> xsltList,
                                   bool skipValidation, XmlMergeCounters counters)
    {
        XmlDocument? xml = Load(toBeMerged[0].Item1, toBeMerged[0].Item2, skipValidation, counters);
        XDocument? merged = null;
        for (int i = 1; i < toBeMerged.Count; i++)
        {
            if (xsltList[i] != "")
            {
                if (merged != null)
                {
                    xml = _engine.ToXmlDocument(merged);
                    merged = null;
                }
                long started = Stopwatch.GetTimestamp();
                xml = _xslt.Apply(xsltList[i], xml!);
                counters.XsltTicks += Stopwatch.GetTimestamp() - started;
            }
            if (toBeMerged[i].Item1 != "")
            {
                // The engine's order: load the next file, convert the accumulated document, convert the next, merge.
                XmlDocument next = Load(toBeMerged[i].Item1, toBeMerged[i].Item2, skipValidation, counters);
                if (merged == null)
                {
                    merged = _engine.ToXDocument(xml!);
                    xml = null;
                }
                XDocument nextX = _engine.ToXDocument(next);
                long started = Stopwatch.GetTimestamp();
                if (toBeMerged[i].Item2 == "")
                    merged.Root!.Add(nextX.Root!.Elements());
                else
                    _engine.MergeElements(merged.Root!, nextX.Root!, toBeMerged[i].Item2);
                counters.MergeTicks += Stopwatch.GetTimestamp() - started;
            }
        }
        return merged != null ? _engine.ToXmlDocument(merged) : xml!;
    }

    /// <summary>
    /// The prefix's call. True with the merged document when the fast path merged; false when the engine's own code
    /// must run (the reason is on <paramref name="call"/>). Logs the fast line, or a once-only stand-aside or failure
    /// line; an engine-path merge's own line comes from <see cref="OnOriginalFinished"/>. Never throws.
    /// </summary>
    public bool TryMergeFast(List<Tuple<string, string>> toBeMerged, List<string> xsltList, bool skipValidation,
                             XmlMergeCall call, out XmlDocument? result)
    {
        result = null;
        try
        {
            Describe(call, toBeMerged, xsltList);
            lock (_gate)
            {
                string? reason = DecideLocked(skipValidation, toBeMerged?.Count ?? 0, out var foreign);
                if (reason != null)
                {
                    call.VanillaReason = reason;
                    if (reason == ReasonForeignPatch)
                        LogStandAsideOnce(foreign);
                    return false;
                }

                var counters = new XmlMergeCounters();
                long compilesBefore = _xslt.Compiles;
                long hitsBefore = _xslt.CacheHits;
                XmlDocument merged;
                try
                {
                    merged = MergeFast(toBeMerged!, xsltList, skipValidation, counters);
                }
                catch (Exception ex)
                {
                    NoteFastFailure(call, ex);
                    return false;
                }
                counters.XsltCompiles = _xslt.Compiles - compilesBefore;
                counters.XsltCacheHits = _xslt.CacheHits - hitsBefore;

                double ms = ElapsedMs(call.StartTimestamp);
                _logger.LogInfo(XmlMergeLines.Fast(call.Type, call.Files, call.Xslts, ms, TicksToMs(counters.LoadTicks),
                    TicksToMs(counters.XsltTicks), TicksToMs(counters.MergeTicks), counters.XsltCompiles,
                    counters.XsltCacheHits));
                // Counted only once the line is out: a logger that throws sends this merge to the engine, which the
                // finalizer then counts as an engine-path merge.
                _window.AddFast(call, ms, counters);
                call.Handled = true;
                result = merged;
                return true;
            }
        }
        catch (Exception ex)
        {
            // Describe, the decision or a log line threw: the engine's own merge runs, and the finalizer settles it.
            result = null;
            call.Handled = false;
            try
            {
                lock (_gate)
                    NoteFastFailure(call, ex);
            }
            catch
            {
                // A failing logger must not stop the engine's merge.
            }
            return false;
        }
    }

    /// <summary>
    /// The finalizer's call, after the engine's own merge ran (or threw). Logs the engine-path line, and when the fast
    /// path threw on this call while the engine's merge succeeded, turns the fast path off for the session.
    /// </summary>
    public void OnOriginalFinished(XmlMergeCall call, Exception? exception)
    {
        if (call == null || call.Handled)
            return;

        lock (_gate)
        {
            double ms = ElapsedMs(call.StartTimestamp);
            _window.AddVanilla(call, ms);
            string result = exception == null ? "ok" : exception.GetType().Name;
            _logger.LogInfo(XmlMergeLines.Vanilla(call.Type, call.Files, call.Xslts, ms, call.VanillaReason, result));

            if (call.FastErrorType != null && exception == null && !_disabled)
            {
                _disabled = true;
                _logger.LogWarning(XmlMergeLines.Disabled(call.Type, call.FastErrorType));
            }
        }
    }

    /// <summary>The module's start line: ready, or off with the binding that did not resolve.</summary>
    public void LogConfigurationHeader()
    {
        string? problem = _engine.BindingProblem;
        if (problem == null)
            _logger.LogInfo(XmlMergeLines.Ready());
        else
            _logger.LogWarning(XmlMergeLines.Off(problem));
    }

    /// <summary>Logs the merges since the previous summary (or process start), then starts a new window.</summary>
    public void LogWindowSummary(string? gameType)
    {
        lock (_gate)
        {
            var w = _window;
            _window = new Window();
            string state = _engine.BindingProblem != null ? "off" : _disabled ? "disabled" : "on";
            _logger.LogInfo(XmlMergeLines.Summary(gameType ?? "none", w.Fast + w.Vanilla, w.Fast, w.Vanilla, w.Files,
                w.TotalMs, w.MaxMs, w.MaxType ?? "none", w.XsltCompiles, w.XsltCacheHits, state));
        }
    }

    /// <summary>The type id: the file name, without extension, of the first non-empty XSD path; else "unknown".</summary>
    internal static string TypeOf(IReadOnlyList<Tuple<string, string>> toBeMerged)
    {
        foreach (var entry in toBeMerged)
        {
            string? xsd = entry?.Item2;
            if (string.IsNullOrEmpty(xsd))
                continue;
            try
            {
                string name = Path.GetFileNameWithoutExtension(xsd);
                return string.IsNullOrEmpty(name) ? UnknownType : name;
            }
            catch (ArgumentException)
            {
                return UnknownType;
            }
        }
        return UnknownType;
    }

    private string? DecideLocked(bool skipValidation, int entryCount, out IReadOnlyList<string> foreign)
    {
        foreign = Array.Empty<string>();
        if (_disabled)
            return ReasonDisabled;
        if (_engine.BindingProblem != null)
            return ReasonBindings;
        if (entryCount == 0)
            return ReasonEmptyList;
        if (skipValidation)
            return ReasonSkipValidation;
        foreign = _engine.ForeignPatches();
        return foreign.Count > 0 ? ReasonForeignPatch : null;
    }

    private XmlDocument Load(string path, string xsdPath, bool skipValidation, XmlMergeCounters counters)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            return _engine.CreateDocumentFromXmlFile(path, xsdPath, skipValidation);
        }
        finally
        {
            counters.LoadTicks += Stopwatch.GetTimestamp() - started;
        }
    }

    private static void Describe(XmlMergeCall call, List<Tuple<string, string>>? toBeMerged, List<string>? xsltList)
    {
        if (toBeMerged == null)
            return;
        call.Type = TypeOf(toBeMerged);
        call.Files = toBeMerged.Count(e => e != null && e.Item1 != "");
        int xslts = 0;
        if (xsltList != null)
        {
            for (int i = 1; i < xsltList.Count && i < toBeMerged.Count; i++)
            {
                if (xsltList[i] != "")
                    xslts++;
            }
        }
        call.Xslts = xslts;
    }

    private void LogStandAsideOnce(IReadOnlyList<string> foreign)
    {
        // The line is the key: it lists the sorted patch set, so one distinct set logs once.
        string line = XmlMergeLines.StandAside(foreign);
        if (_standAsideLogged.Add(line))
            _logger.LogInfo(line);
    }

    private void NoteFastFailure(XmlMergeCall call, Exception ex)
    {
        call.VanillaReason = ReasonFastPathError;
        call.FastErrorType = ex.GetType().Name;
        if (_failureTypesLogged.Add(ex.GetType().FullName ?? ex.GetType().Name))
            _logger.LogWarning(XmlMergeLines.FastFailed(call.Type, ex.GetType().Name, OneLine(ex.Message)));
        // Not once per type like the WARNING: every fallback gets its own full text and stack, because two failures of
        // one exception type can come from different files and different frames.
        _logger.LogDebug(XmlMergeLines.FastFailedDetail(call.Type, ex.ToString()));
    }

    private static string OneLine(string message) => message.Replace("\r", " ").Replace("\n", " ");

    private static double ElapsedMs(long startTimestamp) => TicksToMs(Stopwatch.GetTimestamp() - startTimestamp);

    private static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    /// <summary>The merges one summary line covers.</summary>
    private sealed class Window
    {
        public int Fast;
        public int Vanilla;
        public int Files;
        public double TotalMs;
        public double MaxMs;
        public string? MaxType;
        public long XsltCompiles;
        public long XsltCacheHits;

        public void AddFast(XmlMergeCall call, double ms, XmlMergeCounters counters)
        {
            Fast++;
            Add(call, ms);
            XsltCompiles += counters.XsltCompiles;
            XsltCacheHits += counters.XsltCacheHits;
        }

        public void AddVanilla(XmlMergeCall call, double ms)
        {
            Vanilla++;
            Add(call, ms);
        }

        private void Add(XmlMergeCall call, double ms)
        {
            Files += call.Files;
            TotalMs += ms;
            if (MaxType == null || ms > MaxMs)
            {
                MaxMs = ms;
                MaxType = call.Type;
            }
        }
    }
}

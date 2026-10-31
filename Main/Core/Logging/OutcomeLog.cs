using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using TAOM.Core.Diagnostics;
using TAOM.Dependencies.Foundation;

namespace TAOM.Core.Logging;

/// <summary>The <see cref="IOutcomeLog"/> over <see cref="IModLogger"/> (#1071). The thread rules are on the interface.</summary>
public sealed class OutcomeLog : IOutcomeLog
{
    // The bound LogRepeatLimiter uses: keys and sites built from data cannot grow the process without limit.
    internal const int MaxKeysPerId = 256;
    internal const int MaxSwallowedSites = 256;
    private const string ExitReason = "exit";

    // The design regex, with \z for $ so a trailing newline cannot pass.
    private static readonly Regex IdPattern = new(@"^[a-z0-9]+(\.[a-z0-9-]+)+\z", RegexOptions.CultureInvariant);

    private readonly IModLogger _logger;
    private readonly IStampClock _clock;
    private readonly Func<long> _shieldSwallowed;
    private readonly long _start;
    private readonly ConcurrentDictionary<string, CheckLatch> _checks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, OutcomeCounter> _counters = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SwallowedSite> _sites = new(StringComparer.Ordinal);
    private long _pass;
    private long _fail;
    private long _skip;
    private long _swallowed;
    private long _errors;
    private long _dropped;
    private int _siteCount;
    private int _exitWritten;

    // PatchShield is in TAOM.Dependencies, which Main references; without this its count reaches only diag.log.
    public OutcomeLog(IModLogger logger) : this(logger, StopwatchStampClock.Instance, () => PatchShield.SwallowedTotal) { }

    internal OutcomeLog(IModLogger logger, IStampClock clock, Func<long> shieldSwallowed)
    {
        _logger = logger;
        _clock = clock;
        _shieldSwallowed = shieldSwallowed;
        try { _start = clock.Now; }
        catch { _errors++; }
    }

    public void Pass(string id, string key, string detail) => Record(id, key, detail, "pass", 0, ref _pass);

    public void Fail(string id, string key, string detail) => Record(id, key, detail, "fail", 1, ref _fail);

    public void Skip(string id, string key, string detail) => Record(id, key, detail, "skip", 2, ref _skip);

    public OutcomeCounter Counter(string id)
    {
        try
        {
            if (id != null && _counters.TryGetValue(id, out var known))
                return known;
            if (!IsValidId(id))
            {
                Interlocked.Increment(ref _errors);
                return new OutcomeCounter(id ?? "");
            }
            return _counters.GetOrAdd(id!, i => new OutcomeCounter(i));
        }
        catch
        {
            Interlocked.Increment(ref _errors);
            return new OutcomeCounter(id ?? "");
        }
    }

    public void Swallowed(string feature, string site, Exception ex)
    {
        try
        {
            Interlocked.Increment(ref _swallowed);
            var name = Token(feature) + " " + Token(site); // a token has no space, so the pair cannot collide
            if (_sites.TryGetValue(name, out var known))
            {
                known.Hit();
                return;
            }
            if (Volatile.Read(ref _siteCount) >= MaxSwallowedSites)
                return;
            var fresh = new SwallowedSite(Token(feature), Token(site), ex?.GetType().Name ?? "(none)");
            var entry = _sites.GetOrAdd(name, fresh);
            entry.Hit();
            if (!ReferenceEquals(entry, fresh))
                return;
            Interlocked.Increment(ref _siteCount);
            _logger.LogWarning($"[Swallowed] feature={fresh.Feature} site={fresh.Site} first={fresh.FirstType}. " +
                "This line is logged once per site and the [Session] summary counts the repeats. Root cause: " +
                (ex?.ToString() ?? "(no exception)"));
        }
        catch
        {
            Interlocked.Increment(ref _errors);
        }
    }

    public void WriteSummary(string reason)
    {
        try
        {
            var why = Token(reason);
            if (why == ExitReason && Interlocked.Exchange(ref _exitWritten, 1) == 1)
                return;

            _logger.LogInfo(new StringBuilder("[Session] summary reason=").Append(why)
                .Append(" uptime_s=").Append(UptimeSeconds())
                .Append(" checks_pass=").Append(N(Interlocked.Read(ref _pass)))
                .Append(" checks_fail=").Append(N(Interlocked.Read(ref _fail)))
                .Append(" swallowed=").Append(N(Interlocked.Read(ref _swallowed)))
                .Append(" shield_swallowed=").Append(ShieldSwallowed())
                .Append(" checks_skip=").Append(N(Interlocked.Read(ref _skip)))
                .Append(" errors=").Append(N(Interlocked.Read(ref _errors)))
                .Append(" dropped=").Append(N(Interlocked.Read(ref _dropped)))
                .ToString());

            foreach (var counter in _counters.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
                _logger.LogInfo($"[Session] counter id={counter.Id} pass={N(counter.Passes)} fail={N(counter.Fails)}");

            foreach (var site in _sites.Values.OrderBy(s => s.Feature, StringComparer.Ordinal).ThenBy(s => s.Site, StringComparer.Ordinal))
                _logger.LogInfo($"[Session] swallowed feature={site.Feature} site={site.Site} n={N(site.Count)} first={site.FirstType}");
        }
        catch
        {
            Interlocked.Increment(ref _errors);
        }
    }

    internal static bool IsValidId(string? id) => id != null && IdPattern.IsMatch(id);

    // A counter the facade handed out before this instance existed: the same object joins, so the static field
    // holding it still reaches the summary.
    internal void Adopt(OutcomeCounter counter)
    {
        if (IsValidId(counter.Id))
            _counters.TryAdd(counter.Id, counter);
        else
            Interlocked.Increment(ref _errors);
    }

    internal void AddDropped(long calls) => Interlocked.Add(ref _dropped, calls);

    // "name=value ..." with the whitespace inside a value turned to "_": a word without '=' continues the value before it.
    internal static string SanitiseDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            return "";
        var text = new StringBuilder(detail!.Length);
        foreach (var word in detail.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (text.Length > 0)
                text.Append(word.IndexOf('=') > 0 ? ' ' : '_');
            text.Append(word);
        }
        return text.ToString();
    }

    private void Record(string id, string key, string detail, string result, int slot, ref long total)
    {
        try
        {
            if (!TryGetLatch(id, out var latch))
            {
                Interlocked.Increment(ref _errors);
                return;
            }
            Interlocked.Increment(ref total);
            // Each result has its own keys and cap: a fail after a pass on one key, or after 256 pass keys,
            // still writes its line.
            if (!latch.FirstSight(slot, key ?? ""))
                return;
            var tail = SanitiseDetail(detail);
            _logger.LogInfo("[Check] id=" + id + " result=" + result + (tail.Length == 0 ? "" : " " + tail));
        }
        catch
        {
            Interlocked.Increment(ref _errors);
        }
    }

    private bool TryGetLatch(string id, out CheckLatch latch)
    {
        if (id != null && _checks.TryGetValue(id, out latch!))
            return true;
        latch = null!;
        if (!IsValidId(id))
            return false;
        latch = _checks.GetOrAdd(id, _ => new CheckLatch());
        return true;
    }

    private string UptimeSeconds()
    {
        try
        {
            var frequency = _clock.Frequency;
            return frequency > 0 ? N((_clock.Now - _start) / frequency) : "n/a";
        }
        catch
        {
            return "n/a";
        }
    }

    private string ShieldSwallowed()
    {
        try { return N(_shieldSwallowed()); }
        catch { return "n/a"; }
    }

    private static string N(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Token(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(none)";
        var chars = value!.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]))
                chars[i] = '_';
        }
        return new string(chars);
    }

    private sealed class CheckLatch
    {
        private readonly ConcurrentDictionary<string, byte>[] _keys =
        {
            new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal),
        };
        private readonly int[] _claimed = new int[3];

        // True once per (result, key), and only for the first MaxKeysPerId keys of each result, so pass keys
        // cannot use up the fail budget. A slot is claimed before the add, so two threads racing on one new key
        // can spend two slots; the cap stays a ceiling.
        internal bool FirstSight(int result, string key)
        {
            var keys = _keys[result];
            if (keys.ContainsKey(key) || Volatile.Read(ref _claimed[result]) >= MaxKeysPerId)
                return false;
            if (Interlocked.Increment(ref _claimed[result]) > MaxKeysPerId)
                return false;
            return keys.TryAdd(key, 0);
        }
    }

    private sealed class SwallowedSite
    {
        private long _count;

        internal SwallowedSite(string feature, string site, string firstType)
        {
            Feature = feature;
            Site = site;
            FirstType = firstType;
        }

        internal string Feature { get; }
        internal string Site { get; }
        internal string FirstType { get; }
        internal long Count => Interlocked.Read(ref _count);

        internal void Hit() => Interlocked.Increment(ref _count);
    }
}

/// <summary>A hot-path counter (#1071): one Interlocked increment per call, no string, no I/O.</summary>
public sealed class OutcomeCounter
{
    private long _pass;
    private long _fail;

    internal OutcomeCounter(string id) => Id = id;

    public string Id { get; }
    public long Passes => Interlocked.Read(ref _pass);
    public long Fails => Interlocked.Read(ref _fail);

    public void Pass() => Interlocked.Increment(ref _pass);

    public void Fail() => Interlocked.Increment(ref _fail);
}

/// <summary>
/// The one <see cref="IOutcomeLog"/>, for static Harmony patches and code that runs before IoC (#1071).
/// SubModule.OnSubModuleLoad sets it right after IoC.Configure(). A call made before then is counted and dropped
/// (the summary's <c>dropped=</c>), and a counter taken before then keeps counting and joins the instance.
/// No member throws.
/// </summary>
public static class Outcomes
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<string, OutcomeCounter> _earlyCounters = new(StringComparer.Ordinal);
    private static volatile IOutcomeLog? _log;
    private static volatile SessionHeartbeat? _heartbeat;
    private static long _droppedBeforeInit;
    private static int _processExitHooked;

    public static void Initialize(IOutcomeLog log)
    {
        try
        {
            _log = log;
            if (log is OutcomeLog concrete)
            {
                foreach (var counter in _earlyCounters.Values)
                    concrete.Adopt(counter);
                concrete.AddDropped(Interlocked.Exchange(ref _droppedBeforeInit, 0));
            }
            _heartbeat = new SessionHeartbeat(log, StopwatchStampClock.Instance, HeartbeatInterval);
        }
        catch
        {
            // outcome logging must never stop the mod loading
        }
    }

    // The exit summary for a process that ends without a module unload, as PatchShield's own summary is hooked
    // (Dependencies/SubModule.cs). Subscribes once however often it is called; the instance writes one exit summary.
    public static void SummariseOnProcessExit()
    {
        try
        {
            if (Interlocked.Exchange(ref _processExitHooked, 1) == 0)
                AppDomain.CurrentDomain.ProcessExit += (_, _) => WriteSummary("exit");
        }
        catch
        {
            // no exit summary; the heartbeat still covers the session
        }
    }

    public static void Pass(string id, string key, string detail)
    {
        var log = _log;
        if (log == null) { Drop(); return; }
        try { log.Pass(id, key, detail); }
        catch { /* throw-safe by contract */ }
    }

    public static void Fail(string id, string key, string detail)
    {
        var log = _log;
        if (log == null) { Drop(); return; }
        try { log.Fail(id, key, detail); }
        catch { /* throw-safe by contract */ }
    }

    public static void Skip(string id, string key, string detail)
    {
        var log = _log;
        if (log == null) { Drop(); return; }
        try { log.Skip(id, key, detail); }
        catch { /* throw-safe by contract */ }
    }

    public static OutcomeCounter Counter(string id)
    {
        try
        {
            var log = _log;
            if (log == null)
                return _earlyCounters.GetOrAdd(id ?? "", i => new OutcomeCounter(i));
            return log.Counter(id) ?? new OutcomeCounter(id ?? "");
        }
        catch
        {
            return new OutcomeCounter(id ?? ""); // a static field initialiser must never throw
        }
    }

    public static void Swallowed(string feature, string site, Exception ex)
    {
        var log = _log;
        if (log == null) { Drop(); return; }
        try { log.Swallowed(feature, site, ex); }
        catch { /* throw-safe by contract */ }
    }

    public static void WriteSummary(string reason)
    {
        var log = _log;
        if (log == null) { Drop(); return; }
        try { log.WriteSummary(reason); }
        catch { /* throw-safe by contract */ }
    }

    // From OnApplicationTick: one clock read and one compare per tick.
    public static void Tick()
    {
        try { _heartbeat?.Tick(); }
        catch { /* throw-safe by contract */ }
    }

    private static void Drop() => Interlocked.Increment(ref _droppedBeforeInit);
}

/// <summary>
/// Writes a heartbeat summary each interval of real time (#1071). It covers a debugger stop or a native access
/// violation, where neither the exit nor the crash summary runs, so the last summary is at most one interval older
/// than the last application tick, and the first one comes one interval (10 minutes) after the module loads. The
/// heartbeat writes nothing during a main-thread hang; a battle-load stall the watchdog reports writes a crash
/// summary instead. A long stall writes one summary, not one per missed interval. Main thread only.
/// </summary>
internal sealed class SessionHeartbeat
{
    private readonly IOutcomeLog _log;
    private readonly IStampClock _clock;
    private readonly long _interval;
    private long _due;

    internal SessionHeartbeat(IOutcomeLog log, IStampClock clock, TimeSpan interval)
    {
        _log = log;
        _clock = clock;
        _interval = (long)(interval.TotalSeconds * clock.Frequency);
        _due = clock.Now + _interval;
    }

    internal void Tick()
    {
        try
        {
            var now = _clock.Now;
            if (now < _due)
                return;
            _due = now + _interval;
            _log.WriteSummary("heartbeat");
        }
        catch
        {
            // a heartbeat that fails costs that one summary
        }
    }
}

using System;

namespace TAOM.Core.Logging;

/// <summary>
/// The lines the in-game verdict reader (#1069) reads: a <c>[Check]</c> line that says a feature did what it was
/// built to do, hot-path counters, a count of swallowed exceptions, and the <c>[Session]</c> summary that reports
/// them. Services take it by constructor; static Harmony patches and code that runs before IoC use the
/// <see cref="Outcomes"/> facade. No member throws, also when the logger does.
///
/// Threads: the counters and the once-per-key latch are lock-free, so the async AI tick, TWParallel workers and
/// native agent callbacks may call them. A line goes through <see cref="IModLogger"/> at INFO or WARNING, which
/// <see cref="FileLogger"/> flushes to disk synchronously on the calling thread under its write lock, so off-thread
/// callers (.claude/rules/csharp-architecture.md, "Mission-scope agent handles and the engine's threads") use
/// <see cref="Counter"/> and <see cref="Swallowed"/> only, and Pass, Fail and Skip run on the main thread
/// (OnMissionTick, or DeferredCallbackQueue.RunOrDefer).
/// </summary>
public interface IOutcomeLog
{
    // Writes "[Check] id=<id> result=pass <detail>" at INFO the first time (id, result, key) is seen in the
    // process, so a fail after a pass on one key still writes its line; later calls only count, as do calls past
    // 256 keys of one result for one id. Each result has its own 256, so pass keys never hide the first fail
    // line. key is the scope: "" for once per process, a mission serial for once per mission. detail is
    // "name=value ...": whitespace inside a value becomes "_", and numbers in it are formatted with the
    // invariant culture by the caller. An id that breaks ^[a-z0-9]+(\.[a-z0-9-]+)+$ is counted
    // as an error and writes nothing.
    void Pass(string id, string key, string detail);
    void Fail(string id, string key, string detail);
    void Skip(string id, string key, string detail);

    // Hot path, any thread: one Interlocked increment, no string, no I/O. Hold it in a static readonly field;
    // WriteSummary reports it.
    OutcomeCounter Counter(string id);

    // Replaces an empty catch: one WARNING line per (feature, site) for the process, then a count.
    void Swallowed(string feature, string site, Exception ex);

    // "[Session] summary reason=<reason> ...", then one line per counter and one per swallowed site. An exit
    // summary is written once, so module unload and process exit cannot both write one.
    void WriteSummary(string reason);
}

using System.Globalization;
using TAOM.Core.Logging;

namespace TAOM.Features.Stamina;

/// <summary>
/// Stamina's in-game check lines (#1497, docs/features/ingame-verdicts.md), read by tools/ingame_verdict.py through
/// tools/ingame_checks/1497.toml. Each takes plain counts, so every pass, fail and skip branch has a unit test. The
/// key is the mission's serial, so each battle writes its own lines.
/// </summary>
public static class StaminaChecks
{
    public const string ArmedId = "taom.stamina.armed";
    public const string LockId = "taom.stamina.lock";
    public const string RefreshId = "taom.stamina.refresh";

    /// <summary>A stat recompute batch slower than this fails the refresh check: it runs on the main thread.</summary>
    public const double SlowBatchMs = 1.0;

    /// <summary>At a mission's first tick: whether this mission runs the stamina rules, and why not when it does not.</summary>
    public static void Armed(IOutcomeLog log, string key, bool enabled, bool armed)
    {
        if (armed)
            log.Pass(ArmedId, key, "armed=true");
        else
            log.Skip(ArmedId, key, enabled ? "reason=gate-closed" : "reason=toggle-off");
    }

    /// <summary>
    /// At a mission's end. The lock passes when at least one Exhausted soldier recovered and none stayed locked past
    /// the time a recovery can take; a stuck soldier fails it, because the rules must never trap one.
    /// </summary>
    public static void Lock(IOutcomeLog log, string key, long exhaustions, long recoveries, long stuck)
    {
        var detail = string.Format(CultureInfo.InvariantCulture, "entered={0} recovered={1} stuck={2}", exhaustions, recoveries, stuck);
        if (stuck > 0)
            log.Fail(LockId, key, detail);
        else if (exhaustions == 0)
            log.Skip(LockId, key, "reason=none-exhausted " + detail);
        else if (recoveries == 0)
            log.Skip(LockId, key, "reason=none-recovered " + detail);
        else
            log.Pass(LockId, key, detail);
    }

    /// <summary>At a mission's end: the slowest batch of stat recomputes in one frame.</summary>
    public static void Refresh(IOutcomeLog log, string key, long refreshes, double slowestBatchMs)
    {
        var detail = string.Format(CultureInfo.InvariantCulture, "refreshes={0} slowest_batch_ms={1:0.000}", refreshes, slowestBatchMs);
        if (refreshes == 0)
            log.Skip(RefreshId, key, "reason=no-refresh " + detail);
        // A positive requirement: a time that is not a number fails.
        else if (!(slowestBatchMs <= SlowBatchMs))
            log.Fail(RefreshId, key, detail);
        else
            log.Pass(RefreshId, key, detail);
    }
}

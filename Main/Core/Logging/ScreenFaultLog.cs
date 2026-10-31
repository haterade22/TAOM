using System;
using System.Collections.Generic;

namespace TAOM.Core.Logging;

/// <summary>
/// The guard around every member the engine calls into TAOM UI or campaign code: a mixin's constructor, refresh, click and hover,
/// a popup callback, or a dialogue or game-menu condition or consequence a campaign behavior registers; one log line per call site
/// for the life of its owner (a screen, or a behavior built per campaign). A call site that fails once usually fails every time, so
/// the latch keeps the file to one line per site; a different site still logs. A guarded hover function returns an empty result,
/// because a throw escapes the engine's hover and command dispatch uncaught; <c>InvokeWithLog</c> prints it to the engine log and
/// rethrows. Every throw also goes to <see cref="Outcomes.Swallowed"/>, which writes one WARNING per site for the process and
/// counts the repeats in the [Session] summary (#1071).
/// </summary>
internal sealed class ScreenFaultLog
{
    private readonly IModLogger _logger;
    private readonly string _feature;
    private readonly HashSet<string> _logged = new(StringComparer.Ordinal);

    /// <param name="feature">The log tag, such as <c>ReevesLedger</c>.</param>
    internal ScreenFaultLog(IModLogger logger, string feature)
    {
        _logger = logger;
        _feature = feature;
    }

    /// <summary>Runs <paramref name="action"/>; a throw is logged once for <paramref name="site"/> and swallowed.</summary>
    internal void Run(string site, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
        }
    }

    internal List<T> Lines<T>(string site, Func<List<T>> build)
    {
        try
        {
            return build();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
            return new List<T>();
        }
    }

    internal string Text(string site, Func<string> build)
    {
        try
        {
            return build();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
            return "";
        }
    }

    /// <summary>Runs a condition; a throw is logged once for <paramref name="site"/> and the condition is false.</summary>
    internal bool Condition(string site, Func<bool> condition)
    {
        try
        {
            return condition();
        }
        catch (Exception ex)
        {
            LogOnce(site, ex);
            return false;
        }
    }

    internal void LogOnce(string site, Exception ex)
    {
        Outcomes.Swallowed(_feature, site, ex); // every throw, so the session summary counts the repeats (#1071)
        if (!_logged.Add(site))
            return;
        _logger.LogError($"[{_feature}] {site} threw. This line is logged once and later calls still run. Root cause: {ex}");
    }

    internal void LogOnce(string site, string problem)
    {
        if (!_logged.Add(site))
            return;
        _logger.LogError($"[{_feature}] {site}: {problem}");
    }
}

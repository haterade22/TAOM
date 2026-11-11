using System;
using TAOM.Core.Logging;

namespace TAOM.Features.StoryMissions;

/// <summary>
/// The outcome log the hosted Long Defeat run writes to in a story mission (#1105). The run's director writes <c>taom.long-defeat.*</c> lines, which
/// tools/ingame_checks/1079.toml reads as that issue's evidence, and a story mission has no HUD, so rows such as orders and hud would read as fails.
/// This moves them to <c>taom.story-missions.hosted.*</c>. The counter, swallowed and summary calls pass through unchanged.
/// </summary>
internal sealed class HostedRunOutcomeLog : IOutcomeLog
{
    /// <summary>Where the run's boot check lands (<c>taom.long-defeat.boot</c> renamed); tools/ingame_checks/1105.toml reads it.</summary>
    internal const string BootId = "taom.story-missions.hosted.boot";

    private const string From = "taom.long-defeat.";
    private const string To = "taom.story-missions.hosted.";

    private readonly IOutcomeLog _inner;

    public HostedRunOutcomeLog(IOutcomeLog inner) => _inner = inner;

    public void Pass(string id, string key, string detail) => _inner.Pass(Rename(id), key, detail);

    public void Fail(string id, string key, string detail) => _inner.Fail(Rename(id), key, detail);

    public void Skip(string id, string key, string detail) => _inner.Skip(Rename(id), key, detail);

    public OutcomeCounter Counter(string id) => _inner.Counter(id);

    public void Swallowed(string feature, string site, Exception ex) => _inner.Swallowed(feature, site, ex);

    public void WriteSummary(string reason) => _inner.WriteSummary(reason);

    private static string Rename(string id) => id.StartsWith(From, StringComparison.Ordinal) ? To + id.Substring(From.Length) : id;
}

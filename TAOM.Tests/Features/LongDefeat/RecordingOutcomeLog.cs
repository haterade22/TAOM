using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>An <see cref="IOutcomeLog"/> that keeps each verdict as "result id key detail", in order.</summary>
internal sealed class RecordingOutcomeLog : IOutcomeLog
{
    public List<string> Lines { get; } = new();

    public void Pass(string id, string key, string detail) => Lines.Add($"pass {id} {key} {detail}");

    public void Fail(string id, string key, string detail) => Lines.Add($"fail {id} {key} {detail}");

    public void Skip(string id, string key, string detail) => Lines.Add($"skip {id} {key} {detail}");

    public OutcomeCounter Counter(string id) => new(id);

    public void Swallowed(string feature, string site, Exception ex)
    {
    }

    public void WriteSummary(string reason)
    {
    }

    /// <summary>The verdicts for one check id, each as "result key detail".</summary>
    public List<string> Of(string id) =>
        Lines.Where(l => l.Split(' ')[1] == id).Select(l => l.Substring(l.IndexOf(' ') + 1 + id.Length + 1)).ToList();

    public bool Has(string result, string id) => Lines.Any(l => l.StartsWith(result + " " + id + " ", StringComparison.Ordinal));
}

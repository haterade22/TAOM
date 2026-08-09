using Newtonsoft.Json;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Features.AutoResolveDiagnostics;

/// <summary>
/// Turns a captured battle into one JSON Lines record, tagged for the shared TAOM log.
///
/// Pure and static on purpose — capture needs a live MapEvent and cannot be unit-tested, so every
/// decision that CAN be tested lives here instead. The emitted field names are a contract with
/// tools/analyze_battle_logs.py; changing one silently produces a log that looks healthy and
/// analyses to nothing, which is why they are pinned by tests.
/// </summary>
public static class AutoResolveLogFormatter
{
    /// <summary>Grep tag. Must contain no '{' — the analyzer strips to the first brace.</summary>
    public const string Tag = "[AutoResolve] ";

    /// <summary>Separate tag for the once-per-session troop census, so the analyzer can pick the
    /// two record kinds apart without inspecting their contents.</summary>
    public const string CensusTag = "[AutoResolveCensus] ";

    private static readonly JsonSerializerSettings Settings = new()
    {
        // Formatting.None is load-bearing, not cosmetic: JSON Lines requires one record per line,
        // and indented output would split every battle across dozens of unparseable fragments.
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Include,
        // Newtonsoft's default (FloatFormatHandling.Symbol) writes a non-finite float as a BARE
        // NaN / Infinity token. That is not valid JSON — but Python's json.loads accepts it anyway
        // (parse_constant returns float('nan')), so a poisoned value would sail through the
        // analyzer's malformed-line counter and silently contaminate every mean, median and
        // comparison it touched. Failing loud is not available here; writing null is, and the
        // analyzer already treats a missing float as missing rather than as zero.
        FloatFormatHandling = FloatFormatHandling.DefaultValue,
    };

    /// <summary>
    /// Renders one record. Returns empty on null or on any serialization failure — a diagnostic
    /// must never propagate an exception into the campaign tick.
    /// </summary>
    public static string Format(BattleLogRecord record)
    {
        if (record == null)
            return string.Empty;

        try
        {
            var json = JsonConvert.SerializeObject(record, Settings);

            // Defence in depth. A newline here would corrupt the whole file rather than one line,
            // and troop ids are authored data we do not control.
            if (json.IndexOf('\n') >= 0 || json.IndexOf('\r') >= 0)
                json = json.Replace("\r", string.Empty).Replace("\n", string.Empty);

            return Tag + json;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Renders one census record. Same failure contract as <see cref="Format"/>.</summary>
    public static string FormatCensus(TroopCensusRecord record)
    {
        if (record == null)
            return string.Empty;

        try
        {
            var json = JsonConvert.SerializeObject(record, Settings);
            if (json.IndexOf('\n') >= 0 || json.IndexOf('\r') >= 0)
                json = json.Replace("\r", string.Empty).Replace("\n", string.Empty);
            return CensusTag + json;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Recovers the JSON payload from a log line, tolerating the shared FileLogger's
    /// "[timestamp] [LEVEL] " prefix. Returns empty when the line carries no payload.
    /// </summary>
    public static string ExtractPayload(string line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        int brace = line.IndexOf('{');
        return brace < 0 ? string.Empty : line.Substring(brace);
    }
}

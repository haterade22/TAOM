using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Features.AutoResolveDiagnostics;

/// <inheritdoc cref="IAutoResolveLogWriter"/>
public class AutoResolveLogWriter : IAutoResolveLogWriter
{
    private readonly ITroopCensusAdapter _census;
    private readonly IAutoResolveDiagnosticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    private int _sequence;
    private bool _censusWritten;

    public AutoResolveLogWriter(
        ITroopCensusAdapter census,
        IAutoResolveDiagnosticsSettingsProvider settings,
        IModLogger logger)
    {
        _census = census;
        _settings = settings;
        _logger = logger;
    }

    public void BeginSession()
    {
        _sequence = 0;
        // The bug this line fixes: _censusWritten used to survive the session boundary, so the
        // census ran once per PROCESS rather than once per session. A player who returned to the
        // main menu and started a second campaign got a log with no engine ground truth at all,
        // silently — and the census is what validates every tier and power figure the offline
        // analysis rests on.
        _censusWritten = false;
    }

    /// <summary>The census is subordinate to the master switch — off means off regardless.</summary>
    internal static bool ShouldWriteCensus(bool isEnabled, bool isCensusEnabled) =>
        isEnabled && isCensusEnabled;

    public void WriteCensus()
    {
        try
        {
            // Master switch first, then the census-specific one. Both must be on.
            if (_censusWritten || !ShouldWriteCensus(_settings.IsEnabled, _settings.IsCensusEnabled))
                return;

            int written = 0;
            foreach (var record in _census.Capture())
            {
                var line = AutoResolveLogFormatter.FormatCensus(record);
                if (line.Length == 0)
                    continue;
                // LogDebug, deliberately, and the opposite call from Emit's. LogInfo takes the
                // write lock and flushes to the OS on the CALLING thread for every line; the census
                // is ~8,300 lines, so INFO here would pay thousands of synchronous flushes on the
                // session-launch thread. Unlike a battle record, a census line is not crash
                // evidence — it is written before any gameplay, and the summary below stays INFO so
                // there is still a durable record that the census ran.
                _logger.LogDebug(line);
                written++;
            }

            // Latch AFTER the write pass, not before: setting it first meant an exception midway
            // left a partial census and permanently foreclosed the retry.
            _censusWritten = true;
            _logger.LogInfo($"[AutoResolve] troop census complete: {written} troop types");
        }
        catch
        {
            // A diagnostic must never propagate into session launch.
        }
    }

    public string NextRecordId() => FormatRecordId(CurrentDay(), _sequence++);

    /// <summary>Pure half of the id, split out because CampaignTime.Now needs a live campaign.</summary>
    internal static string FormatRecordId(int day, int sequence) => $"{day}.{sequence}";

    /// <summary>Virtual so the id can be exercised without a live campaign.</summary>
    protected virtual int CurrentDay() =>
        (int)TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays;

    public void Emit(BattleLogRecord? record)
    {
        if (record == null)
            return;

        var line = AutoResolveLogFormatter.Format(record);
        if (line.Length == 0)
            return;

        // LogInfo, not LogDebug. DEBUG is drained asynchronously and is lost on a hard native
        // crash; INFO flushes synchronously. For a diagnostic whose last line is the evidence,
        // that difference is the whole point (FileLogger.cs:79-89).
        _logger.LogInfo(line);
    }
}

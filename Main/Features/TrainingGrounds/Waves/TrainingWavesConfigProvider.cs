using System;
using System.IO;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.TrainingGrounds.Waves.Config;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// Loads <c>training_grounds/training_waves.json</c> on first use. A missing file gives the compiled default table with an info
/// line; an unreadable one gives it with an error; a value that breaks its rule is handled by <see cref="WaveTableReader"/>, one
/// warning each. Cached for the process (Reuse.Singleton), so an edit needs a full restart, not a new campaign or a load.
/// </summary>
internal sealed class TrainingWavesConfigProvider : ITrainingWavesConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<WaveTable> _table;

    public TrainingWavesConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _table = new Lazy<WaveTable>(Load);
    }

    public WaveTable GetTable() => _table.Value;

    private WaveTable Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "training_grounds", "training_waves.json");
        if (!File.Exists(path))
        {
            _logger.LogInfo($"TrainingWavesConfigProvider: training_waves.json not found at {path}, using the default table");
            return WaveTable.Default;
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _logger.LogError($"TrainingWavesConfigProvider: could not read training_waves.json: {ex.Message}");
            return WaveTable.Default;
        }

        var table = WaveTableReader.Read(text, _logger.LogWarning);
        _logger.LogInfo($"TrainingWavesConfigProvider: loaded training_waves.json, {table.Waves.Count} wave(s)");
        return table;
    }
}

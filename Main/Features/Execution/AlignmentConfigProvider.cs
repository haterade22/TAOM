using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.Execution;

public class AlignmentConfigProvider : IAlignmentConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public AlignmentConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public Dictionary<string, string> LoadAlignments()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "execution", "alignment.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"AlignmentConfigProvider: alignment.json not found: {path}");
            return new Dictionary<string, string>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var alignments = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            _logger.LogInfo($"Loaded {alignments.Count} kingdom alignments");
            return alignments;
        }
        catch (Exception ex)
        {
            _logger.LogError($"AlignmentConfigProvider: Failed to parse alignment.json: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }
}

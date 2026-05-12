using System;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Logging;

namespace TAOM.Features.EditorCacheRebuild.Validation;

public class ValidationReportWriter : IValidationReportWriter
{
    private readonly IModLogger _logger;

    public ValidationReportWriter(IModLogger logger)
    {
        _logger = logger;
    }

    public void Write(string filePath, ValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.LogDebug("ValidationReportWriter: empty file path, skipping report write");
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText(filePath, json);
            _logger.LogInfo($"ValidationReportWriter: wrote {filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"ValidationReportWriter: failed to write {filePath}: {ex.Message}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.CharacterCreation;

public class CCBodyPropertiesProvider : ICCBodyPropertiesProvider
{
    private const int ExpectedKeyHexLength = 128;

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private Dictionary<string, string> _byCulture;

    public CCBodyPropertiesProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public string GetBodyPropertiesXml(string cultureId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(cultureId))
            return null;
        return _byCulture.TryGetValue(cultureId.ToLowerInvariant(), out var xml) ? xml : null;
    }

    private void EnsureLoaded()
    {
        if (_byCulture != null)
            return;

        _byCulture = new Dictionary<string, string>();
        LoadConfig();
    }

    private void LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "charactercreation", "cc_body_properties.xml");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"CCBodyPropertiesProvider: Config not found: {path}");
            return;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Load(path);
        }
        catch (Exception ex)
        {
            _logger.LogError($"CCBodyPropertiesProvider: Failed to parse {path}: {ex.Message}");
            return;
        }

        var root = doc.Root;
        if (root == null)
        {
            _logger.LogError($"CCBodyPropertiesProvider: Failed to parse {path}: no root element");
            return;
        }

        foreach (var cultureEl in root.Elements("Culture"))
        {
            var rawId = cultureEl.Attribute("id")?.Value;
            if (string.IsNullOrWhiteSpace(rawId))
            {
                _logger.LogWarning("CCBodyPropertiesProvider: <Culture> entry missing id, skipped");
                continue;
            }

            var id = rawId.Trim().ToLowerInvariant();
            var bodyEl = cultureEl.Element("BodyProperties");
            if (bodyEl == null)
            {
                _logger.LogWarning($"CCBodyPropertiesProvider: Culture '{id}' missing <BodyProperties> child, skipped");
                continue;
            }

            var key = bodyEl.Attribute("key")?.Value;
            if (string.IsNullOrEmpty(key))
            {
                _logger.LogWarning($"CCBodyPropertiesProvider: Culture '{id}' BodyProperties has empty/missing key, skipped");
                continue;
            }

            if (key.Length != ExpectedKeyHexLength)
            {
                _logger.LogWarning($"CCBodyPropertiesProvider: Culture '{id}' key length {key.Length} != expected {ExpectedKeyHexLength}, skipped");
                continue;
            }

            if (_byCulture.ContainsKey(id))
                _logger.LogWarning($"CCBodyPropertiesProvider: duplicate culture id '{id}', last entry wins");

            _byCulture[id] = bodyEl.ToString(SaveOptions.DisableFormatting);
        }

        _logger.LogInfo($"CCBodyPropertiesProvider: Loaded {_byCulture.Count} culture body-property entries");
    }
}

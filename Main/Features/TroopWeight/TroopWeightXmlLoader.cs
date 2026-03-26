using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;

namespace TAOM.Features.TroopWeight;

public class TroopWeightXmlLoader : ITroopWeightXmlLoader
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private Dictionary<string, float> _troopWeights;
    private bool _isLoaded;

    public TroopWeightXmlLoader(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public Dictionary<string, float> GetTroopWeights()
    {
        if (!_isLoaded)
            LoadTroopWeights();

        return _troopWeights;
    }

    public void ReloadWeights()
    {
        _isLoaded = false;
        LoadTroopWeights();
    }

    private void LoadTroopWeights()
    {
        var xmlPath = Path.Combine(_pathService.ModuleDataPath, "TroopWeights", "troop_weights.xml");
        var weights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(xmlPath))
        {
            _logger.LogWarning($"Troop weights XML not found at: {xmlPath} — all troops will use default weight 1.0");
            _troopWeights = weights;
            _isLoaded = true;
            return;
        }

        try
        {
            var doc = new XmlDocument();
            doc.Load(xmlPath);

            var weightNodes = doc.SelectNodes("//TroopWeight");
            if (weightNodes == null)
            {
                _troopWeights = weights;
                _isLoaded = true;
                return;
            }

            foreach (XmlNode node in weightNodes)
            {
                var id = node.Attributes?["id"]?.Value;
                var weightStr = node.Attributes?["weight"]?.Value;

                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogWarning("TroopWeight element missing required 'id' attribute — skipping");
                    continue;
                }

                if (!float.TryParse(weightStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
                {
                    _logger.LogWarning($"Invalid weight value '{weightStr}' for troop '{id}' — skipping");
                    continue;
                }

                if (weight <= 0)
                {
                    _logger.LogWarning($"Weight must be positive for troop '{id}' (got {weight}) — skipping");
                    continue;
                }

                if (weights.ContainsKey(id))
                {
                    _logger.LogWarning($"Duplicate troop ID '{id}' — using last value");
                }

                weights[id] = weight;
            }

            _logger.LogInfo($"Loaded {weights.Count} troop weights from XML");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load troop weights: {ex.Message}");
        }

        _troopWeights = weights;
        _isLoaded = true;
    }
}

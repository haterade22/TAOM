using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Core.Domain;

public class RaceManager : IRaceManager
{
    private readonly IModLogger _logger;
    private readonly IFaceGenAdapter _faceGenAdapter;
    private Dictionary<int, string>? _idToName;
    private Dictionary<string, int>? _nameToId;
    private bool _initialized;
    private readonly object _initLock = new();

    private readonly ConcurrentDictionary<int, string> _raceNameCache = new();

    public RaceManager(IModLogger logger, IFaceGenAdapter faceGenAdapter)
    {
        _logger = logger;
        _faceGenAdapter = faceGenAdapter;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_initLock)
        {
            if (_initialized) return;

            (_idToName, _nameToId) = InitializeRaceMappings();
            _initialized = true;
        }
    }

    private (Dictionary<int, string>, Dictionary<string, int>) InitializeRaceMappings()
    {
        var idToName = new Dictionary<int, string>();
        var nameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var raceNames = _faceGenAdapter.GetRaceNames();
            if (raceNames != null)
            {
                _logger.LogInfo($"Initializing RaceManager with {raceNames.Length} races from game data");
                for (var i = 0; i < raceNames.Length; i++)
                {
                    var raceName = raceNames[i];
                    idToName[i] = raceName;
                    nameToId[raceName] = i;
                    _logger?.LogDebug($"  Race ID {i} = '{raceName}'");
                }
            }
            else
            {
                _logger.LogWarning("FaceGen.GetRaceNames() returned null, using fallback mapping");
                idToName[0] = "human";
                nameToId["human"] = 0;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error initializing race mappings: {ex.Message}");
            idToName[0] = "human";
            nameToId["human"] = 0;
        }

        return (idToName, nameToId);
    }

    public List<int> GetAllRaceIds()
    {
        EnsureInitialized();
        return new List<int>(_idToName!.Keys);
    }

    public List<string> GetAllRaceNames()
    {
        EnsureInitialized();
        return new List<string>(_idToName!.Values);
    }

    public bool IsValidRaceName(string name)
    {
        EnsureInitialized();
        return !string.IsNullOrEmpty(name) && _nameToId!.ContainsKey(name);
    }

    public bool IsValidRaceId(int id)
    {
        EnsureInitialized();
        return _idToName!.ContainsKey(id);
    }

    public int GetRaceIdFromName(string name)
    {
        EnsureInitialized();
        if (!_nameToId!.TryGetValue(name, out var id))
        {
            _logger.LogWarning($"Unknown race name '{name}' encountered. Defaulting to ID 0 (human). " +
                              $"Known race names: {string.Join(", ", _nameToId.Keys)}");
            return 0;
        }
        return id;
    }

    public string GetRaceNameFromId(int id)
    {
        if (_raceNameCache.TryGetValue(id, out var cachedName))
        {
            return cachedName;
        }

        EnsureInitialized();

        if (_idToName!.TryGetValue(id, out var name))
        {
            _raceNameCache[id] = name;
            return name;
        }

        _logger.LogWarning($"Unknown race ID {id} encountered. Defaulting to 'human'. " +
                           $"Known race IDs: {string.Join(", ", _idToName.Keys)}");

        var fallback = _idToName.TryGetValue(0, out var value) ? value : "human";
        _raceNameCache[id] = fallback;
        return fallback;
    }
}

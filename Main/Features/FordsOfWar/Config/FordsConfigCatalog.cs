using System;
using System.Collections.Generic;
using System.IO;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Validating loader for <c>fords_of_war/match.json</c>, <c>factions.json</c> and <c>maps.json</c> (docs/features/fords-of-war).
/// Each file is read once per process, on first use of its part, so an edit needs a restart of the game. The load never throws
/// (a faulted <see cref="Lazy{T}"/> rethrows forever): a file that is missing, not JSON or not an object leaves the default
/// match numbers, no factions or no maps, and a bad entry is skipped or reverted with one warning (see the three readers).
/// Ids are matched case-insensitively. Display names stay raw <c>{=KEY}English</c> text; the client resolves them.
/// </summary>
internal sealed class FordsConfigCatalog
{
    private const string FolderName = "fords_of_war";
    private const string MatchFile = "match.json";
    private const string FactionsFile = "factions.json";
    private const string MapsFile = "maps.json";

    private readonly Lazy<FordsMatchConfig> _match;
    private readonly Lazy<FordsFactionSet> _factions;
    private readonly Lazy<IReadOnlyList<MapEntry>> _maps;

    public FordsConfigCatalog(IPathService pathService, IModLogger logger)
        : this(logger, name => ReadFile(pathService, name, logger))
    {
    }

    private FordsConfigCatalog(IModLogger logger, Func<string, string?> read)
    {
        _match = new Lazy<FordsMatchConfig>(() => Guard(logger, MatchFile, FordsMatchConfig.Default,
            s => FordsMatchConfigReader.Read(read(MatchFile), s)));
        _factions = new Lazy<FordsFactionSet>(() => Guard(logger, FactionsFile, FordsFactionSet.Empty,
            s => FordsFactionsReader.Read(read(FactionsFile), s)));
        _maps = new Lazy<IReadOnlyList<MapEntry>>(() => Guard<IReadOnlyList<MapEntry>>(logger, MapsFile, Array.Empty<MapEntry>(),
            s => FordsMapsReader.Read(read(MapsFile), s)));
    }

    /// <summary>For tests: the same validation over text instead of the files. Null text is the missing-file case.</summary>
    internal static FordsConfigCatalog FromTexts(string? match, string? factions, string? maps, IModLogger logger) =>
        new(logger, name => name == MatchFile ? match : name == FactionsFile ? factions : maps);

    public FordsMatchConfig Match => _match.Value;

    /// <summary>Every valid faction, in file order.</summary>
    public IReadOnlyList<FactionCatalog> Factions => _factions.Value.Factions;

    /// <summary>Every valid map, in file order; the first is the default.</summary>
    public IReadOnlyList<MapEntry> Maps => _maps.Value;

    public bool TryGetFaction(string factionId, out FactionCatalog faction) => _factions.Value.TryGet(factionId, out faction);

    /// <summary>The raw <c>{=KEY}English</c> name of a faction; the id itself when the faction is unknown.</summary>
    public string FactionDisplayName(string factionId) => _factions.Value.DisplayName(factionId);

    public bool TryGetMap(string sceneId, out MapEntry map)
    {
        map = null!;
        if (string.IsNullOrEmpty(sceneId))
            return false;

        foreach (var candidate in _maps.Value)
        {
            if (string.Equals(candidate.SceneId, sceneId, StringComparison.OrdinalIgnoreCase))
            {
                map = candidate;
                return true;
            }
        }

        return false;
    }

    private static T Guard<T>(IModLogger logger, string file, T fallback, Func<FordsConfigScope, T> load)
    {
        var scope = new FordsConfigScope(logger, file);
        try
        {
            return load(scope);
        }
        catch (Exception ex)
        {
            scope.Warn($"could not be loaded ({ex.GetType().Name}: {ex.Message}); using the fallback");
            return fallback;
        }
    }

    private static string? ReadFile(IPathService pathService, string name, IModLogger logger)
    {
        var path = Path.Combine(pathService.ModuleDataPath, FolderName, name);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"FordsConfigCatalog: {name} could not be read at {path} ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }
}

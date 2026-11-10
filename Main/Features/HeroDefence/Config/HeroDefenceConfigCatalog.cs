using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroDefence.Domain;

namespace TAOM.Features.HeroDefence;

/// <summary>Validating loader for <c>hero_defence/waves.json</c>, <c>cultures.json</c> and <c>rosters.json</c>, read once per process. The load never throws, because a faulted <see cref="Lazy{T}"/> rethrows forever.</summary>
internal sealed class HeroDefenceConfigCatalog
{
    private const string FolderName = "hero_defence";
    private const string WavesFile = "waves.json";
    private const string CulturesFile = "cultures.json";
    private const string RostersFile = "rosters.json";

    private readonly Lazy<HeroDefenceWavesFile> _wavesFile;
    private readonly Lazy<WaveCatalog> _waves;

    public HeroDefenceConfigCatalog(IPathService pathService, IModLogger logger)
        : this(logger, name => ReadFile(pathService, name, logger))
    {
    }

    private HeroDefenceConfigCatalog(IModLogger logger, Func<string, string?> readFile)
    {
        _wavesFile = new Lazy<HeroDefenceWavesFile>(() => Guard(logger, WavesFile, HeroDefenceWavesFile.Defaults, s => HeroDefenceWavesReader.Read(readFile(WavesFile), s)));
        _waves = new Lazy<WaveCatalog>(() => Compose(logger, readFile));
    }

    /// <summary>For tests: the same validation over text instead of the files. Null text is the missing-file case.</summary>
    internal static HeroDefenceConfigCatalog FromTexts(string? waves, string? cultures, string? rosters, IModLogger logger) =>
        new(logger, name => name switch
        {
            WavesFile => waves,
            CulturesFile => cultures,
            RostersFile => rosters,
            _ => null,
        });

    public DefenceRules Rules => _wavesFile.Value.Rules;

    public WaveCatalog Waves => _waves.Value;

    private WaveCatalog Compose(IModLogger logger, Func<string, string?> readFile)
    {
        var waves = _wavesFile.Value.Catalog;
        var rosters = Guard<IReadOnlyDictionary<string, IReadOnlyList<RosterRow>>>(logger, RostersFile, new Dictionary<string, IReadOnlyList<RosterRow>>(), s => HeroDefenceRosterReader.Read(readFile(RostersFile), s));
        var cultures = Guard(logger, CulturesFile, HeroDefenceCulturesFile.Empty, s => HeroDefenceCulturesReader.Read(readFile(CulturesFile), waves.PowerPool, rosters, s));
        var composed = waves with
        {
            Cultures = cultures.Cultures,
            Signatures = cultures.Signatures,
        };
        return composed with { Opening = KnownOpening(composed, new HeroDefenceConfigScope(logger, ScopeName(WavesFile))) };
    }

    /// <summary>
    /// Drops an opening id that names no culture, that repeats the wave before it (two waves in a row never share a culture), or
    /// whose culture has no infantry at or below the band of the wave it would pin, because that wave would field nobody.
    /// </summary>
    private static IReadOnlyList<string> KnownOpening(WaveCatalog catalog, HeroDefenceConfigScope scope)
    {
        var kept = new List<string>();
        foreach (var id in catalog.Opening)
        {
            var culture = catalog.Cultures.FirstOrDefault(c => c.Id == id);
            if (culture == null)
                scope.Warn($"'opening' names '{id}', which is not a valid culture; dropped");
            else if (kept.Count > 0 && kept[kept.Count - 1] == id)
                scope.Warn($"'opening' names '{id}' twice in a row; dropped the second");
            else if (!catalog.HasEntered(culture, kept.Count + 1))
                scope.Warn($"'opening' names '{id}' for wave {kept.Count + 1}, but it has not entered: no infantry at or below that wave's band; dropped");
            else
                kept.Add(id);
        }

        return kept;
    }

    private static string ScopeName(string file) => FolderName + "/" + file;

    private static T Guard<T>(IModLogger logger, string file, T fallback, Func<HeroDefenceConfigScope, T> load)
    {
        var scope = new HeroDefenceConfigScope(logger, ScopeName(file));
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
            logger.LogWarning($"HeroDefenceConfigCatalog: {name} could not be read at {path} ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }
}

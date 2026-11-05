using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Validating loader for <c>long_defeat/fortresses.json</c>, <c>waves.json</c> and <c>shop.json</c>, read once per process, and the owner of
/// the <c>balance</c> hash over those three and <c>affixes.json</c>, <c>boons.json</c> and <c>classes.json</c> (their readers arrive with their
/// slices). The load never throws, because a faulted <see cref="Lazy{T}"/> rethrows forever.
/// </summary>
internal sealed class LongDefeatConfigCatalog
{
    private const string FolderName = "long_defeat";
    private const string FortressesFile = "fortresses.json";
    private const string WavesFile = "waves.json";
    private const string ShopFile = "shop.json";
    private const string AffixesFile = "affixes.json";
    private const string BoonsFile = "boons.json";
    private const string ClassesFile = "classes.json";

    private readonly Lazy<string> _balance;
    private readonly Lazy<WavesConfig> _waves;
    private readonly Lazy<IReadOnlyDictionary<string, ShopPlan>> _shops;
    private readonly Lazy<IReadOnlyList<FortressEntry>> _fortresses;

    public LongDefeatConfigCatalog(IPathService pathService, IModLogger logger)
        : this(logger, name => ReadFile(pathService, name, logger))
    {
    }

    private LongDefeatConfigCatalog(IModLogger logger, Func<string, string?> readFile)
    {
        var texts = new ConcurrentDictionary<string, string?>();
        string? read(string name) => texts.GetOrAdd(name, readFile);
        _balance = new Lazy<string>(() => HashOf(read(FortressesFile), read(WavesFile), read(ShopFile),
            read(AffixesFile), read(BoonsFile), read(ClassesFile)));
        _waves = new Lazy<WavesConfig>(() => Guard(logger, WavesFile, WavesConfig.Empty,
            s => WavesReader.Read(read(WavesFile), s)));
        _shops = new Lazy<IReadOnlyDictionary<string, ShopPlan>>(() => Guard<IReadOnlyDictionary<string, ShopPlan>>(logger, ShopFile,
            new Dictionary<string, ShopPlan>(), s => ShopReader.Read(read(ShopFile), s)));
        _fortresses = new Lazy<IReadOnlyList<FortressEntry>>(() => Guard<IReadOnlyList<FortressEntry>>(logger, FortressesFile,
            Array.Empty<FortressEntry>(), s => FortressesReader.Read(read(FortressesFile), _waves.Value, _shops.Value, s)));
    }

    /// <summary>For tests: the same validation over text instead of the files. Null text is the missing-file case.</summary>
    internal static LongDefeatConfigCatalog FromTexts(string? fortresses, string? waves, string? shop, IModLogger logger,
        string? affixes = null, string? boons = null, string? classes = null) =>
        new(logger, name => name switch
        {
            FortressesFile => fortresses,
            WavesFile => waves,
            ShopFile => shop,
            AffixesFile => affixes,
            BoonsFile => boons,
            ClassesFile => classes,
            _ => null,
        });

    /// <summary>16 hex digits over the six files as loaded, in the order fortresses, waves, shop, affixes, boons, classes (a missing file joins as empty text), line endings normalized: a checkout's line endings must not make a new balance version.</summary>
    public string BalanceHash => _balance.Value;

    private static string HashOf(params string?[] texts)
    {
        var joined = string.Join("\n\0\n", texts.Select(t => (t ?? "").Replace("\r\n", "\n")));
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(joined)), 0, 8).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>Every valid fortress, in file order.</summary>
    public IReadOnlyList<FortressEntry> Fortresses => _fortresses.Value;

    public RunRules Rules => _waves.Value.Rules;

    public bool TryGetFortress(string id, out FortressEntry fortress)
    {
        fortress = null!;
        if (string.IsNullOrEmpty(id))
            return false;

        foreach (var candidate in _fortresses.Value)
        {
            if (string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                fortress = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>The plan for one run. The options leave here with a seed: the one given, else the start time's ticks.</summary>
    public RunPlan BuildPlan(FortressEntry fortress, RunOptions options) =>
        new(fortress.Id, fortress.Garrison, fortress.Waves, fortress.Shop, Rules, options.Seeded(DateTime.UtcNow.Ticks)) { Provenance = RunProvenance.Unknown with { Balance = BalanceHash }, Bounties = _waves.Value.Bounties, Keep = fortress.Keep, Muster = fortress.Muster };

    private static T Guard<T>(IModLogger logger, string file, T fallback, Func<LongDefeatConfigScope, T> load)
    {
        var scope = new LongDefeatConfigScope(logger, file);
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
            logger.LogWarning($"LongDefeatConfigCatalog: {name} could not be read at {path} ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }
}

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
/// Validating loader for <c>long_defeat/fortresses.json</c>, <c>waves.json</c>, <c>shop.json</c>, <c>affixes.json</c>, <c>boons.json</c> and <c>classes.json</c>, read once per
/// process, and the owner of the <c>balance</c> hash over those six. The load never throws, because a faulted <see cref="Lazy{T}"/> rethrows forever.
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

    private readonly IModLogger _logger;
    private readonly Lazy<string> _balance;
    private readonly Lazy<WavesConfig> _waves;
    private readonly Lazy<AffixesConfig> _affixes;
    private readonly Lazy<BoonPlan> _boons;
    private readonly Lazy<ClassPlan> _classes;
    private readonly HashSet<string> _unnamedCultures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<IReadOnlyDictionary<string, ShopPlan>> _shops;
    private readonly Lazy<IReadOnlyList<FortressEntry>> _fortresses;

    public LongDefeatConfigCatalog(IPathService pathService, IModLogger logger)
        : this(logger, name => ReadFile(pathService, name, logger))
    {
    }

    private LongDefeatConfigCatalog(IModLogger logger, Func<string, string?> readFile)
    {
        _logger = logger;
        var texts = new ConcurrentDictionary<string, string?>();
        string? read(string name) => texts.GetOrAdd(name, readFile);
        _balance = new Lazy<string>(() => HashOf(read(FortressesFile), read(WavesFile), read(ShopFile),
            read(AffixesFile), read(BoonsFile), read(ClassesFile)));
        _waves = new Lazy<WavesConfig>(() => Guard(logger, WavesFile, WavesConfig.Empty,
            s => WavesReader.Read(read(WavesFile), s)));
        _affixes = new Lazy<AffixesConfig>(() => Guard(logger, AffixesFile, AffixesConfig.None,
            s => AffixesReader.Read(read(AffixesFile), s)));
        _boons = new Lazy<BoonPlan>(() => Guard(logger, BoonsFile, BoonPlan.None, s => BoonsReader.Read(read(BoonsFile), s)));
        _classes = new Lazy<ClassPlan>(() => Guard(logger, ClassesFile, ClassPlan.None, s => ClassesReader.Read(read(ClassesFile), s)));
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

    /// <summary>Every class of classes.json in file order, whatever fortress offers it.</summary>
    public ClassPlan Classes => _classes.Value;

    /// <summary>The classes a fortress offers: those with a kit for its defender culture, in file order. The setup prompt lists these.</summary>
    public IReadOnlyList<RunClass> ClassesFor(FortressEntry fortress) => _classes.Value.OnOffer(fortress.DefenderCulture);

    /// <summary>
    /// The plan for one run. The options leave here with a seed (the one given, else the start time's ticks) and with the class that was chosen: the one asked for when the
    /// fortress offers it, else the bot's draw from the seed or a person's first class. A class asked for and not on offer costs one warning.
    /// </summary>
    public RunPlan BuildPlan(FortressEntry fortress, RunOptions options)
    {
        var seeded = options.Seeded(DateTime.UtcNow.Ticks);
        var chosen = ClassPlan.Choose(ClassesFor(fortress), seeded.ClassId, seeded.Seed ?? 0UL, seeded.Autoplay);
        if (seeded.ClassId != null && !string.Equals(seeded.ClassId, chosen?.Id, StringComparison.OrdinalIgnoreCase))
            _logger.LogWarning($"LongDefeatConfigCatalog: the class '{seeded.ClassId}' is not on offer at '{fortress.Id}' (defenders of {fortress.DefenderCulture}); playing {chosen?.Id ?? "in his own gear"} instead");

        return new RunPlan(fortress.Id, fortress.Garrison, fortress.Waves, fortress.Shop, Rules, seeded.ClassId == chosen?.Id ? seeded : seeded with { ClassId = chosen?.Id })
        {
            Provenance = RunProvenance.Unknown with { Balance = BalanceHash },
            Bounties = _waves.Value.Bounties,
            Keep = fortress.Keep,
            Muster = fortress.Muster,
            Affixes = AffixesFor(fortress.AttackerCulture),
            Boons = _boons.Value,
            Loadout = chosen?.LoadoutFor(fortress.DefenderCulture),
        };
    }

    /// <summary>The affixes with the names of one attacker culture. A culture the file has no table for shows its powers as their ids, with one warning per culture.</summary>
    private AffixPlan AffixesFor(string culture)
    {
        var config = _affixes.Value;
        if (config.Schedule.Count > 0 && !config.HasNames(culture) && _unnamedCultures.Add(culture))
            _logger.LogWarning($"LongDefeatConfigCatalog: {AffixesFile}: no 'names.{culture}' table; that culture's affixes show as their power ids");
        return config.For(culture);
    }

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

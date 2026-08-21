using System;
using System.Collections.Generic;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.HeroRace.Configuration;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Loads, serves and persists the two race-framing configs.
///
/// <para>Replaces three independent loaders of the same two files: CharacterSpawnerService built
/// its own lookup, the never-invoked CharacterTableauService built a second, and the never-invoked
/// RacePositionConfigurationService built a third that MERGED the two files and fell back from the
/// image config to the avatar config. That merge was the live bug in the set — the two surfaces
/// carry different tuning for the same race on purpose.</para>
///
/// <para>Registered as a singleton: the configs are read once at startup, and the rows handed out
/// are the live instances the tuner edits. Reloading requires a call to <see cref="Reload"/> or a
/// process restart, not a new campaign.</para>
/// </summary>
public class RacePositionStore : IRacePositionStore
{
    private const string AvatarConfigName = "CharacterAvatarPatch";
    private const string ImageConfigName = "CharacterImagePatch";
    private const string MountPrefix = "mount_";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    private RacePositionConfig _avatarConfig;
    private RacePositionConfig _imageConfig;
    private Dictionary<string, RacePositionConfigItem> _avatarLookup;
    private Dictionary<string, RacePositionConfigItem> _imageLookup;

    public RacePositionStore(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Reload();
    }

    public void Reload()
    {
        _avatarConfig = Load(AvatarConfigName);
        _imageConfig = Load(ImageConfigName);
        RebuildLookups();
    }

    private RacePositionConfig Load(string name)
    {
        var config = RacePositionConfig.LoadConfig(name, _pathService, out var warnings);

        // A dropped row is indistinguishable from a race that was never configured, so the warning
        // is the only way anyone finds out their hand edit did nothing.
        foreach (var warning in warnings)
            _logger.LogWarning($"[RacePositions] {warning}");

        return config;
    }

    private void RebuildLookups()
    {
        _avatarLookup = BuildLookup(_avatarConfig);
        _imageLookup = BuildLookup(_imageConfig);
    }

    private static Dictionary<string, RacePositionConfigItem> BuildLookup(RacePositionConfig config)
    {
        var lookup = new Dictionary<string, RacePositionConfigItem>(StringComparer.OrdinalIgnoreCase);
        if (config?.Items == null)
            return lookup;

        foreach (var item in config.Items)
        {
            if (item != null && !string.IsNullOrEmpty(item.Race))
                lookup[item.Race] = item;
        }

        return lookup;
    }

    public RacePositionConfigItem ResolveAvatar(string raceName) => Find(_avatarLookup, raceName);

    public RacePositionConfigItem ResolveImage(string raceName) => Find(_imageLookup, raceName);

    public RacePositionConfigItem ResolveAvatarMount(string raceName)
        => string.IsNullOrEmpty(raceName) ? null : Find(_avatarLookup, MountPrefix + raceName);

    private static RacePositionConfigItem Find(Dictionary<string, RacePositionConfigItem> lookup, string raceName)
    {
        if (lookup == null || string.IsNullOrEmpty(raceName))
            return null;

        if (!lookup.TryGetValue(raceName, out var item) || item == null)
            return null;

        // Second gate behind the load-time validator, and it lives HERE rather than in one consumer
        // so both surfaces inherit it. The validator runs on Reload only; the in-game tuner creates
        // and edits rows afterwards, and every consumer of a row feeds it to a native SetFrame.
        // Returning null degrades to vanilla framing, which is the same contract as an absent row.
        return IsFinite(item) ? item : null;
    }

    private static bool IsFinite(RacePositionConfigItem item)
        => FiniteFloatValidator.IsFinite(item.Horizontal)
           && FiniteFloatValidator.IsFinite(item.Vertical)
           && FiniteFloatValidator.IsFinite(item.Zoom);

    public IReadOnlyList<RacePositionConfigItem> List(RacePositionSurface surface)
        => ConfigFor(surface).Items;

    public RacePositionConfigItem GetOrAdd(RacePositionSurface surface, string raceName)
    {
        if (string.IsNullOrWhiteSpace(raceName))
            return null;

        var race = raceName.Trim().ToLowerInvariant();
        var lookup = LookupFor(surface);

        if (lookup.TryGetValue(race, out var existing))
            return existing;

        var item = new RacePositionConfigItem { Race = race };
        ConfigFor(surface).Items.Add(item);
        lookup[race] = item;
        return item;
    }

    /// <summary>
    /// Writes both configs. Both are STAGED to temp files first and only then swapped in, because
    /// the pair is reloaded as a set: if the second write failed after the first had already
    /// replaced its target (a denied ACL, a full disk), the next reload would combine new avatar
    /// data with old image data and nothing would say so.
    /// </summary>
    public void Save()
    {
        var staged = new List<string>();

        if (RacePositionConfig.StageWrite(AvatarConfigName, _avatarConfig, _pathService))
            staged.Add(AvatarConfigName);

        if (RacePositionConfig.StageWrite(ImageConfigName, _imageConfig, _pathService))
            staged.Add(ImageConfigName);

        // Both temps exist by here, so the remaining work is two renames. That is as close to
        // atomic as this gets without a transaction, and it moves the likely failure (serialising,
        // or writing a new file) to before anything live has been touched.
        foreach (var name in staged)
            RacePositionConfig.CommitStagedWrite(name, _pathService);
    }

    private RacePositionConfig ConfigFor(RacePositionSurface surface)
        => surface == RacePositionSurface.Image ? _imageConfig : _avatarConfig;

    private Dictionary<string, RacePositionConfigItem> LookupFor(RacePositionSurface surface)
        => surface == RacePositionSurface.Image ? _imageLookup : _avatarLookup;
}

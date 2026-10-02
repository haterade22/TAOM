using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionUI.Resources;

/// <summary>
/// Owns the lifetime of Kysaro's runtime images (#704). His module loaded every one of them at the
/// first themed screen and held them until the game closed (about 337 MB of sprites plus up to 221 MB
/// of loading screens, uncompressed). Mike's decision (2026-10-01) is load on demand:
/// <list type="bullet">
/// <item>each themed screen holds the image groups it draws (<see cref="FrontEndImageGroups"/>): the
/// main menu its own images, every character-creation screen their shared frame, the faction screen
/// that frame plus its art. A group is released <see cref="ReleaseDelayTicks"/> ticks after nothing
/// holds it (<see cref="Tick"/>), so moving between character-creation stages does not reload it;</item>
/// <item>art loads one image at a time as it is shown (<see cref="EnsureArt"/>);</item>
/// <item>one loading screen is held at a time (<see cref="ShowLoadingImage"/>);</item>
/// <item>the loading-window frame, the fonts and the skill icons stay for the process: the loading
/// window is built once at startup and the engine has no way to remove a font.</item>
/// </list>
/// The engine rebuilds its sprite, font and brush tables only when the native side loads a module at
/// runtime (<c>GauntletUISubModule.OnNewModuleLoad</c>, v1.5.3); <see cref="OnEngineResourcesRefreshed"/>
/// puts back everything loaded before the open screens are rebuilt, in place of Kysaro's postfix on
/// <c>RefreshSpriteData</c>.
/// </summary>
public sealed class FrontEndSpriteService
{
    /// <summary>About a second at 60 fps: longer than the gap between one stage's movie being released
    /// and the next one's being loaded.</summary>
    public const int ReleaseDelayTicks = 60;

    /// <summary>The sprite-name prefix of a loading screen.</summary>
    public const string LoadingSpritePrefix = "fs_loading_";

    private const string SpriteSuffix = ".png";
    private const string NinePatchSuffix = ".nine";
    private const string SkillIconMapFile = "sprite_overrides.json";
    private const string ResidentBrushFile = "TAOMLoading";

    private readonly IFrontEndResourceAdapter _adapter;
    private readonly FactionUIPaths _paths;
    private readonly IModLogger _logger;

    private readonly ImageGroup _menu = new(FrontEndImageGroups.MainMenu, FrontEndSpriteKind.MenuChrome, "main menu", "TAOMMainMenu");
    private readonly ImageGroup _creation = new(FrontEndImageGroups.CharacterCreation, FrontEndSpriteKind.Chrome, "character creation", "TAOMCharCreation", "TAOMFactionScreen");
    private readonly ImageGroup _art = new(FrontEndImageGroups.FactionArt, FrontEndSpriteKind.Art, "faction art");
    private readonly ImageGroup[] _groups;

    private readonly Dictionary<string, LoadedImage> _resident = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LoadedImage> _skillIcons = new(StringComparer.Ordinal);
    private readonly List<PendingRelease> _pendingImages = new();

    private Dictionary<string, string>? _catalog;
    private IReadOnlyList<string>? _fontDirectories;
    private IReadOnlyList<(string FileName, string VanillaName)>? _skillIconMap;
    private LoadedImage? _loadingImage;
    private bool _residentLoaded;

    public FrontEndSpriteService(IFrontEndResourceAdapter adapter, FactionUIPaths paths, IModLogger logger)
    {
        _adapter = adapter;
        _paths = paths;
        _logger = logger;
        _groups = new[] { _menu, _creation, _art };
    }

    /// <summary>True when the runtime sprites folder has an image of this name, loaded or not.</summary>
    public bool HasImage(string spriteName) => Catalog.ContainsKey(spriteName);

    /// <summary>Sprite name to PNG path for every image in the runtime sprites folder, read once.</summary>
    private Dictionary<string, string> Catalog => _catalog ??= _adapter.ListFiles(_paths.RuntimeSprites, SpriteSuffix)
        .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    /// <summary>The loading-window frame, its fonts and its brushes. Idempotent; called before the
    /// loading window's movie is built and before every group load.</summary>
    public void EnsureResident()
    {
        if (!_adapter.IsUiReady || _residentLoaded)
            return;

        _fontDirectories ??= _adapter.ListDirectories(_paths.RuntimeFonts);
        foreach (var fontDirectory in _fontDirectories)
            _adapter.RegisterFont(fontDirectory, Path.GetFileName(fontDirectory));
        LoadAll(FrontEndSpriteKind.Resident, _resident);
        _adapter.LoadBrushFile(ResidentBrushFile);
        _residentLoaded = true;
    }

    /// <summary>A themed screen is about to load: makes the groups it draws available, and keeps them
    /// from being released for at least <see cref="ReleaseDelayTicks"/> ticks.</summary>
    public void Acquire(FrontEndImageGroups groups)
    {
        foreach (var group in _groups)
        {
            if ((groups & group.Flag) == 0)
                continue;
            group.IdleTicks = 0;
            if (!_adapter.IsUiReady || group.BrushFiles.Length == 0)
                continue;

            EnsureResident();
            if (!LoadAll(group.Kind, group.Images))
                continue;
            foreach (var brushFile in group.BrushFiles)
                _adapter.LoadBrushFile(brushFile);
            _logger.LogInfo($"[FactionUI] front-end images loaded: {group.Label}, {group.Images.Count} images, {MegaBytes(group.Images.Values):F1} MB");
        }
    }

    /// <summary>Makes one image available by name, loading it if it is not already; false when no such
    /// image exists. The faction screen calls this before binding a portrait, emblem or background,
    /// because Gauntlet resolves a sprite name once, when the bound value is set.</summary>
    public bool EnsureArt(string spriteName)
    {
        foreach (var group in _groups)
        {
            if (!group.Images.ContainsKey(spriteName))
                continue;
            group.IdleTicks = 0;
            return true;
        }
        if (!_adapter.IsUiReady || !Catalog.TryGetValue(spriteName, out var path))
            return false;

        var owner = GroupOf(FrontEndSpriteCatalog.Classify(spriteName));
        if (owner == null)
            return _adapter.IsSpriteRegistered(spriteName);

        var image = LoadImage(path, spriteName, withNinePatch: true);
        if (image == null)
            return false;
        owner.Images[spriteName] = image;
        owner.IdleTicks = 0;
        return true;
    }

    /// <summary>Registers Kysaro's skill icons under vanilla's sprite names, for the session. The map is
    /// read once per process.</summary>
    public void ApplySkillIcons()
    {
        if (!_adapter.IsUiReady)
            return;

        foreach (var (fileName, vanillaName) in _skillIconMap ??= ReadSkillIconMap())
        {
            if (_skillIcons.ContainsKey(fileName) || !Catalog.TryGetValue(fileName, out var path))
                continue;

            var loaded = LoadImage(path, vanillaName, withNinePatch: false);
            if (loaded != null)
                _skillIcons[fileName] = loaded;
        }
    }

    /// <summary>Registers one loading screen and returns its sprite name, or null when it cannot be
    /// loaded. The image it replaces is released after <see cref="ReleaseDelayTicks"/>, since it can
    /// still be on screen this frame.</summary>
    public string? ShowLoadingImage(string filePath)
    {
        var spriteName = LoadingSpritePrefix + Path.GetFileNameWithoutExtension(filePath);
        if (_loadingImage?.PrimaryName == spriteName)
            return spriteName;

        LoadedImage? image;
        var pending = _pendingImages.FirstOrDefault(p => p.Image.PrimaryName == spriteName);
        if (pending != null)
        {
            _pendingImages.Remove(pending);
            image = pending.Image;
        }
        else
        {
            if (!_adapter.IsUiReady)
                return null;
            image = LoadImage(filePath, spriteName, withNinePatch: false);
            if (image == null)
                return null;
        }

        ReleaseLoadingImage();
        _loadingImage = image;
        return spriteName;
    }

    /// <summary>The loading screen held now is released after <see cref="ReleaseDelayTicks"/>, as when
    /// the themed loading screens are switched off.</summary>
    public void ReleaseLoadingImage()
    {
        if (_loadingImage == null)
            return;
        _pendingImages.Add(new PendingRelease(_loadingImage, ReleaseDelayTicks));
        _loadingImage = null;
    }

    /// <summary>Once per application tick, with the groups the open themed screens hold: releases each
    /// group nothing has held for <see cref="ReleaseDelayTicks"/> ticks, and runs the pending image
    /// releases.</summary>
    public void Tick(FrontEndImageGroups held)
    {
        foreach (var group in _groups)
        {
            if ((held & group.Flag) != 0)
            {
                group.IdleTicks = 0;
                continue;
            }
            if (group.Images.Count == 0 || ++group.IdleTicks < ReleaseDelayTicks)
                continue;
            group.IdleTicks = 0;
            ReleaseGroup(group);
        }

        for (var i = _pendingImages.Count - 1; i >= 0; i--)
        {
            var pending = _pendingImages[i];
            if (--pending.TicksLeft > 0)
                continue;
            // Removed before the release, so a release that throws is not retried every tick.
            _pendingImages.RemoveAt(i);
            Release(pending.Image);
        }
    }

    /// <summary>The engine has rebuilt its sprite, font and brush tables: puts every loaded image back
    /// under its names (a skill icon over the vanilla sprite the rebuild restored), the fonts, and the
    /// brush files of every loaded group, before the open screens are rebuilt.</summary>
    public void OnEngineResourcesRefreshed()
    {
        if (!_adapter.IsUiReady)
            return;

        if (_residentLoaded)
        {
            foreach (var fontDirectory in _fontDirectories ?? Array.Empty<string>())
                _adapter.RegisterFont(fontDirectory, Path.GetFileName(fontDirectory));
        }
        foreach (var image in AllLoadedImages())
            Register(image);

        if (_residentLoaded)
            _adapter.LoadBrushFile(ResidentBrushFile);
        foreach (var group in _groups.Where(g => g.Images.Count > 0))
        {
            foreach (var brushFile in group.BrushFiles)
                _adapter.LoadBrushFile(brushFile);
        }
    }

    private ImageGroup? GroupOf(FrontEndSpriteKind kind) => kind switch
    {
        FrontEndSpriteKind.MenuChrome => _menu,
        FrontEndSpriteKind.Chrome => _creation,
        FrontEndSpriteKind.Art => _art,
        _ => null,
    };

    private IEnumerable<LoadedImage> AllLoadedImages()
    {
        foreach (var image in _resident.Values)
            yield return image;
        foreach (var image in _skillIcons.Values)
            yield return image;
        foreach (var group in _groups)
        {
            foreach (var image in group.Images.Values)
                yield return image;
        }
        if (_loadingImage != null)
            yield return _loadingImage;
        foreach (var pending in _pendingImages)
            yield return pending.Image;
    }

    /// <summary>Loads every catalogued image of this kind not loaded yet; true when one was added.</summary>
    private bool LoadAll(FrontEndSpriteKind kind, Dictionary<string, LoadedImage> loaded)
    {
        var added = false;
        foreach (var entry in Catalog)
        {
            if (loaded.ContainsKey(entry.Key) || FrontEndSpriteCatalog.Classify(entry.Key) != kind)
                continue;

            var image = LoadImage(entry.Value, entry.Key, withNinePatch: true);
            if (image == null)
                continue;
            loaded[entry.Key] = image;
            added = true;
        }
        return added;
    }

    private LoadedImage? LoadImage(string path, string spriteName, bool withNinePatch)
    {
        var texture = _adapter.LoadTexture(path);
        if (texture == null)
        {
            _logger.LogWarning($"[FactionUI] could not load image {path}");
            return null;
        }

        var image = new LoadedImage(texture);
        image.Add(spriteName, null);
        if (withNinePatch && FrontEndSpriteCatalog.TryParseNinePatch(
                _adapter.ReadAllText(Path.ChangeExtension(path, NinePatchSuffix)), out var ninePatch))
        {
            image.Add(spriteName + "_9", ninePatch);
        }

        Register(image);
        return image;
    }

    private void Register(LoadedImage image)
    {
        foreach (var (name, nine) in image.Registrations)
            _adapter.RegisterSprite(name, image.Texture, nine);
    }

    private void ReleaseGroup(ImageGroup group)
    {
        var count = group.Images.Count;
        var megaBytes = MegaBytes(group.Images.Values);
        foreach (var image in group.Images.Values)
            Release(image);
        group.Images.Clear();
        _logger.LogInfo($"[FactionUI] front-end images released: {group.Label}, {count} images, {megaBytes:F1} MB");
    }

    private void Release(LoadedImage image)
    {
        foreach (var (name, _) in image.Registrations)
            _adapter.UnregisterSprite(name);
        _adapter.ReleaseTexture(image.Texture);
    }

    /// <summary>
    /// The skill-icon map: image file name to the vanilla sprite it replaces. Keys starting with
    /// <c>_</c> are notes. Any other entry that is not a non-empty name mapped to a non-empty name, or
    /// whose image is not in the runtime sprites, is skipped with a warning, then one summary warning
    /// (TAOM's config-provider rule); malformed JSON keeps every vanilla icon. Read once per process,
    /// so each problem is reported once.
    /// </summary>
    private IReadOnlyList<(string FileName, string VanillaName)> ReadSkillIconMap()
    {
        var text = _adapter.ReadAllText(Path.Combine(_paths.ConfigDirectory, SkillIconMapFile));
        if (text == null)
            return Array.Empty<(string, string)>();

        JObject root;
        try
        {
            root = JObject.Parse(text);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning($"[FactionUI] {SkillIconMapFile} is not valid JSON, vanilla skill icons kept: {ex.Message}");
            return Array.Empty<(string, string)>();
        }

        var map = new List<(string, string)>();
        var skipped = 0;
        foreach (var property in root.Properties())
        {
            if (property.Name.StartsWith("_", StringComparison.Ordinal))
                continue;
            var vanillaName = property.Value.Type == JTokenType.String ? (string?)property.Value : null;
            if (property.Name.Length == 0 || string.IsNullOrEmpty(vanillaName))
            {
                skipped++;
                var key = property.Name.Length == 0 ? "(an empty key)" : property.Name;
                _logger.LogWarning($"[FactionUI] {SkillIconMapFile}: {key} must map an image name to a vanilla sprite name; entry skipped");
                continue;
            }
            if (!Catalog.ContainsKey(property.Name))
            {
                skipped++;
                _logger.LogWarning($"[FactionUI] {SkillIconMapFile}: no image {property.Name}{SpriteSuffix} in {_paths.RuntimeSprites}; vanilla's {vanillaName} kept");
                continue;
            }
            map.Add((property.Name, vanillaName!));
        }

        if (skipped > 0)
            _logger.LogWarning($"[FactionUI] {SkillIconMapFile}: {skipped} {(skipped == 1 ? "entry" : "entries")} skipped, see the warnings above");
        return map;
    }

    private static double MegaBytes(IEnumerable<LoadedImage> images) =>
        images.Sum(i => (double)i.Texture.MemorySizeBytes) / (1024 * 1024);

    private sealed class ImageGroup
    {
        public ImageGroup(FrontEndImageGroups flag, FrontEndSpriteKind kind, string label, params string[] brushFiles)
        {
            Flag = flag;
            Kind = kind;
            Label = label;
            BrushFiles = brushFiles;
        }

        public FrontEndImageGroups Flag { get; }

        public FrontEndSpriteKind Kind { get; }

        public string Label { get; }

        /// <summary>The brush files that name this group's images, re-read once they are registered.
        /// Empty for the art, which no brush names: it is bound by data, one image at a time.</summary>
        public string[] BrushFiles { get; }

        public Dictionary<string, LoadedImage> Images { get; } = new(StringComparer.Ordinal);

        public int IdleTicks { get; set; }
    }

    private sealed class LoadedImage
    {
        private readonly List<(string Name, NinePatch? NinePatch)> _registrations = new();

        public LoadedImage(FrontEndTexture texture)
        {
            Texture = texture;
        }

        public FrontEndTexture Texture { get; }

        public string PrimaryName => _registrations[0].Name;

        public IReadOnlyList<(string Name, NinePatch? NinePatch)> Registrations => _registrations;

        public void Add(string name, NinePatch? ninePatch) => _registrations.Add((name, ninePatch));
    }

    private sealed class PendingRelease
    {
        public PendingRelease(LoadedImage image, int ticksLeft)
        {
            Image = image;
            TicksLeft = ticksLeft;
        }

        public LoadedImage Image { get; }

        public int TicksLeft { get; set; }
    }
}

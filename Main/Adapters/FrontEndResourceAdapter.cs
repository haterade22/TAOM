using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using TAOM.Core.Logging;
using NativeTexture = TaleWorlds.Engine.Texture;
using TwoDimensionTexture = TaleWorlds.TwoDimension.Texture;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IFrontEndResourceAdapter"/> over <see cref="UIResourceManager"/> and
/// <see cref="NativeTexture"/>. The sprite and font registration is Kysaro's TAOM_FactionUI technique
/// (decompiled, #704): a module PNG becomes an engine texture, wrapped as a Gauntlet texture, under a
/// sprite name the prefabs and brushes reference.
/// </summary>
public sealed class FrontEndResourceAdapter : IFrontEndResourceAdapter
{
    // SpriteCategory.IsLoaded has a private setter; a font's glyph sheet is drawn only from a category
    // that reports itself loaded. Resolved once.
    private static readonly MethodInfo? CategoryIsLoadedSetter =
        AccessTools.PropertySetter(typeof(SpriteCategory), nameof(SpriteCategory.IsLoaded));

    private readonly IModLogger _logger;

    // A font's glyph sheet, kept for the process like the font itself, so registering it again after the
    // engine rebuilds its tables reuses the texture instead of loading a second copy.
    private readonly Dictionary<string, NativeTexture> _fontSheets = new(StringComparer.Ordinal);

    public FrontEndResourceAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool IsUiReady => UIResourceManager.SpriteData != null;

    public bool IsSpriteRegistered(string spriteName) =>
        UIResourceManager.SpriteData?.Sprites?.ContainsKey(spriteName) ?? false;

    public IReadOnlyList<string> ListFiles(string directory, string suffix)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();
        return Directory.GetFiles(directory, "*" + suffix)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> ListDirectories(string directory)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();
        return Directory.GetDirectories(directory)
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool FileExists(string path) => File.Exists(path);

    public string? ReadAllText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FactionUI] could not read {path}: {ex.Message}");
            return null;
        }
    }

    public FrontEndTexture? LoadTexture(string filePath)
    {
        var texture = NativeTexture.LoadTextureFromPath(Path.GetFileName(filePath), Path.GetDirectoryName(filePath));
        if (texture == null)
            return null;
        return new FrontEndTexture(texture, texture.MemorySize);
    }

    public void RegisterSprite(string spriteName, FrontEndTexture texture, NinePatch? ninePatch)
    {
        var sprites = UIResourceManager.SpriteData?.Sprites;
        if (sprites == null || texture.Native is not NativeTexture engineTexture)
            return;

        var gauntletTexture = new TwoDimensionTexture(new EngineTexture(engineTexture));
        var nine = ninePatch is { } n
            ? new SpriteNinePatchParameters(n.Left, n.Right, n.Top, n.Bottom)
            : SpriteNinePatchParameters.Empty;
        sprites[spriteName] = new FrontEndRuntimeSprite(spriteName, gauntletTexture, nine);
    }

    public void UnregisterSprite(string spriteName) => UIResourceManager.SpriteData?.Sprites?.Remove(spriteName);

    // Vanilla frees a UI sheet the same way: SpriteCategory.Unload calls EngineTexture's ITexture.Release,
    // which is Texture.ReleaseImmediately (v1.5.3 EngineTexture.cs:47-49). Texture.Release only drops
    // the managed handle.
    public void ReleaseTexture(FrontEndTexture texture)
    {
        if (texture.Native is NativeTexture engineTexture && !engineTexture.IsReleased)
            engineTexture.ReleaseImmediately();
    }

    public void LoadBrushFile(string brushFileName) => UIResourceManager.BrushFactory?.LoadBrushFile(brushFileName);

    public bool RegisterFont(string fontDirectory, string fontName)
    {
        var fontFactory = UIResourceManager.FontFactory;
        var spriteData = UIResourceManager.SpriteData;
        if (fontFactory == null || spriteData == null)
            return false;
        if (fontFactory.GetFont(fontName)?.Name == fontName)
            return false;

        if (!_fontSheets.TryGetValue(fontName, out var engineTexture))
        {
            if (LoadTexture(Path.Combine(fontDirectory, fontName + ".png"))?.Native is not NativeTexture loaded)
                return false;
            engineTexture = loaded;
            _fontSheets[fontName] = engineTexture;
        }

        var gauntletTexture = new TwoDimensionTexture(new EngineTexture(engineTexture));
        var category = new SpriteCategory(fontName + "_fontsheet", 1, true)
        {
            SheetSizes = new[] { new Vec2i(gauntletTexture.Width, gauntletTexture.Height) },
        };
        category.SpriteSheets.Add(gauntletTexture);
        CategoryIsLoadedSetter?.Invoke(category, new object[] { true });

        var part = new SpritePart(fontName, category, gauntletTexture.Width, gauntletTexture.Height)
        {
            SheetID = 1,
            SheetX = 0,
            SheetY = 0,
        };
        part.UpdateInitValues();
        spriteData.Sprites[fontName] = new SpriteGeneric(fontName, part, SpriteNinePatchParameters.Empty);

        return fontFactory.TryAddFontDefinition(fontDirectory + "/", fontName, spriteData);
    }

    public bool HasPrefab(string movieName) => UIResourceManager.WidgetFactory?.IsCustomType(movieName) ?? false;
}

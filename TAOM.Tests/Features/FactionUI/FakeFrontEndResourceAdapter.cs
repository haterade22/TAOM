using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// In-memory stand-in for the engine's sprite registry, texture loader, brush and font factories, its
/// prefab table and the module's files, so the front-end image lifecycle can be proven with no game
/// running.
/// </summary>
internal sealed class FakeFrontEndResourceAdapter : IFrontEndResourceAdapter
{
    public bool UiReady { get; set; } = true;

    public Dictionary<string, string> Files { get; } = new();

    public HashSet<string> Directories { get; } = new();

    public Dictionary<string, FrontEndTexture> Registered { get; } = new();

    public Dictionary<string, NinePatch?> RegisteredNinePatches { get; } = new();

    public List<string> LoadedTextures { get; } = new();

    public List<FrontEndTexture> ReleasedTextures { get; } = new();

    public List<string> BrushFileLoads { get; } = new();

    public List<string> FontsRegistered { get; } = new();

    public HashSet<string> FailingTextures { get; } = new();

    /// <summary>Movie names whose prefab is missing from the modules' GUI folders.</summary>
    public HashSet<string> MissingPrefabs { get; } = new();

    /// <summary>Textures whose release throws, to prove a failed release is not retried every tick.</summary>
    public HashSet<string> TexturesThatThrowOnRelease { get; } = new();

    public int ListDirectoriesCalls { get; private set; }

    public Dictionary<string, int> ReadCounts { get; } = new();

    public bool IsUiReady => UiReady;

    public void AddFile(string path, string contents = "")
    {
        Files[path] = contents;
        var dir = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(dir))
        {
            Directories.Add(dir!);
            dir = Path.GetDirectoryName(dir);
        }
    }

    public bool IsSpriteRegistered(string spriteName) => Registered.ContainsKey(spriteName);

    public IReadOnlyList<string> ListFiles(string directory, string extension) =>
        Files.Keys
            .Where(f => string.Equals(Path.GetDirectoryName(f), directory, StringComparison.OrdinalIgnoreCase)
                        && f.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public IReadOnlyList<string> ListDirectories(string directory)
    {
        ListDirectoriesCalls++;
        return Directories
            .Where(d => string.Equals(Path.GetDirectoryName(d), directory, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool FileExists(string path) => Files.ContainsKey(path);

    public string? ReadAllText(string path)
    {
        ReadCounts[path] = ReadCounts.TryGetValue(path, out var count) ? count + 1 : 1;
        return Files.TryGetValue(path, out var text) ? text : null;
    }

    public FrontEndTexture? LoadTexture(string filePath)
    {
        if (FailingTextures.Contains(filePath) || !Files.ContainsKey(filePath))
            return null;
        LoadedTextures.Add(filePath);
        return new FrontEndTexture(filePath, 1024);
    }

    public void RegisterSprite(string spriteName, FrontEndTexture texture, NinePatch? ninePatch)
    {
        Registered[spriteName] = texture;
        RegisteredNinePatches[spriteName] = ninePatch;
    }

    public void UnregisterSprite(string spriteName)
    {
        Registered.Remove(spriteName);
        RegisteredNinePatches.Remove(spriteName);
    }

    public void ReleaseTexture(FrontEndTexture texture)
    {
        if (texture.Native is string path && TexturesThatThrowOnRelease.Contains(path))
            throw new InvalidOperationException("release failed");
        ReleasedTextures.Add(texture);
    }

    public void LoadBrushFile(string brushFileName) => BrushFileLoads.Add(brushFileName);

    public bool RegisterFont(string fontDirectory, string fontName)
    {
        if (FontsRegistered.Contains(fontName))
            return false;
        FontsRegistered.Add(fontName);
        return true;
    }

    public bool HasPrefab(string movieName) => !MissingPrefabs.Contains(movieName);

    /// <summary>
    /// Simulates <c>UIResourceManager.Refresh</c>: a fresh sprite table holding only vanilla's sprites
    /// (each name in <paramref name="vanillaNames"/> back under a vanilla texture), new font and brush
    /// factories that know none of ours.
    /// </summary>
    public void SimulateEngineRefresh(params string[] vanillaNames)
    {
        Registered.Clear();
        RegisteredNinePatches.Clear();
        foreach (var name in vanillaNames)
            Registered[name] = new FrontEndTexture("vanilla:" + name, 1024);
        FontsRegistered.Clear();
        BrushFileLoads.Clear();
    }
}

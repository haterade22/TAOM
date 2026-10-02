using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// The engine's UI resource tables (sprites, brushes, fonts), its PNG texture loader and the module's
/// files, as the themed front end (#704) needs them. Lets the image lifecycle in
/// <c>FrontEndSpriteService</c> be proven offline.
/// </summary>
public interface IFrontEndResourceAdapter
{
    /// <summary>False until the engine has built its sprite table; nothing can be registered before.</summary>
    bool IsUiReady { get; }

    bool IsSpriteRegistered(string spriteName);

    /// <summary>Full paths of the files in <paramref name="directory"/> whose names end with
    /// <paramref name="suffix"/>, sorted; empty when the directory does not exist.</summary>
    IReadOnlyList<string> ListFiles(string directory, string suffix);

    /// <summary>Full paths of the sub-directories, sorted; empty when the directory does not exist.</summary>
    IReadOnlyList<string> ListDirectories(string directory);

    bool FileExists(string path);

    /// <summary>The file's text, or null when it is missing or unreadable.</summary>
    string? ReadAllText(string path);

    /// <summary>Loads a PNG as an engine texture, or returns null when the engine cannot.</summary>
    FrontEndTexture? LoadTexture(string filePath);

    /// <summary>Adds or replaces a sprite of this name in the engine's sprite table.</summary>
    void RegisterSprite(string spriteName, FrontEndTexture texture, NinePatch? ninePatch);

    void UnregisterSprite(string spriteName);

    /// <summary>Frees the texture's engine memory now, as vanilla does when it unloads a sprite sheet.
    /// Nothing may draw it afterwards.</summary>
    void ReleaseTexture(FrontEndTexture texture);

    /// <summary>Re-reads a brush file so its sprite and font references bind to what is registered now.</summary>
    void LoadBrushFile(string brushFileName);

    /// <summary>Registers the bitmap font in <paramref name="fontDirectory"/>; false when it was
    /// already registered or could not be loaded.</summary>
    bool RegisterFont(string fontDirectory, string fontName);

    /// <summary>True when a prefab of this name was found in the modules' GUI folders.</summary>
    bool HasPrefab(string movieName);
}

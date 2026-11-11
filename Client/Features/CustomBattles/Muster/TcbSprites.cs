using System;
using System.Collections.Generic;
using System.IO;
using TAOM.Features.CustomBattles.Muster;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace TAOM.Client.Features.CustomBattles.Muster;

/// <summary>
/// The muster screen's own resources. Its chrome is TAOM's shared front-end art (ec_*, cc_*, fs_*, mm_*), registered
/// and held by FrontEndMovieService for as long as the TaomCustomBattle movie lives, and its fonts are the resident
/// FS_CinzelWide and FS_Garamond. What stays here: the map previews, GUI/CustomBattleMuster/Maps/&lt;scene id&gt;.png,
/// registered as loose sprites on first use and kept for the process (releasing them at shutdown turned a clean exit
/// into a fail-fast exit in the standalone module).
/// </summary>
internal static class TcbSprites
{
    /// <summary>A map without a preview shows the encyclopedia's landscape stone, one of the images the movie holds.</summary>
    internal const string NoMap = "ec_stone_landscape";

    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static string _root = "";

    internal static string MapDirectory => Path.Combine(_root, "GUI", "CustomBattleMuster", "Maps");

    private static TaleWorlds.Engine.Texture Load(string file) =>
        TaleWorlds.Engine.Texture.LoadTextureFromPath(Path.GetFileName(file), Path.GetDirectoryName(file));

    private sealed class LooseSprite : Sprite
    {
        private readonly TaleWorlds.TwoDimension.Texture _texture;
        public LooseSprite(string name, TaleWorlds.TwoDimension.Texture t, SpriteNinePatchParameters nine) : base(name, t.Width, t.Height, nine) { _texture = t; }
        public override TaleWorlds.TwoDimension.Texture Texture => _texture;
        public override Vec2 GetMinUvs() => Vec2.Zero;
        public override Vec2 GetMaxUvs() => Vec2.One;
    }

    private static string? MapFile(string? sceneId) => sceneId == null ? null : Path.Combine(MapDirectory, sceneId + ".png");

    internal static bool HasMapPreview(string? sceneId) => sceneId != null && File.Exists(MapFile(sceneId));

    /// <summary>The sprite name of a map's preview, else <see cref="NoMap"/>.</summary>
    internal static string MapSprite(string? sceneId)
    {
        if (!HasMapPreview(sceneId)) return NoMap;
        var name = "tcb_map_" + sceneId;
        var data = UIResourceManager.SpriteData;
        if (data == null) return NoMap;
        if (!Cache.ContainsKey(name))
        {
            try
            {
                var engine = Load(MapFile(sceneId)!);
                if (engine == null) return NoMap;
                Cache[name] = new LooseSprite(name, new TaleWorlds.TwoDimension.Texture(new EngineTexture(engine)), SpriteNinePatchParameters.Empty);
            }
            catch (Exception e) { CustomBattleMuster.Log("map preview " + sceneId + ": " + e.Message); return NoMap; }
        }
        data.Sprites[name] = Cache[name];
        return name;
    }

    /// <summary>Records the module root for the map previews. The screen's brush file is not read here: the engine
    /// binds a brush's font and sprites once, when the file is parsed, so FrontEndSpriteService reads it again inside
    /// the movie's acquire, after the resident fonts and the held images are registered (it is one of the character
    /// creation group's brush files), and again after an engine resource refresh.</summary>
    internal static void Prepare(string moduleRoot) => _root = moduleRoot;
}

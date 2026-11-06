using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.TwoDimension;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.Campaign
{
    // This cache owns its textures. They are deliberately not registered in RuntimeSprites or its lifetime list.
    internal static class EncyclopediaPictureCache
    {
        internal const int Capacity = 12;

        private sealed class PictureSprite : Sprite
        {
            public override Texture Texture { get; }

            public PictureSprite(string name, Texture texture)
                : base(name, texture.Width, texture.Height, SpriteNinePatchParameters.Empty)
            {
                Texture = texture;
            }

            public override Vec2 GetMinUvs() => Vec2.Zero;
            public override Vec2 GetMaxUvs() => Vec2.One;
        }

        private sealed class Picture
        {
            internal TaleWorlds.Engine.Texture EngineTexture;
            internal Sprite Sprite;
            internal int Users;
            internal long LastUsed;
        }

        private static readonly Dictionary<string, Picture> Pictures = new Dictionary<string, Picture>(StringComparer.OrdinalIgnoreCase);
        private static long _clock;
        private static readonly Lazy<JObject> PictureMap = new Lazy<JObject>(() =>
        {
            string file = Path.Combine(CampaignPresentation.ModuleRoot, "ModuleData", "FactionUI", "encyclopedia_pictures.json");
            return File.Exists(file) ? JObject.Parse(File.ReadAllText(file)) : new JObject();
        });

        internal static string ResolveSettlement(string id, string culture, string kind)
        {
            var map = PictureMap.Value;
            string fallback = (string)map["default"] ?? "generic_settlement.png";
            string directory = Path.GetFullPath(Path.Combine(CampaignPresentation.ModuleRoot, "GUI", "FactionUI", "EncyclopediaPictures"));
            string file = (string)map["settlements"]?[id]
                ?? (string)map["cultures"]?[culture]?[kind]
                ?? (string)map["types"]?[kind] ?? fallback;
            string path = Path.GetFullPath(Path.Combine(directory, file));
            if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Encyclopedia picture must be inside GUI/EncyclopediaPictures.");
            if (File.Exists(path)) return path;
            path = Path.GetFullPath(Path.Combine(directory, fallback));
            if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Encyclopedia fallback picture must be inside GUI/EncyclopediaPictures.");
            return path;
        }

        internal static Sprite Acquire(string file)
        {
            if (!Pictures.TryGetValue(file, out var picture))
            {
                var texture = TaleWorlds.Engine.Texture.LoadTextureFromPath(Path.GetFileName(file), Path.GetDirectoryName(file));
                if (texture == null) return null;
                var uiTexture = new TaleWorlds.TwoDimension.Texture(new EngineTexture(texture));
                picture = new Picture
                {
                    EngineTexture = texture,
                    Sprite = new PictureSprite("encyclopedia-picture:" + file, uiTexture)
                };
                Pictures.Add(file, picture);
                CampaignPresentation.LogInfo("Encyclopedia picture loaded: " + Path.GetFileName(file));
            }
            picture.Users++;
            picture.LastUsed = ++_clock;
            Trim();
            return picture.Sprite;
        }

        internal static void Release(string file)
        {
            if (!Pictures.TryGetValue(file, out var picture)) return;
            picture.Users--;
            picture.LastUsed = ++_clock;
            Trim();
        }

        private static void Trim()
        {
            while (Pictures.Count > Capacity)
            {
                var oldest = Pictures.Where(x => x.Value.Users == 0).OrderBy(x => x.Value.LastUsed).FirstOrDefault();
                if (oldest.Key == null) return; // A visible preview keeps its texture pinned.
                Pictures.Remove(oldest.Key);
                oldest.Value.EngineTexture.Release();
            }
        }

        internal static void ReleaseAll()
        {
            foreach (var picture in Pictures.Values) picture.EngineTexture.Release();
            Pictures.Clear();
        }
    }
}

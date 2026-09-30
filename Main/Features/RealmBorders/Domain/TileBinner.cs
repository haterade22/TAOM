using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Groups painted quads into square map tiles, one mesh per tile, by the tile holding each quad's
/// centre. <see cref="ContentHash"/> lets a refresh rebuild only the tiles whose quads changed, so a
/// capture touches the few tiles around the fief instead of the whole map.
/// </summary>
public static class TileBinner
{
    public static Dictionary<(int X, int Y), List<BorderQuad>> Bin(IEnumerable<BorderQuad> quads, float tileSize)
    {
        if (quads == null)
            throw new ArgumentNullException(nameof(quads));
        if (!(tileSize > 0f))
            throw new ArgumentOutOfRangeException(nameof(tileSize));

        var tiles = new Dictionary<(int X, int Y), List<BorderQuad>>();
        foreach (var quad in quads)
        {
            float cx = (quad.NearStart.Position.X + quad.FarStart.Position.X + quad.FarEnd.Position.X + quad.NearEnd.Position.X) / 4f;
            float cy = (quad.NearStart.Position.Y + quad.FarStart.Position.Y + quad.FarEnd.Position.Y + quad.NearEnd.Position.Y) / 4f;
            var key = ((int)Math.Floor(cx / tileSize), (int)Math.Floor(cy / tileSize));
            if (!tiles.TryGetValue(key, out var list))
                tiles[key] = list = new List<BorderQuad>();
            list.Add(quad);
        }
        return tiles;
    }

    /// <summary>FNV-1a over the quads' positions (to 1/100 unit), colours and UVs, in order.</summary>
    public static ulong ContentHash(IReadOnlyList<BorderQuad> quads)
    {
        if (quads == null)
            throw new ArgumentNullException(nameof(quads));
        ulong hash = 14695981039346656037UL;
        void Mix(long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }
        }

        void MixVertex(BorderVertex v)
        {
            Mix((long)Math.Round(v.Position.X * 100f));
            Mix((long)Math.Round(v.Position.Y * 100f));
            Mix(v.Colour);
            Mix((long)Math.Round(v.U * 1000f));
            Mix((long)Math.Round(v.V * 1000f));
        }

        foreach (var q in quads)
        {
            MixVertex(q.NearStart);
            MixVertex(q.FarStart);
            MixVertex(q.FarEnd);
            MixVertex(q.NearEnd);
        }
        return hash;
    }
}

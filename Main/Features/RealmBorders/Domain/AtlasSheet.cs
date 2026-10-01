using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// The parchment map laid over the whole map at full zoom-out: a grid of quads over the terrain square,
/// split into tiles so no one mesh grows large, each corner carrying the texture coordinate of its map
/// position. The engine counts a texture's rows from the bottom (seen in game 2026-10-01: the first mapping
/// drew the picture upside down), so a corner at (x, y) samples (x / size, y / size) and the picture's top
/// row lands on the map's north edge.
/// </summary>
public static class AtlasSheet
{
    /// <summary>The TAOM_Map terrain square the parchment art covers: 16 nodes of 100 units.</summary>
    public const float TerrainSize = 1600f;

    /// <param name="colour">Every corner's colour: the ink underlay's (the banner material ignores it for the paper).</param>
    public static List<IReadOnlyList<BorderQuad>> Tiles(float size, int tilesPerSide, int cellsPerTile, uint colour)
    {
        int cellsPerSide = tilesPerSide * cellsPerTile;
        float cell = size / cellsPerSide;
        BorderVertex Corner(int column, int row) =>
            new BorderVertex(new MapPoint(column * cell, row * cell), colour, (float)column / cellsPerSide, (float)row / cellsPerSide);

        var tiles = new List<IReadOnlyList<BorderQuad>>(tilesPerSide * tilesPerSide);
        for (int tileRow = 0; tileRow < tilesPerSide; tileRow++)
        {
            for (int tileColumn = 0; tileColumn < tilesPerSide; tileColumn++)
            {
                var quads = new List<BorderQuad>(cellsPerTile * cellsPerTile);
                for (int r = 0; r < cellsPerTile; r++)
                {
                    int row = tileRow * cellsPerTile + r;
                    for (int c = 0; c < cellsPerTile; c++)
                    {
                        int column = tileColumn * cellsPerTile + c;
                        quads.Add(new BorderQuad(Corner(column, row), Corner(column, row + 1),
                            Corner(column + 1, row + 1), Corner(column + 1, row)));
                    }
                }
                tiles.Add(quads);
            }
        }
        return tiles;
    }

    /// <summary>
    /// The sheet's opacity at a camera distance: nothing below <paramref name="start"/> of the furthest
    /// zoom, full from <paramref name="full"/> of it, linear between. A distance, limit or band that is not a
    /// usable number shows nothing, so the sheet never covers the map by mistake.
    /// </summary>
    public static float Alpha(float cameraDistance, float maxDistance, float start, float full)
    {
        if (!FiniteFloatValidator.IsFinite(cameraDistance) || !(cameraDistance >= 0f)
            || !FiniteFloatValidator.IsFinite(maxDistance) || !(maxDistance > 0f)
            || !FiniteFloatValidator.IsFinite(start) || !FiniteFloatValidator.IsFinite(full) || !(full > start))
            return 0f;
        float t = (cameraDistance / maxDistance - start) / (full - start);
        if (!(t > 0f))
            return 0f;
        return t >= 1f ? 1f : t;
    }
}

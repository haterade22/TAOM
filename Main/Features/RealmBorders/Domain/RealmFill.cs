using System;
using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Colours each realm's land between its borders: a flat, translucent tint in the realm's colour on a
/// grid of quads <see cref="Stride"/> cells apart, draped like the borders. The tint fades out over the
/// cells next to another realm, wild land or water: beside another realm its cell-stepped edge stays under
/// the border's own wash and the smooth line carries the edge; beside wild land and water, where no line is
/// drawn, the fade itself is the edge. A quad takes one colour: a corner of another realm, or of
/// none, fades to nothing in that colour, so two realms' tints never blend and no dark fringe appears.
/// </summary>
public static class RealmFill
{
    /// <summary>Cells between fill vertices: about 6 world units on TAOM_Map, fine enough to follow the hills.</summary>
    public const int Stride = 2;

    /// <summary>Depth (cells from the realm's edge) at which the tint reaches full strength.</summary>
    public const float FadeCells = 1.5f;

    /// <param name="groupOfProvince">The map-mode group of each province; null is not tinted.</param>
    /// <param name="colours">Each tinted group's colour; a group not listed is not tinted.</param>
    /// <param name="alpha">The tint's strength, 0 to 1.</param>
    public static void Paint(ProvinceMap map, IReadOnlyList<string?> groupOfProvince, IReadOnlyDictionary<string, uint> colours,
        float alpha, List<BorderQuad> into)
    {
        if (map == null)
            throw new ArgumentNullException(nameof(map));
        if (groupOfProvince == null)
            throw new ArgumentNullException(nameof(groupOfProvince));
        if (colours == null)
            throw new ArgumentNullException(nameof(colours));
        if (into == null)
            throw new ArgumentNullException(nameof(into));
        if (!(alpha > 0f))
            return;
        alpha = Math.Min(1f, alpha);

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        var colourOfId = new List<uint>();
        var idOfProvince = new int[groupOfProvince.Count];
        for (int province = 0; province < idOfProvince.Length; province++)
        {
            string? group = groupOfProvince[province];
            if (group == null || !colours.TryGetValue(group, out uint colour))
            {
                idOfProvince[province] = -1;
                continue;
            }
            if (!ids.TryGetValue(group, out int id))
            {
                ids[group] = id = colourOfId.Count;
                colourOfId.Add(colour);
            }
            idOfProvince[province] = id;
        }

        int columns = map.Columns, rows = map.Rows;
        var cells = new int[columns * rows];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int province = map[c, r];
                cells[r * columns + c] = province >= 0 && province < idOfProvince.Length ? idOfProvince[province] : -1;
            }
        }
        float[] depth = RealmLabelPlacer.Depths(cells, columns, rows);

        int vertexColumns = (columns - 1) / Stride + 1, vertexRows = (rows - 1) / Stride + 1;
        var vertexId = new int[vertexColumns * vertexRows];
        var vertexAlpha = new float[vertexColumns * vertexRows];
        for (int j = 0; j < vertexRows; j++)
        {
            for (int i = 0; i < vertexColumns; i++)
            {
                int cell = j * Stride * columns + i * Stride;
                int v = j * vertexColumns + i;
                vertexId[v] = cells[cell];
                vertexAlpha[v] = cells[cell] < 0 ? 0f : alpha * Math.Min(1f, depth[cell] / FadeCells);
            }
        }

        float size = map.CellSize;
        MapPoint Position(int i, int j) =>
            new MapPoint(map.MinX + (i * Stride + 0.5f) * size, map.MinY + (j * Stride + 0.5f) * size);

        var corners = new int[4];
        for (int j = 0; j + 1 < vertexRows; j++)
        {
            for (int i = 0; i + 1 < vertexColumns; i++)
            {
                corners[0] = j * vertexColumns + i;
                corners[1] = j * vertexColumns + i + 1;
                corners[2] = (j + 1) * vertexColumns + i + 1;
                corners[3] = (j + 1) * vertexColumns + i;
                int best = corners[0];
                foreach (int corner in corners)
                {
                    if (vertexAlpha[corner] > vertexAlpha[best])
                        best = corner;
                }
                if (!(vertexAlpha[best] > 0f))
                    continue;

                int id = vertexId[best];
                uint colour = colourOfId[id];
                BorderVertex Vertex(int corner, int ci, int cj) =>
                    new BorderVertex(Position(ci, cj), BorderPainter.WithAlpha(colour, vertexId[corner] == id ? vertexAlpha[corner] : 0f), 0f, 0f);
                into.Add(new BorderQuad(
                    Vertex(corners[0], i, j),
                    Vertex(corners[1], i + 1, j),
                    Vertex(corners[2], i + 1, j + 1),
                    Vertex(corners[3], i, j + 1)));
            }
        }
    }
}

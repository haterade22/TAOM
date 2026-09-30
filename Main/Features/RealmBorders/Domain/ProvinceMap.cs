using System;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// The fixed partition of the land into provinces, one per fief, on the same cells as the
/// <see cref="TerrainGrid"/> it was grown from. It never changes during a campaign: ownership
/// changes only which province edges are drawn.
/// </summary>
public sealed class ProvinceMap
{
    public const int Unclaimed = -1;

    private readonly int[] _labels;

    public ProvinceMap(int columns, int rows, float minX, float minY, float cellSize, int[] labels)
    {
        if (columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(rows));
        if (!(cellSize > 0f))
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (labels == null || labels.Length != columns * rows)
            throw new ArgumentException($"expected {columns * rows} labels", nameof(labels));

        Columns = columns;
        Rows = rows;
        MinX = minX;
        MinY = minY;
        CellSize = cellSize;
        _labels = labels;
    }

    public int Columns { get; }

    public int Rows { get; }

    public float MinX { get; }

    public float MinY { get; }

    public float CellSize { get; }

    /// <summary>The province of a cell; <see cref="Unclaimed"/> outside the map.</summary>
    public int this[int column, int row] =>
        (uint)column < (uint)Columns && (uint)row < (uint)Rows ? _labels[row * Columns + column] : Unclaimed;

    /// <summary>The province under a world position; <see cref="Unclaimed"/> outside the map.</summary>
    public int ProvinceAt(float x, float y)
    {
        double column = Math.Floor((x - MinX) / CellSize);
        double row = Math.Floor((y - MinY) / CellSize);
        // A positive requirement: every comparison is false for NaN, so a NaN position is unclaimed
        // rather than reaching the int cast below (int.MinValue on net472).
        if (!(column >= 0 && row >= 0 && column < Columns && row < Rows))
            return Unclaimed;
        return _labels[(int)row * Columns + (int)column];
    }
}

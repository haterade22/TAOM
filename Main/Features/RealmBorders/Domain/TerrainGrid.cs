using System;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// The campaign map sampled into square cells. Column 0 is the west edge and row 0 the south edge
/// (<see cref="MinX"/>, <see cref="MinY"/>), so a cell's world position grows with its indices.
/// </summary>
public sealed class TerrainGrid
{
    private readonly TerrainClass[] _cells;

    public TerrainGrid(int columns, int rows, float minX, float minY, float cellSize, TerrainClass[] cells)
    {
        if (columns <= 0)
            throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0)
            throw new ArgumentOutOfRangeException(nameof(rows));
        if (!(cellSize > 0f))
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (cells == null || cells.Length != columns * rows)
            throw new ArgumentException($"expected {columns * rows} cells", nameof(cells));

        Columns = columns;
        Rows = rows;
        MinX = minX;
        MinY = minY;
        CellSize = cellSize;
        _cells = cells;
    }

    public int Columns { get; }

    public int Rows { get; }

    public float MinX { get; }

    public float MinY { get; }

    public float CellSize { get; }

    public TerrainClass this[int column, int row] => _cells[row * Columns + column];

    internal TerrainClass At(int index) => _cells[index];
}

using System;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// A cell corner of a <see cref="ProvinceMap"/>: column 0..Columns, row 0..Rows. Corner (c, r) sits at
/// world (MinX + c * CellSize, MinY + r * CellSize), so boundaries are exact and shared by both sides.
/// </summary>
public readonly struct LatticePoint : IEquatable<LatticePoint>
{
    public LatticePoint(int column, int row)
    {
        Column = column;
        Row = row;
    }

    public int Column { get; }

    public int Row { get; }

    public bool Equals(LatticePoint other) => Column == other.Column && Row == other.Row;

    public override bool Equals(object? obj) => obj is LatticePoint other && Equals(other);

    public override int GetHashCode() => unchecked(Column * 397 ^ Row);

    public override string ToString() => $"({Column}, {Row})";
}

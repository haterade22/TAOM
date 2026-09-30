namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// One point a province grows from: the fief itself or one of its villages, all sharing the fief's
/// province index. <see cref="HeadStart"/> is subtracted from the seed's starting cost, so a town
/// reaches further than a castle and a castle further than a village.
/// </summary>
public readonly struct ProvinceSeed
{
    public ProvinceSeed(int province, float x, float y, float headStart)
    {
        Province = province;
        X = x;
        Y = y;
        HeadStart = headStart;
    }

    public int Province { get; }

    public float X { get; }

    public float Y { get; }

    public float HeadStart { get; }
}

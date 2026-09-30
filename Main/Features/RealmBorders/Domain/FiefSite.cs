using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// A town or castle as the territory model sees it: where it stands, whether it is a town (a longer
/// reach than a castle), and where its villages stand, which seed the same province.
/// </summary>
public sealed class FiefSite
{
    public FiefSite(string id, MapPoint position, bool isTown, IReadOnlyList<MapPoint> villages)
    {
        Id = id;
        Position = position;
        IsTown = isTown;
        Villages = villages;
    }

    public string Id { get; }

    public MapPoint Position { get; }

    public bool IsTown { get; }

    public IReadOnlyList<MapPoint> Villages { get; }
}

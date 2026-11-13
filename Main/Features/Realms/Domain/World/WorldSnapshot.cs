using System.Collections.Generic;

namespace TAOM.Features.Realms.Domain.World;

public enum FiefKind
{
    Town,
    Castle,
    Village,
}

/// <summary>A kingdom as the game shows it when titles are generated.</summary>
public sealed class KingdomInfo
{
    public KingdomInfo(string id, string name, string? cultureId, string? rulerHeroId)
    {
        Id = id;
        Name = name;
        CultureId = cultureId;
        RulerHeroId = rulerHeroId;
    }

    public string Id { get; }
    public string Name { get; }
    public string? CultureId { get; }
    public string? RulerHeroId { get; }
}

/// <summary>A town, castle or village as the game shows it when titles are generated.</summary>
public sealed class FiefInfo
{
    public FiefInfo(
        string id, string name, FiefKind kind, string? kingdomId, string? ownerHeroId,
        string? boundFiefId, float x, float y, string? cultureId)
    {
        Id = id;
        Name = name;
        Kind = kind;
        KingdomId = kingdomId;
        OwnerHeroId = ownerHeroId;
        BoundFiefId = boundFiefId;
        X = x;
        Y = y;
        CultureId = cultureId;
    }

    public string Id { get; }
    public string Name { get; }
    public FiefKind Kind { get; }

    /// <summary>The owner clan's kingdom, or null for independent clans.</summary>
    public string? KingdomId { get; }

    public string? OwnerHeroId { get; }

    /// <summary>For villages: the town or castle the village belongs to.</summary>
    public string? BoundFiefId { get; }

    public float X { get; }
    public float Y { get; }
    public string? CultureId { get; }
}

public sealed class WorldSnapshot
{
    public WorldSnapshot(IReadOnlyList<KingdomInfo> kingdoms, IReadOnlyList<FiefInfo> fiefs)
    {
        Kingdoms = kingdoms;
        Fiefs = fiefs;
    }

    public IReadOnlyList<KingdomInfo> Kingdoms { get; }
    public IReadOnlyList<FiefInfo> Fiefs { get; }
}

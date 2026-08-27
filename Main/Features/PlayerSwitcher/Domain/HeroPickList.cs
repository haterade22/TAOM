using System.Collections.Generic;

namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// The three groups the picker renders, already ordered, de-duplicated and filtered.
/// Groups are never null; an empty one renders its "none" text.
/// </summary>
public readonly struct HeroPickList
{
    private static readonly IReadOnlyList<HeroPickRow> NoRows = new HeroPickRow[0];

    public HeroPickList(
        IReadOnlyList<HeroPickRow> rulingHouse,
        IReadOnlyList<HeroPickRow> clanLeaders,
        IReadOnlyList<HeroPickRow> wanderers)
    {
        RulingHouse = rulingHouse ?? NoRows;
        ClanLeaders = clanLeaders ?? NoRows;
        Wanderers = wanderers ?? NoRows;
    }

    public IReadOnlyList<HeroPickRow> RulingHouse { get; }
    public IReadOnlyList<HeroPickRow> ClanLeaders { get; }
    public IReadOnlyList<HeroPickRow> Wanderers { get; }

    public static HeroPickList Empty => new HeroPickList(NoRows, NoRows, NoRows);

    public int TotalCount => RulingHouse.Count + ClanLeaders.Count + Wanderers.Count;

    public bool IsEmpty => TotalCount == 0;
}

using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.Realms.Domain.Succession;

/// <summary>A hero as succession needs to see them. Dead heroes are included so a line can pass through them.</summary>
public sealed class HeroInfo
{
    public HeroInfo(
        string id, bool isAlive, bool isFemale, float age, string? clanId,
        string? fatherId = null, string? motherId = null, IReadOnlyList<string>? childIds = null)
    {
        Id = id;
        IsAlive = isAlive;
        IsFemale = isFemale;
        Age = age;
        ClanId = clanId;
        FatherId = fatherId;
        MotherId = motherId;
        ChildIds = childIds ?? new string[0];
    }

    public string Id { get; }
    public bool IsAlive { get; }
    public bool IsFemale { get; }
    public float Age { get; }
    public string? ClanId { get; }
    public string? FatherId { get; }
    public string? MotherId { get; }
    public IReadOnlyList<string> ChildIds { get; }
}

/// <summary>The deceased's family tree and clan, read once from the game at the moment of death.</summary>
public sealed class FamilySnapshot
{
    private readonly Dictionary<string, HeroInfo> _heroes;

    public FamilySnapshot(IEnumerable<HeroInfo> heroes)
    {
        _heroes = new Dictionary<string, HeroInfo>();
        foreach (var hero in heroes)
            _heroes[hero.Id] = hero;
    }

    public HeroInfo? Get(string? id) => id != null && _heroes.TryGetValue(id, out var hero) ? hero : null;

    public IEnumerable<HeroInfo> ClanMembers(string? clanId) =>
        clanId == null ? Enumerable.Empty<HeroInfo>() : _heroes.Values.Where(h => h.ClanId == clanId);
}

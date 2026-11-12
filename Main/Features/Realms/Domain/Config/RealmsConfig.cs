using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Laws;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Config;

/// <summary>
/// Everything ModuleData/realms_titles.xml can set. <see cref="Defaults"/> is used when the file is
/// missing, and each value the file leaves out keeps its default.
/// </summary>
public sealed class RealmsConfig
{
    public bool IncludeVillages { get; set; } = true;

    /// <summary>Target size of an automatically grouped duchy. Below 2 no automatic duchies are made.</summary>
    public int CountiesPerDuchy { get; set; } = 3;

    public TitleLaws DefaultLaws { get; set; } = TitleLaws.Default;

    /// <summary>Laws for a kingdom's realm title, by kingdom id. Every title under it follows them.</summary>
    public Dictionary<string, TitleLaws> KingdomLaws { get; } = new();

    /// <summary>A realm's capital settlement, by kingdom id: its ruler keeps it as their seat when granting titles.</summary>
    public Dictionary<string, string> KingdomCapitals { get; } = new();

    public List<HighTitleConfig> HighTitles { get; } = new();

    public List<DuchyConfig> Duchies { get; } = new();

    public RankNameTable RankNames { get; } = RankNameTable.CreateDefault();

    public ClaimsConfig Claims { get; } = new();

    /// <summary>Grants, revocations, the chronicle and the weekly effects of titles.</summary>
    public FeudalConfig Feudal { get; } = new();

    /// <summary>Kingdoms whose realm title starts vacant although the realm has a ruler (Gondor's Stewards rule for an absent king).</summary>
    public HashSet<string> VacantThrones { get; } = new(StringComparer.Ordinal);

    public List<UnionConfig> Unions { get; } = new();

    /// <summary>Realm titles of no kingdom of the game, formed over another title's lands (Khazad-dûm over Moria).</summary>
    public List<TitularRealmConfig> TitularRealms { get; } = new();

    /// <summary>
    /// The name a kingdom takes while its ruler's highest title is this one, by title id ("u_reunited": "Reunited
    /// Kingdom"), from <c>realm_name</c>. A title without one lends its own name.
    /// </summary>
    public Dictionary<string, string> RealmNames { get; } = new(StringComparer.Ordinal);

    public List<StartingClaimConfig> StartingClaims { get; } = new();

    public List<CoronationConfig> Coronations { get; } = new();

    public List<OfficeConfig> Offices { get; } = new();

    public static RealmsConfig Defaults() => new();
}

/// <summary>A title above several kingdoms, for example Arnor. Starts vacant unless one hero holds every member kingdom.</summary>
public sealed class HighTitleConfig
{
    public HighTitleConfig(string id, string name, IReadOnlyList<string> memberKingdomIds, TitleLaws? laws)
    {
        Id = id;
        Name = name;
        MemberKingdomIds = memberKingdomIds;
        Laws = laws;
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<string> MemberKingdomIds { get; }
    public TitleLaws? Laws { get; }
}

/// <summary>
/// A realm title (rank Kingdom, id k_ + id) that no kingdom of the game has: Khazad-dûm, which the orcs of Moria hold
/// as their own realm. It has no de jure lands of its own; it is formed, or pressed by a claim, by holding more than half
/// of the counties below <see cref="LandsTitleId"/>, and only by a hero of one of <see cref="FormingCultureIds"/> (none
/// listed: anyone). It sits under <see cref="LiegeTitleId"/> and starts vacant. A save made before it existed gets it
/// on its next load.
/// </summary>
public sealed class TitularRealmConfig
{
    public TitularRealmConfig(string id, string name, string? cultureId, string? liegeTitleId, string landsTitleId,
        IReadOnlyList<string> formingCultureIds, TitleLaws? laws)
    {
        Id = id;
        Name = name;
        CultureId = cultureId;
        LiegeTitleId = liegeTitleId;
        LandsTitleId = landsTitleId;
        FormingCultureIds = formingCultureIds;
        Laws = laws;
    }

    public string Id { get; }

    /// <summary>The title id: <c>k_</c> + <see cref="Id"/>.</summary>
    public string TitleId => "k_" + Id;

    public string Name { get; }

    /// <summary>The culture its rank name and holder style follow ("erebor": "Mansion of Khazad-dûm").</summary>
    public string? CultureId { get; }

    /// <summary>The title above it ("e_durins_folk"); null or missing: none.</summary>
    public string? LiegeTitleId { get; }

    /// <summary>The title whose counties it is formed from ("k_mistymountainorcs").</summary>
    public string LandsTitleId { get; }

    public IReadOnlyList<string> FormingCultureIds { get; }
    public TitleLaws? Laws { get; }

    public bool Allows(string? cultureId) =>
        FormingCultureIds.Count == 0 || (cultureId != null && FormingCultureIds.Contains(cultureId));
}

/// <summary>
/// A hand-made duchy: these counties (by town id) are grouped before automatic grouping runs. Its baronies
/// (by castle id) join the nearest of its counties, or sit directly under the duchy when it has none.
/// </summary>
public sealed class DuchyConfig
{
    public DuchyConfig(string id, string name, string kingdomId, IReadOnlyList<string> countySettlementIds,
        IReadOnlyList<string>? baronySettlementIds = null)
    {
        Id = id;
        Name = name;
        KingdomId = kingdomId;
        CountySettlementIds = countySettlementIds;
        BaronySettlementIds = baronySettlementIds ?? new string[0];
    }

    public string Id { get; }
    public string Name { get; }
    public string KingdomId { get; }
    public IReadOnlyList<string> CountySettlementIds { get; }
    public IReadOnlyList<string> BaronySettlementIds { get; }
}

/// <summary>
/// Display templates per rank, for example "{=realms_rank_county}County of {NAME}", with per-culture overrides and
/// prefix rules (<see cref="TitlePrefixRule"/>) that name the titles of one realm, region or settlement their own way,
/// and holder styles per rank and culture ("Aran {NAME}" for an elven king). The game layer turns the template into a
/// TextObject; this table only chooses it.
/// </summary>
public sealed class RankNameTable
{
    private readonly Dictionary<TitleRank, string> _defaults = new();
    private readonly Dictionary<string, Dictionary<TitleRank, string>> _cultures = new();
    private readonly List<TitlePrefixRule> _prefixes = new();
    private readonly Dictionary<TitleRank, (string Male, string? Female)> _holderDefaults = new();
    private readonly Dictionary<string, Dictionary<TitleRank, (string Male, string? Female)>> _holderCultures = new();
    private readonly HashSet<string> _plainHolderCultures = new(StringComparer.Ordinal);

    /// <summary>The prefix rules in file order.</summary>
    public IReadOnlyList<TitlePrefixRule> Prefixes => _prefixes;

    public void AddPrefix(TitlePrefixRule rule) => _prefixes.Add(rule);

    public static RankNameTable CreateDefault()
    {
        var table = new RankNameTable();
        table.SetDefault(TitleRank.Lordship, "{=realms_rank_lordship}Lordship of {NAME}");
        table.SetDefault(TitleRank.Barony, "{=realms_rank_barony}Barony of {NAME}");
        table.SetDefault(TitleRank.County, "{=realms_rank_county}County of {NAME}");
        table.SetDefault(TitleRank.Duchy, "{=realms_rank_duchy}Duchy of {NAME}");
        table.SetDefault(TitleRank.Kingdom, "{=realms_rank_kingdom}Kingdom of {NAME}");
        table.SetDefault(TitleRank.Empire, "{=realms_rank_empire}High Kingship of {NAME}");
        table.SetDefault(TitleRank.Union, "{=realms_rank_union}Reunited Kingdom of {NAME}");
        return table;
    }

    public void SetDefault(TitleRank rank, string template) => _defaults[rank] = template;

    public void SetForCulture(string cultureId, TitleRank rank, string template)
    {
        if (!_cultures.TryGetValue(cultureId, out var ranks))
            _cultures[cultureId] = ranks = new Dictionary<TitleRank, string>();
        ranks[rank] = template;
    }

    public string Template(TitleRank rank, string? cultureId)
    {
        if (cultureId != null && _cultures.TryGetValue(cultureId, out var ranks) && ranks.TryGetValue(rank, out var own))
            return own;
        return _defaults.TryGetValue(rank, out var template) ? template : "{NAME}";
    }

    /// <summary>
    /// The template for one title: the best prefix rule that matches it (highest priority, then the later one in the
    /// file), else its culture's rank name, else the default rank name. <paramref name="catalog"/> supplies the titles
    /// above it for <c>under</c> conditions; without it those rules never match.
    /// </summary>
    public string Template(TitleDefinition title, TitleCatalog? catalog) =>
        BestPrefix(title, catalog)?.Template ?? Template(title.Rank, title.CultureId);

    /// <summary>
    /// How the holder of this title is styled: the holder style of the prefix rule that names it, when that rule sets
    /// one and the holder is of the title's own culture, else the holder style of the title's rank in the holder's culture
    /// (<see cref="HolderTemplate(TitleRank, string?, bool)"/>): an orc who takes Dol Amroth is not its Prince, and a
    /// Rohirrim who holds a Gondorian fief is styled the way the Rohirrim style a lord. Without
    /// <paramref name="holderCultureId"/> the title's culture is used.
    /// </summary>
    public string? HolderTemplate(TitleDefinition title, TitleCatalog? catalog, string? holderCultureId, bool isFemale)
    {
        var culture = holderCultureId ?? title.CultureId;
        var rule = BestPrefix(title, catalog);
        if (rule?.HolderMale == null || (title.CultureId != null && culture != title.CultureId))
            return HolderTemplate(title.Rank, culture, isFemale);
        var template = isFemale ? rule.HolderFemale : rule.HolderMale;
        return string.IsNullOrEmpty(template) ? null : template;
    }

    /// <summary>
    /// The title a hero is known by, of the titles they hold by right: the highest, unless one of them is named by a prefix
    /// rule with a holder style and <c>known_over</c> at least that rank, and that style applies to this holder (their own
    /// culture, as in <see cref="HolderTemplate(TitleDefinition, TitleCatalog?, string?, bool)"/>). Of several such titles
    /// the highest wins; ties go to the lower id, as with the highest title. Null when they hold none.
    /// </summary>
    public TitleDefinition? KnownBy(IEnumerable<TitleDefinition> held, TitleCatalog? catalog, string? holderCultureId)
    {
        var ordered = held.OrderByDescending(t => t.Rank).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();
        if (ordered.Count == 0)
            return null;
        var highest = ordered[0].Rank;
        foreach (var title in ordered)
        {
            var culture = holderCultureId ?? title.CultureId;
            if (BestPrefix(title, catalog)?.KnownOver is { } over && over >= highest
                && (title.CultureId == null || culture == title.CultureId))
                return title;
        }

        return ordered[0];
    }

    private TitlePrefixRule? BestPrefix(TitleDefinition title, TitleCatalog? catalog)
    {
        IReadOnlyCollection<string>? above = null;
        IReadOnlyCollection<string> Above() => above ??= catalog != null
            ? new HashSet<string>(catalog.AncestorsOf(title.Id).Select(t => t.Id), StringComparer.Ordinal)
            : Array.Empty<string>();

        TitlePrefixRule? best = null;
        foreach (var rule in _prefixes)
            if ((best == null || rule.Priority >= best.Priority) && rule.Matches(title, Above))
                best = rule;
        return best;
    }

    public void SetHolderDefault(TitleRank rank, string male, string? female) => _holderDefaults[rank] = (male, female);

    public void SetHolderForCulture(string cultureId, TitleRank rank, string male, string? female)
    {
        if (!_holderCultures.TryGetValue(cultureId, out var ranks))
            _holderCultures[cultureId] = ranks = new Dictionary<TitleRank, (string, string?)>();
        ranks[rank] = (male, female);
    }

    /// <summary>The titles of this culture take no default holder style, only the ones the culture sets itself.</summary>
    public void SetHoldersPlain(string cultureId) => _plainHolderCultures.Add(cultureId);

    /// <summary>
    /// How the holder of a title of this rank and culture is styled, for example "{=...}Aran {NAME}" with the hero's name
    /// for {NAME}; null when the holder has no style (the plain name is shown). A culture entry for the rank wins over the
    /// default entry as a whole: when it sets no female form, a female holder has no style rather than the default's.
    /// A culture marked plain (<see cref="SetHoldersPlain"/>) never takes the default.
    /// </summary>
    public string? HolderTemplate(TitleRank rank, string? cultureId, bool isFemale)
    {
        if (!(cultureId != null && _holderCultures.TryGetValue(cultureId, out var ranks) && ranks.TryGetValue(rank, out var style))
            && ((cultureId != null && _plainHolderCultures.Contains(cultureId)) || !_holderDefaults.TryGetValue(rank, out style)))
            return null;
        var template = isFemale ? style.Female : style.Male;
        return string.IsNullOrEmpty(template) ? null : template;
    }
}

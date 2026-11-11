using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Config;

/// <summary>
/// One &lt;Prefix&gt; of realms_titles.xml: a display template ("{=key}Fief of {NAME}") for the titles that meet every
/// condition it sets. A condition left out matches any title. Conditions read the de jure hierarchy fixed when the
/// titles were made, so a name does not change when a fief changes hands:
/// <list type="bullet">
/// <item><c>rank</c>: the title's rank;</item>
/// <item><c>title</c>: the title's own id ("c_town_EW5"), to bind a name to one title;</item>
/// <item><c>settlement</c>: the settlement a lordship, barony or county stands for ("town_EW5");</item>
/// <item><c>kingdom</c>: the realm the title was made in (its own id for a realm title), for a realm's vassals;</item>
/// <item><c>under</c>: a title id anywhere above it, for the vassals of one duchy or high title;</item>
/// <item><c>culture</c>: the culture the title was made with.</item>
/// </list>
/// Each condition takes one value or several separated by spaces. Of the rules that match, the highest priority wins;
/// on equal priority the one later in the file wins. A rule may also style the holder (<c>holder</c>, <c>holder_female</c>).
/// </summary>
public sealed class TitlePrefixRule
{
    private static readonly IReadOnlyCollection<string> Any = Array.Empty<string>();

    public TitlePrefixRule(
        string template, int priority = 0,
        IEnumerable<TitleRank>? ranks = null, IEnumerable<string>? titleIds = null, IEnumerable<string>? settlementIds = null,
        IEnumerable<string>? kingdomIds = null, IEnumerable<string>? underIds = null, IEnumerable<string>? cultureIds = null,
        string? holderMale = null, string? holderFemale = null, TitleRank? knownOver = null)
    {
        Template = template;
        KnownOver = holderMale != null ? knownOver : null;
        HolderMale = holderMale;
        HolderFemale = holderFemale;
        Priority = priority;
        Ranks = ranks?.ToList() ?? new List<TitleRank>();
        TitleIds = Set(titleIds);
        SettlementIds = Set(settlementIds);
        KingdomIds = Set(kingdomIds);
        UnderIds = Set(underIds);
        CultureIds = Set(cultureIds);
    }

    public string Template { get; }
    public int Priority { get; }
    public IReadOnlyCollection<TitleRank> Ranks { get; }
    public IReadOnlyCollection<string> TitleIds { get; }
    public IReadOnlyCollection<string> SettlementIds { get; }
    public IReadOnlyCollection<string> KingdomIds { get; }
    public IReadOnlyCollection<string> UnderIds { get; }
    public IReadOnlyCollection<string> CultureIds { get; }

    /// <summary>
    /// How the holder of a title this rule names is styled ("{=...}Prince {NAME}"), or null to use the rank and culture
    /// holder style. When it is set, a female holder takes <see cref="HolderFemale"/>, or her plain name without it.
    /// </summary>
    public string? HolderMale { get; }
    public string? HolderFemale { get; }

    /// <summary>
    /// With a holder style: the highest rank of title over which the holder is still known by this one ("Prince Imrahil"
    /// while he also holds the Duchy of Belfalas). Null: the holder is known by their highest title as usual.
    /// </summary>
    public TitleRank? KnownOver { get; }

    /// <summary>True when the rule sets no condition at all, so it names every title of every rank.</summary>
    public bool IsUnconditional =>
        Ranks.Count == 0 && TitleIds.Count == 0 && SettlementIds.Count == 0 && KingdomIds.Count == 0 && UnderIds.Count == 0 && CultureIds.Count == 0;

    /// <param name="ancestorIds">The ids of every title above this one; only asked for when the rule has <c>under</c>.</param>
    public bool Matches(TitleDefinition title, Func<IReadOnlyCollection<string>> ancestorIds)
    {
        if (Ranks.Count > 0 && !Ranks.Contains(title.Rank))
            return false;
        if (!Allows(TitleIds, title.Id) || !Allows(SettlementIds, title.SettlementId)
            || !Allows(KingdomIds, title.KingdomId) || !Allows(CultureIds, title.CultureId))
            return false;
        if (UnderIds.Count == 0)
            return true;
        var above = ancestorIds();
        return UnderIds.Any(above.Contains);
    }

    private static bool Allows(IReadOnlyCollection<string> values, string? value) =>
        values.Count == 0 || (value != null && values.Contains(value));

    private static IReadOnlyCollection<string> Set(IEnumerable<string>? values)
    {
        var set = new HashSet<string>(values?.Where(v => !string.IsNullOrWhiteSpace(v)) ?? Any, StringComparer.Ordinal);
        return set.Count == 0 ? Any : set;
    }
}

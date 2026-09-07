using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// The alignment-compatible candidate-clan pools the AI partner-search draw picks from, cached per
/// culture and rebuilt when <see cref="MarriageClanPoolStamp"/> says the campaign has moved on.
/// </summary>
/// <remarks>
/// Lives outside <c>Patch81_MarriageClanDraw</c> so the patch stays a transpiler plus a thin
/// delegation (ADR-002). It handles <c>Clan</c> directly, which is correct for a boundary helper:
/// the adapter rule (ADR-007) bans sealed TaleWorlds types in SERVICES, and the actual alignment
/// decision is already delegated to <see cref="IMarriageAlignmentService"/>, which sees only
/// culture id strings.
/// <para>
/// The cache is process-static because the patch that uses it is. Everything that makes that safe
/// is in the stamp: a second campaign in the same process, clans created or eliminated, and a new
/// day each force a clear.
/// </para>
/// </remarks>
public static class MarriageClanPoolCache
{
    private static readonly Dictionary<string, MBReadOnlyList<Clan>> PoolByCulture = new();
    private static readonly MarriageClanPoolStamp Stamp = new();

    /// <summary>
    /// The clans a <paramref name="cultureId"/> clan may marry into, or <c>null</c> when the caller
    /// should fall back to the unfiltered <paramref name="all"/>. Null rather than an empty list is
    /// deliberate: <c>MBRandom.RandomInt(0)</c> returns 0 and vanilla's indexer would then throw, so
    /// an empty pool must never reach the draw.
    /// </summary>
    public static MBReadOnlyList<Clan>? GetOrBuild(
        string cultureId, MBReadOnlyList<Clan> all, IMarriageAlignmentService service)
    {
        if (Stamp.ShouldInvalidate(Campaign.Current?.UniqueGameId, all.Count, (int)CampaignTime.Now.ToDays))
        {
            PoolByCulture.Clear();
        }
        else if (PoolByCulture.TryGetValue(cultureId, out var cached))
        {
            return cached;
        }

        var filtered = new MBReadOnlyList<Clan>();
        for (var i = 0; i < all.Count; i++)
        {
            var other = all[i];
            if (other == null) continue;
            // Pass the possibly-null culture id straight through: the service resolves it to
            // Neutral, which is compatible, so a culture-less clan stays as reachable as in vanilla.
            if (service.AreCulturesCompatible(cultureId, other.Culture?.StringId))
                filtered.Add(other);
        }

        if (filtered.Count == 0) return null;

        PoolByCulture[cultureId] = filtered;
        return filtered;
    }
}

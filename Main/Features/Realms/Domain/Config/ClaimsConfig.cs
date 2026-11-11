using System.Collections.Generic;
using TAOM.Features.Realms.Domain.Titles;

namespace TAOM.Features.Realms.Domain.Config;

/// <summary>
/// Prices and limits for buying and pressing claims, from the &lt;Claims&gt; element of realms_titles.xml.
/// A rank without a buy price cannot be claimed by purchase; a rank without a usurp cost cannot be usurped.
/// </summary>
public sealed class ClaimsConfig
{
    /// <summary>Price of a bought claim in the buyer's own special resource (TAOM's Castar, Marks, Gems and so on).</summary>
    public Dictionary<TitleRank, int> BuyPrices { get; } = new()
    {
        [TitleRank.Barony] = 40,
        [TitleRank.County] = 80,
        [TitleRank.Duchy] = 200,
        [TitleRank.Kingdom] = 500,
    };

    /// <summary>Influence it costs to take a claimed title by right.</summary>
    public Dictionary<TitleRank, int> UsurpInfluence { get; } = new()
    {
        [TitleRank.Lordship] = 10,
        [TitleRank.Barony] = 50,
        [TitleRank.County] = 100,
        [TitleRank.Duchy] = 200,
        [TitleRank.Kingdom] = 400,
        [TitleRank.Empire] = 800,
        [TitleRank.Union] = 1200,
    };

    /// <summary>
    /// When a hero dies, each living child gains an ancestral claim on the dead parent's titles of this rank or higher
    /// that the child did not inherit, and inherits the parent's own ancestral claims.
    /// </summary>
    public TitleRank AncestralMinRank { get; set; } = TitleRank.County;

    /// <summary>How many of the nearest towns and castles count as bordering each of the buyer's own.</summary>
    public int Neighbors { get; set; } = 5;

    /// <summary>Relation change with the holder when someone buys a claim on their title.</summary>
    public int BuyRelation { get; set; } = -10;

    /// <summary>Relation change with the holder when their title is usurped.</summary>
    public int UsurpRelation { get; set; } = -20;

    /// <summary>Whether AI clan leaders buy claims (in gold) and usurp, on the weekly tick.</summary>
    public bool AiEnabled { get; set; } = true;

    /// <summary>Chance per week that an AI clan leader looks for a claim to buy.</summary>
    public float AiChance { get; set; } = 0.05f;

    /// <summary>Gold an AI pays per point of the buy price (AI heroes have no special resource balance).</summary>
    public int AiGoldPerPoint { get; set; } = 100;

    /// <summary>An AI only spends when it has this many times the cost, so claims never empty its purse or influence.</summary>
    public float AiReserve { get; set; } = 2f;

    /// <summary>Claim prices as a percentage of <see cref="BuyPrices"/> (the MCM's Claim Price). 100 keeps the file's prices.</summary>
    public int BuyPricePercent { get; set; } = 100;

    /// <summary>
    /// Influence costs to usurp, form and restore as a percentage of the file's (the MCM's Title Influence Cost). 100
    /// keeps them.
    /// </summary>
    public int InfluencePercent { get; set; } = 100;

    public int? BuyPrice(TitleRank rank) =>
        BuyPrices.TryGetValue(rank, out var price) && price > 0 ? RealmsPercent.Of(price, BuyPricePercent) : null;

    public int? UsurpCost(TitleRank rank) =>
        UsurpInfluence.TryGetValue(rank, out var cost) && cost >= 0 ? ScaleInfluence(cost) : null;

    /// <summary>An influence cost from the file (a coronation's own price) at <see cref="InfluencePercent"/>.</summary>
    public int ScaleInfluence(int cost) => RealmsPercent.Of(cost, InfluencePercent);
}

/// <summary>A price or payment from realms_titles.xml at a percentage set in the mod options.</summary>
public static class RealmsPercent
{
    /// <summary>
    /// <paramref name="value"/> at <paramref name="percent"/>, rounded. A positive value stays at least 1 unless the
    /// percentage is 0, so a cut price never becomes free by rounding.
    /// </summary>
    public static int Of(int value, int percent)
    {
        if (value <= 0 || percent == 100)
            return value;
        if (percent <= 0)
            return 0;
        return System.Math.Max(1, (int)System.Math.Round(value * (percent / 100.0), System.MidpointRounding.AwayFromZero));
    }
}

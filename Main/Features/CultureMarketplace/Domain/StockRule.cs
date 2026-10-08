using System.Text.RegularExpressions;

namespace TAOM.Features.CultureMarketplace.Domain;

/// <summary>Which items a <see cref="StockRule"/> takes.</summary>
public enum StockKind
{
    Any,
    Armour,
    Weapons,

    /// <summary>Everything but character armour: weapons, shields, mounts, harness, goods.</summary>
    NotArmour,
}

/// <summary>
/// One &lt;Stock&gt; row of a culture's market override: items this culture's markets carry beyond its own.
/// <see cref="From"/> limits the candidates to one culture's items (null: every item, tagged or not),
/// <see cref="Kind"/> to character armour, weapons or everything but armour, <see cref="Match"/> to ids the pattern finds.
/// Rows apply in file order and the first to add an item sets its draw <see cref="Weight"/>.
/// </summary>
public sealed class StockRule
{
    public string From { get; }
    public StockKind Kind { get; }
    public Regex Match { get; }
    public float Weight { get; }

    public StockRule(string from, StockKind kind, Regex match, float weight)
    {
        From = from;
        Kind = kind;
        Match = match;
        Weight = weight;
    }
}

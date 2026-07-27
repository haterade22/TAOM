using System;
using System.Collections.Generic;

namespace TAOM.Features.MenuLinkColors;

/// <summary>
/// The set of cultures that have a hand-authored LOTR link style in TAOM's
/// <c>GameMenu.InfoText</c> brush, and the naming convention that ties the two together.
///
/// This class holds NO colors — the palette lives only in
/// <c>Main/_Module/GUI/Brushes/GameMenu.xml</c>. It holds only the ids, because emitting a style
/// name the brush does not define is a silent failure: <c>Brush.GetStyleOrDefault</c> falls back
/// to the <c>Default</c> style with no error, so the link would render as ordinary black body
/// text. <c>MenuLinkBrushCoverageTests</c> pins this set against the brush XML and
/// <c>taom_spcultures.xml</c> so the two cannot drift.
///
/// The eight bandit cultures are deliberately absent — they rarely own a settlement, and leaving
/// them out means their links keep vanilla's colors rather than losing color entirely.
/// </summary>
public static class TaomCultureLinkStyles
{
    /// <summary>Prefix for every TAOM-authored link style. Matches the brush XML style names.</summary>
    public const string StylePrefix = "Link.Taom.";

    private static readonly HashSet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        // Free peoples
        "gondor",
        "rivendell",
        "lothlorien",
        "mirkwood",
        "erebor",
        "vlandia",   // Rohirrim
        "sturgia",   // Barding
        "empire",    // Dunlendings
        "battania",  // Variag

        // Men of the East and South
        "khuzait",   // Easterlings
        "aserai",    // Haradrim
        "abanissa",
        "shaghana",

        // Sauron's dominion
        "mordor",
        "isengard",
        "gundabad",
        "dolguldur",
        "goblin",
        "mistymountainorcs",
        "umbar"
    };

    /// <summary>Every culture StringId that has an authored link style.</summary>
    public static IEnumerable<string> SupportedCultureIds => Supported;

    /// <summary>
    /// True when <paramref name="cultureId"/> has an authored brush style. Ordinal, case-sensitive:
    /// culture StringIds are lowercase in <c>taom_spcultures.xml</c>, and a case variant would
    /// produce a style name the brush XML never defines.
    /// </summary>
    public static bool IsSupported(string? cultureId) =>
        !string.IsNullOrWhiteSpace(cultureId) && Supported.Contains(cultureId!);

    /// <summary>Builds the brush style name for a culture. Caller must check <see cref="IsSupported"/>.</summary>
    public static string StyleNameFor(string cultureId) => StylePrefix + cultureId;
}

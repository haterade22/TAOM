using System.Collections.Generic;

namespace TAOM.Features.UncapturableHeroes.Domain;

/// <summary>
/// Deserialization target for <c>uncapturable_heroes/uncapturable_heroes_config.json</c>.
///
/// Deserialized with <c>ObjectCreationHandling.Replace</c> so a JSON list REPLACES the compiled
/// default instead of Json.NET's append-merge (which would leave every compiled entry in place
/// alongside the author's, so an author who lists one hero silently gets theirs PLUS Sauron).
/// </summary>
public class UncapturableHeroesConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Named compiled hero sets that can never be taken prisoner. Only
    /// <c>nazgul_nine</c> is known, resolving to <see cref="NazgulFamily.INazgulRegistry"/>.
    /// Unknown names are skipped and warned, never coerced.
    ///
    /// This axis is load-bearing, because the Nine are NOT reachable by race. Verified against
    /// TAOM data on 2026-08-26: six of them (<c>lord_1_15</c>, <c>lord_1_155</c>, <c>lord_1_16</c>,
    /// <c>lord_1_28</c>, <c>lord_1_38</c>, <c>lord_1_48</c>) carry no <c>race</c> attribute in
    /// <c>lords.xslt</c> at all, so they inherit vanilla race 0 (human); the other three
    /// (<c>lord_1_48_1/_2/_3</c>) are <c>race="uruk"</c> in <c>characters/lords.xml</c>. A
    /// race-keyed list would free six of the Nine, and adding <c>uruk</c> to catch the other three
    /// would protect every uruk lord in the game.</summary>
    public List<string> HeroSets { get; set; } = new List<string> { "nazgul_nine" };

    /// <summary>Individual hero StringIds that can never be taken prisoner. <c>lord_1_17</c> is
    /// Sauron; he is listed here as well as via <see cref="UncapturableRaces"/> so the feature
    /// survives a future data change that drops his race attribute.</summary>
    public List<string> HeroIds { get; set; } = new List<string> { "lord_1_17" };

    /// <summary>The default rule: any hero whose FaceGen race is named here is uncapturable
    /// without being listed by id. On shipped data <c>sauron</c> matches exactly one hero, because
    /// <c>lord_1_17</c> is the only character in the mod carrying that race. Names are validated
    /// lazily against <c>IRaceManager.IsValidRaceName</c>, because the FaceGen registry is not
    /// populated at provider construction.</summary>
    public List<string> UncapturableRaces { get; set; } = new List<string> { "sauron" };

    /// <summary>Hero StringIds handed back to vanilla capture. Evaluated FIRST, so it beats the
    /// rule and both include lists. Empty on shipped data.</summary>
    public List<string> ExcludeHeroIds { get; set; } = new List<string>();

    /// <summary>Whether to write a line to the campaign message feed when one of these heroes
    /// escapes a capture the player could have seen. Escapes elsewhere in the world are always
    /// silent regardless of this flag.</summary>
    public bool AnnounceEscape { get; set; } = true;
}

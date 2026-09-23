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
    /// This axis is load-bearing: it names exactly the Nine whatever their race data says. When
    /// the feature shipped (2026-08-26) no race could reach them: six (<c>lord_1_15</c>,
    /// <c>lord_1_155</c>, <c>lord_1_16</c>, <c>lord_1_28</c>, <c>lord_1_38</c>, <c>lord_1_48</c>)
    /// carried no <c>race</c> attribute, so they were vanilla race 0 (human), and the other three
    /// (<c>lord_1_48_1/_2/_3</c>) were <c>race="uruk"</c>. Since #644 all nine are
    /// <c>race="nazghul"</c>, which <see cref="UncapturableRaces"/> could name, but the shipped
    /// rule lists only <c>sauron</c>.</summary>
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

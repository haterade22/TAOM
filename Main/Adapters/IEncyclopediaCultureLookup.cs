namespace TAOM.Adapters;

/// <summary>
/// Resolves an encyclopedia link — the <c>href="event:..."</c> payload the engine embeds in
/// hyperlink markup — to the culture StringId of the object it points at.
///
/// Wraps <c>MBObjectManager</c> plus the sealed <c>Settlement</c> / <c>Hero</c> / <c>Kingdom</c> /
/// <c>Clan</c> types so services never touch them directly (ADR-007).
/// </summary>
public interface IEncyclopediaCultureLookup
{
    /// <summary>
    /// Resolves a link of the form <c>"&lt;ElementName&gt;-&lt;StringId&gt;"</c> (e.g.
    /// <c>"Settlement-town_ES1"</c>, <c>"Hero-lord_1_1"</c>, <c>"Kingdom-gondor"</c>,
    /// <c>"Faction-clan_xyz"</c>) to the owning object's culture StringId.
    /// Returns <c>null</c> when the link is malformed, the object cannot be resolved, or the
    /// resolved object exposes no culture.
    /// </summary>
    string? GetCultureId(string? encyclopediaLink);
}

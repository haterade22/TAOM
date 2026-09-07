namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// Free/Evil marriage rule. All three members key on CULTURE StringIds (the keys in
/// <c>execution/alignment.json</c>), not kingdom ids: culture is stable across a hero's career,
/// is present even on a clanless hero, and is what the lore rule is actually about. Every culture
/// that seeds a lord is classified, which <c>ShippedCultureAlignmentCoverageTests</c> pins.
/// </summary>
public interface IMarriageAlignmentService
{
    /// <summary>
    /// True when the pairing must be refused. A null / unknown culture id resolves to Neutral and
    /// never blocks. <paramref name="involvesPlayerClan"/> lets the player and AI halves be
    /// disabled independently.
    /// </summary>
    bool IsMarriageBlocked(string? cultureIdA, string? cultureIdB, bool involvesPlayerClan);

    /// <summary>
    /// The bare side rule, with no enable/apply toggles: false only when one culture is Free and the
    /// other Evil. Used to narrow the AI's partner-clan draw, where the toggles are already checked
    /// once via <see cref="ShouldSteerAiPartnerSearch"/>.
    /// </summary>
    bool AreCulturesCompatible(string? cultureIdA, string? cultureIdB);

    /// <summary>
    /// True when the AI partner-clan draw should be narrowed. False when the feature is off, the AI
    /// half is off, or steering is disabled, in which case the draw stays vanilla.
    /// </summary>
    bool ShouldSteerAiPartnerSearch { get; }
}

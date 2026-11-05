namespace TAOM.Features.Execution;

public interface IAlignmentService
{
    FactionSide GetKingdomSide(string kingdomId);

    /// <summary>
    /// Resolves a Free/Evil/Neutral side for a culture StringId, using the same id→side table as
    /// <see cref="GetKingdomSide"/>. Most TAOM culture ids equal their kingdom id (or appear in the
    /// table directly); the two custom mismatches — Gondor culture <c>gondor</c> (kingdom
    /// <c>empire_w</c>) and Mordor culture <c>mordor</c> (kingdom <c>empire_s</c>) — carry explicit
    /// <c>gondor</c>/<c>mordor</c> entries in <c>execution/alignment.json</c>. Unknown/bandit cultures
    /// resolve to Neutral. Used by the AlignmentDesertion feature to side a troop by its culture.
    /// </summary>
    FactionSide GetCultureSide(string cultureId);

    /// <summary>
    /// Side of a participant identified by faction id first (a kingdom id, or a clan id), falling back to
    /// culture id when the id does not classify. Catches the cases where <see cref="GetKingdomSide"/> alone
    /// reads Neutral and would silently disable alignment logic: a kingdom-less hero (independent or
    /// enlisted player, a minor clan leader serving no kingdom, a victim whose clan was destroyed by the
    /// kill that is being evaluated) and a player-founded kingdom whose id is absent from alignment.json.
    /// CaravanTrade and WarOfTheRingMomentum keep private copies of this rule; each pairs a faction id with
    /// that faction's own culture.
    /// <para>
    /// The fallback fires when the id is unlisted OR listed Neutral, so pass the culture of the SAME
    /// entity the id names (a faction with its own culture, a clanless hero with the hero's own). A
    /// kingdom paired with another entity's culture sides a Neutral or player-founded owner as that
    /// entity. alignment.json lists no clan ids on this line, so a clan that serves no kingdom is sided
    /// by the clan's own culture.
    /// </para>
    /// <para>
    /// Callers hold the pair as a <see cref="SideIds"/> built by <c>TAOM.Adapters.FactionSides</c> (the
    /// only place that zips engine ids) and resolve it with the
    /// <c>ResolveSide(this IAlignmentService, SideIds)</c> extension (#957).
    /// </para>
    /// </summary>
    FactionSide ResolveSide(string kingdomId, string cultureId);

    bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB);
    bool AreSameAlignment(string kingdomIdA, string kingdomIdB);

    /// <summary>Same semantics as the string overload, for sides already resolved via <see cref="ResolveSide"/>.</summary>
    bool AreEnemyAlignments(FactionSide sideA, FactionSide sideB);

    /// <summary>Same semantics as the string overload, for sides already resolved via <see cref="ResolveSide"/>.</summary>
    bool AreSameAlignment(FactionSide sideA, FactionSide sideB);
}

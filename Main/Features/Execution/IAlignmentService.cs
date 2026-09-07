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
    /// Side of a participant identified by kingdom id first, falling back to culture id when the
    /// kingdom does not classify. Catches the cases where <see cref="GetKingdomSide"/> alone reads
    /// Neutral and would silently disable alignment logic: a kingdom-less hero (independent or
    /// enlisted player, minor/mercenary clan leader, a victim whose clan was destroyed by the kill
    /// that is being evaluated) and a player-founded kingdom whose id is absent from alignment.json.
    /// Mirrors the private <c>ResolveSide</c> helpers in CaravanTrade, WarOfTheRingMomentum and
    /// PrisonerRecruitment.
    /// </summary>
    FactionSide ResolveSide(string kingdomId, string cultureId);

    bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB);
    bool AreSameAlignment(string kingdomIdA, string kingdomIdB);

    /// <summary>Same semantics as the string overload, for sides already resolved via <see cref="ResolveSide"/>.</summary>
    bool AreEnemyAlignments(FactionSide sideA, FactionSide sideB);

    /// <summary>Same semantics as the string overload, for sides already resolved via <see cref="ResolveSide"/>.</summary>
    bool AreSameAlignment(FactionSide sideA, FactionSide sideB);
}

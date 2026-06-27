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

    bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB);
    bool AreSameAlignment(string kingdomIdA, string kingdomIdB);
}

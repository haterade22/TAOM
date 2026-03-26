namespace TAOM.Features.Execution;

public interface IAlignmentService
{
    FactionSide GetKingdomSide(string kingdomId);
    bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB);
    bool AreSameAlignment(string kingdomIdA, string kingdomIdB);
}

namespace TAOM.Features.Execution.Hooks;

public interface IOnExecutionAction
{
    bool ShouldApplyHonorPenalty(string victimKingdomId, string executorKingdomId);
    bool IsKinslaying(string victimKingdomId, string executorKingdomId);
    int GetRelationModifier(string executorKingdomId, string victimKingdomId, string evaluatorKingdomId, int baseRelationChange);
}

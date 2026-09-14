namespace TAOM.Features.Execution.Hooks;

/// <summary>
/// The alignment rule for executions, consumed by the two Blood Feud seams.
///
/// <para>
/// Both halves take <see cref="ExecutionParticipant"/> values rather than bare kingdom ids. A
/// participant with no kingdom is still placed on a side by culture, so an independent or enlisted
/// player, a mercenary clan leader, or a victim whose clan the kill just destroyed never collapses
/// the calculation back to vanilla's flat penalty. That escape used to exist and it charged every
/// honourable clan leader in the world, which is exactly the Free Peoples.
/// </para>
/// </summary>
public interface IOnExecutionAction
{
    /// <summary>Whether vanilla's Honor and Mercy hit for starting a blood feud should apply.</summary>
    bool ShouldApplyHonorPenalty(ExecutionParticipant victim, ExecutionParticipant executor);

    /// <summary>
    /// The alignment-modified relation change for one third-party clan leader evaluating the kill.
    /// Zero means the engine skips that clan entirely through its own <c>!= 0</c> guard: no hit and
    /// no count in the summary notice, not a smaller hit.
    /// </summary>
    int GetRelationModifier(
        ExecutionParticipant executor,
        ExecutionParticipant victim,
        ExecutionParticipant evaluator,
        int baseRelationDelta);

    /// <summary>
    /// Whether the feud was started against the player by an AI clan executing one of the player's
    /// own kin. The v1.5.x relation seam carries no executor, and on that path vanilla charges the
    /// player's relations with third clans for a kill the player did not commit. The alignment rule
    /// judges the player's own executions and has no verdict there, so the caller leaves vanilla's
    /// number alone. A missing victim clan is not evidence either way and keeps the rule on.
    /// </summary>
    bool IsPlayerTheBereaved(string victimClanId, string playerClanId);
}

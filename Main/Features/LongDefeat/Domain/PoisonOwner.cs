using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// Who owns a poison kill (phase2-plan.md section 4, "A kill must have a living affector"). <c>AgentMoraleInteractionLogic</c> hands every kill's affector to <c>CustomBattleMoraleModel</c>, which reads
/// <c>affectorAgent.Formation</c> with no null test, and <c>Agent.Die</c> swaps the victim's last hitter in for an owner of -1 or the victim itself, an index that may since belong to another agent.
/// So a kill is never sent without a real owner: the poisoner while he stands, else the commander, else any attacker that stands, else nobody and the killing tick is withheld. The adapter passes
/// each candidate as a key only when the registry holds him and he is active at the call; this function only orders them.
/// </summary>
internal static class PoisonOwner
{
    /// <summary>The first usable candidate in order poisoner, commander, attacker; null (withhold) when none is usable. A key that is not a registered agent (0 or below) is never usable.</summary>
    public static AgentKey? Choose(AgentKey? poisoner, AgentKey? commander, AgentKey? attacker)
    {
        if (Usable(poisoner))
            return poisoner;
        if (Usable(commander))
            return commander;
        return Usable(attacker) ? attacker : null;
    }

    private static bool Usable(AgentKey? key) => key is { Value: > 0 };
}

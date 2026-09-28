using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// The engine side of <see cref="CreatureBanditRules.IsCreatureBandit"/>: reads the three facts off an agent in
/// cheapest-first order, because the weapon guards ask it for every agent. <c>Character</c> is a managed field
/// and null for every ordinary mount; <c>IsHuman</c> is a flags-pointer read; only a creature bandit reaches the
/// id lookup. It asks IsHuman, not IsMount: route A clears Mountable once the creature is built, and every guard
/// must keep recognising it after that. Thread-safe (reads only), so the guards on the engine's worker threads
/// may call it.
/// </summary>
internal static class CreatureBanditAgents
{
    internal static bool Is(Agent? agent)
    {
        var characterId = agent?.Character?.StringId;
        return characterId != null
            && CreatureBanditRules.IsCreatureBandit(characterId, agent!.IsHuman, agent.RiderAgent != null);
    }

    /// <summary>The mount lock's question: a creature bandit carries no rider. Counts each refusal for the diagnostics.</summary>
    internal static bool RefusesRider(Agent? mount)
    {
        if (!Is(mount)) return false;
        Diagnostics.CreatureBanditDiag.NoteMountRefused();
        return true;
    }

    /// <summary>
    /// The morale models' question (<c>CanPanicDueToMorale</c>, which <c>CommonAIComponent.CanPanic</c> asks first,
    /// v1.5.3 <c>CommonAIComponent.cs:174-177</c>): a creature bandit never panics. Asked on the engine's worker
    /// threads; the check only reads and the note only counts.
    /// </summary>
    internal static bool RefusesMoralePanic(Agent? agent)
    {
        if (!Is(agent)) return false;
        Diagnostics.CreatureBanditDiag.NoteMoralePanicBlocked(agent!);
        return true;
    }

    /// <summary>
    /// The battle reward model's question (<c>CanTroopBeTakenPrisoner</c>, asked before a defeated troop joins the
    /// winner's prisoners, v1.5.3 <c>MapEvent.cs:1855</c>): a creature troop is never a prisoner. Main thread.
    /// </summary>
    internal static bool RefusesPrisoner(string? troopId)
    {
        if (!CreatureBanditRules.IsCreatureTroop(troopId)) return false;
        Diagnostics.CreatureBroodCampaignDiag.NotePrisonerRefused(troopId!);
        return true;
    }
}

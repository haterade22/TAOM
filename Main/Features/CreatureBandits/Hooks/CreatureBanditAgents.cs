using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureBandits.Hooks;

/// <summary>
/// The engine side of <see cref="CreatureBanditRules.IsCreatureBandit"/>: reads the three facts off an agent in
/// cheapest-first order, because the weapon guards ask it for every agent. <c>Character</c> comes first: a managed
/// field, null for every ordinary mount. <c>IsHuman</c> comes next: one flags read that rules out every soldier and
/// husk rider. Only a non-humanoid agent with a <c>Character</c> reads the troop id and the rider. It asks IsHuman,
/// not IsMount: route A clears Mountable once the creature is built, and every guard
/// must keep recognising it after that. Thread-safe (reads only), so the guards on the engine's worker threads
/// may call it.
/// </summary>
internal static class CreatureBanditAgents
{
    internal static bool Is(Agent? agent)
    {
        var character = agent?.Character;
        if (character == null || agent!.IsHuman)
            return false;
        return CreatureBanditRules.IsCreatureBandit(character.StringId, isHuman: false, agent.RiderAgent != null);
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
    /// winner's prisoners, v1.5.3 <c>MapEvent.cs:1855</c>): a creature troop or a bandit troll (#694) is never a
    /// prisoner. Main thread.
    /// </summary>
    internal static bool RefusesPrisoner(string? troopId)
    {
        if (!CreatureBanditRules.IsNeverPrisoner(troopId)) return false;
        Diagnostics.CreatureBroodCampaignDiag.NotePrisonerRefused(troopId!);
        return true;
    }

    /// <summary>
    /// The battle reward model's freed-prisoner chances without a brood or troll band among the winners (#694: each
    /// stays spiders or trolls only). Vanilla gives a winning bandit party any freed bandit prisoner
    /// (<c>DefaultBattleRewardModel.GetLootPrisonerChances</c>, v1.5.3 lines 253-275); the other winners share its
    /// chance, and with none left the prisoner goes free (<see cref="CreatureBanditRules.WithoutRefusedWinners{T}"/>).
    /// The engine's list comes back untouched when no band won. Main thread (MapEvent's loot pass).
    /// </summary>
    internal static MBReadOnlyList<KeyValuePair<MapEventParty, float>> WithoutCreatureBandWinners(
        MBReadOnlyList<KeyValuePair<MapEventParty, float>> chances)
    {
        var kept = CreatureBanditRules.WithoutRefusedWinners(chances,
            winner => CreatureBanditRules.IsCreatureBandClan(winner.Party.MobileParty?.ActualClan?.StringId));
        return kept == null ? chances : new MBList<KeyValuePair<MapEventParty, float>>(kept);
    }
}

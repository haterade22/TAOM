using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of Patch170. Public members are called directly. <c>MapEventComponent.AfterLoad(MapEvent)</c> is
/// internal, so it is reached by name through <c>AccessTools</c> (gated by ReflectionSiteBindingTests).
/// </summary>
internal sealed class HalfFinishedEncounterAdapter : IHalfFinishedEncounterAdapter
{
    private static readonly MethodInfo? AfterLoad = AccessTools.Method(typeof(MapEventComponent), "AfterLoad", new[] { typeof(MapEvent) });

    public HalfFinishedEncounterSnapshot Read()
    {
        var battle = PlayerEncounter.Battle;
        if (battle == null)
            return new HalfFinishedEncounterSnapshot(false, false, false, 0, 0);

        var component = battle.Component;
        return new HalfFinishedEncounterSnapshot(
            true,
            battle.IsFinalized,
            component != null && component.MapEvent == null,
            battle.Sides[0]?.Parties.Count ?? 0,
            battle.Sides[1]?.Parties.Count ?? 0);
    }

    public void RestoreBackReference()
    {
        var battle = PlayerEncounter.Battle;
        if (AfterLoad == null || battle?.Component == null)
            throw new InvalidOperationException("MapEventComponent.AfterLoad(MapEvent) is not available.");
        AfterLoad.Invoke(battle.Component, new object[] { battle });
    }

    public int EndSide(int sideIndex)
    {
        var side = PlayerEncounter.Battle?.Sides[sideIndex];
        if (side == null)
            return 0;
        int parties = side.Parties.Count;
        if (parties > 0)
            side.HandleMapEventEnd();
        return parties;
    }

    public int CountMarkedAliveHeroes()
    {
        int count = 0;
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero.DeathMark == KillCharacterAction.KillCharacterActionDetail.DiedInBattle)
                count++;
        }

        return count;
    }
}

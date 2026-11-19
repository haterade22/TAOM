using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of Patch170 and the orphaned battle sweep. Public members are called directly.
/// <c>MapEventComponent.AfterLoad(MapEvent)</c> and <c>FinalizeComponent()</c> are internal, so they are reached by
/// name through <c>AccessTools</c> (gated by ReflectionSiteBindingTests).
/// </summary>
internal sealed class HalfFinishedEncounterAdapter : IHalfFinishedEncounterAdapter
{
    private static readonly MethodInfo? AfterLoad = AccessTools.Method(typeof(MapEventComponent), "AfterLoad", new[] { typeof(MapEvent) });
    private static readonly MethodInfo? FinalizeComponent = AccessTools.Method(typeof(MapEventComponent), "FinalizeComponent", Type.EmptyTypes);

    public HalfFinishedEncounterSnapshot Read()
    {
        var battle = PlayerEncounter.Battle;
        return battle == null ? new HalfFinishedEncounterSnapshot(false, false, false, 0, 0) : Read(battle);
    }

    public void RestoreBackReference() => RestoreBackReference(PlayerEncounter.Battle);

    public int EndSide(int sideIndex) => EndSide(PlayerEncounter.Battle, sideIndex);

    public void FinishEvent()
    {
        var battle = PlayerEncounter.Battle;
        if (battle != null)
            FinishEvent(battle);
    }

    /// <summary>The state Patch170 and the orphaned-battle sweep both judge.</summary>
    internal static HalfFinishedEncounterSnapshot Read(MapEvent battle)
    {
        var component = battle.Component;
        return new HalfFinishedEncounterSnapshot(
            true,
            battle.IsFinalized,
            component != null && component.MapEvent == null,
            battle.Sides[0]?.Parties.Count ?? 0,
            battle.Sides[1]?.Parties.Count ?? 0);
    }

    /// <summary>The load step the event missed. Shared with <see cref="OrphanedBattleEventAdapter"/>.</summary>
    internal static void RestoreBackReference(MapEvent? battle)
    {
        if (AfterLoad == null || battle?.Component == null)
            throw new InvalidOperationException("MapEventComponent.AfterLoad(MapEvent) is not available.");
        AfterLoad.Invoke(battle.Component, new object[] { battle });
    }

    /// <summary>The side-end step of vanilla's finish. Shared with <see cref="OrphanedBattleEventAdapter"/>.</summary>
    internal static int EndSide(MapEvent? battle, int sideIndex)
    {
        var side = battle?.Sides[sideIndex];
        if (side == null)
            return 0;
        int parties = side.Parties.Count;
        if (parties > 0)
            side.HandleMapEventEnd();
        return parties;
    }

    /// <summary>
    /// The steps <c>MapEvent.FinalizeEventAux</c> runs after the sides end. Used by Patch170 and <see cref="OrphanedBattleEventAdapter"/>.
    /// After a load the visual is null and the siege and raid components' flags are unsaved, so most of this does
    /// nothing; in the same session it fires what the crash skipped, and a blockade's <c>OnFinalize</c> always runs.
    /// </summary>
    internal static void FinishEvent(MapEvent battle)
    {
        battle.MapEventVisual?.OnMapEventEnd();
        if (battle.Component == null)
            return;
        if (FinalizeComponent == null)
            throw new InvalidOperationException("MapEventComponent.FinalizeComponent() is not available.");
        FinalizeComponent.Invoke(battle.Component, null);
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

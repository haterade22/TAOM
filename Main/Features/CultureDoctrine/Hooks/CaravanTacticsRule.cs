using System;
using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// The one vanilla behavior that adds a tactic outside <c>MissionCombatantsLogic</c>:
/// <c>MissionCaravanOrVillagerTacticsHandler.EarlyStart</c> gives a team a
/// <c>TacticDefensiveLine</c> when the player's map event has a caravan party on that side, or a
/// villager party with no settlement involved, regardless of side or Tactics skill. It runs
/// before the doctrine's <c>EarlyStart</c>, so <c>ClearTacticOptions</c> would discard it. This
/// mirrors its condition so the roster keeps the row. Main thread, once per team.
/// </summary>
public static class CaravanTacticsRule
{
    // By name, not by type: the handler lives in SandBox.dll, and a compile-time reference from
    // this assembly's method bodies trips the test host's type scans. CultureDoctrineBindingTests
    // pins that the type still exists under this name.
    public const string HandlerTypeName = "MissionCaravanOrVillagerTacticsHandler";

    private static readonly IReadOnlyList<DoctrineTactic> DefensiveLine = new[] { DoctrineTactic.DefensiveLine };

    /// <summary>The tactics vanilla would have added for this team beyond the standard set.</summary>
    public static IReadOnlyList<DoctrineTactic>? Ensured(Mission mission, BattleSideEnum side)
    {
        if (!HasHandler(mission))
            return null;
        return VanillaGivesDefensiveLine(side) ? DefensiveLine : null;
    }

    private static bool HasHandler(Mission mission)
    {
        var behaviors = mission.MissionBehaviors;
        for (var i = 0; i < behaviors.Count; i++)
            if (behaviors[i].GetType().Name == HandlerTypeName)
                return true;
        return false;
    }

    private static bool VanillaGivesDefensiveLine(BattleSideEnum side)
    {
        if (Campaign.Current == null)
            return false;
        var mapEvent = MapEvent.PlayerMapEvent;
        if (mapEvent == null)
            return false;
        var parties = mapEvent.PartiesOnSide(side);
        var noSettlement = mapEvent.MapEventSettlement == null;
        for (var i = 0; i < parties.Count; i++)
        {
            var party = parties[i].Party;
            if (party == null || !party.IsMobile)
                continue;
            var mobile = party.MobileParty;
            if (mobile.IsCaravan || (noSettlement && mobile.IsVillager))
                return true;
        }
        return false;
    }
}

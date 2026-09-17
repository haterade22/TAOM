using System;
using System.Collections.Generic;
using TAOM.Features.CultureDoctrine.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CultureDoctrine.Hooks;

/// <summary>
/// Which <c>IBattleCombatant</c>s a team's doctrine is resolved from, and the Tactics skill it
/// registers with. A side normally owns one team, which gets every combatant on the side. The
/// split case is the player's side when the engine created <c>Mission.PlayerAllyTeam</c>: the
/// engine places a troop on the ally team iff its origin is neither under the player's command
/// nor in the player's army (`Mission.GetAgentTeam`, `Mission.cs:5233-5240`), so the same rule,
/// applied per combatant, decides which team a party's troops and culture count toward. The
/// Tactics skill is the SIDE's maximum for both teams, which is what vanilla registers with
/// (`MissionCombatantsLogic.cs:169`). Reads the engine at the boundary and returns the pure
/// struct the domain consumes.
/// </summary>
public static class TeamCombatantSelector
{
    public readonly struct Selection
    {
        public Selection(List<SideCombatant> combatants, int sideTacticsSkill)
        {
            Combatants = combatants;
            SideTacticsSkill = sideTacticsSkill;
        }

        /// <summary>The combatants whose troops fight on this team.</summary>
        public List<SideCombatant> Combatants { get; }

        /// <summary>The best Tactics skill on the whole side.</summary>
        public int SideTacticsSkill { get; }
    }

    public static Selection Select(Mission mission, MissionCombatantsLogic combatants, Team team)
    {
        var playerTeam = mission.PlayerTeam;
        var allyTeam = mission.PlayerAllyTeam;
        var split = allyTeam != null && playerTeam != null && team.Side == playerTeam.Side;
        return Select(combatants.GetAllCombatants(), team.Side, split, teamIsAllyTeam: team == allyTeam, IsInPlayersArmy);
    }

    /// <summary>The rule without the engine objects, so it can be tested with fakes.</summary>
    public static Selection Select(
        IEnumerable<IBattleCombatant> all, BattleSideEnum side, bool splitPlayerSide, bool teamIsAllyTeam,
        Func<IBattleCombatant, bool> isInPlayersArmy)
    {
        var result = new List<SideCombatant>();
        var skill = 0;
        foreach (var combatant in all)
        {
            if (combatant == null || combatant.Side != side)
                continue;
            skill = Math.Max(skill, combatant.GetTacticsSkillAmount());
            if (splitPlayerSide)
            {
                var onAllyTeam = !combatant.IsUnderPlayersCommand(side) && !isInPlayersArmy(combatant);
                if (onAllyTeam != teamIsAllyTeam)
                    continue;
            }
            result.Add(new SideCombatant(
                combatant.BasicCulture?.StringId,
                combatant.GetNumberOfMissionReadyTroops(),
                combatant.GetTacticsSkillAmount()));
        }
        return new Selection(result, skill);
    }

    // Campaign only: a party in the army the player belongs to fights on the player's team even
    // when the player does not command it (an AI-led army the player joined). Custom Battle
    // combatants are not parties and have no army.
    private static bool IsInPlayersArmy(IBattleCombatant combatant)
    {
        // MobileParty.MainParty dereferences Campaign.Current; a Custom Battle has neither.
        if (Campaign.Current == null || !(combatant is PartyBase party))
            return false;
        var army = party.MobileParty?.Army;
        return army != null && army == MobileParty.MainParty?.Army;
    }
}

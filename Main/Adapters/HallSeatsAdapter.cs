using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.HallSeats.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Reads the lord's hall for Hall Seats (docs/features/hall-seats.md). Called from the prefix of
/// <c>MissionAgentHandler.SpawnDefaultLocationCharacter</c> on the first spawn of a <c>SpawnLocationCharacters</c> pass, so
/// <c>LocationCharactersAreReadyToSpawn</c> has already added the guards and musicians, and nothing of this pass has
/// spawned yet. The lord ladder (<c>sp_notable</c>, then <c>npc_common_limited</c>, then <c>npc_common</c>, then a
/// fallback onto one shared point that is never marked used) is vanilla's <c>MissionAgentHandler.SpawnWanderingAgent</c>.
/// The seats are the distinct unused usable machines under those tags (<see cref="HallSeatRules.CountSeats"/>): vanilla
/// lists a machine under every tag its entity carries and one spawn marks the whole machine used. Two private fields are
/// read, <c>MissionConversationLogic._teleportNearCharacter</c> and <c>MissionAgentHandler._usedSpawnPoints</c>, both
/// pinned by <c>Patch148HallSeatsBindingTests</c> and the reflection-sites catalogue.
/// </summary>
internal sealed class HallSeatsAdapter : IHallSeatsAdapter
{
    internal const string LordsHallId = "lordshall";

    internal static readonly string[] LordSpotTags = { "sp_notable", "npc_common_limited", "npc_common" };

    private static readonly AccessTools.FieldRef<MissionConversationLogic, CharacterObject>? TeleportNearCharacter =
        TryFieldRef();

    private static readonly AccessTools.FieldRef<MissionAgentHandler, HashSet<UsableMachine>>? UsedSpawnPoints =
        TryUsedSpawnPoints();

    public HallSeatSnapshot? ReadLordsHall(object missionAgentHandler)
    {
        if (missionAgentHandler is not MissionAgentHandler handler)
            return null;

        var location = CampaignMission.Current?.Location;
        if (location == null || !string.Equals(location.StringId, LordsHallId, StringComparison.Ordinal))
            return null;

        var mission = Mission.Current;
        if (mission == null || Campaign.Current == null || UsedSpawnPoints == null)
            return null;

        var settlement = PlayerEncounter.LocationEncounter?.Settlement ?? Settlement.CurrentSettlement;
        if (settlement == null)
            return null;

        var spawned = new HashSet<IAgentOriginBase>(mission.Agents.Where(a => a.Origin != null).Select(a => a.Origin));
        var talkTarget = ConversationTarget(mission);
        var freeSpots = FreeLordSpots(handler, UsedSpawnPoints(handler));

        var lords = new List<HallSeatCandidate>();
        var takenByOthers = 0;
        foreach (var character in location.GetCharacterList().ToList())
        {
            if (character == null || (character.AgentOrigin != null && spawned.Contains(character.AgentOrigin)))
                continue;

            var hero = character.Character?.HeroObject;
            if (hero != null && hero.IsLord && !character.ForceSpawnInSpecialTargetTag)
            {
                lords.Add(Candidate(character, hero, settlement, talkTarget));
                continue;
            }

            if (LandsOnALordSpot(character, handler))
                takenByOthers++;
        }

        return new HallSeatSnapshot(mission.SceneName, lords, freeSpots, takenByOthers);
    }

    private static int FreeLordSpots(MissionAgentHandler handler, HashSet<UsableMachine> used)
    {
        var perTag = new List<IReadOnlyList<HallSeatMachine>>();
        foreach (var tag in LordSpotTags)
        {
            perTag.Add(handler.GetAllUsablePointsWithTag(tag)
                .Where(machine => !used.Contains(machine))
                .Select(machine => new HallSeatMachine(machine, MissionAgentHandler.GetPointCountOfUsableMachine(machine, checkForUnusedOnes: true)))
                .ToList());
        }

        return HallSeatRules.CountSeats(perTag);
    }

    private static bool LandsOnALordSpot(LocationCharacter character, MissionAgentHandler handler)
    {
        if (character.ForceSpawnInSpecialTargetTag)
            return false;
        var tag = character.SpecialTargetTag;
        if (string.IsNullOrEmpty(tag) || Array.IndexOf(LordSpotTags, tag) >= 0)
            return true;
        // HasUsablePointWithTag only says the tag exists, not that a free machine is left (docs/features/hall-seats.md "Known limits").
        return !handler.HasUsablePointWithTag(tag);
    }

    private static HallSeatCandidate Candidate(LocationCharacter character, Hero hero, Settlement settlement, CharacterObject? talkTarget)
    {
        var clan = hero.Clan;
        var ownerClan = settlement.OwnerClan;
        var quests = Campaign.Current.QuestManager;

        var mustKeep = HallSeatRules.IsMustKeep(
            playerClan: clan != null && clan == Clan.PlayerClan,
            settlementOwner: ownerClan != null && ownerClan.Leader == hero,
            kingdomLeader: hero.IsKingdomLeader,
            governor: hero.GovernorOf != null && hero.GovernorOf == settlement.Town,
            conversationTarget: talkTarget != null && hero.CharacterObject == talkTarget,
            hasIssue: hero.Issue != null,
            questGiver: quests != null && quests.IsQuestGiver(hero),
            questTarget: quests != null && quests.TrackedObjects.ContainsKey(hero));

        return new HallSeatCandidate(
            character,
            hero.StringId,
            mustKeep,
            ownerClan: clan != null && clan == ownerClan,
            clanLeader: clan != null && clan.Leader == hero,
            clanTier: clan?.Tier ?? 0,
            clanRenown: clan?.Renown ?? 0f);
    }

    private static CharacterObject? ConversationTarget(Mission mission)
    {
        var logic = mission.GetMissionBehavior<MissionConversationLogic>();
        return logic == null || TeleportNearCharacter == null ? null : TeleportNearCharacter(logic);
    }

    private static AccessTools.FieldRef<MissionAgentHandler, HashSet<UsableMachine>>? TryUsedSpawnPoints()
    {
        try
        {
            return AccessTools.FieldRefAccess<MissionAgentHandler, HashSet<UsableMachine>>("_usedSpawnPoints");
        }
        catch (Exception)
        {
            // Engine drift: without the used set the free seats are unknown, so the hall is not read and every lord spawns as in vanilla.
            return null;
        }
    }

    private static AccessTools.FieldRef<MissionConversationLogic, CharacterObject>? TryFieldRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<MissionConversationLogic, CharacterObject>("_teleportNearCharacter");
        }
        catch (Exception)
        {
            // Engine drift: without the target the lord the player asked to see is ranked like any other.
            return null;
        }
    }
}

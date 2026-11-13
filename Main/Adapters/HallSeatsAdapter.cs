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
/// spawned yet. The lord ladder (<c>sp_notable</c>, then <c>npc_common_limited</c>, then <c>npc_common</c>, then the first
/// usable point of any tag, never marked used) is vanilla's <c>MissionAgentHandler.SpawnWanderingAgent</c>.
/// The conversation target is the private <c>MissionConversationLogic._teleportNearCharacter</c>, pinned by
/// <c>Patch148HallSeatsBindingTests</c> and the reflection-sites catalogue.
/// </summary>
internal sealed class HallSeatsAdapter : IHallSeatsAdapter
{
    internal const string LordsHallId = "lordshall";

    internal static readonly string[] LordSpotTags = { "sp_notable", "npc_common_limited", "npc_common" };

    private static readonly AccessTools.FieldRef<MissionConversationLogic, CharacterObject>? TeleportNearCharacter =
        TryFieldRef();

    public HallSeatSnapshot? ReadLordsHall(object missionAgentHandler)
    {
        if (missionAgentHandler is not MissionAgentHandler handler)
            return null;

        var location = CampaignMission.Current?.Location;
        if (location == null || !string.Equals(location.StringId, LordsHallId, StringComparison.Ordinal))
            return null;

        var mission = Mission.Current;
        if (mission == null || Campaign.Current == null)
            return null;

        var settlement = PlayerEncounter.LocationEncounter?.Settlement ?? Settlement.CurrentSettlement;
        if (settlement == null)
            return null;

        var spawned = new HashSet<IAgentOriginBase>(mission.Agents.Where(a => a.Origin != null).Select(a => a.Origin));
        var talkTarget = ConversationTarget(mission);
        var freeSpots = FreeLordSpots(handler.FindUnusedUsablePointCount());

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

    internal static int FreeLordSpots(IReadOnlyDictionary<string, int> unused)
    {
        var total = 0;
        foreach (var tag in LordSpotTags)
        {
            if (unused.TryGetValue(tag, out var count) && count > 0)
                total += count;
        }

        return total;
    }

    private static bool LandsOnALordSpot(LocationCharacter character, MissionAgentHandler handler)
    {
        if (character.ForceSpawnInSpecialTargetTag)
            return false;
        var tag = character.SpecialTargetTag;
        if (string.IsNullOrEmpty(tag) || Array.IndexOf(LordSpotTags, tag) >= 0)
            return true;
        return !handler.HasUsablePointWithTag(tag);
    }

    private static HallSeatCandidate Candidate(LocationCharacter character, Hero hero, Settlement settlement, CharacterObject? talkTarget)
    {
        var clan = hero.Clan;
        var ownerClan = settlement.OwnerClan;
        var quests = Campaign.Current.QuestManager;

        var mustKeep = (clan != null && clan == Clan.PlayerClan)
                       || (ownerClan != null && ownerClan.Leader == hero)
                       || hero.IsKingdomLeader
                       || (hero.GovernorOf != null && hero.GovernorOf == settlement.Town)
                       || (talkTarget != null && hero.CharacterObject == talkTarget)
                       || hero.Issue != null
                       || (quests != null && (quests.IsQuestGiver(hero) || quests.TrackedObjects.ContainsKey(hero)));

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

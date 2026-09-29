using Helpers;
using TAOM.Features.CreatureBandits.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// The party steps both creature band spawners share (#692, #694): create a looter-shaped band of the clan near an
/// anchor and tell it to patrol there, and give a band that wandered off its patrol the order back. The steps after
/// creation are vanilla's own for a looter party (v1.5.3 <c>BanditSpawnCampaignBehavior.SpawnLooterParty</c> and
/// <c>InitializeBanditParty</c>, lines 465-476 and 575-582): visual dirty, clan, aggressiveness, trade. No food: a
/// bandit party never eats (<c>DefaultMobilePartyFoodConsumptionModel.cs:87-94</c>). Like vanilla's own bandit spawn
/// (<c>BanditSpawnCampaignBehavior.GetSpawnPositionAroundSettlement</c>, lines 543-564), it retries a point in the player's
/// sight up to 15 times for one outside it (<see cref="OutOfPlayerSight"/>). The anchor becomes the band's home settlement:
/// both spawners re-patrol around it, and the troll spawner counts bands by its kingdom. Main thread only, like every
/// campaign hook.
/// </summary>
internal static class CreatureBandParties
{
    private const int OutOfSightRetries = 15;

    internal static void Spawn(Clan clan, Settlement anchor)
    {
        float radius = CreatureBanditsConfig.SpawnRadiusDays * Campaign.Current.EstimatedAverageBanditPartySpeed * CampaignTime.HoursInDay;
        CampaignVec2 position = OutOfPlayerSight(
            NavigationHelper.FindPointAroundPosition(anchor.GatePosition, MobileParty.NavigationType.Default, radius), radius);
        MobileParty band = BanditPartyComponent.CreateLooterParty(clan.StringId + "_1", clan, anchor, isBossParty: false,
            clan.DefaultPartyTemplate, position);

        band.Party.SetVisualAsDirty();
        band.ActualClan = clan;
        band.Aggressiveness = 1f - 0.2f * MBRandom.RandomFloat;
        band.InitializePartyTrade(0);
        band.SetMovePatrolAroundSettlement(anchor, MobileParty.NavigationType.Default, isTargetingPort: false);
        CreatureBroodCampaignDiag.Spawned(band, anchor, radius);
    }

    // Vanilla's rule, step for step (v1.5.3 BanditSpawnCampaignBehavior.GetSpawnPositionAroundSettlement, 543-564): a
    // point in the player's sight is retried up to 15 times around itself, taking the first reachable, valid one whose
    // path distance from the player is past his sight; if none is, the first point stands.
    private static CampaignVec2 OutOfPlayerSight(CampaignVec2 position, float radius)
    {
        MobileParty? player = MobileParty.MainParty;
        if (player == null) return position;
        float sightSquared = player.SeeingRange * player.SeeingRange;
        if (position.DistanceSquared(player.Position) >= sightSquared) return position;
        for (int i = 0; i < OutOfSightRetries; i++)
        {
            CampaignVec2 retry = NavigationHelper.FindReachablePointAroundPosition(position, MobileParty.NavigationType.Default, radius);
            if (!NavigationHelper.IsPositionValidForNavigationType(retry, MobileParty.NavigationType.Default)) continue;
            float distance = DistanceHelper.FindClosestDistanceFromMobilePartyToPoint(player, retry, MobileParty.NavigationType.Default, out _);
            if (distance * distance > sightSquared) return retry;
        }
        return position;
    }

    internal static int ReturnStraysToPatrol(Clan clan)
    {
        int count = 0;
        foreach (WarPartyComponent component in clan.WarPartyComponents)
        {
            MobileParty party = component.MobileParty;
            Settlement? home = party?.HomeSettlement;
            if (party == null || !CreatureBanditRules.NeedsPatrolOrder(home != null, party.MapEvent != null,
                    party.DefaultBehavior == AiBehavior.PatrolAroundPoint))
                continue;
            string was = party.DefaultBehavior.ToString();
            party.SetMovePatrolAroundSettlement(home!, MobileParty.NavigationType.Default, isTargetingPort: false);
            count++;
            CreatureBroodCampaignDiag.Stray(party, was, home!);
        }
        return count;
    }
}

using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Wraps <c>BanditPartyComponent.CreateLooterParty</c>, <c>MobileParty.SetMove*</c>,
/// <c>DestroyPartyAction</c>, a bounded <c>Settlement.All</c> scan, and the main party's
/// food roster / locator-grid enemy scan for the Enlistment field-duty system.
/// </summary>
public sealed class DutyWorldAdapter : IDutyWorldAdapter
{
    private const string LootersClanId = "looters";
    private const float SpawnOffsetRadius = 2.5f;

    private readonly IModLogger _logger;

    public DutyWorldAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string SpawnLooterParty(string idPrefix, string anchorSettlementId, bool patrolNotEngage)
    {
        try
        {
            var settlement = FindSettlement(anchorSettlementId);
            var clan = Clan.BanditFactions?.FirstOrDefault(c => c.StringId == LootersClanId);
            if (settlement == null || clan == null)
            {
                _logger?.LogWarning($"[Enlistment.Duties] SpawnLooterParty: settlement='{anchorSettlementId}' or looters clan unresolved");
                return null;
            }

            var stringId = idPrefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var spawnPosition = OffsetPosition(settlement.GatePosition, SpawnOffsetRadius);
            var party = BanditPartyComponent.CreateLooterParty(
                stringId, clan, settlement, isBossParty: false, clan.DefaultPartyTemplate, spawnPosition);
            if (party == null)
                return null;

            ApplyAi(party, patrolNotEngage, settlement);
            return party.StringId;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment.Duties] SpawnLooterParty('{idPrefix}', '{anchorSettlementId}') failed: {ex.Message}");
            return null;
        }
    }

    public void SetPartyAi(string partyId, bool engagePlayer, string anchorSettlementId)
    {
        try
        {
            var party = FindParty(partyId);
            var settlement = FindSettlement(anchorSettlementId);
            if (party == null)
                return;
            ApplyAi(party, patrolNotEngage: !engagePlayer, anchorSettlement: settlement);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment.Duties] SetPartyAi('{partyId}') failed: {ex.Message}");
        }
    }

    public void DestroyParty(string partyId)
    {
        try
        {
            var party = FindParty(partyId);
            if (party == null || !party.IsActive)
                return;
            DestroyPartyAction.Apply(null, party);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment.Duties] DestroyParty('{partyId}') failed: {ex.Message}");
        }
    }

    public string FindNearestFriendlySettlement(string commanderHeroId)
        => FindNearestSettlement(commanderHeroId, requireSameFaction: true, villageOnly: false, _logger);

    public string FindNearestFriendlyVillage(string commanderHeroId)
        => FindNearestSettlement(commanderHeroId, requireSameFaction: true, villageOnly: true, _logger);

    public string FindNearestAllySettlement(string commanderHeroId)
        => FindNearestSettlement(commanderHeroId, requireSameFaction: false, villageOnly: false, _logger);

    /// <summary>
    /// Counts exactly what <see cref="ConsumePlayerFood"/> can remove — deliverable
    /// <c>IsFood</c> stacks. Deliberately NOT <c>ItemRoster.TotalFood</c>: vanilla's
    /// property also folds in livestock via <c>item.HorseComponent.MeatCount</c>
    /// (ItemRoster.cs:452), so a player driving cattle read as "has enough food", the
    /// consume step removed nothing, and the delivery completed for free (Codex P2-2).
    /// </summary>
    public int CountPlayerFood()
    {
        try
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null)
                return 0;

            var total = 0;
            foreach (var element in roster)
            {
                if (element.EquipmentElement.Item?.IsFood == true && element.Amount > 0)
                    total += element.Amount;
            }
            return total;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment.Duties] CountPlayerFood failed: {ex.Message}");
            return 0;
        }
    }

    /// <summary>Removes up to <paramref name="amount"/> food and returns how much it actually took.</summary>
    public int ConsumePlayerFood(int amount)
    {
        if (amount <= 0)
            return 0;
        try
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null)
                return 0;

            // Cheapest food first. Deviation from the donor's fixed grain->fish->meat->cheese
            // chain: vanilla DefaultItems only defines Grain/Meat as food (no fish/cheese) —
            // see the feature doc for the noted deviation.
            var remaining = amount;
            var foodElements = roster
                .Where(e => e.EquipmentElement.Item != null && e.EquipmentElement.Item.IsFood && e.Amount > 0)
                .OrderBy(e => e.EquipmentElement.Item.Value)
                .ToList();

            foreach (var element in foodElements)
            {
                if (remaining <= 0)
                    break;
                var take = Math.Min(remaining, element.Amount);
                roster.AddToCounts(element.EquipmentElement, -take);
                remaining -= take;
            }
            return amount - remaining;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment.Duties] ConsumePlayerFood({amount}) failed: {ex.Message}");
            return 0;
        }
    }

    public void GrantPlayerFood(int amount)
    {
        if (amount <= 0)
            return;
        try
        {
            MobileParty.MainParty?.ItemRoster?.AddToCounts(DefaultItems.Grain, amount);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment.Duties] GrantPlayerFood({amount}) failed: {ex.Message}");
        }
    }

    public bool IsEnemyNearPlayer(float radius)
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main?.MapFaction == null)
                return false;

            var data = MobileParty.StartFindingLocatablesAroundPosition(main.Position.ToVec2(), radius);
            for (var party = MobileParty.FindNextLocatable(ref data); party != null; party = MobileParty.FindNextLocatable(ref data))
            {
                if (party == main || party.MapFaction == null)
                    continue;
                if (party.MapFaction.IsAtWarWith(main.MapFaction))
                    return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment.Duties] IsEnemyNearPlayer({radius}) failed: {ex.Message}");
            return false;
        }
    }

    private static void ApplyAi(MobileParty party, bool patrolNotEngage, Settlement anchorSettlement)
    {
        if (!patrolNotEngage)
        {
            var main = MobileParty.MainParty;
            if (main != null)
            {
                party.SetMoveEngageParty(main, MobileParty.NavigationType.Default);
                return;
            }
        }

        if (anchorSettlement != null)
            party.SetMovePatrolAroundSettlement(anchorSettlement, MobileParty.NavigationType.Default, isTargetingPort: false);
        else
            party.SetMoveModeHold();
    }

    private static string FindNearestSettlement(string commanderHeroId, bool requireSameFaction, bool villageOnly, IModLogger logger)
    {
        try
        {
            var commanderParty = Campaign.Current?.CampaignObjectManager?.Find<Hero>(commanderHeroId)?.PartyBelongedTo;
            var faction = commanderParty?.MapFaction;
            if (faction == null)
                return null;

            var origin = commanderParty.Position;
            Settlement best = null;
            var bestDistanceSq = float.MaxValue;

            foreach (var settlement in Settlement.All)
            {
                if (settlement?.MapFaction == null || settlement.IsHideout)
                    continue;
                if (villageOnly && !settlement.IsVillage)
                    continue;

                var sameFaction = settlement.MapFaction == faction;
                if (requireSameFaction && !sameFaction)
                    continue;
                if (!requireSameFaction && (sameFaction || settlement.MapFaction.IsAtWarWith(faction)))
                    continue;

                var distanceSq = origin.DistanceSquared(settlement.Position);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    best = settlement;
                }
            }

            return best?.StringId;
        }
        catch (Exception ex)
        {
            logger?.LogError($"[Enlistment.Duties] FindNearestSettlement('{commanderHeroId}') failed: {ex.Message}");
            return null;
        }
    }

    private static Settlement FindSettlement(string settlementId)
    {
        // Settlement.Find, NOT CampaignObjectManager.Find<Settlement>: CampaignObjectManager
        // registers only MobileParty/Hero/Clan/Kingdom, so Find<Settlement> returns null
        // UNCONDITIONALLY on 1.4.7. This silently broke every spawned hunt duty.
        return string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
    }

    private static MobileParty FindParty(string partyId)
    {
        return string.IsNullOrEmpty(partyId)
            ? null
            : Campaign.Current?.CampaignObjectManager?.Find<MobileParty>(partyId);
    }

    private static CampaignVec2 OffsetPosition(CampaignVec2 origin, float radius)
    {
        var angle = MBRandom.RandomFloat * 2f * (float)Math.PI;
        var offset = new Vec2((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius);
        return origin + offset;
    }

    public float GetPlayerMorale()
    {
        try
        {
            return MobileParty.MainParty?.Morale ?? -1f;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetPlayerMorale failed: {ex.Message}");
            return -1f;
        }
    }

    /// <summary>
    /// Raises only. A serving soldier in a fed, paid company does not brood — but a player who has
    /// EARNED high morale must not be dragged down to a floor by the same call.
    /// </summary>
    public bool RaisePlayerMoraleTo(float floor)
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main == null || !(floor > 0f) || float.IsInfinity(floor))
                return false;
            if (!(main.Morale < floor))
                return false;

            main.RecentEventsMorale += floor - main.Morale;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] RaisePlayerMoraleTo failed: {ex.Message}");
            return false;
        }
    }

    public bool HealPlayerHero(int hitPoints)
    {
        try
        {
            var hero = Hero.MainHero;
            if (hero == null || hitPoints <= 0 || hero.HitPoints >= hero.MaxHitPoints)
                return false;

            // addXp: true matches vanilla — PartyHealCampaignBehavior.HealMemberHeroes passes true
            // on the very path this replaces. Recovering under the company surgeon should train
            // Medicine exactly as recovering in your own party does; without it the two regimes
            // (parked, where we heal, and detached, where vanilla does) would silently differ in
            // whether the day's recovery taught you anything. ~6 XP/day at heal 11, self-limiting.
            hero.Heal(hitPoints, addXp: true);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] HealPlayerHero failed: {ex.Message}");
            return false;
        }
    }
}

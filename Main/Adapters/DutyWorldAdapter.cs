using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// The enlisted soldier's daily upkeep: the main party's food roster, its morale, and the hero's
/// health. Four members, one consumer (<c>EnlistmentDailyService</c>).
///
/// It used to be much larger. Nine members — <c>SpawnLooterParty</c>, <c>SetPartyAi</c>,
/// <c>DestroyParty</c>, the three <c>FindNearest*</c> scans, <c>IsEnemyNearPlayer</c>,
/// <c>ConsumePlayerFood</c> and <c>GetPlayerMorale</c> — existed for the travel-based field-duty
/// model and died with it (2026-08-09). Two of those were already dead before that.
///
/// <c>DestroyParty</c> is worth naming specifically: it carried a re-entrancy guard added hours
/// earlier for #375, because <c>DestroyPartyAction</c> dispatches <c>MobilePartyDestroyed</c>
/// BEFORE it deactivates the party, so a handler calling back in re-entered <c>Apply</c> and
/// recursed to a stack overflow. Deleting the method removes that surface rather than guarding it —
/// nothing in the mod destroys a party any more. Do not reintroduce it without re-reading that RCA.
/// </summary>
public sealed class DutyWorldAdapter : IDutyWorldAdapter
{
    private readonly IModLogger _logger;

    public DutyWorldAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sums <c>IsFood</c> stacks. Deliberately NOT <c>ItemRoster.TotalFood</c>: vanilla's property
    /// also folds in livestock via <c>item.HorseComponent.MeatCount</c> (ItemRoster.cs:452), so a
    /// player driving cattle read as "has enough food" while the stores held none.
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
            _logger?.LogError($"[Enlistment] CountPlayerFood failed: {ex.Message}");
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
            _logger?.LogError($"[Enlistment] GrantPlayerFood({amount}) failed: {ex.Message}");
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

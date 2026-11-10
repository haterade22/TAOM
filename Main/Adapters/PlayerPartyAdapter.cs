using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.TroopWeight;

namespace TAOM.Adapters;

/// <summary>Reads the main hero id and the main party's daily wage, and adds purchased emissary troops to the
/// roster (ADR-007). <c>GrantTroop</c> returns false on any reason a charge must NOT follow — no main party,
/// unresolvable troop id, or an add exception.</summary>
public sealed class PlayerPartyAdapter : IPlayerPartyAdapter
{
    private readonly IModLogger _logger;
    private readonly ITroopWeightService _troopWeight;
    private bool _wageFailureLogged;
    private bool _freeSlotsFailureLogged;

    public PlayerPartyAdapter(IModLogger logger, ITroopWeightService troopWeight)
    {
        _logger = logger;
        _troopWeight = troopWeight;
    }

    public string GetMainHeroId() => Hero.MainHero?.StringId;

    /// <summary>
    /// <c>DefaultClanFinanceModel.CalculatePartyWage</c> charges <c>MobileParty.TotalWage</c>, which asks the
    /// installed <c>PartyWageModel</c>, so any wage rule a model override adds is already in this number.
    /// </summary>
    public int GetMainPartyDailyWage()
    {
        try
        {
            var wage = Hero.MainHero?.PartyBelongedTo?.TotalWage ?? 0;
            _wageFailureLogged = false;
            return wage > 0 ? wage : 0;
        }
        catch (Exception ex)
        {
            if (!_wageFailureLogged)
            {
                _wageFailureLogged = true;
                _logger.LogError($"[Enlistment] PlayerPartyAdapter.GetMainPartyDailyWage failed: {ex.Message}");
            }
            return 0;
        }
    }

    public int GetMainPartyFreeSlots()
    {
        try
        {
            var party = Hero.MainHero?.PartyBelongedTo?.Party;
            var freeSlots = party == null ? 0 : Math.Max(0, party.PartySizeLimit - party.NumberOfAllMembers);
            _freeSlotsFailureLogged = false;
            return freeSlots;
        }
        catch (Exception ex)
        {
            if (!_freeSlotsFailureLogged)
            {
                _freeSlotsFailureLogged = true;
                _logger.LogError($"[EliteEmissary] PlayerPartyAdapter.GetMainPartyFreeSlots failed: {ex.Message}");
            }
            return 0;
        }
    }

    // The main party pays the Troop Weight size penalty (TroopWeightPenaltyScope exempts only garrisons and
    // patrols), so one troop fills its weight in slots while the option is on.
    public float GetPartySlotsPerTroop(string troopId) =>
        (TaomSettings.Instance?.EnableTroopWeight ?? true) ? _troopWeight.GetTroopWeight(troopId) : 1f;

    public bool GrantTroop(string troopId, int count)
    {
        if (string.IsNullOrEmpty(troopId) || count <= 0)
            return false;

        try
        {
            var roster = Hero.MainHero?.PartyBelongedTo?.MemberRoster;
            if (roster == null)
            {
                _logger.LogWarning("[EliteEmissary] GrantTroop: no main-party roster (dead hero / no party)");
                return false;
            }

            var character = MBObjectManager.Instance?.GetObject<CharacterObject>(troopId);
            if (character == null)
            {
                _logger.LogError($"[EliteEmissary] GrantTroop: troop id '{troopId}' did not resolve to a CharacterObject");
                return false;
            }

            roster.AddToCounts(character, count);
            _logger.LogInfo($"[EliteEmissary] GrantTroop: added {count}x {troopId} to the main party");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[EliteEmissary] GrantTroop('{troopId}' x{count}) failed: {ex.Message}");
            return false;
        }
    }
}

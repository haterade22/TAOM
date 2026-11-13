using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Keeps recruiting in step with alignment desertion: a party or garrison is not offered a troop that
/// <see cref="IAlignmentDesertionService"/> would remove on the next daily tick. Used by
/// <c>TaomPrisonerRecruitmentCalculationModel</c> (prisoners), <c>TaomVolunteerModel</c> (volunteers and garrison
/// auto-recruit) and <c>Patch114_AiVolunteerEstimate_Postfix</c> (the AI trip estimate).
/// </summary>
public interface IDesertionRecruitGuard
{
    /// <summary>True when a troop recruited into this party would desert it (lord parties and the main party only).</summary>
    bool WouldDesertFromParty(PartyBase? party, CharacterObject? troop);

    /// <summary>
    /// The volunteer index a hero may recruit up to, capped before the seller's first slot that would desert.
    /// <paramref name="buyerIsPlayer"/> picks the convention: inclusive for the player, an exclusive bound for the AI
    /// (<see cref="DesertionRecruitCap.CapVolunteerIndex"/>).
    /// </summary>
    int CapHeroIndex(Hero? buyerHero, Hero? sellerHero, int vanillaIndex, bool buyerIsPlayer);

    /// <summary>The volunteer slot count a garrison may auto-recruit, capped before the first slot that would desert.</summary>
    int CapGarrisonSlots(Settlement? settlement, Hero? sellerHero, int vanillaCount);

    /// <summary>
    /// The AI trip estimate (volunteer count, average wage) recounted without the volunteers that would desert the
    /// hero; null when none would, so the caller keeps its own estimate.
    /// </summary>
    (int, float)? RecountEstimate(Hero? hero, Settlement? settlement);
}

/// <summary>
/// Engine boundary only: resolves the owner the way <c>AlignmentDesertionBehavior</c> does (the leader's clan for a
/// lord party or the main party, the owner clan for a garrison, co-op skips from <see cref="DesertionOwnerPolicy"/>)
/// and reads culture ids off the troops. The decisions are <see cref="IAlignmentDesertionService.WouldDesert"/> and
/// <see cref="DesertionRecruitCap"/>. A failure logs once and leaves vanilla's answer.
/// </summary>
public sealed class DesertionRecruitGuard : IDesertionRecruitGuard
{
    private readonly IAlignmentDesertionService _service;
    private readonly IPlayerControlQuery _playerControl;
    private readonly IModLogger _logger;
    private bool _logged;

    public DesertionRecruitGuard(IAlignmentDesertionService service, IPlayerControlQuery playerControl, IModLogger logger)
    {
        _service = service;
        _playerControl = playerControl;
        _logger = logger;
    }

    public bool WouldDesertFromParty(PartyBase? party, CharacterObject? troop)
    {
        if (troop == null || troop.IsHero || !_service.IsEnabled)
            return false;
        try
        {
            var mobile = party?.MobileParty;
            // Desertion only runs on lord field parties and the main party (AlignmentDesertionBehavior).
            if (mobile == null || (!mobile.IsLordParty && !mobile.IsMainParty))
                return false;
            return TryOwner(mobile.LeaderHero?.Clan, isGarrison: false, out var kingdomId, out var isPlayerOwned)
                   && _service.WouldDesert(kingdomId, isPlayerOwned, isGarrison: false, troop.Culture?.StringId!);
        }
        catch (Exception ex)
        {
            LogOnce(ex);
            return false;
        }
    }

    public int CapHeroIndex(Hero? buyerHero, Hero? sellerHero, int vanillaIndex, bool buyerIsPlayer)
    {
        if (vanillaIndex < 0 || !_service.IsEnabled)
            return vanillaIndex;
        try
        {
            if (!TryOwner(buyerHero?.Clan, isGarrison: false, out var kingdomId, out var isPlayerOwned))
                return vanillaIndex;
            var first = FirstDeserterSlot(sellerHero, kingdomId, isPlayerOwned, isGarrison: false);
            return DesertionRecruitCap.CapVolunteerIndex(vanillaIndex, first, buyerIsPlayer);
        }
        catch (Exception ex)
        {
            LogOnce(ex);
            return vanillaIndex;
        }
    }

    public int CapGarrisonSlots(Settlement? settlement, Hero? sellerHero, int vanillaCount)
    {
        if (vanillaCount <= 0 || !_service.IsEnabled)
            return vanillaCount;
        try
        {
            if (!TryOwner(settlement?.OwnerClan, isGarrison: true, out var kingdomId, out var isPlayerOwned))
                return vanillaCount;
            var first = FirstDeserterSlot(sellerHero, kingdomId, isPlayerOwned, isGarrison: true);
            return DesertionRecruitCap.CapSlotCount(vanillaCount, first);
        }
        catch (Exception ex)
        {
            LogOnce(ex);
            return vanillaCount;
        }
    }

    public (int, float)? RecountEstimate(Hero? hero, Settlement? settlement)
    {
        if (hero == null || settlement == null || !_service.IsEnabled)
            return null;
        try
        {
            if (!TryOwner(hero.Clan, isGarrison: false, out var kingdomId, out var isPlayerOwned))
                return null;

            // Vanilla's window (AiVisitSettlementBehavior, v1.5.6): 4 slots in its own map faction, 2 elsewhere.
            var slotsConsidered = hero.MapFaction != settlement.MapFaction ? 2 : 4;
            var changed = false;
            var count = 0;
            var wages = 0;
            foreach (var notable in settlement.Notables)
            {
                if (notable == null || !notable.IsAlive)
                    continue;
                var slots = notable.VolunteerTypes;
                if (slots == null)
                    continue;
                var first = FirstDeserterSlot(notable, kingdomId, isPlayerOwned, isGarrison: false);
                var end = DesertionRecruitCap.EstimateSlotEnd(slotsConsidered, slots.Length, first);
                if (end < Math.Min(slotsConsidered, slots.Length))
                    changed = true;
                for (var i = 0; i < end; i++)
                {
                    if (slots[i] == null)
                        continue;
                    count++;
                    wages += Campaign.Current.Models.PartyWageModel.GetCharacterWage(slots[i]);
                }
            }

            if (!changed)
                return null;
            return (count, count > 0 ? wages / count : 0);
        }
        catch (Exception ex)
        {
            LogOnce(ex);
            return null;
        }
    }

    private int FirstDeserterSlot(Hero? seller, string kingdomId, bool isPlayerOwned, bool isGarrison)
    {
        var slots = seller?.VolunteerTypes;
        if (slots == null)
            return DesertionRecruitCap.NoDeserter;
        var cultures = new string?[slots.Length];
        for (var i = 0; i < slots.Length; i++)
            cultures[i] = slots[i] == null ? null : slots[i].Culture?.StringId ?? string.Empty;
        return DesertionRecruitCap.FirstDeserterSlot(cultures,
            culture => _service.WouldDesert(kingdomId, isPlayerOwned, isGarrison, culture!));
    }

    // Mirrors AlignmentDesertionBehavior.TryResolveOwner, plus the owner-level gates. Under the default settings the gate passes for every AI lord of a Free or Evil kingdom;
    // it stops Neutral, kingdomless and switched-off owners.
    private bool TryOwner(Clan? clan, bool isGarrison, out string kingdomId, out bool isPlayerOwned)
    {
        kingdomId = string.Empty;
        isPlayerOwned = false;
        var kingdom = clan?.Kingdom;
        if (kingdom == null)
            return false;
        var owner = _playerControl.OfClan(clan);
        if (DesertionOwnerPolicy.ShouldSkip(owner, clan == Clan.PlayerClan))
            return false;
        isPlayerOwned = DesertionOwnerPolicy.IsPlayerOwned(owner);
        kingdomId = kingdom.StringId;
        return _service.ShouldEvaluate(kingdomId, isPlayerOwned, isGarrison);
    }

    private void LogOnce(Exception ex)
    {
        if (_logged)
            return;
        _logged = true;
        _logger.LogError($"[AlignDesert] recruit guard failed, keeping vanilla's answer: {ex}");
    }
}

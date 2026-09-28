using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// The quest and event routes to lord kit (docs/features/armour-acquisition.md; Mike, 2026-09-27: forge,
/// quest and event, all three). "The Lord's Harness" is one career-quest definition in
/// taom_career_quests.xml (<see cref="QuestId"/>, three objectives in any order), run by the CareerQuest
/// shell; whether it is running is the quest manager's to say, so the only state here is per hero: ready
/// to claim, then claimed. The claim happens at an armoury of the lord level. "A Lord's Harness Unclaimed"
/// is the post-battle event. Both hand out a piece of the right culture's lord kit
/// (<see cref="LordPieceChoices"/>).
/// </summary>
public sealed class LordHarnessService
{
    /// <summary>The career-quest definition id, and its career_id, which names no career.</summary>
    public const string QuestId = "taom_lords_harness";

    private readonly ArmourAcquisitionState _state;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IArmourGateService _gate;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ICultureMarketplaceConfigProvider _marketplace;

    public LordHarnessService(ArmourAcquisitionState state, IArmourAcquisitionConfigProvider config, IArmourGateService gate,
        IArmouryPlayerAdapter player, ICultureMarketplaceConfigProvider marketplace)
    {
        _state = state;
        _config = config;
        _gate = gate;
        _player = player;
        _marketplace = marketplace;
    }

    public int Stage(string heroId) => _state.HarnessStage.TryGetValue(heroId, out var stage) ? stage : 0;

    /// <summary>
    /// Offer the quest to the main hero: not running, never completed, no recent refusal, an armoury of the
    /// lord level in a town of the hero's own culture, and an elite piece carried or worn (checked last: it
    /// reads the whole inventory).
    /// </summary>
    public bool ShouldOffer(string heroId, int today, int townLevel, bool townIsHeroCulture, bool questRunning)
    {
        if (questRunning || Stage(heroId) != 0 || !townIsHeroCulture)
            return false;
        var config = _config.GetConfig();
        if (townLevel < config.LordLevel)
            return false;
        if (_state.HarnessDeclinedDay.TryGetValue(heroId, out var declined) && today - declined < config.HarnessOfferCooldownDays)
            return false;
        return _player.ReadInventory().Select(p => p.ItemId).Concat(_player.ReadEquippedItemIds())
            .Any(id => _gate.GetClass(id) == ArmourClass.Elite);
    }

    public void Decline(string heroId, int today) => _state.HarnessDeclinedDay[heroId] = today;

    public void OnAccepted(string heroId) => _state.HarnessDeclinedDay.Remove(heroId);

    /// <summary>
    /// The quest ended. Success readies the harness for the quest's owner, the hero who accepted it, even
    /// when the player has since switched hero. Returns true when it did.
    /// </summary>
    public bool OnQuestEnded(string ownerHeroId, bool success)
    {
        if (!success || string.IsNullOrEmpty(ownerHeroId) || Stage(ownerHeroId) != 0)
            return false;
        _state.HarnessStage[ownerHeroId] = ArmourAcquisitionState.HarnessReady;
        return true;
    }

    public bool IsReadyToClaim(string heroId) => Stage(heroId) == ArmourAcquisitionState.HarnessReady;

    public bool CanClaimAt(int townLevel) => townLevel >= _config.GetConfig().LordLevel;

    /// <summary>
    /// Fits the main hero with one piece of their lord kit: gives the piece, then marks the harness claimed.
    /// A piece that is not one of the hero's choices, or cannot be given, leaves the harness claimable.
    /// </summary>
    public bool Claim(string itemId)
    {
        var heroId = _player.HeroId;
        if (!IsReadyToClaim(heroId) || !LordPieceChoices(_player.CultureId).Contains(itemId))
            return false;
        if (!_player.AddPiece(itemId, null, 1))
            return false;
        _state.HarnessStage[heroId] = ArmourAcquisitionState.HarnessClaimed;
        return true;
    }

    /// <summary>
    /// The pieces a lord harness of a culture may be, from the class table only (never a vanilla piece
    /// classed by its engine tier). A culture with no armour of its own uses the culture its markets draw
    /// armour from (culture_marketplace_config.xml, armour_from). The culture's lord pieces; a culture with
    /// none offers its elite pieces; with neither, any culture's lord pieces as a last resort.
    /// </summary>
    public IReadOnlyList<string> LordPieceChoices(string? cultureId)
    {
        var donor = cultureId == null ? null : _marketplace.GetArmourDonor(cultureId);
        var kit = string.IsNullOrEmpty(donor) ? cultureId : donor;
        if (kit != null)
        {
            var lord = _gate.GetPieces(ArmourClass.Lord, kit);
            if (lord.Count > 0)
                return lord;
            var elite = _gate.GetPieces(ArmourClass.Elite, kit);
            if (elite.Count > 0)
                return elite;
        }
        return _gate.GetPieces(ArmourClass.Lord, null);
    }

    /// <summary>
    /// "A Lord's Harness Unclaimed": after a battle the player's side won against lords (their cultures in
    /// <paramref name="defeatedLordCultureIds"/>), off cooldown, a rare roll finds one lord's harness among
    /// the spoils. Returns which lord and which piece, and stamps the cooldown, or null.
    /// </summary>
    public (int LordIndex, string ItemId)? RollHarnessFind(string heroId, int today, bool won,
        IReadOnlyList<string?> defeatedLordCultureIds, Random rng)
    {
        var config = _config.GetConfig();
        int? last = _state.LordEventLastDay.TryGetValue(heroId, out var day) ? day : null;
        if (!LordHarnessEventPolicy.ShouldTrigger(won, defeatedLordCultureIds.Count, today, last,
                config.LordEventCooldownDays, config.LordEventChance, rng.NextDouble()))
            return null;
        var lordIndex = rng.Next(defeatedLordCultureIds.Count);
        var choices = LordPieceChoices(defeatedLordCultureIds[lordIndex]);
        if (choices.Count == 0)
            return null;
        _state.LordEventLastDay[heroId] = today;
        return (lordIndex, choices[rng.Next(choices.Count)]);
    }
}

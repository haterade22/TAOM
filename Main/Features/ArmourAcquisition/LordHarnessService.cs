using System;
using System.Collections.Generic;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CultureMarketplace;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// "A Lord's Harness Unclaimed", the event route to lord kit (docs/features/armour-acquisition.md): after a
/// battle won against lords, a rare find among the spoils. The quest route is the lord's gear ladder
/// (<see cref="LordsLadderService"/>, #693); the forge is the armoury bench.
/// </summary>
public sealed class LordHarnessService
{
    private readonly ArmourAcquisitionState _state;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IArmourGateService _gate;
    private readonly ICultureMarketplaceConfigProvider _marketplace;

    public LordHarnessService(ArmourAcquisitionState state, IArmourAcquisitionConfigProvider config, IArmourGateService gate,
        ICultureMarketplaceConfigProvider marketplace)
    {
        _state = state;
        _config = config;
        _gate = gate;
        _marketplace = marketplace;
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
    /// After a battle the player's side won against lords (their cultures in
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

using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// KEYforce's armoury upgrade (docs/features/armour-acquisition.md): a carried piece becomes the next class
/// of its line (the class table's <c>next</c>) for the target class's recipe: flat gold plus a share of the
/// value the piece gains, the recipe's metals, and for lord kit the player's kingdom special resource
/// (waived when the kingdom has none). The town's armoury level caps the class it can work. The piece keeps
/// its quality modifier (Mike, 2026-09-27; the engine applies a modifier to any piece,
/// EquipmentElement.GetModified*Armor). The offer and the upgrade judge what the player can pay by one rule.
/// </summary>
public sealed class ArmouryUpgradeService
{
    private readonly IArmourGateService _gate;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ISpecialResourceSpender _spender;

    public ArmouryUpgradeService(IArmourGateService gate, IArmourAcquisitionConfigProvider config,
        IArmouryPlayerAdapter player, ISpecialResourceSpender spender)
    {
        _gate = gate;
        _config = config;
        _player = player;
        _spender = spender;
    }

    /// <summary>
    /// Every carried stack that has a next piece and a recipe, priced, with the reason it cannot be done
    /// when it cannot. Doable offers first, then by piece id.
    /// </summary>
    public IReadOnlyList<UpgradeOffer> BuildOffers(int townLevel)
    {
        var config = _config.GetConfig();
        var inventory = _player.ReadInventory();
        var wallet = ReadWallet(inventory);
        var offers = new List<UpgradeOffer>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stack in inventory)
        {
            if (stack.Count <= 0 || !seen.Add(stack.ItemId + "\u0001" + stack.ModifierId))
                continue;
            var cls = _gate.GetClass(stack.ItemId);
            if (cls == null || !ArmourClassRules.IsUpgradeSource(cls.Value))
                continue;
            var next = _gate.GetNext(stack.ItemId);
            var targetClass = next == null ? null : _gate.GetClass(next);
            if (next == null || targetClass == null || !config.Recipes.TryGetValue(targetClass.Value, out var recipe))
                continue;

            var gold = PriceGold(stack.ItemId, next, recipe);
            var required = config.RequiredLevel(targetClass.Value);
            var block = townLevel < required
                ? UpgradeBlock.ArmouryLevelTooLow
                : Affordability(gold, recipe.Materials, recipe.SpecialResource, wallet);
            offers.Add(new UpgradeOffer(stack.ItemId, stack.ModifierId, next, targetClass.Value, gold, recipe.Materials,
                recipe.SpecialResource, required, block));
        }
        return offers
            .OrderBy(o => o.CanUpgrade ? 0 : 1)
            .ThenBy(o => o.SourceItemId, StringComparer.Ordinal)
            .ThenBy(o => o.ModifierId ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Does the upgrade, re-checking everything first because the player may have spent or sold since the
    /// offer was built. The piece is taken before anything is charged, and given back if the resource spend
    /// is refused, so a failed upgrade never costs the player. Returns why it was not done, or None.
    /// </summary>
    public UpgradeBlock Execute(UpgradeOffer offer)
    {
        var inventory = _player.ReadInventory();
        if (!inventory.Any(p => p.ItemId == offer.SourceItemId && p.ModifierId == offer.ModifierId && p.Count > 0))
            return UpgradeBlock.NoLongerCarried;
        var wallet = ReadWallet(inventory);
        var block = Affordability(offer.Gold, offer.Materials, offer.SpecialResource, wallet);
        if (block != UpgradeBlock.None)
            return block;

        if (!_player.RemovePiece(offer.SourceItemId, offer.ModifierId))
            return UpgradeBlock.NoLongerCarried;
        if (offer.SpecialResource > 0f && wallet.ResourceAmount.HasValue
            && !_spender.TrySpend(_player.HeroId, _player.KingdomId, _player.CultureId, offer.SpecialResource))
        {
            _player.AddPiece(offer.SourceItemId, offer.ModifierId, 1);
            return UpgradeBlock.NotEnoughResource;
        }

        foreach (var material in offer.Materials)
            _player.RemoveItem(material.ItemId, material.Count);
        if (offer.Gold > 0)
            _player.ChargeGold(offer.Gold);
        _player.AddPiece(offer.TargetItemId, offer.ModifierId, 1);
        return UpgradeBlock.None;
    }

    private ArmouryWallet ReadWallet(IReadOnlyList<InventoryPiece> inventory) =>
        new(_player.Gold,
            inventory.GroupBy(p => p.ItemId).ToDictionary(g => g.Key, g => g.Sum(p => p.Count)),
            _spender.GetBalance(_player.HeroId, _player.KingdomId, _player.CultureId)?.Amount);

    /// <summary>The one affordability rule: gold, then metals, then the resource (waived with none).</summary>
    private static UpgradeBlock Affordability(int gold, IReadOnlyList<UpgradeMaterial> materials, float specialResource,
        ArmouryWallet wallet)
    {
        if (wallet.Gold < gold)
            return UpgradeBlock.NotEnoughGold;
        if (materials.Any(m => wallet.CountOf(m.ItemId) < m.Count))
            return UpgradeBlock.MissingMaterials;
        if (specialResource > 0f && wallet.ResourceAmount.HasValue && !(wallet.ResourceAmount.Value >= specialResource))
            return UpgradeBlock.NotEnoughResource;
        return UpgradeBlock.None;
    }

    private int PriceGold(string sourceId, string targetId, UpgradeRecipe recipe)
    {
        var gained = (long)(_gate.GetRecord(targetId)?.Value ?? 0) - (_gate.GetRecord(sourceId)?.Value ?? 0);
        var share = gained > 0 ? Math.Round(recipe.ValueShare * (double)gained) : 0d;
        var total = recipe.Gold + share;
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }
}

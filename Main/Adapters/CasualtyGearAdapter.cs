using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.BattleGearLoot.Domain;

namespace TAOM.Adapters;

/// <summary>
/// One beaten troop's battle kit as loot: a hero's own kit slot by slot, any other troop's kit rebuilt with the engine's
/// spawn call so it has the shape and modifiers an agent would have worn.
/// </summary>
internal sealed class CasualtyGearAdapter : ICasualtyGearAdapter
{
    private readonly CharacterObject _casualty;
    private readonly ItemRoster _target;
    private readonly EquipmentElement[] _kit = new EquipmentElement[Equipment.EquipmentSlotLength];
    private List<GearPiece>? _pieces;

    internal bool Granted { get; private set; }

    private CasualtyGearAdapter(CharacterObject casualty, ItemRoster target)
    {
        _casualty = casualty;
        _target = target;
    }

    internal static CasualtyGearAdapter For(CharacterObject casualty, ItemRoster target) =>
        new CasualtyGearAdapter(casualty, target);

    public bool VanillaGivesNothing
    {
        get
        {
            float value = Campaign.Current.Models.BattleRewardModel.GetExpectedLootedItemValueFromCasualty(Hero.MainHero, _casualty);
            return value.ApproximatelyEqualsTo(0f);
        }
    }

    public IReadOnlyList<GearPiece> Pieces => _pieces ??= BuildPieces();

    public void Grant(IReadOnlyList<int> slots)
    {
        bool metallurgy = MobileParty.MainParty.HasPerk(DefaultPerks.Engineering.Metallurgy);
        foreach (int slot in slots)
        {
            var element = _kit[slot];
            if (element.Item == null)
                continue;
            if (metallurgy && element.ItemModifier != null && element.ItemModifier.PriceMultiplier < 1f
                && MBRandom.RandomFloat < DefaultPerks.Engineering.Metallurgy.PrimaryBonus)
                element = new EquipmentElement(element.Item);
            Granted = true;
            _target.AddToCounts(element, 1);
        }
    }

    private List<GearPiece> BuildPieces()
    {
        var pieces = new List<GearPiece>();
        var equipment = KitOf(_casualty);
        if (equipment == null)
            return pieces;

        for (int slot = 0; slot < Equipment.EquipmentSlotLength; slot++)
        {
            var element = equipment[slot];
            var item = element.Item;
            if (item == null)
                continue;
            _kit[slot] = element;
            pieces.Add(new GearPiece(
                slot,
                item.StringId,
                !item.NotMerchandise,
                item.IsBannerItem,
                element.IsQuestItem,
                item.HorseComponent?.Monster?.StringId));
        }

        return pieces;
    }

    private static Equipment? KitOf(CharacterObject character)
    {
        if (character.IsHero)
        {
            var kit = character.HeroObject?.BattleEquipment;
            return ReferenceEquals(kit, Campaign.Current.DeadBattleEquipment) ? null : kit;
        }

        return Equipment.GetRandomEquipmentElements(character, true, Equipment.EquipmentType.Battle);
    }
}

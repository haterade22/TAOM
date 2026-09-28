using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Localization;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>The armoury's player-facing text, in one place so the presenter stays thin.</summary>
internal static class ArmouryTexts
{
    public static TextObject ClassName(ArmourClass cls) => cls switch
    {
        ArmourClass.Light => new TextObject("{=taom_armour_class_light}light"),
        ArmourClass.Medium => new TextObject("{=taom_armour_class_medium}medium"),
        ArmourClass.Heavy => new TextObject("{=taom_armour_class_heavy}heavy"),
        ArmourClass.Elite => new TextObject("{=taom_armour_class_elite}elite"),
        ArmourClass.Lord => new TextObject("{=taom_armour_class_lord}lord"),
        _ => new TextObject("{=taom_armour_class_other}fine"),
    };

    public static string OfferTitle(string source, string target, ArmourClass cls) =>
        new TextObject("{=taom_armoury_offer}{SOURCE} into {TARGET} ({CLASS})")
            .SetTextVariable("SOURCE", source).SetTextVariable("TARGET", target)
            .SetTextVariable("CLASS", ClassName(cls)).ToString();

    /// <summary>"1500 gold, 3 Steel, 4 Iron, 150 Castar": the whole price on one line.</summary>
    public static string Price(UpgradeOffer offer, Func<string, string> itemName, string? resourceName)
    {
        var parts = new List<string>
        {
            new TextObject("{=taom_armoury_gold}{GOLD} gold").SetTextVariable("GOLD", offer.Gold).ToString(),
        };
        parts.AddRange(offer.Materials.Select(m => Amount(m.Count, itemName(m.ItemId))));
        if (offer.SpecialResource > 0f && resourceName != null)
            parts.Add(Amount((int)Math.Round(offer.SpecialResource), resourceName));
        return string.Join(", ", parts);
    }

    // The count and the name in one localizable string, so a language can order them its own way.
    private static string Amount(int count, string name) =>
        new TextObject("{=taom_armoury_amount}{COUNT} {ITEM}").SetTextVariable("COUNT", count).SetTextVariable("ITEM", name).ToString();

    public static string Hint(UpgradeOffer offer, int townLevel, string price) => offer.Block switch
    {
        UpgradeBlock.ArmouryLevelTooLow => new TextObject("{=taom_armoury_block_level}This armoury cannot work {CLASS} pieces: it needs level {NEED}, and this one is level {LEVEL}.")
            .SetTextVariable("CLASS", ClassName(offer.TargetClass)).SetTextVariable("NEED", offer.RequiredLevel)
            .SetTextVariable("LEVEL", townLevel).ToString(),
        UpgradeBlock.NotEnoughGold => new TextObject("{=taom_armoury_block_gold}You cannot pay it: {PRICE}.").SetTextVariable("PRICE", price).ToString(),
        UpgradeBlock.MissingMaterials => new TextObject("{=taom_armoury_block_metal}You lack the metals: {PRICE}.").SetTextVariable("PRICE", price).ToString(),
        UpgradeBlock.NotEnoughResource => new TextObject("{=taom_armoury_block_resource}Your people's treasure falls short: {PRICE}.").SetTextVariable("PRICE", price).ToString(),
        _ => new TextObject("{=taom_armoury_cost}Costs {PRICE}.").SetTextVariable("PRICE", price).ToString(),
    };

    /// <summary>What an upgrade came to. The armoury level is judged on the offer only, not again at the upgrade.</summary>
    public static string Outcome(UpgradeBlock outcome, string source, string target) => outcome switch
    {
        UpgradeBlock.None => new TextObject("{=taom_armoury_done}The armourer hands you {TARGET}.").SetTextVariable("TARGET", target).ToString(),
        UpgradeBlock.NoLongerCarried => new TextObject("{=taom_armoury_gone}You no longer carry {SOURCE}.").SetTextVariable("SOURCE", source).ToString(),
        UpgradeBlock.NotEnoughGold => new TextObject("{=taom_armoury_short_gold}You no longer have the gold for it.").ToString(),
        UpgradeBlock.MissingMaterials => new TextObject("{=taom_armoury_short_metal}You no longer have the metals for it.").ToString(),
        _ => new TextObject("{=taom_armoury_short_resource}Your people's treasure no longer covers it.").ToString(),
    };
}

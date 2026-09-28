using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Boundary presentation of a town armoury (docs/features/armour-acquisition.md): the upgrade picker and its
/// confirmation as engine inquiries, with the lord's gear ladder's rows above the upgrades
/// (<see cref="LadderPresenter"/>). Every decision is a service's (<see cref="ArmouryUpgradeService"/>,
/// <see cref="LordsLadderService"/>, <see cref="ArmouryLevelService"/>).
/// A co-op guest is turned away before anything is charged: the host's next roster resync would erase the
/// upgraded piece (the EliteEmissary "pay-real-get-phantom" finding).
/// </summary>
public sealed class ArmouryPresenter
{
    private readonly IArmourGateService _gate;
    private readonly ArmouryLevelService _levels;
    private readonly ArmouryUpgradeService _upgrades;
    private readonly LadderPresenter _ladder;
    private readonly IArmouryTownAdapter _towns;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ISpecialResourceSpender _spender;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public ArmouryPresenter(IArmourGateService gate, ArmouryLevelService levels, ArmouryUpgradeService upgrades,
        LadderPresenter ladder, IArmouryTownAdapter towns, IArmouryPlayerAdapter player, ISpecialResourceSpender spender,
        IArmourAcquisitionConfigProvider config, ICoopSessionProvider coop, IModLogger logger)
    {
        _gate = gate;
        _levels = levels;
        _upgrades = upgrades;
        _ladder = ladder;
        _towns = towns;
        _player = player;
        _spender = spender;
        _config = config;
        _coop = coop;
        _logger = logger;
    }

    public void Open(string townId)
    {
        if (_coop.ShouldDeferToHost)
        {
            Notify(new TextObject("{=taom_armoury_coop_guest}The armourer works only for the host of this campaign.").ToString(), Colors.Red);
            return;
        }
        var level = _levels.GetTownLevel(townId);
        var resourceName = _spender.GetBalance(_player.HeroId, _player.KingdomId, _player.CultureId)?.DisplayName;
        var config = _config.GetConfig();

        var elements = new List<InquiryElement>(_ladder.Rows(level));
        foreach (var offer in _upgrades.BuildOffers(level))
        {
            var price = ArmouryTexts.Price(offer, Name, resourceName);
            elements.Add(new InquiryElement(offer, ArmouryTexts.OfferTitle(Name(offer.SourceItemId), Name(offer.TargetItemId), offer.TargetClass),
                null, offer.CanUpgrade, ArmouryTexts.Hint(offer, level, price)));
        }
        if (elements.Count == 0)
        {
            Notify(new TextObject("{=taom_armoury_nothing}You carry nothing this armourer can improve. Bring a piece of armour and the metals its next form needs.").ToString(), Colors.White);
            return;
        }

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            new TextObject("{=taom_armoury_title}The Armoury of {TOWN}").SetTextVariable("TOWN", _towns.GetName(townId)).ToString(),
            new TextObject("{=taom_armoury_desc}This armoury works to level {LEVEL} of 3. Heavy pieces need level {HEAVY}, elite pieces level {ELITE}, and lord kit a master armourer at level {LORD}. The piece keeps its quality.")
                .SetTextVariable("LEVEL", level).SetTextVariable("HEAVY", config.HeavyLevel)
                .SetTextVariable("ELITE", config.EliteLevel).SetTextVariable("LORD", config.LordLevel).ToString(),
            elements, isExitShown: true, minSelectableOptionCount: 1, maxSelectableOptionCount: 1,
            new TextObject("{=taom_armoury_choose}Choose").ToString(), new TextObject("{=taom_armoury_leave}Leave").ToString(),
            selected => OnChosen(selected, resourceName), _ => { }), pauseGameActiveState: true);
    }

    private void OnChosen(List<InquiryElement> selected, string? resourceName)
    {
        var picked = selected.FirstOrDefault()?.Identifier;
        if (_ladder.TryHandle(picked) || picked is not UpgradeOffer offer)
            return;
        var price = ArmouryTexts.Price(offer, Name, resourceName);
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_armoury_confirm_title}Improve {SOURCE}?").SetTextVariable("SOURCE", Name(offer.SourceItemId)).ToString(),
            new TextObject("{=taom_armoury_confirm_body}The armourer will rework it into {TARGET} for {PRICE}.")
                .SetTextVariable("TARGET", Name(offer.TargetItemId)).SetTextVariable("PRICE", price).ToString(),
            true, true, new TextObject("{=taom_armoury_confirm_yes}Have it done").ToString(), new TextObject("{=taom_armoury_cancel}Cancel").ToString(),
            () => Execute(offer), null), pauseGameActiveState: true);
    }

    private void Execute(UpgradeOffer offer)
    {
        var outcome = _upgrades.Execute(offer);
        _logger.LogInfo($"[ArmourAcquisition] Upgrade {offer.SourceItemId} -> {offer.TargetItemId}: {(outcome == UpgradeBlock.None ? "done" : outcome.ToString())}.");
        Notify(ArmouryTexts.Outcome(outcome, Name(offer.SourceItemId), Name(offer.TargetItemId)),
            outcome == UpgradeBlock.None ? Colors.Green : Colors.Red);
    }

    private string Name(string itemId) => _gate.GetName(itemId);

    private static void Notify(string text, Color color) => InformationManager.DisplayMessage(new InformationMessage(text, color));
}

using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.ArmourAcquisition.Hooks;

/// <summary>
/// Boundary presentation of a town armoury (docs/features/armour-acquisition.md): the upgrade picker and its
/// confirmation as engine inquiries, with the lord's gear ladder's rows above the upgrades
/// (<see cref="LadderPresenter"/>). Every decision is a service's (<see cref="ArmouryUpgradeService"/>,
/// <see cref="LordsLadderService"/>, <see cref="ArmouryLevelService"/>).
/// A co-op guest with TAOM.CoopCompat asks the host, which runs the upgrade for the guest's hero (#1329), and lists the
/// ladder rows from the co-op server's view of the guest's rung (<see cref="GuestLadderPresenter"/>). Without the relay a guest is turned away before anything is charged: the
/// host's next roster resync would erase the upgraded piece (the EliteEmissary "pay-real-get-phantom" finding).
/// </summary>
public sealed class ArmouryPresenter
{
    private readonly IArmourGateService _gate;
    private readonly ArmouryLevelService _levels;
    private readonly ArmouryUpgradeService _upgrades;
    private readonly LadderPresenter _ladder;
    private readonly GuestLadderPresenter _guestLadder;
    private readonly IArmouryTownAdapter _towns;
    private readonly IArmouryPlayerAdapter _player;
    private readonly ISpecialResourceSpender _spender;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly GuestTradeClient _guestTrade;
    private readonly IModLogger _logger;

    public ArmouryPresenter(IArmourGateService gate, ArmouryLevelService levels, ArmouryUpgradeService upgrades,
        LadderPresenter ladder, GuestLadderPresenter guestLadder, IArmouryTownAdapter towns, IArmouryPlayerAdapter player, ISpecialResourceSpender spender,
        IArmourAcquisitionConfigProvider config, GuestTradeClient guestTrade, IModLogger logger)
    {
        _gate = gate;
        _levels = levels;
        _upgrades = upgrades;
        _ladder = ladder;
        _guestLadder = guestLadder;
        _towns = towns;
        _player = player;
        _spender = spender;
        _config = config;
        _guestTrade = guestTrade;
        _logger = logger;
    }

    public void Open(string townId)
    {
        if (_guestTrade.IsGuest)
        {
            AskHost(new GuestTradeRequest(GuestTradeAction.ArmouryBalance, townId), reply => Show(townId, guest: true, reply.ArmouryLevel, reply.Ladder));
            return;
        }
        if (_guestTrade.IsGuestWithoutLink)
        {
            Notify(new TextObject("{=taom_armoury_coop_guest}The armourer works only for the host of this campaign.").ToString(), Colors.Red);
            return;
        }
        Show(townId, guest: false);
    }

    private void Show(string townId, bool guest, int? hostLevel = null, GuestLadderView? hostLadder = null)
    {
        var level = hostLevel ?? _levels.GetTownLevel(townId);
        var resourceName = _spender.GetBalance(_player.HeroId, _player.KingdomId, _player.CultureId)?.DisplayName;
        var config = _config.GetConfig();

        var elements = new List<InquiryElement>(guest ? _guestLadder.Rows(hostLadder) : _ladder.Rows(level));
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
            selected => OnChosen(selected, resourceName, townId, guest), _ => { }), pauseGameActiveState: true);
    }

    private void OnChosen(List<InquiryElement> selected, string? resourceName, string townId, bool guest)
    {
        var picked = selected.FirstOrDefault()?.Identifier;
        if ((guest ? _guestLadder.TryHandle(picked, townId) : _ladder.TryHandle(picked)) || picked is not UpgradeOffer offer)
            return;
        var price = ArmouryTexts.Price(offer, Name, resourceName);
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_armoury_confirm_title}Improve {SOURCE}?").SetTextVariable("SOURCE", Name(offer.SourceItemId)).ToString(),
            new TextObject("{=taom_armoury_confirm_body}The armourer will rework it into {TARGET} for {PRICE}.")
                .SetTextVariable("TARGET", Name(offer.TargetItemId)).SetTextVariable("PRICE", price).ToString(),
            true, true, new TextObject("{=taom_armoury_confirm_yes}Have it done").ToString(), new TextObject("{=taom_armoury_cancel}Cancel").ToString(),
            () => Execute(offer, townId, guest), null), pauseGameActiveState: true);
    }

    private void Execute(UpgradeOffer offer, string townId, bool guest)
    {
        if (guest)
        {
            AskHost(new GuestTradeRequest(GuestTradeAction.ArmouryUpgrade, townId, offer.SourceItemId, offer.ModifierId, offer.TargetItemId),
                reply => ShowOutcome(offer, (UpgradeBlock)reply.Status));
            return;
        }
        ShowOutcome(offer, _upgrades.Execute(offer));
    }

    private void AskHost(GuestTradeRequest request, System.Action<GuestTradeReply> onHandled) =>
        GuestTradeTexts.AskHost(_guestTrade, request, onHandled, text => Notify(text, Colors.Red));

    private void ShowOutcome(UpgradeOffer offer, UpgradeBlock outcome)
    {
        _logger.LogInfo($"[ArmourAcquisition] Upgrade {offer.SourceItemId} -> {offer.TargetItemId}: {(outcome == UpgradeBlock.None ? "done" : outcome.ToString())}.");
        Notify(ArmouryTexts.Outcome(outcome, Name(offer.SourceItemId), Name(offer.TargetItemId)),
            outcome == UpgradeBlock.None ? Colors.Green : Colors.Red);
    }

    private string Name(string itemId) => _gate.GetName(itemId);

    private static void Notify(string text, Color color) => InformationManager.DisplayMessage(new InformationMessage(text, color));
}

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.SupplyLines.Components;

/// <summary>
/// Party component for a supply caravan: a player-clan map party whose movement is driven by
/// <c>SupplyCaravanService</c> (teleport-along-path; AI disabled at spawn and re-pinned on load),
/// never by party AI.
///
/// <para>Persisted (definer id 102): the party itself rides the vanilla save, so this component and
/// its fields must round-trip. TAOM's first custom PartyComponent; keep it dumb — identity only,
/// all behaviour in services.</para>
/// </summary>
public sealed class SupplyCaravanComponent : PartyComponent
{
    [SaveableField(101)] private readonly string _orderId;

    [SaveableField(102)] private readonly Settlement _homeSettlement;

    public SupplyCaravanComponent(string orderId, Settlement homeSettlement)
    {
        _orderId = orderId;
        _homeSettlement = homeSettlement;
    }

    public string OrderId => _orderId;

    public override Hero PartyOwner => Hero.MainHero;

    public override TextObject Name => new TextObject("{=taom_sl_caravan_name}Supply Caravan");

    public override Settlement HomeSettlement => _homeSettlement;

    // Hostile AI still targets the caravan through vanilla aggression rules; this only stops the
    // caravan itself picking fights, which a teleport-driven party must never do.
    public override bool AvoidHostileActions => true;

    public override Banner GetDefaultComponentBanner() => Hero.MainHero?.ClanBanner;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        MobileParty?.Party?.SetVisualAsDirty();
    }
}

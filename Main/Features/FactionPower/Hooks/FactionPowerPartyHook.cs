using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FactionPower.Hooks;

/// <summary>
/// The narrow seam <c>TaomPartySizeModel</c> calls for the lord party size and garrison size limits. The realm multiplier is
/// asked first and a campaign at 100 percent returns before the player-clan query is made.
/// </summary>
public interface IFactionPowerPartyHook
{
    void ApplyLordLimit(PartyBase? party, ref ExplainedNumber limit);

    void ApplyGarrisonLimit(Settlement? settlement, ref ExplainedNumber limit);
}

public sealed class FactionPowerPartyHook : IFactionPowerPartyHook
{
    private readonly FactionPowerService _service;
    private readonly IPlayerControlQuery _players;

    public FactionPowerPartyHook(FactionPowerService service, IPlayerControlQuery players)
    {
        _service = service;
        _players = players;
    }

    public void ApplyLordLimit(PartyBase? party, ref ExplainedNumber limit)
    {
        var mobile = party?.MobileParty;
        if (mobile == null) return;

        var multiplier = _service.Multiplier(FactionPowerRealm.KeyOf(mobile.MapFaction));
        if (multiplier == 1f) return;

        _service.ScaleLordLimit(
            multiplier, mobile.IsMainParty, mobile.IsLordParty, party!.LeaderHero != null,
            PlayerClanCheck.IsAPlayersClan(mobile.ActualClan, _players), ref limit);
    }

    public void ApplyGarrisonLimit(Settlement? settlement, ref ExplainedNumber limit)
    {
        if (settlement == null) return;

        var multiplier = _service.Multiplier(FactionPowerRealm.KeyOf(settlement.MapFaction));
        if (multiplier == 1f) return;

        _service.ScaleGarrisonLimit(multiplier, PlayerClanCheck.IsAPlayersClan(settlement.OwnerClan, _players), ref limit);
    }
}

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FactionPower.Hooks;

/// <summary>
/// The narrow seam <c>TaomCombatSimulationModel.SimulateHit</c> calls for lever 4. The multiplier of the striker party's realm
/// is asked first; the main-party and player-clan facts are read only when it is not 1, so a campaign at 100 percent pays one
/// dictionary read per simulated strike.
/// </summary>
public interface IFactionPowerStrikeHook
{
    void ApplyStrike(PartyBase? strikerParty, ref ExplainedNumber damage);
}

public sealed class FactionPowerStrikeHook : IFactionPowerStrikeHook
{
    private readonly FactionPowerService _service;
    private readonly IPlayerControlQuery _players;

    public FactionPowerStrikeHook(FactionPowerService service, IPlayerControlQuery players)
    {
        _service = service;
        _players = players;
    }

    public void ApplyStrike(PartyBase? strikerParty, ref ExplainedNumber damage)
    {
        if (strikerParty == null) return;

        var multiplier = _service.Multiplier(FactionPowerRealm.KeyOf(strikerParty.MapFaction));
        if (multiplier == 1f) return;

        var mobile = strikerParty.MobileParty;
        var clan = mobile != null ? FactionPowerRealm.OwnerClanOf(mobile) : strikerParty.Settlement?.OwnerClan;
        _service.ScaleStrike(multiplier, mobile?.IsMainParty == true, PlayerClanCheck.IsAPlayersClan(clan, _players), ref damage);
    }
}

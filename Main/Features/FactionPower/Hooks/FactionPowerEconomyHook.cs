using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FactionPower.Hooks;

/// <summary>
/// The narrow seam the militia and wage models call for the economy levers: militia growth (2b) and wage relief (5). The
/// realm multiplier is asked first, so a campaign at 100 percent returns before the player-clan query is made.
/// </summary>
public interface IFactionPowerEconomyHook
{
    void ApplyMilitiaChange(Settlement? settlement, ref ExplainedNumber change);

    void ApplyWageRelief(MobileParty? party, ref ExplainedNumber wage);
}

public sealed class FactionPowerEconomyHook : IFactionPowerEconomyHook
{
    private readonly FactionPowerService _service;
    private readonly IPlayerControlQuery _players;

    public FactionPowerEconomyHook(FactionPowerService service, IPlayerControlQuery players)
    {
        _service = service;
        _players = players;
    }

    public void ApplyMilitiaChange(Settlement? settlement, ref ExplainedNumber change)
    {
        if (settlement == null) return;

        var multiplier = _service.Multiplier(FactionPowerRealm.KeyOf(settlement.MapFaction));
        if (multiplier == 1f) return;

        _service.ScaleMilitiaChange(
            multiplier, PlayerClanCheck.IsAPlayersClan(settlement.OwnerClan, _players), settlement.Militia, ref change);
    }

    public void ApplyWageRelief(MobileParty? party, ref ExplainedNumber wage)
    {
        if (party == null) return;

        var multiplier = _service.Multiplier(FactionPowerRealm.KeyOf(party.MapFaction));
        if (!(multiplier > 1f)) return;

        _service.ScaleWage(
            multiplier, party.IsGarrison, party.IsMainParty, party.IsLordParty, party.LeaderHero != null,
            PlayerClanCheck.IsAPlayersClan(FactionPowerRealm.OwnerClanOf(party), _players), ref wage);
    }
}

/// <summary>
/// Lever 5 for <c>TaomPartyWageModel.GetTotalWage</c>, which sits at its entry point line baseline and cannot take a field.
/// Resolves the hook on first use, as <c>FreeGarrisonUpkeepLine</c> resolves its config.
/// </summary>
public static class FactionPowerWageLine
{
    private static IFactionPowerEconomyHook? _hook;

    public static void Apply(MobileParty? party, ref ExplainedNumber wage)
        => (_hook ??= IoC.Resolve<IFactionPowerEconomyHook>()).ApplyWageRelief(party, ref wage);
}

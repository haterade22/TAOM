using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>What kills, towers and heroes pay out.</summary>
internal sealed class BountyRules
{
    private readonly FordsEconomyConfig _economy;

    public BountyRules(FordsEconomyConfig economy)
    {
        _economy = economy;
    }

    public int KillBounty(int unitValue)
    {
        var scaled = (int)Math.Round(Math.Max(0, unitValue) * _economy.KillBountyFraction, MidpointRounding.AwayFromZero);
        return Math.Max(_economy.MinKillBounty, scaled);
    }

    /// <summary>One slot's share of a tower bounty; the remainder of an uneven split is dropped.</summary>
    public int TowerBountyPerSlot(int destroyingSideSlotCount) =>
        destroyingSideSlotCount > 0 ? _economy.TowerBounty / destroyingSideSlotCount : 0;

    public int HeroKillBounty => _economy.HeroKillBounty;
}

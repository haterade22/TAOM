using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.EliteEmissary;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// TAOM's emissary and armoury, built for one guest's hero on the host (#1329). Each call builds the service with
/// the hero's own adapters, so the purse, party and inventory are the guest's and every rule is TAOM's own.
/// </summary>
public interface IGuestTradeShops
{
    IEliteEmissaryService Emissary(GuestTradeHero hero);

    /// <summary>The armoury feature is on and the settlement is a town.</summary>
    bool HasArmoury(string settlementId);

    /// <summary>The town's armoury level on the host, visiting armourer included.</summary>
    int ArmouryLevel(string settlementId);

    /// <summary>The upgrades the hero's party can have at this armoury level.</summary>
    IReadOnlyList<UpgradeOffer> ArmouryOffers(GuestTradeHero hero, int armouryLevel);

    UpgradeBlock Upgrade(GuestTradeHero hero, UpgradeOffer offer);

    /// <summary>
    /// Armour Acquisition is on and the server's "Lords' Ladder for co-op players" switch is on, read now. Checked before
    /// <see cref="Ladder"/> is called.
    /// </summary>
    bool LadderOpen { get; }

    /// <summary>The armoury level a ladder piece needs (the config's lord level).</summary>
    int LadderLordLevel { get; }

    /// <summary>TAOM's lord's gear ladder for the hero: its state is the server's, saved per hero.</summary>
    LordsLadderService Ladder(GuestTradeHero hero);
}

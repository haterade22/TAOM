using System;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

internal sealed partial class SurvivalRun
{
    /// <summary>Gold the last horn paid; 0 before any.</summary>
    public int LastHornBonus { get; private set; }

    /// <summary>Seconds of the phase that were left when the last horn sounded, so a check can recompute what it should have paid.</summary>
    public float LastHornSecondsLeft { get; private set; }

    /// <summary>Spends the price at once; the pack spawns on the next tick.</summary>
    public BuyResult TryBuy(string offerId)
    {
        if (Result != null)
            return BuyResult.RunOver;
        if (!BetweenWaves)
            return BuyResult.NotOpen;

        var offer = _shop.FirstOrDefault(o => o.Id == offerId);
        if (offer == null)
            return BuyResult.UnknownOffer;
        if (offer.Price > _purse.Balance)
            return BuyResult.NoGold;
        if (_lastDefenders + PendingDefenders + offer.PackSize > _r.GarrisonCap)
            return BuyResult.GarrisonFull;
        if (!_purse.TrySpend(offer.Price, PurseSink.Packs))
            return BuyResult.NoGold;

        _purchases.Add(new FortPurchase(_wave + 1, PurchaseKind.Pack, offer.Id, 0, _plan.Shop.Offers.First(o => o.Id == offer.Id).Price, offer.Price, 0f));
        _packs.Add(offer);
        _bought[(int)offer.Slot]++;
        _pendingEvents.Add(new RunEvent(RunEventKind.Bought, _wave, "offer=" + offer.Id));
        return BuyResult.Ok;
    }

    /// <summary>The horn: pays for the time skipped, then starts the next wave on the next tick.</summary>
    public StartResult TryStartWave()
    {
        if (Result != null)
            return StartResult.RunOver;
        if (!BetweenWaves)
            return StartResult.NotOpen;

        LastHornSecondsLeft = _left;
        var raw = Math.Ceiling(Math.Max(0f, _left) * (double)_r.EarlyGoldPerSecond * _factors.EarlyGold);
        LastHornBonus = raw > 0d ? (int)Math.Min(GoldFactors.Apply(_r.EarlyGoldCap, _factors.EarlyGold), raw) : 0;
        _purse.Earn(PurseSource.EarlyStart, LastHornBonus);
        _pendingEvents.Add(new RunEvent(RunEventKind.Horn, _wave + 1, "gold=" + LastHornBonus));
        BeginWave();
        return StartResult.Ok;
    }
}

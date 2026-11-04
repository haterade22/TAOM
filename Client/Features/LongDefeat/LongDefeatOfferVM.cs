using System;
using TaleWorlds.Library;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>One pack in the shop: its name, pack size, price and whether the gold reaches. A click buys it by id.</summary>
public sealed class LongDefeatOfferVM : LongDefeatViewModel
{
    private readonly Action<string> _onBuy;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _packText = string.Empty;
    private string _priceText = string.Empty;
    private bool _canAfford;

    internal LongDefeatOfferVM(Action<string> onBuy) => _onBuy = onBuy;

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        private set => Set(ref _name, value, nameof(Name));
    }

    [DataSourceProperty]
    public string PackText
    {
        get => _packText;
        private set => Set(ref _packText, value, nameof(PackText));
    }

    [DataSourceProperty]
    public string PriceText
    {
        get => _priceText;
        private set => Set(ref _priceText, value, nameof(PriceText));
    }

    [DataSourceProperty]
    public bool CanAfford
    {
        get => _canAfford;
        private set => Set(ref _canAfford, value, nameof(CanAfford));
    }

    public void ExecuteBuy() => _onBuy(_id);

    internal void Update(ShopOffer offer, int gold)
    {
        if (_id != offer.Id)
        {
            _id = offer.Id;
            Name = LongDefeatHudText.OfferName(offer.Name);
            PackText = LongDefeatHudText.PackSize(offer.PackSize);
            PriceText = LongDefeatHudText.Price(offer.Price);
        }

        CanAfford = gold >= offer.Price;
    }
}

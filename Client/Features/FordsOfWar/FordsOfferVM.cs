using System;
using TaleWorlds.Library;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>One purchasable troop pack: name, pack size, price, the F-key that buys it and whether the gold reaches.</summary>
public sealed class FordsOfferVM : FordsViewModel
{
    private readonly Action<int> _onBuy;
    private string _name = string.Empty;
    private string _packText = string.Empty;
    private string _priceText = string.Empty;
    private string _keyText = string.Empty;
    private bool _canAfford;
    private string _iconSprite = string.Empty;
    private string _priceColor = FordsFactionStyle.PriceColor(true);

    internal FordsOfferVM(int index, Action<int> onBuy)
    {
        Index = index;
        _onBuy = onBuy;
    }

    internal int Index { get; }

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
    public string KeyText
    {
        get => _keyText;
        private set => Set(ref _keyText, value, nameof(KeyText));
    }

    [DataSourceProperty]
    public bool CanAfford
    {
        get => _canAfford;
        private set => Set(ref _canAfford, value, nameof(CanAfford));
    }

    [DataSourceProperty]
    public string IconSprite
    {
        get => _iconSprite;
        private set => Set(ref _iconSprite, value, nameof(IconSprite));
    }

    [DataSourceProperty]
    public string PriceColor
    {
        get => _priceColor;
        private set => Set(ref _priceColor, value, nameof(PriceColor));
    }

    public void ExecuteBuy() => _onBuy(Index);

    internal void Update(MatchView.UnitOfferView offer, string name, bool live)
    {
        Name = name;
        PackText = FordsHudText.PackSize(offer.PackSize);
        PriceText = FordsHudText.Price(offer.Price);
        KeyText = Index < FordsCommandVM.OfferHotkeys ? "F" + (Index + 1) : string.Empty;
        CanAfford = live && offer.CanAfford;
        IconSprite = FordsFactionStyle.RoleIcon(offer.Role);
        PriceColor = FordsFactionStyle.PriceColor(offer.CanAfford);
    }
}

using System;
using TaleWorlds.Library;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>One hero of the shop (#1337): his name and text, one state line (the price while he is for sale, otherwise where he is) and whether a click can buy him. A click buys him by id; the run refuses a hero who is not for sale.</summary>
public sealed class LongDefeatHeroVM : LongDefeatViewModel
{
    private readonly Action<string> _onBuy;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _detailText = string.Empty;
    private string _stateText = string.Empty;
    private bool _canBuy;

    internal LongDefeatHeroVM(Action<string> onBuy) => _onBuy = onBuy;

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        private set => Set(ref _name, value, nameof(Name));
    }

    [DataSourceProperty]
    public string DetailText
    {
        get => _detailText;
        private set => Set(ref _detailText, value, nameof(DetailText));
    }

    [DataSourceProperty]
    public string StateText
    {
        get => _stateText;
        private set => Set(ref _stateText, value, nameof(StateText));
    }

    /// <summary>True only for a hero who is for sale and whom the gold reaches; any other row is grey.</summary>
    [DataSourceProperty]
    public bool CanBuy
    {
        get => _canBuy;
        private set => Set(ref _canBuy, value, nameof(CanBuy));
    }

    public void ExecuteBuy() => _onBuy(_id);

    internal void Update(HeroRow row, int gold)
    {
        if (_id != row.Offer.Id)
        {
            _id = row.Offer.Id;
            Name = LongDefeatHudText.OfferName(row.Offer.Name);
            DetailText = LongDefeatHudText.OfferName(row.Offer.Text);
        }

        StateText = LongDefeatHudText.HeroStateLine(row.State, row.Price);
        CanBuy = row.State == HeroState.ForSale && gold >= row.Price;
    }
}

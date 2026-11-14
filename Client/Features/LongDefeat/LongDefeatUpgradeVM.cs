using System;
using TaleWorlds.Library;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>One upgrade step in the shop (#1161): its name, how many soldiers of its troop stand and how many the gold pays for, and the price for one. A click buys the step by id.</summary>
public sealed class LongDefeatUpgradeVM : LongDefeatViewModel
{
    private readonly Action<string> _onUpgrade;
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _detailText = string.Empty;
    private string _priceText = string.Empty;
    private bool _canAfford;

    internal LongDefeatUpgradeVM(Action<string> onUpgrade) => _onUpgrade = onUpgrade;

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

    public void ExecuteUpgrade() => _onUpgrade(_id);

    internal void Update(UpgradeRow row)
    {
        if (_id != row.Step.Id)
        {
            _id = row.Step.Id;
            Name = LongDefeatHudText.OfferName(row.Step.Name);
            PriceText = LongDefeatHudText.UpgradePrice(row.Step.Price);
        }

        DetailText = LongDefeatHudText.UpgradeDetail(row.Standing, row.Affordable);
        CanAfford = row.Affordable > 0;
    }
}

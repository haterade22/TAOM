using System;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>One upgrade track in the Upgrades panel: what it gives so far, and what the next level costs.</summary>
public sealed class FordsUpgradeVM : FordsViewModel
{
    private readonly int _index;
    private readonly Action<int> _onBuy;
    private string _name = string.Empty;
    private string _statusText = string.Empty;
    private string _actionText = string.Empty;
    private string _actionColor = FordsFactionStyle.PriceColor(true);
    private bool _canBuy;

    internal FordsUpgradeVM(int index, Action<int> onBuy)
    {
        _index = index;
        _onBuy = onBuy;
    }

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        private set => Set(ref _name, value, nameof(Name));
    }

    [DataSourceProperty]
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value, nameof(StatusText));
    }

    [DataSourceProperty]
    public string ActionText
    {
        get => _actionText;
        private set => Set(ref _actionText, value, nameof(ActionText));
    }

    [DataSourceProperty]
    public string ActionColor
    {
        get => _actionColor;
        private set => Set(ref _actionColor, value, nameof(ActionColor));
    }

    [DataSourceProperty]
    public string KeyText => "Shift+F" + (_index + 1);

    [DataSourceProperty]
    public bool CanBuy
    {
        get => _canBuy;
        private set => Set(ref _canBuy, value, nameof(CanBuy));
    }

    public void ExecuteBuy() => _onBuy(_index);

    internal void Update(MatchView.UpgradeView upgrade, bool matchOpen)
    {
        Name = new TextObject(upgrade.Name).ToString();
        StatusText = FordsHudText.UpgradeStatus(upgrade.Effect, upgrade.StepPerLevel, upgrade.Level, upgrade.MaxLevel);
        ActionText = upgrade.NextPrice is { } price ? FordsHudText.UpgradeNext(price) : FordsHudText.UpgradeDone();
        ActionColor = FordsFactionStyle.PriceColor(upgrade.NextPrice == null || upgrade.CanAfford);
        CanBuy = matchOpen && upgrade.NextPrice != null && upgrade.CanAfford;
    }
}

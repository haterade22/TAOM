using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Library;

namespace TAOM.Features.SpecialResources.UI;

[ViewModelMixin("RefreshValues")]
internal class SpecialResourceMapBarMixin : BaseViewModelMixin<MapInfoVM>
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceConfigProvider _config;
    private int _lastAmount = -1;
    private Domain.SpecialResource _lastResource;
    private string _lastKingdomId;
    private string _lastCultureId;

    private string _resourceDisplayText;
    private string _resourceIconSprite;
    private string _resourceTooltipTitle;
    private bool _isResourceVisible;
    private bool _hasResourceWarning;

    public SpecialResourceMapBarMixin(MapInfoVM viewModel) : base(viewModel)
    {
        _service = IoC.Resolve<ISpecialResourceService>();
        _config = IoC.Resolve<ISpecialResourceConfigProvider>();
    }

    public override void OnRefresh()
    {
        if (Campaign.Current == null)
        {
            IsResourceVisible = false;
            return;
        }

        var hero = Hero.MainHero;
        if (hero == null)
        {
            IsResourceVisible = false;
            return;
        }

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        var cultureId = hero.Culture?.StringId;

        if (kingdomId != _lastKingdomId || cultureId != _lastCultureId)
        {
            _lastResource = _service.ResolveResource(kingdomId, cultureId);
            _lastKingdomId = kingdomId;
            _lastCultureId = cultureId;
            _lastAmount = -1;
        }

        var resource = _lastResource;

        if (resource == null)
        {
            IsResourceVisible = false;
            return;
        }

        IsResourceVisible = true;

        var amount = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);
        var intAmount = (int)amount;
        HasResourceWarning = amount <= 0f;

        ResourceIconSprite = resource.IconSpriteName;

        if (intAmount != _lastAmount)
        {
            var tier = _service.GetCurrentTier(hero.StringId, kingdomId, cultureId);
            ResourceDisplayText = tier != null
                ? $"{intAmount} ({tier.Name})"
                : $"{intAmount}";
            ResourceTooltipTitle = resource.DisplayName;
            _lastAmount = intAmount;
        }
    }

    [DataSourceProperty]
    public string ResourceDisplayText
    {
        get => _resourceDisplayText;
        set
        {
            if (_resourceDisplayText != value)
            {
                _resourceDisplayText = value;
                OnPropertyChanged(nameof(ResourceDisplayText));
            }
        }
    }

    [DataSourceProperty]
    public string ResourceIconSprite
    {
        get => _resourceIconSprite;
        set
        {
            if (_resourceIconSprite != value)
            {
                _resourceIconSprite = value;
                OnPropertyChanged(nameof(ResourceIconSprite));
            }
        }
    }

    [DataSourceProperty]
    public string ResourceTooltipTitle
    {
        get => _resourceTooltipTitle;
        set
        {
            if (_resourceTooltipTitle != value)
            {
                _resourceTooltipTitle = value;
                OnPropertyChanged(nameof(ResourceTooltipTitle));
            }
        }
    }

    [DataSourceProperty]
    public bool IsResourceVisible
    {
        get => _isResourceVisible;
        set
        {
            if (_isResourceVisible != value)
            {
                _isResourceVisible = value;
                OnPropertyChanged(nameof(IsResourceVisible));
            }
        }
    }

    [DataSourceProperty]
    public bool HasResourceWarning
    {
        get => _hasResourceWarning;
        set
        {
            if (_hasResourceWarning != value)
            {
                _hasResourceWarning = value;
                OnPropertyChanged(nameof(HasResourceWarning));
            }
        }
    }
}

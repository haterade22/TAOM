using System;
using TaleWorlds.Library;
using TAOM.Adapters;

namespace TAOM.Features.FactionUI.UI.FactionScreen;

/// <summary>
/// One hero card: the "Custom Character" card, a named-hero card or a wanderer template (copied without
/// a name), or a lord or leader from the browse lists (copied with his name) (#704, Kysaro's). A card or
/// lord that is a living hero with a clan is taken over instead (<c>FactionPickService</c>).
/// </summary>
public sealed class HeroItemVM : ViewModel
{
    private readonly Action<HeroItemVM> _onSelect;
    private bool _isSelected;
    private string _portraitSprite = "";

    /// <summary>The "Custom Character" card, selected when the faction is shown.</summary>
    public HeroItemVM(string name, string portraitSprite, Action<HeroItemVM> onSelect)
    {
        Name = name;
        PortraitSprite = portraitSprite;
        IsCustom = true;
        IsAvailable = true;
        _isSelected = true;
        _onSelect = onSelect;
    }

    /// <summary>A pick; a card whose character TAOM cannot find is shown but cannot be picked.</summary>
    public HeroItemVM(string name, RosterEntry? pick, Action<HeroItemVM> onSelect)
    {
        Name = name;
        IsPreset = true;
        Pick = pick;
        IsAvailable = pick != null;
        _onSelect = onSelect;
    }

    public bool IsPreset { get; }

    /// <summary>The character or hero this card picks; null for "Custom Character".</summary>
    public RosterEntry? Pick { get; }

    public string RevealSprite { get; set; } = "";

    public float RevealWidth { get; set; } = 500f;

    public float RevealScale { get; set; } = 1f;

    [DataSourceProperty] public bool IsCustom { get; }

    [DataSourceProperty] public bool IsAvailable { get; }

    [DataSourceProperty] public string Name { get; }

    [DataSourceProperty]
    public string PortraitSprite
    {
        get => _portraitSprite;
        set
        {
            if (value == _portraitSprite)
                return;
            _portraitSprite = value;
            OnPropertyChangedWithValue(value, nameof(PortraitSprite));
            OnPropertyChanged(nameof(HasPortrait));
            OnPropertyChanged(nameof(ShowPlaceholder));
        }
    }

    [DataSourceProperty] public bool HasPortrait => IsPreset && !string.IsNullOrEmpty(PortraitSprite);

    [DataSourceProperty] public bool ShowPlaceholder => IsPreset && string.IsNullOrEmpty(PortraitSprite);

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value == _isSelected)
                return;
            _isSelected = value;
            OnPropertyChangedWithValue(value, nameof(IsSelected));
        }
    }

    public void ExecuteSelectHero() => _onSelect(this);
}

using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher.UI;

/// <summary>
/// One selectable lord in the picker.
/// </summary>
/// <remarks>
/// Deliberately does NOT derive from vanilla `ClanPartyMemberItemVM`, despite that being the VM the
/// `ClanLordTuple` prefab was written for.
///
/// That base class takes `(Hero hero, MobileParty party)` and its constructor body opens with
/// `IsLeader = hero == party.LeaderHero;` with no null guard. A wanderer sitting in a tavern has no
/// `PartyBelongedTo`, wanderers are offered by default, and one such candidate would throw inside
/// `PlayerSwitcherVM.Build`, get swallowed by the attach patch's try/catch, and make the entire
/// panel silently fail to appear. Subclassing bought a compile-time break on an engine change; it
/// cost the feature working at all for most cultures, which is the worse trade.
///
/// So this supplies the tuple's binding contract directly. `PlayerSwitcherPrefabContractTests` and
/// `PlayerSwitcherBindingTests` are what now guard the contract that inheritance used to.
/// </remarks>
public class HeroPickItemVM : ViewModel
{
    private readonly Action<HeroPickItemVM> _onSelected;

    private bool _isSelected;
    private string _name = string.Empty;
    private string _currentActionText = string.Empty;
    private CharacterImageIdentifierVM? _visual;
    private BannerImageIdentifierVM? _banner9;

    public HeroPickItemVM(Hero hero, HeroPickRow row, Action<HeroPickItemVM> onSelected)
    {
        HeroObject = hero;
        Row = row;
        _onSelected = onSelected;

        RefreshValues();
    }

    public Hero HeroObject { get; }

    /// <summary>The engine-free row this VM renders. The panel plans the handover from it.</summary>
    public HeroPickRow Row { get; }

    public override void RefreshValues()
    {
        base.RefreshValues();

        Name = HeroObject?.Name?.ToString() ?? Row.Name ?? string.Empty;

        // The lord's portrait. Built from the character code exactly as the vanilla clan rows do.
        if (HeroObject?.CharacterObject != null)
            Visual = new CharacterImageIdentifierVM(CampaignUIHelper.GetCharacterCode(HeroObject.CharacterObject));

        // A clanless wanderer has no banner, which is normal rather than exceptional here: the
        // whole point of the Wanderers group is heroes with no house behind them.
        if (HeroObject?.ClanBanner != null)
            Banner_9 = new BannerImageIdentifierVM(HeroObject.ClanBanner, nineGrid: true);
    }

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        set { if (value != _name) { _name = value ?? string.Empty; OnPropertyChangedWithValue(value, nameof(Name)); } }
    }

    [DataSourceProperty]
    public CharacterImageIdentifierVM? Visual
    {
        get => _visual;
        set { if (value != _visual) { _visual = value; OnPropertyChangedWithValue(value, nameof(Visual)); } }
    }

    [DataSourceProperty]
    public BannerImageIdentifierVM? Banner_9
    {
        get => _banner9;
        set { if (value != _banner9) { _banner9 = value; OnPropertyChangedWithValue(value, nameof(Banner_9)); } }
    }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); } }
    }

    /// <summary>
    /// The tuple shows a child marker from this. Nobody selectable is a child (the picker filters
    /// them out), so it is always false, but the binding has to exist or the row renders against
    /// nothing and Gauntlet says nothing about it.
    /// </summary>
    [DataSourceProperty]
    public bool IsChild => false;

    [DataSourceProperty]
    public string CurrentActionText
    {
        get => _currentActionText;
        set { if (value != _currentActionText) { _currentActionText = value ?? string.Empty; OnPropertyChangedWithValue(value, nameof(CurrentActionText)); } }
    }

    /// <summary>The TAOM prefab's own click, bound on the ClanLordTuple element.</summary>
    public void OnPreBuildCharacterSelected() => _onSelected?.Invoke(this);

    /// <summary>
    /// The vanilla tuple's inner ButtonWidget click. Which of the two actually fires is a Gauntlet
    /// routing detail that cannot be settled offline, so both route to the same place.
    /// </summary>
    public void OnCharacterSelect() => _onSelected?.Invoke(this);

    /// <summary>
    /// The tuple binds an encyclopedia link. Intentionally inert here: opening the encyclopedia
    /// mid-character-creation pushes a screen over a flow that has no campaign behind it yet.
    /// </summary>
    public void ExecuteLink()
    {
    }

    public void ExecuteBeginHint()
    {
    }

    public void ExecuteEndHint()
    {
    }
}

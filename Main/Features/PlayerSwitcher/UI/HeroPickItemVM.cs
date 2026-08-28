using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher.UI;

/// <summary>
/// One selectable lord in the picker.
/// </summary>
/// <remarks>
/// Derives from the vanilla clan row VM on purpose. Inheriting a compile-time base class means an
/// engine bump that removes or reshapes ClanPartyMemberItemVM breaks the BUILD, which is the
/// strongest gate available; re-implementing its portrait, banner and name bindings by hand would
/// turn the same change into a silently blank row.
///
/// The base supplies Name, Visual, Banner_9, ExecuteLink, ExecuteBeginHint and ExecuteEndHint. The
/// vanilla ClanLordTuple binds four more that the base does not: IsSelected, IsChild,
/// CurrentActionText and OnCharacterSelect. Those are added here.
/// </remarks>
public class HeroPickItemVM : ClanPartyMemberItemVM
{
    private readonly Action<HeroPickItemVM> _onSelected;
    private bool _isSelected;
    private string _currentActionText = string.Empty;

    public HeroPickItemVM(Hero hero, HeroPickRow row, Action<HeroPickItemVM> onSelected)
        : base(hero, hero?.PartyBelongedTo)
    {
        Row = row;
        _onSelected = onSelected;
    }

    /// <summary>The engine-free row this VM renders. The panel plans the handover from it.</summary>
    public HeroPickRow Row { get; }

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

    /// <summary>
    /// The tuple uses this to show a child marker. Nobody selectable here is a child (the picker
    /// filters them out), so it is always false, but the binding must exist or the row renders
    /// against nothing.
    /// </summary>
    [DataSourceProperty]
    public bool IsChild => false;

    [DataSourceProperty]
    public string CurrentActionText
    {
        get => _currentActionText;
        set
        {
            if (value == _currentActionText)
                return;
            _currentActionText = value ?? string.Empty;
            OnPropertyChangedWithValue(value, nameof(CurrentActionText));
        }
    }

    /// <summary>
    /// The TAOM prefab's own click, bound on the ClanLordTuple element.
    /// </summary>
    public void OnPreBuildCharacterSelected() => _onSelected?.Invoke(this);

    /// <summary>
    /// The vanilla tuple's inner ButtonWidget click. Which of the two actually fires is a Gauntlet
    /// routing detail that cannot be settled offline, so both route to the same place and the
    /// answer stops mattering.
    /// </summary>
    public void OnCharacterSelect() => _onSelected?.Invoke(this);
}

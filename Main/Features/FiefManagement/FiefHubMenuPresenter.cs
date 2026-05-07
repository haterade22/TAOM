using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.FiefManagement.Models;

namespace TAOM.Features.FiefManagement;

public class FiefHubMenuPresenter : IFiefHubMenuPresenter
{
    private readonly IFiefHubService _service;
    private readonly IFiefManagementSettingsProvider _settings;
    private readonly ISettlementOwnershipAdapter _ownership;

    private IReadOnlyList<FiefSummary> _menuFiefs = System.Array.Empty<FiefSummary>();
    private FiefSummary _menuCurrentFief;
    private bool _menuCurrentAtPlayer;
    private int _selectedIndex;

    public FiefHubMenuPresenter(
        IFiefHubService service,
        IFiefManagementSettingsProvider settings,
        ISettlementOwnershipAdapter ownership)
    {
        _service = service;
        _settings = settings;
        _ownership = ownership;
    }

    public int Count => _menuFiefs.Count;

    public void Reset() => _selectedIndex = 0;

    public void Refresh()
    {
        _menuFiefs = _service.GetOrderedFiefs();
        var count = _menuFiefs.Count;
        if (count <= 0)
        {
            _selectedIndex = 0;
            _menuCurrentFief = null;
            _menuCurrentAtPlayer = false;
            return;
        }
        if (_selectedIndex < 0) _selectedIndex = 0;
        else if (_selectedIndex >= count) _selectedIndex = count - 1;
        _menuCurrentFief = _menuFiefs[_selectedIndex];
        _menuCurrentAtPlayer = _service.PlayerIsAt(_menuCurrentFief);
    }

    public TextObject BuildTitle()
    {
        if (_menuCurrentFief == null)
            return new TextObject("{=taom_fief_hub_empty}Fief Management — no fiefs owned");
        var title = new TextObject("{=taom_fief_hub_title}Fief Management — {NAME} ({KIND}) [{INDEX}/{COUNT}]");
        title.SetTextVariable("NAME", _menuCurrentFief.Name);
        title.SetTextVariable("KIND", _menuCurrentFief.IsTown
            ? new TextObject("{=taom_fief_kind_town}Town")
            : new TextObject("{=taom_fief_kind_castle}Castle"));
        title.SetTextVariable("INDEX", _selectedIndex + 1);
        title.SetTextVariable("COUNT", _menuFiefs.Count);
        return title;
    }

    public bool ManageOptionEnabled(out TextObject disabledHint)
    {
        disabledHint = null;
        if (_menuFiefs.Count <= 0 || _menuCurrentFief == null) return false;
        if (!_settings.AllowRemoteBuildingQueue && !_menuCurrentAtPlayer)
        {
            disabledHint = new TextObject("{=taom_fief_hub_remote_disabled}Remote building queue disabled — visit the fief to manage.");
            return false;
        }
        return true;
    }

    public void StepNext() => _selectedIndex = _service.Next(_selectedIndex);
    public void StepPrevious() => _selectedIndex = _service.Previous(_selectedIndex);

    public Settlement ResolveCurrentSettlement()
    {
        var current = _menuCurrentFief;
        return current == null ? null : _ownership.Resolve(current.Id);
    }
}

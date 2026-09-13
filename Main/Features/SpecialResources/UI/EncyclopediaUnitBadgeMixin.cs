using System;
using System.Collections.Generic;
using System.Reflection;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Features.SpecialResources.UI;

/// <summary>
/// The encyclopedia troop-tree badge (#590): marks a troop that costs a special resource to
/// upgrade, recruit or keep, with the troop's own resource icon and a hover tooltip listing the
/// costs. Bound by the third corner widget in TAOM's clone of
/// <c>EncyclopediaUnitTreeNodeItem.xml</c>, which binds this VM as <c>{Unit}</c>.
///
/// Neither <c>EncyclopediaTroopTreeNodeVM</c> nor <c>EncyclopediaUnitVM</c> exposes the troop,
/// so its id is read once from the private <c>_character</c> field (pinned in
/// <c>ReflectionSiteBindingTests</c>); everything past that line is strings and domain records.
/// The tooltip is a lazy <see cref="BasicTooltipViewModel"/>: nothing is rendered until hover,
/// so a language change is picked up at the next hover and no refresh hook is needed.
/// </summary>
[ViewModelMixin]
internal sealed class EncyclopediaUnitBadgeMixin : BaseViewModelMixin<EncyclopediaUnitVM>
{
    private static readonly FieldInfo CharacterField =
        AccessTools.Field(typeof(EncyclopediaUnitVM), "_character");

    // Rate-limited per exception type and shared across every node, the same shape as the map-bar
    // mixin: both entry points run inside engine dispatch (construction of every node on the page,
    // and hover), where a throw would take the page or the hint system down, and nothing on either
    // path writes to our log. One instance per node would log the same fault twenty times a page.
    private static readonly HashSet<string> FailuresLogged = new HashSet<string>(StringComparer.Ordinal);

    private ISpecialResourceService _service;
    private IModLogger _logger;
    private TroopResourceCostEntry _cost;
    private SpecialResource _resource;

    private bool _hasSpecialResourceCost;
    private string _specialResourceIconSprite = "";
    private BasicTooltipViewModel _specialResourceTooltip;

    public EncyclopediaUnitBadgeMixin(EncyclopediaUnitVM viewModel) : base(viewModel)
    {
        // Assigned before anything that can throw: the prefab binds it unconditionally.
        SpecialResourceTooltip = new BasicTooltipViewModel(BuildTooltip);

        try
        {
            var config = IoC.Resolve<ISpecialResourceConfigProvider>();
            _service = IoC.Resolve<ISpecialResourceService>();
            _logger = IoC.Resolve<IModLogger>();

            // Boundary conversion: the troop id leaves the engine here.
            var character = CharacterField?.GetValue(viewModel) as CharacterObject;
            _cost = character == null ? null : config.GetTroopCost(character.StringId);
            _resource = _cost == null ? null : config.GetById(_cost.ResourceId);

            if (_cost != null && _resource == null && FailuresLogged.Add("unknown-resource:" + _cost.ResourceId))
            {
                _logger?.LogWarning($"[SpecRes] encyclopedia badge: troop '{_cost.TroopId}' names resource_id "
                                  + $"'{_cost.ResourceId}', which special_resources_config.xml does not define; no badge");
            }

            var shown = _resource != null && SpecialResourceTroopBadge.IsShown(_cost);
            SpecialResourceIconSprite = shown ? _resource.IconSpriteName : "";
            HasSpecialResourceCost = shown;
        }
        catch (Exception ex)
        {
            HasSpecialResourceCost = false;
            LogOnce("constructing the encyclopedia troop badge", ex);
        }
    }

    private List<TooltipProperty> BuildTooltip()
    {
        var result = new List<TooltipProperty>(6);
        try
        {
            if (_resource == null || _cost == null) return result;

            // TooltipProperty takes strings only (no TextObject overload in v1.4.8), so every line is
            // rendered here; the {=key} tag is what makes it translatable.
            result.Add(new TooltipProperty(SpecialResourceTroopBadge.Title(_resource).ToString(), "", 0,
                onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.Title));

            foreach (var row in SpecialResourceTroopBadge.Rows(_cost))
                result.Add(new TooltipProperty(row.Key.ToString(), row.Value, 0));

            var hero = Hero.MainHero;
            var playerResource = hero == null
                ? null
                : _service?.ResolveResource(hero.Clan?.Kingdom?.StringId, hero.Culture?.StringId);
            var note = SpecialResourceTroopBadge.PaidInNote(_resource, playerResource);
            if (note != null)
            {
                result.Add(new TooltipProperty("", note.ToString(), 0,
                    onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.MultiLine));
            }
        }
        catch (Exception ex)
        {
            // An empty list, never a rethrow: this runs inside the engine's hover dispatch.
            result.Clear();
            LogOnce("building the encyclopedia troop badge tooltip", ex);
        }

        return result;
    }

    private void LogOnce(string action, Exception ex)
    {
        if (!FailuresLogged.Add(ex.GetType().FullName ?? "(unknown)")) return;
        _logger?.LogError($"[SpecRes] {action} threw, so the badge is hidden on this node. Root cause: {ex}");
    }

    [DataSourceProperty]
    public bool HasSpecialResourceCost
    {
        get => _hasSpecialResourceCost;
        set
        {
            if (_hasSpecialResourceCost != value)
            {
                _hasSpecialResourceCost = value;
                // On `this`, never on ViewModel: the bindings resolve against the mixin (#166 P1).
                OnPropertyChangedWithValue(value, nameof(HasSpecialResourceCost));
            }
        }
    }

    [DataSourceProperty]
    public string SpecialResourceIconSprite
    {
        get => _specialResourceIconSprite;
        set
        {
            if (_specialResourceIconSprite != value)
            {
                _specialResourceIconSprite = value;
                OnPropertyChangedWithValue(value, nameof(SpecialResourceIconSprite));
            }
        }
    }

    [DataSourceProperty]
    public BasicTooltipViewModel SpecialResourceTooltip
    {
        get => _specialResourceTooltip;
        set
        {
            if (_specialResourceTooltip != value)
            {
                _specialResourceTooltip = value;
                OnPropertyChangedWithValue(value, nameof(SpecialResourceTooltip));
            }
        }
    }
}

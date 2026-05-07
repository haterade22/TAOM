using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.Roles.Models;

namespace TAOM.Features.CompanionTactics.Roles;

/// <summary>
/// Boundary class. Resolves sealed Hero / VM types into <see cref="IHeroCombatAdapter"/>
/// for the role service, then writes the VM mutation back. All reflection (TypeIconData
/// property + cached tooltip field) is cached at construction.
/// </summary>
public sealed class RoleTooltipDecorator : IRoleTooltipDecorator
{
    private readonly ICompanionRoleService _roles;
    private readonly ICompanionTacticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    private readonly PropertyInfo _typeIconDataProp;
    private readonly FieldInfo _cachedTooltipField;

    public RoleTooltipDecorator(
        ICompanionRoleService roles,
        ICompanionTacticsSettingsProvider settings,
        IModLogger logger)
    {
        _roles = roles;
        _settings = settings;
        _logger = logger;

        _typeIconDataProp = AccessTools.Property(typeof(PartyCharacterVM), "TypeIconData");
        _cachedTooltipField = AccessTools.Field(typeof(OrderOfBattleHeroItemVM), "_cachedTooltipProperties");
    }

    public void DecoratePartyCharacter(PartyCharacterVM vm)
    {
        if (vm == null) return;
        if (!_settings.EnableCompanionRoleTooltips) return;

        var hero = ResolvePlayerCompanyHero(vm.Character);
        if (hero == null) return;

        var role = _roles.GetPrimaryRole(new HeroCombatAdapter(hero));
        if (role == CombatRole.Unknown) return;

        var label = _roles.GetRoleShortText(role);
        var prefix = $"[{label}] ";
        var current = vm.Name ?? string.Empty;
        if (!current.StartsWith(prefix, StringComparison.Ordinal))
        {
            // Strip any prior bracketed prefix (e.g., "[BOW] Aragorn" -> "Aragorn") before re-applying.
            if (current.Length > 2 && current[0] == '[')
            {
                var endIdx = current.IndexOf("] ", StringComparison.Ordinal);
                if (endIdx > 0 && endIdx <= 5) current = current.Substring(endIdx + 2);
            }
            vm.Name = prefix + current;
        }
    }

    public void DecorateOOBHeroItem(OrderOfBattleHeroItemVM vm)
    {
        if (vm == null) return;
        if (!_settings.EnableOOBRoleDisplay) return;
        if (_cachedTooltipField == null) return;

        var hero = ResolveAgentHero(vm.Agent);
        if (hero == null) return;

        var role = _roles.GetPrimaryRole(new HeroCombatAdapter(hero));
        if (role == CombatRole.Unknown) return;
        if (_cachedTooltipField.GetValue(vm) is not List<TooltipProperty> { Count: > 0 } props) return;

        ApplyRolePrefix(props, role);
    }

    public void AppendRoleToCaptainTooltip(OrderOfBattleHeroItemVM vm, List<TooltipProperty> tooltip)
    {
        if (vm == null || tooltip == null || tooltip.Count == 0) return;
        if (!_settings.EnableOOBRoleDisplay) return;

        var hero = ResolveAgentHero(vm.Agent);
        if (hero == null) return;

        var role = _roles.GetPrimaryRole(new HeroCombatAdapter(hero));
        if (role == CombatRole.Unknown) return;

        ApplyRolePrefix(tooltip, role);
    }

    private void ApplyRolePrefix(List<TooltipProperty> props, CombatRole role)
    {
        try
        {
            var label = _roles.GetRoleShortText(role);
            var prefix = $"[{label}] ";
            var first = props[0];
            var def = first?.DefinitionLabel ?? string.Empty;
            if (def.StartsWith(prefix, StringComparison.Ordinal)) return;

            // Strip prior bracketed prefix
            if (def.Length > 2 && def[0] == '[')
            {
                var endIdx = def.IndexOf("] ", StringComparison.Ordinal);
                if (endIdx > 0 && endIdx <= 5) def = def.Substring(endIdx + 2);
            }
            props[0] = new TooltipProperty(prefix + def, first?.ValueLabel ?? string.Empty,
                first?.TextHeight ?? 0, false, TooltipProperty.TooltipPropertyFlags.Title);
        }
        catch (Exception ex)
        {
            if (_settings.CompanionRolesDebug)
                _logger.LogWarning($"[CompanionRoles] tooltip prefix error: {ex.Message}");
        }
    }

    private static Hero ResolvePlayerCompanyHero(BasicCharacterObject character)
    {
        if (character is not CharacterObject co || !co.IsHero) return null;
        var hero = co.HeroObject;
        if (hero == null || hero == Hero.MainHero) return null;
        if (!hero.IsPlayerCompanion && hero.Clan != Clan.PlayerClan) return null;
        return hero;
    }

    private static Hero ResolveAgentHero(Agent agent)
    {
        if (agent == null) return null;
        if (agent.Character is not CharacterObject co || !co.IsHero) return null;
        var hero = co.HeroObject;
        if (hero == null || hero == Hero.MainHero) return null;
        return hero;
    }
}

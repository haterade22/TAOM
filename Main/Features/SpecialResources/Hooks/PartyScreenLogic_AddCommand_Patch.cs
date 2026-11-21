using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources.Hooks;

[HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.AddCommand))]
[HarmonyPatchCategory("Patch26_SpecialResources")]
public static class PartyScreenLogic_AddCommand_Patch
{
    private static IOnPartyUpgradeResourceCheck _hook;
    private static IModLogger _logger;
    private static FieldInfo _totalNumberField;
    private static SpecialResourcesBehavior _behavior;

    public static void Initialize(IOnPartyUpgradeResourceCheck hook, IModLogger logger)
    {
        _hook = hook;
        _logger = logger;
    }

    public static void SetBehavior(SpecialResourcesBehavior behavior) => _behavior = behavior;

    [HarmonyPrefix]
    public static bool Prefix(PartyScreenLogic __instance, ref PartyScreenLogic.PartyCommand command)
    {
        if (_hook == null) return true;
        if (command.Code == PartyScreenLogic.PartyCommandCode.RecruitTroop) return GateRecruit(__instance, ref command);
        if (command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop) return true;

        _behavior?.AttachToPartyScreen(__instance);

        var heroId = Hero.MainHero?.StringId;
        var kingdomId = Hero.MainHero?.Clan?.Kingdom?.StringId;
        var cultureId = Hero.MainHero?.Culture?.StringId;
        if (heroId == null) return true;

        var targetId = command.Character?.UpgradeTargets?[command.UpgradeTarget]?.StringId;
        if (targetId == null) return true;

        var costPerUnit = _hook.GetUpgradeCost(targetId);
        if (costPerUnit <= 0) return true;

        var clamped = _hook.ClampUpgradeCount(heroId, kingdomId, cultureId, targetId, command.TotalNumber, RecruitedThisScreen(__instance));

        if (clamped <= 0)
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: BLOCKED {targetId} x{command.TotalNumber} (insufficient resources)");
            return false;
        }

        if (clamped < command.TotalNumber)
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: CLAMPED {targetId} {command.TotalNumber}→{clamped}");
            SetTotalNumber(ref command, clamped);
            InformationManager.DisplayMessage(new InformationMessage(
                SpecialResourceMessages.UpgradeClamped(clamped).ToString(), Colors.Yellow));
        }
        else
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: ALLOWED {targetId} x{command.TotalNumber}");
        }

        return true;
    }

    // #563: outside co-op a prisoner recruited here is charged its recruit_cost at Done (OnUnitRecruited, once per
    // unit), so the command is clamped to the balance after queued upgrades and this screen's earlier recruits; in a
    // live co-op session the hook returns the full count. Returning false skips only RecruitPrisoner (no change).
    private static bool GateRecruit(PartyScreenLogic logic, ref PartyScreenLogic.PartyCommand command)
    {
        var hero = Hero.MainHero;
        var troopId = command.Character?.StringId;
        if (hero?.StringId == null || troopId == null || command.TotalNumber <= 0) return true;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        var cultureId = hero.Culture?.StringId;
        var clamped = _hook.ClampRecruitCount(hero.StringId, kingdomId, cultureId, troopId, command.TotalNumber, RecruitedThisScreen(logic));
        if (clamped >= command.TotalNumber) return true;

        if (clamped <= 0)
        {
            _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: BLOCKED recruit {troopId} x{command.TotalNumber} (insufficient resources)");
            InformationManager.DisplayMessage(new InformationMessage(
                SpecialResourceMessages.RecruitNeedsResource(_hook.GetRecruitCost(troopId), _hook.GetResourceDisplayName(kingdomId, cultureId)).ToString(),
                Colors.Yellow));
            return false;
        }

        _logger?.LogDebug($"[SpecRes] Patch26-AddCmd: CLAMPED recruit {troopId} {command.TotalNumber}→{clamped}");
        SetTotalNumber(ref command, clamped);
        InformationManager.DisplayMessage(new InformationMessage(
            SpecialResourceMessages.RecruitClamped(clamped).ToString(), Colors.Yellow));
        return true;
    }

    // The prisoners recruited earlier in this screen; the engine empties the list at Done and on Reset.
    private static List<RecruitCartEntry> RecruitedThisScreen(PartyScreenLogic logic)
    {
        var history = logic?.CurrentData?.RecruitedPrisonersHistory;
        var entries = new List<RecruitCartEntry>(history?.Count ?? 0);
        if (history == null) return entries;
        foreach (var item in history)
        {
            if (item?.Item1 != null) entries.Add(new RecruitCartEntry(item.Item1.StringId, item.Item2));
        }
        return entries;
    }

    private static void SetTotalNumber(ref PartyScreenLogic.PartyCommand command, int value)
    {
        _totalNumberField ??= typeof(PartyScreenLogic.PartyCommand)
            .GetField("<TotalNumber>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? typeof(PartyScreenLogic.PartyCommand)
            .GetField("TotalNumber", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        _totalNumberField?.SetValue(command, value);
    }
}

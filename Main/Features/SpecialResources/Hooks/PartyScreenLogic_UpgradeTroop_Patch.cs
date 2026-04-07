using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources.Hooks;

[HarmonyPatch(typeof(PartyScreenLogic), "UpgradeTroop")]
[HarmonyPatchCategory("Patch26_SpecialResources")]
public static class PartyScreenLogic_UpgradeTroop_Patch
{
    private static IOnPartyUpgradeResourceCheck _hook;
    private static IModLogger _logger;

    public static void Initialize(IOnPartyUpgradeResourceCheck hook, IModLogger logger)
    {
        _hook = hook;
        _logger = logger;
    }

    public static void Postfix(PartyScreenLogic.PartyCommand command)
    {
        if (_hook == null) return;
        if (command?.TotalNumber <= 0) return;

        var heroId = Hero.MainHero?.StringId;
        if (heroId == null) return;

        var targetId = command.Character?.UpgradeTargets?[command.UpgradeTarget]?.StringId;
        if (targetId == null) return;

        var costPerUnit = _hook.GetUpgradeCost(targetId);
        if (costPerUnit <= 0) return;

        _hook.QueueUpgradeSpend(heroId, targetId, command.TotalNumber);
        _logger?.LogDebug($"[SpecRes] Patch26-Upgrade: queued {targetId} x{command.TotalNumber} (cost/unit={costPerUnit})");
    }
}

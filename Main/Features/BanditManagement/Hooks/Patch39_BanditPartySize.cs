using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;

namespace TAOM.Features.BanditManagement.Hooks;

/// <summary>
/// Patch39 — Postfix on <see cref="DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty"/>.
///
/// Vanilla flow: <c>FindAppropriateInitialRoster</c> consults the (private) ratio function which
/// returns 0.4..1.2 for bandit parties, then for each <see cref="PartyTemplateStack"/> rolls
/// <c>num = MinValue + (MaxValue - MinValue) * ratio</c> as the troop count. Vanilla asserts
/// <c>ratio &lt;= 1.0</c>, so we cannot simply Postfix the ratio.
///
/// Our scaling: after the roster is built, walk each stack, scale its troop count UP by
/// <see cref="IBanditScalingService.GetPartySizeMultiplier"/>, and cap at the stack's
/// <c>MaxValue</c>. That respects the upper bound vanilla party templates already encode
/// while letting endgame bandit parties hit full templated strength reliably instead of
/// the random vanilla draw.
///
/// Non-bandit parties (player, lords, caravans, villagers, patrols) pass through untouched.
/// </summary>
[HarmonyPatch(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty))]
[HarmonyPatchCategory("Patch39_BanditPartySize")]
public static class Patch39_BanditPartySize
{
    private static IBanditScalingService _service;

    private static IBanditScalingService GetService() =>
        _service ??= TAOM.IoC.Resolve<IBanditScalingService>();

    [HarmonyPostfix]
    public static void Postfix(ref TroopRoster __result, MobileParty party, PartyTemplateObject partyTemplate)
    {
        if (__result == null || party == null || partyTemplate == null) return;
        if (!party.IsBandit) return;

        var service = GetService();
        if (service == null || !service.IsEnabled) return;

        var playerProgress = Campaign.Current?.PlayerProgress ?? 0f;
        var multiplier = service.GetPartySizeMultiplier(playerProgress);
        if (multiplier <= 1f) return;

        var stacks = partyTemplate.Stacks;
        if (stacks == null) return;

        for (int i = 0; i < stacks.Count; i++)
        {
            var stack = stacks[i];
            var character = stack.Character;
            if (character == null) continue;

            var current = __result.GetTroopCount(character);
            if (current <= 0) continue;

            var scaled = MathF.Round(current * multiplier);
            if (scaled > stack.MaxValue) scaled = stack.MaxValue;

            var delta = scaled - current;
            if (delta > 0)
                __result.AddToCounts(character, delta);
        }
    }
}

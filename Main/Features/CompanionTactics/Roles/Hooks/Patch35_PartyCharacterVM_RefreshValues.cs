using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.CompanionTactics.Roles.Hooks;

/// <summary>
/// Postfix on <c>PartyCharacterVM.RefreshValues</c>: appends the role prefix to companion
/// character names. Thin entry point — delegates to <see cref="IRoleTooltipDecorator"/>.
/// </summary>
[HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.RefreshValues))]
[HarmonyPatchCategory("Patch35_CompanionTactics")]
public static class Patch35_PartyCharacterVM_RefreshValues
{
    private static IRoleTooltipDecorator _decorator;

    [HarmonyPostfix]
    public static void Postfix(PartyCharacterVM __instance)
    {
        try
        {
            _decorator ??= IoC.Resolve<IRoleTooltipDecorator>();
            _decorator?.DecoratePartyCharacter(__instance);
        }
        catch
        {
            // Defensive: never crash the UI on a postfix.
        }
    }
}

using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;

namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>
/// The per-screen character-code cache. A hit sets __result and skips the whole GetCharacterCode body (vanilla computes
/// the equipment code and the body properties itself there; CharacterCode.CreateFrom only builds the string from them);
/// a miss runs vanilla and the postfix stores its result. Safe next to BannerColorPersistence: Patch23's postfix (applied
/// earlier, same priority, so it runs first) replaces the result with a code rebuilt in the clan colours
/// (ClanColoredCharacterCode.Recolor, #993), so the cache stores the recoloured code, and on a hit Recolor returns it
/// unchanged. Nothing writes to a built code, so rows can share one (pinned by PartyFastOpenBindingTests, covering
/// CharacterImageIdentifier, CharacterImageIdentifierVM and UpgradeTargetVM). The key
/// is the character, the troop type and the side, which are the only inputs besides the screen's fixed owner parties.
/// The pair links through __state.
/// </summary>
[HarmonyPatch(typeof(PartyCharacterVM), "GetCharacterCode",
    new[] { typeof(CharacterObject), typeof(PartyScreenLogic.TroopType), typeof(PartyScreenLogic.PartyRosterSide) })]
[HarmonyPatchCategory(PartyFastOpenCalls.PatchCategory)]
[ClientOnly]
public static class PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(CharacterObject character, PartyScreenLogic.TroopType type,
        PartyScreenLogic.PartyRosterSide side, ref CharacterCode __result, out bool __state)
    {
        __state = false;
        try
        {
            var service = PartyFastOpenCalls.Service;
            if (service == null || character == null) return true;
            if (service.TryGetCode(character, (int)type, (int)side, out var cached) && cached is CharacterCode code)
            {
                __result = code;
                return false;
            }
            __state = true;
        }
        catch
        {
            // Vanilla builds the code.
        }
        return true;
    }

    [HarmonyPostfix]
    public static void Postfix(CharacterObject character, PartyScreenLogic.TroopType type,
        PartyScreenLogic.PartyRosterSide side, CharacterCode __result, bool __state)
    {
        if (!__state) return;
        try
        {
            PartyFastOpenCalls.Service?.StoreCode(character, (int)type, (int)side, __result);
        }
        catch
        {
            // The row keeps the code vanilla built.
        }
    }
}

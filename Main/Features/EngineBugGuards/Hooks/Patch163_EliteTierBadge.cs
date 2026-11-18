using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.CustomBattle;
using TAOM.Features.EngineBugGuards.Domain;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch163 (#1444; ported from Silas He's v1.5.6 fix package): troops of tier 8 to 10 show no tier badge. Native
/// ships badges 1 to 7, and v1.5.7 <c>CampaignUIHelper.GetCharacterTierData</c> returns an empty badge for
/// <c>tier &gt; 7</c> (:1380); TAOM's <c>MaxCharacterTier</c> is 10. Custom battle's copy
/// (<c>CustomBattleTroopTypeVM.GetCharacterTierData</c> :209-221) clamps the tier to 7 first, so those troops read
/// "Tier 7". Both now draw native's tier 7 glyph with native's own <c>str_party_troop_tier</c> hint carrying the real
/// tier, built as native builds it (:1386-1388). Tiers 1 to 7 stay with the engine. Display only.
/// </summary>
[HarmonyPatchCategory(Category)]
[ClientOnly]
public static class Patch163_EliteTierBadge
{
    internal const string Category = "Patch163_EliteTierBadge";

    internal static StringItemWithHintVM Badge(string icon, int tier)
    {
        GameTexts.SetVariable("TIER_LEVEL", tier);
        return new StringItemWithHintVM(icon, new TextObject("{=!}" + GameTexts.FindText("str_party_troop_tier")));
    }

    [HarmonyPatch(typeof(CampaignUIHelper), nameof(CampaignUIHelper.GetCharacterTierData))]
    [HarmonyPatchCategory(Category)]
    [ClientOnly]
    public static class Patch163_CampaignTierBadge
    {
        [HarmonyPrefix]
        public static bool Prefix(CharacterObject character, bool isBig, ref StringItemWithHintVM __result)
        {
            int tier = character?.Tier ?? 0;
            string? icon = EngineDisplayRules.EliteTierIcon(tier, isBig);
            if (icon == null)
                return true;
            __result = Badge(icon, tier);
            return false;
        }
    }

    [HarmonyPatch(typeof(CustomBattleTroopTypeVM), nameof(CustomBattleTroopTypeVM.GetCharacterTierData))]
    [HarmonyPatchCategory(Category)]
    [ClientOnly]
    public static class Patch163_CustomBattleTierBadge
    {
        [HarmonyPrefix]
        public static bool Prefix(BasicCharacterObject character, bool isBig, ref StringItemWithHintVM __result)
        {
            if (character == null)
                return true;
            int tier = EngineDisplayRules.TroopTier(character.Level, character.IsHero);
            string? icon = EngineDisplayRules.EliteTierIcon(tier, isBig);
            if (icon == null)
                return true;
            __result = Badge(icon, tier);
            return false;
        }
    }
}

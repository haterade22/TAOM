using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.List;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TAOM.Features.FactionUI.Campaign
{
    internal static class EncyclopediaTroopNames
    {
        internal const string PatchCategory = "TAOM.EncyclopediaTroopNames";
        private static readonly Regex BracketedText = new Regex(@"\[[^\[\]]*\]");
        private static readonly Regex ExtraSpaces = new Regex(@"[ \t]{2,}");
        private static readonly object TooltipMarker = new object();

        [ThreadStatic] private static bool _showingEncyclopediaTooltip;

        internal static string DisplayName(string name)
        {
            if (string.IsNullOrEmpty(name) || !BracketedText.IsMatch(name)) return name;
            return ExtraSpaces.Replace(BracketedText.Replace(name, ""), " ").Trim();
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch(typeof(EncyclopediaUnitPageVM), nameof(EncyclopediaUnitPageVM.GetName))]
        private static class BreadcrumbPatch
        {
            private static void Postfix(ref string __result) => __result = DisplayName(__result);
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch(typeof(EncyclopediaUnitPageVM), nameof(EncyclopediaUnitPageVM.RefreshValues))]
        private static class PortraitPatch
        {
            private static void Postfix(EncyclopediaUnitPageVM __instance) =>
                __instance.NameText = DisplayName(__instance.NameText);
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch(typeof(EncyclopediaUnitVM), nameof(EncyclopediaUnitVM.RefreshValues))]
        private static class TroopCardPatch
        {
            private static void Postfix(EncyclopediaUnitVM __instance) =>
                __instance.NameText = DisplayName(__instance.NameText);
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch(typeof(EncyclopediaListItemVM), nameof(EncyclopediaListItemVM.RefreshValues))]
        private static class TroopListPatch
        {
            private static void Postfix(EncyclopediaListItemVM __instance)
            {
                if (__instance.Object is CharacterObject troop && !troop.IsHero)
                    __instance.Name = DisplayName(__instance.Name);
            }
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch(typeof(EncyclopediaSearchResultVM), nameof(EncyclopediaSearchResultVM.UpdateSearchedText))]
        private static class SearchResultPatch
        {
            private static void Postfix(EncyclopediaSearchResultVM __instance, string searchedText)
            {
                if (__instance.PageType != "NPCCharacter") return;
                string name = DisplayName(__instance.OrgNameText);
                if (name == __instance.OrgNameText) return;

                // Keep the original name available for matching and highlight visible matches.
                int match = name.IndexOf(searchedText, StringComparison.InvariantCultureIgnoreCase);
                if (match >= 0 && searchedText.Length > 0)
                {
                    string matchedText = name.Substring(match, Math.Min(searchedText.Length, name.Length - match));
                    if (matchedText.Length > 0)
                        name = name.Replace(matchedText, "<a>" + matchedText + "</a>");
                }
                __instance.NameText = name;
            }
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch]
        private static class EncyclopediaHoverPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(EncyclopediaUnitVM), nameof(EncyclopediaUnitVM.ExecuteBeginHint));
                yield return AccessTools.Method(typeof(EncyclopediaListItemVM), nameof(EncyclopediaListItemVM.ExecuteBeginTooltip));
            }

            private static void Prefix(out bool __state)
            {
                __state = _showingEncyclopediaTooltip;
                _showingEncyclopediaTooltip = true;
            }

            private static void Finalizer(bool __state) => _showingEncyclopediaTooltip = __state;
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch(typeof(InformationManager), nameof(InformationManager.ShowTooltip))]
        private static class MarkTroopTooltipPatch
        {
            private static void Prefix(Type type, ref object[] args)
            {
                // The tooltip can refresh after the hover callback returns, so carry the context with it.
                if (_showingEncyclopediaTooltip && type == typeof(CharacterObject) &&
                    args?.Length == 1 && args[0] is CharacterObject troop && !troop.IsHero)
                    args = new[] { args[0], TooltipMarker };
            }
        }

        [HarmonyPatchCategory(PatchCategory)]
        [HarmonyPatch(typeof(TooltipRefresherCollection), nameof(TooltipRefresherCollection.RefreshCharacterTooltip))]
        private static class TroopTooltipTitlePatch
        {
            private static void Postfix(PropertyBasedTooltipVM propertyBasedTooltipVM, object[] args)
            {
                if (args.Length != 2 || !ReferenceEquals(args[1], TooltipMarker)) return;
                var title = propertyBasedTooltipVM.TooltipPropertyList
                    .FirstOrDefault(property => property.PropertyModifier == (int)TooltipProperty.TooltipPropertyFlags.Title);
                if (title != null) title.ValueLabel = DisplayName(title.ValueLabel);
            }
        }
    }
}

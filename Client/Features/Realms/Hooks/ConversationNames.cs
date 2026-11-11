using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Conversation;
using TaleWorlds.Library;
using TAOM.Features.Realms;

namespace TAOM.Client.Features.Realms.Hooks;

/// <summary>
/// The speaker's name above a conversation carries their style and highest title, as their party name does: "Lord Faramir
/// of Ithilien". The engine sets it in <c>MissionConversationVM.Refresh</c> (on every line) to the plain name, or to the
/// culture's noble title for a clan leader in a kingdom; a postfix replaces it when the speaker holds a styled title.
/// A speaker without one keeps the engine's name.
/// </summary>
internal static class ConversationNames
{
    private static bool _patched;
    private static bool _faultReported;

    public static void Patch()
    {
        if (_patched)
            return;
        _patched = true;
        try
        {
            var original = AccessTools.Method(typeof(MissionConversationVM), nameof(MissionConversationVM.Refresh));
            if (original == null)
            {
                Debug.Print("[TAOM.Realms] MissionConversationVM.Refresh not found; conversations keep the plain hero name.");
                return;
            }
            new Harmony("TAOM.Realms.ConversationNames").Patch(original, postfix: new HarmonyMethod(typeof(ConversationNames), nameof(Postfix)));
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] conversation name styles are off: {ex}");
        }
    }

    private static void Postfix(MissionConversationVM __instance)
    {
        try
        {
            var hero = (TaleWorlds.CampaignSystem.Campaign.Current?.ConversationManager?.SpeakerAgent?.Character as CharacterObject)?.HeroObject;
            var behavior = RealmsCampaign.Current;
            if (hero == null || behavior == null || !behavior.Service.IsInitialized || behavior.IsDisabled || !behavior.Settings.TitleStyles)
                return;
            var styled = TitleNames.KnownAs(hero, behavior);
            if (styled != null)
                __instance.CurrentCharacterNameLbl = styled.ToString();
        }
        catch (Exception ex)
        {
            if (_faultReported)
                return;
            _faultReported = true;
            Debug.Print($"[TAOM.Realms] conversation name style failed; the engine's name is kept: {ex}");
        }
    }
}

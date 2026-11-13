using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.EncounterLines.Hooks;

/// <summary>
/// Patch149's text seam: a postfix on public <c>ConversationManager.UpdateCurrentSentenceText</c>, which stores the text of the
/// sentence being spoken. For the three sentences the service handles (caravan and villager surrender, the hideout boss's
/// challenge) it replaces the stored text with <c>{=!}{TAOM_ENC_LINE}</c>; the <c>{=!}</c> id keeps
/// <c>CurrentSentenceText</c> from re-resolving the line by a game-text id. The private fields are bound once in
/// <see cref="Initialize"/> (the sentence list as <c>object</c>, read as its list type); a failed binding or any fault leaves vanilla's text and warns once. Callers, thread and co-op:
/// docs/reference/harmony-patch-registry.md "Patch149_EncounterLines".
/// </summary>
[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.UpdateCurrentSentenceText))]
[HarmonyPatchCategory(EncounterLinesModule.PatchCategory)]
public static class EncounterSentenceTextPatch
{
    internal const string SeamText = "{=!}{" + EncounterLinesService.SentenceLineVariable + "}";

    private static bool _reported;
    private static IModLogger? _logger;
    private static AccessTools.FieldRef<ConversationManager, object>? _sentences;
    private static AccessTools.FieldRef<ConversationManager, int>? _currentSentence;
    private static AccessTools.FieldRef<ConversationManager, TextObject>? _currentSentenceText;

    internal static EncounterLinesService? Service { get; private set; }

    internal static void Initialize(EncounterLinesService service, IModLogger logger)
    {
        Service = service;
        _logger = logger;
        _reported = false;
        try
        {
            _sentences = AccessTools.FieldRefAccess<ConversationManager, object>("_sentences");
            _currentSentence = AccessTools.FieldRefAccess<ConversationManager, int>("_currentSentence");
            _currentSentenceText = AccessTools.FieldRefAccess<ConversationManager, TextObject>("_currentSentenceText");
        }
        catch (Exception ex)
        {
            _sentences = null;
            Report("the ConversationManager fields did not bind, surrender and hideout lines stay vanilla: " + ex.Message);
        }
    }

    [HarmonyPostfix]
    public static void Postfix(ConversationManager __instance)
    {
        var service = Service;
        var sentences = _sentences;
        var current = _currentSentence;
        var text = _currentSentenceText;
        if (service == null || sentences == null || current == null || text == null || __instance == null)
            return;

        try
        {
            var index = current(__instance);
            var list = sentences(__instance) as List<ConversationSentence>;
            if (index < 0 || list == null || index >= list.Count)
                return;
            if (service.TrySetSentenceLine(list[index]?.Id))
                text(__instance) = new TextObject(SeamText);
        }
        catch (Exception ex)
        {
            Report("a surrender or hideout line failed, vanilla's text stands: " + ex.Message);
        }
    }

    private static void Report(string message)
    {
        if (_reported)
            return;
        _reported = true;
        _logger?.LogWarning("[EncounterLines] " + message);
    }
}

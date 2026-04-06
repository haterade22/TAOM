using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.BannerColorPersistence.Hooks;

/// <summary>
/// Patches MapConversationTableau.SpawnOpponentLeader (private, SandBox.View.dll).
/// Vanilla uses CharacterHelper.GetDeterministicColorsForCharacter which returns generic
/// faction colors. This postfix applies clan-specific colors to the spawned AgentVisuals.
/// Applied manually since both the type and method are private/in a View assembly.
/// </summary>
public static class MapConversationTableau_SpawnOpponentLeader_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("SandBox.View.Map.MapConversationTableau");
        return type == null ? null : AccessTools.Method(type, "SpawnOpponentLeader");
    }

    public static void Postfix(object __instance)
    {
        if (!(_service?.IsConversationTableauColorsEnabled() ?? false)) return;

        var dataField = AccessTools.Field(__instance.GetType(), "_data");
        if (dataField == null) return;

        var data = dataField.GetValue(__instance);
        if (data == null) return;

        var partnerDataProp = AccessTools.Property(data.GetType(), "ConversationPartnerData");
        var partnerData = partnerDataProp?.GetValue(data);
        if (partnerData == null) return;

        var characterProp = AccessTools.Property(partnerData.GetType(), "Character");
        var character = characterProp?.GetValue(partnerData) as CharacterObject;
        if (character == null) return;

        var info = _heroAdapter?.GetClanColorInfo(character);
        if (info == null || !(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        // Apply to the last-added AgentVisuals in the _agentVisuals list
        var visualsField = AccessTools.Field(__instance.GetType(), "_agentVisuals");
        if (visualsField?.GetValue(__instance) is not System.Collections.IList agentVisuals) return;
        if (agentVisuals.Count == 0) return;

        var lastVisual = agentVisuals[agentVisuals.Count - 1];
        if (lastVisual == null) return;

        // Set colors on the AgentVisualsData via the _data field on AgentVisuals
        var visDataField = AccessTools.Field(lastVisual.GetType(), "_data");
        var visData = visDataField?.GetValue(lastVisual);
        if (visData == null) return;

        var clothColor1Method = AccessTools.Method(visData.GetType(), "ClothColor1", new[] { typeof(uint) });
        var clothColor2Method = AccessTools.Method(visData.GetType(), "ClothColor2", new[] { typeof(uint) });
        clothColor1Method?.Invoke(visData, new object[] { info.Value.Color1 });
        clothColor2Method?.Invoke(visData, new object[] { info.Value.Color2 });
    }
}

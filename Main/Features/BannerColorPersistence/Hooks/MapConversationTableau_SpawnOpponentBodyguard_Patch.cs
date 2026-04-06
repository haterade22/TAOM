using System.Reflection;
using HarmonyLib;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.BannerColorPersistence.Hooks;

/// <summary>
/// Patches MapConversationTableau.SpawnOpponentBodyguardCharacter (private, SandBox.View.dll).
/// Vanilla uses party.LeaderHero.MapFaction colors. This postfix applies clan-specific colors.
/// Applied manually — type and method are private/in a View assembly.
/// </summary>
public static class MapConversationTableau_SpawnOpponentBodyguard_Patch
{
    private static IBannerColorService? _service;
    private static IBannerHeroAdapter? _heroAdapter;

    // Cached reflection — resolved once in TargetMethod, reused per spawn
    private static FieldInfo? _agentVisualsField;
    private static FieldInfo? _visDataField;
    private static MethodInfo? _clothColor1Method;
    private static MethodInfo? _clothColor2Method;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("SandBox.View.Map.MapConversationTableau");
        if (type == null) return null;

        _agentVisualsField = AccessTools.Field(type, "_agentVisuals");

        var agentVisualsType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.AgentVisuals");
        if (agentVisualsType != null)
        {
            _visDataField = AccessTools.Field(agentVisualsType, "_data");
            var visDataType = _visDataField?.FieldType;
            if (visDataType != null)
            {
                _clothColor1Method = AccessTools.Method(visDataType, "ClothColor1", new[] { typeof(uint) });
                _clothColor2Method = AccessTools.Method(visDataType, "ClothColor2", new[] { typeof(uint) });
            }
        }

        return AccessTools.Method(type, "SpawnOpponentBodyguardCharacter",
            new[] { typeof(CharacterObject), typeof(int), typeof(PartyBase) });
    }

    public static void Postfix(object __instance, CharacterObject character)
    {
        if (!(_service?.IsConversationTableauColorsEnabled() ?? false)) return;
        if (character == null) return;

        var info = _heroAdapter?.GetClanColorInfo(character);
        if (info == null || !(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        if (_agentVisualsField?.GetValue(__instance) is not System.Collections.IList agentVisuals) return;
        if (agentVisuals.Count == 0) return;

        var lastVisual = agentVisuals[agentVisuals.Count - 1];
        if (lastVisual == null) return;

        var visData = _visDataField?.GetValue(lastVisual);
        if (visData == null) return;

        _clothColor1Method?.Invoke(visData, new object[] { info.Value.Color1 });
        _clothColor2Method?.Invoke(visData, new object[] { info.Value.Color2 });
    }
}

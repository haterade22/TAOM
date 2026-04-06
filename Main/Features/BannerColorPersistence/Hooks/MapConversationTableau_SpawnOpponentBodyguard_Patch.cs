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

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("SandBox.View.Map.MapConversationTableau");
        return type == null
            ? null
            : AccessTools.Method(type, "SpawnOpponentBodyguardCharacter",
                new[] { typeof(CharacterObject), typeof(int), typeof(PartyBase) });
    }

    public static void Postfix(object __instance, CharacterObject character)
    {
        if (!(_service?.IsConversationTableauColorsEnabled() ?? false)) return;
        if (character == null) return;

        var info = _heroAdapter?.GetClanColorInfo(character);
        if (info == null || !(_service?.ShouldUseClanColor(info.Value) ?? false)) return;

        var visualsField = AccessTools.Field(__instance.GetType(), "_agentVisuals");
        if (visualsField?.GetValue(__instance) is not System.Collections.IList agentVisuals) return;
        if (agentVisuals.Count == 0) return;

        var lastVisual = agentVisuals[agentVisuals.Count - 1];
        if (lastVisual == null) return;

        var visDataField = AccessTools.Field(lastVisual.GetType(), "_data");
        var visData = visDataField?.GetValue(lastVisual);
        if (visData == null) return;

        var clothColor1Method = AccessTools.Method(visData.GetType(), "ClothColor1", new[] { typeof(uint) });
        var clothColor2Method = AccessTools.Method(visData.GetType(), "ClothColor2", new[] { typeof(uint) });
        clothColor1Method?.Invoke(visData, new object[] { info.Value.Color1 });
        clothColor2Method?.Invoke(visData, new object[] { info.Value.Color2 });
    }
}

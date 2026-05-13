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

    // Cached reflection — resolved once in Initialize, reused per spawn
    private static FieldInfo? _dataField;
    private static PropertyInfo? _partnerDataProp;
    private static FieldInfo? _characterField;
    private static FieldInfo? _agentVisualsField;
    private static FieldInfo? _visDataField;
    private static MethodInfo? _clothColor1Method;
    private static MethodInfo? _clothColor2Method;
    // Phase 9b #150 — Post-construction Refresh(needBatched=false, data, forceUseFaceCache=false)
    // applies the new ClothColor1Data/ClothColor2Data to the rendered meshes via
    // AddTeamColorToMesh / AddSkinArmorWeaponMultiMeshesToEntity. Without this, ClothColor1/2
    // fluent setters only mutate C# state — native already captured the vanilla deterministic
    // colors at MBAgentVisuals.CreateAgentVisuals time and our writes are silent no-ops.
    private static MethodInfo? _refreshMethod;

    public static void Initialize(IBannerColorService service, IBannerHeroAdapter heroAdapter)
    {
        _service = service;
        _heroAdapter = heroAdapter;
    }

    public static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("SandBox.View.Map.MapConversationTableau");
        if (type == null) return null;

        // Cache reflection for the tableau type
        _dataField = AccessTools.Field(type, "_data");
        _agentVisualsField = AccessTools.Field(type, "_agentVisuals");

        // Cache MapConversationTableauData members
        var dataType = _dataField?.FieldType;
        if (dataType != null)
        {
            _partnerDataProp = AccessTools.Property(dataType, "ConversationPartnerData");
            var partnerType = _partnerDataProp?.PropertyType;
            if (partnerType != null)
                _characterField = AccessTools.Field(partnerType, "Character");
        }

        // Cache AgentVisuals._data and AgentVisualsData methods
        var agentVisualsType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.AgentVisuals");
        if (agentVisualsType != null)
        {
            _visDataField = AccessTools.Field(agentVisualsType, "_data");
            var visDataType = _visDataField?.FieldType;
            if (visDataType != null)
            {
                _clothColor1Method = AccessTools.Method(visDataType, "ClothColor1", new[] { typeof(uint) });
                _clothColor2Method = AccessTools.Method(visDataType, "ClothColor2", new[] { typeof(uint) });

                // Public Refresh(bool needBatchedVersionForWeaponMeshes, AgentVisualsData data,
                // bool forceUseFaceCache = false) — line 210 of
                // TaleWorlds.MountAndBlade.View.AgentVisuals in v1.3.15/v1.4 decompile.
                _refreshMethod = AccessTools.Method(agentVisualsType, "Refresh",
                    new[] { typeof(bool), visDataType, typeof(bool) });
            }
        }

        return AccessTools.Method(type, "SpawnOpponentLeader");
    }

    public static void Postfix(object __instance)
    {
        if (!(_service?.IsConversationTableauColorsEnabled() ?? false)) return;

        var data = _dataField?.GetValue(__instance);
        if (data == null) return;

        var partnerData = _partnerDataProp?.GetValue(data);
        if (partnerData == null) return;

        // Character is a public FIELD on ConversationCharacterData (not a property)
        var character = _characterField?.GetValue(partnerData) as CharacterObject;
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

        // Phase 9b #150 — push the new colors to native meshes. Without Refresh(...), the fluent
        // ClothColor1/2 setters above only update the C# AgentVisualsData field; the native
        // renderer captured the vanilla deterministic colors at MBAgentVisuals.CreateAgentVisuals
        // time during the AgentVisuals ctor and our post-construction writes are silent no-ops.
        // Refresh re-invokes AddTeamColorToMesh / AddSkinArmorWeaponMultiMeshesToEntity with the
        // updated ClothColorData on the data, pushing the clan colors all the way to the GPU.
        _refreshMethod?.Invoke(lastVisual, new object[] { false, visData, false });
    }
}

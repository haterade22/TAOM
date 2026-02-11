using System.IO;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;

namespace TAOM.Features.BattleScenes.Hooks;

[HarmonyPatch(typeof(Campaign), "InitializeScenes")]
[HarmonyPatchCategory("Patch0_BattleScenes")]
public class Campaign_InitializeScenes_Patch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        var taomModulePath = ModuleHelper.GetModuleFullPath("TAOM") + "ModuleData/";
        var sandboxModulePath = ModuleHelper.GetModuleFullPath("Sandbox") + "ModuleData/";

        var battleScenesPath = taomModulePath + "sp_battle_scenes.xml";
        if (File.Exists(battleScenesPath))
        {
            GameSceneDataManager.Instance.LoadSPBattleScenes(battleScenesPath);
        }

        var conversationPath = sandboxModulePath + "conversation_scenes.xml";
        if (File.Exists(conversationPath))
        {
            GameSceneDataManager.Instance.LoadConversationScenes(conversationPath);
        }

        var meetingPath = sandboxModulePath + "meeting_scenes.xml";
        if (File.Exists(meetingPath))
        {
            GameSceneDataManager.Instance.LoadMeetingScenes(meetingPath);
        }

        return false;
    }
}

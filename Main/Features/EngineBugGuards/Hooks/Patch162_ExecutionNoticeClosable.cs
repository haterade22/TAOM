using HarmonyLib;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch162 (#1444; ported from Silas He's v1.5.6 fix package): the "clan member executed a lord" notice cannot be
/// closed. v1.5.7 <c>BloodFeudClanMemberExecutedLordMapNotificationItemVM</c>'s constructor sets
/// <c>ForceInspection = true</c> (:13); SandBox's <c>MapNotificationItem.xml</c> binds it to the widget, which then
/// hides the close button (<c>MapNotificationItemWidget</c> in TaleWorlds.MountAndBlade.GauntletUI.Widgets, :317) and
/// refuses right-click removal (:360), so each notice must be opened, which
/// only plays the execution picture. Mike chose to make it closable like any other notice (2026-11-18). The setter writes
/// only outside development mode (MapNotificationItemBaseVM :120-127), so a dev-mode game keeps the native behaviour.
/// </summary>
[HarmonyPatch(typeof(BloodFeudClanMemberExecutedLordMapNotificationItemVM), MethodType.Constructor,
    typeof(BloodFeudClanMemberExecutedLordMapNotification))]
[HarmonyPatchCategory(Category)]
[ClientOnly]
public static class Patch162_ExecutionNoticeClosable
{
    internal const string Category = "Patch162_ExecutionNoticeClosable";

    [HarmonyPostfix]
    public static void Postfix(BloodFeudClanMemberExecutedLordMapNotificationItemVM __instance)
        => __instance.ForceInspection = false;
}

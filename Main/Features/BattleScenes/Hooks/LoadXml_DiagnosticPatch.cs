using System;
using System.Collections;
using System.Reflection;
using System.Xml;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.BattleScenes.Hooks;

[HarmonyPatch(typeof(MBObjectManager), nameof(MBObjectManager.LoadXml),
    new Type[] { typeof(XmlDocument), typeof(bool) })]
[HarmonyPatchCategory("Patch0_BattleScenes")]
public class LoadXml_DiagnosticPatch
{
    private static readonly FieldInfo ObjectTypeRecordsField =
        AccessTools.Field(typeof(MBObjectManager), "ObjectTypeRecords");

    private static readonly MethodInfo GetPresumedObjectMethod =
        AccessTools.Method(typeof(MBObjectManager), "GetPresumedObject",
            new Type[] { typeof(string), typeof(string), typeof(bool) });

    [HarmonyPrefix]
    public static bool Prefix(MBObjectManager __instance, XmlDocument doc, bool isDevelopment)
    {
        var objectTypeRecords = (IList)ObjectTypeRecordsField.GetValue(__instance);

        int i = 0;
        bool flag = false;
        string typeName = null;

        for (; i < doc.ChildNodes.Count; i++)
        {
            foreach (var record in objectTypeRecords)
            {
                var elementListNameProp = record.GetType().GetProperty("ElementListName");
                var elementNameProp = record.GetType().GetProperty("ElementName");
                string elementListName = (string)elementListNameProp.GetValue(record);

                if (elementListName == doc.ChildNodes[i].Name)
                {
                    typeName = (string)elementNameProp.GetValue(record);
                    flag = true;
                    break;
                }
            }
            if (flag)
                break;
        }

        if (!flag)
            return false;

        bool isNpcType = typeName == "NPCCharacter";
        if (isNpcType)
        {
            Debug.Print($"TAOM-DIAG: === Loading {typeName} file with {doc.ChildNodes[i].ChildNodes.Count} entries ===",
                0, Debug.DebugColor.Yellow, 17592186044416uL);
        }

        for (XmlNode xmlNode = doc.ChildNodes[i].ChildNodes[0]; xmlNode != null; xmlNode = xmlNode.NextSibling)
        {
            if (xmlNode.NodeType == XmlNodeType.Comment)
                continue;

            string id = xmlNode.Attributes?["id"]?.Value ?? "(no id)";

            try
            {
                var presumedObject = (MBObjectBase)GetPresumedObjectMethod.Invoke(
                    __instance, new object[] { typeName, id, true });

                if (presumedObject == null)
                {
                    if (isNpcType)
                        Debug.Print($"TAOM-DIAG: GetPresumedObject returned null for {id}",
                            0, Debug.DebugColor.Red, 17592186044416uL);
                    continue;
                }

                if (isNpcType)
                {
                    Debug.Print($"TAOM-DIAG: Deserializing {id} (IsReady={presumedObject.IsReady}, type={presumedObject.GetType().Name})",
                        0, Debug.DebugColor.White, 17592186044416uL);
                }

                presumedObject.Deserialize(__instance, xmlNode);
                presumedObject.AfterInitialized();

                if (isNpcType)
                {
                    Debug.Print($"TAOM-DIAG: OK {id} (IsReady={presumedObject.IsReady})",
                        0, Debug.DebugColor.Green, 17592186044416uL);
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"TAOM-DIAG: EXCEPTION loading {typeName} '{id}': {ex.GetType().Name}: {ex.Message}",
                    0, Debug.DebugColor.Red, 17592186044416uL);
                Debug.Print($"TAOM-DIAG: Stack: {ex.StackTrace}",
                    0, Debug.DebugColor.Red, 17592186044416uL);
                if (ex.InnerException != null)
                {
                    Debug.Print($"TAOM-DIAG: Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}",
                        0, Debug.DebugColor.Red, 17592186044416uL);
                    Debug.Print($"TAOM-DIAG: Inner Stack: {ex.InnerException.StackTrace}",
                        0, Debug.DebugColor.Red, 17592186044416uL);
                }
            }
        }

        return false; // Skip original
    }
}

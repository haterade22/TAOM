using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Features.SaveLoadDiagnostics.Hooks;

// Per-behavior SyncData sizes at save (plan 114, SAVE-TIME-05). The engine builds each record during
// Game.SaveAux's OnBeforeSave pass, before SaveManager.Save (installed v1.4.8 Game.cs:199-205), so a
// sample is only stored here and SaveBegin writes the summary. Observe only. Own category, apart from
// the BehaviorData finalizer, because it binds two private engine fields by name.
[HarmonyPatchCategory(Category)]
public static class CampaignBehaviorDataStore_SaveBehaviorData_Size_Patch
{
    internal const string Category = "Patch61_SaveLoadDiagnostics_BehaviorSize";

    private static ISaveLoadDiagnosticsService? _service;
    private static IModLogger? _logger;
    private static FieldInfo? _behaviorDictField;
    private static FieldInfo? _recordsField;

    public static void Initialize(ISaveLoadDiagnosticsService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyTargetMethod]
    public static MethodBase? TargetMethod()
    {
        var store = CampaignBehaviorDataStore_LoadBehaviorData_Patch.ResolveStoreType();
        if (store == null)
        {
            _logger?.LogWarning("[SaveLoad] CampaignBehaviorDataStore type not found: per-behavior save sizes not applied (engine drift?)");
            return null;
        }

        _behaviorDictField = AccessTools.Field(store, "_behaviorDict");
        var record = AccessTools.Inner(store, "BehaviorSaveData");
        _recordsField = record == null ? null : AccessTools.Field(record, "_records");
        if (_behaviorDictField == null || _recordsField == null)
            _logger?.LogWarning("[SaveLoad] CampaignBehaviorDataStore record fields not found: per-behavior save sizes are not recorded (engine drift?)");

        return AccessTools.Method(store, "SaveBehaviorData");
    }

    [HarmonyPrefix]
    public static void Prefix(out long __state) => __state = Stopwatch.GetTimestamp();

    [HarmonyPostfix]
    public static void Postfix(object __instance, CampaignBehaviorBase campaignBehavior, long __state)
    {
        var svc = _service;
        var dictField = _behaviorDictField;
        var recordsField = _recordsField;
        if (svc == null || dictField == null || recordsField == null) return;
        try
        {
            double ms = (Stopwatch.GetTimestamp() - __state) * 1000.0 / Stopwatch.Frequency;
            var record = (dictField.GetValue(__instance) as IDictionary)?[campaignBehavior.StringId];
            if (record == null) return;
            var records = recordsField.GetValue(record) as IDictionary<string, object>;
            svc.RecordBehaviorSave(campaignBehavior.StringId, campaignBehavior.GetType().FullName,
                BehaviorRecordStats.Measure(records), ms);
        }
        catch { /* diagnostic only */ }
    }
}

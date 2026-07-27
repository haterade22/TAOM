using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.TroopProgression;

// Loads Gondor per-settlement recruitment pools from ModuleData/recruitment_pools/gondor.json.
// JSON percentages are converted to integer weights (4-digit precision: percent * 10000).
// Groups with a "condition" string trigger AddSettlementConditional — currently only the Ithil
// Guard rule ("Apply only if town_ES2 is captured by Gondor") is recognised. Any unknown
// condition string is logged and the group is skipped (fail-closed, not fail-open).
internal static class GondorRecruitmentJsonLoader
{
    private const string JsonRelativePath = "Modules/TAOM/ModuleData/recruitment_pools/gondor.json";
    private const int PercentToWeightScale = 10000;

    public static void Load(
        Action<string, (string, int)[]> addSettlement,
        Action<string, Func<VolunteerContext, bool>, (string, int)[]> addSettlementConditional,
        IModLogger logger)
    {
        string path;
        try
        {
            path = Path.Combine(BasePath.Name, JsonRelativePath);
        }
        catch (Exception ex)
        {
            logger?.LogError($"GondorRecruitmentJsonLoader: failed to resolve game base path ({ex.Message}); Gondor JSON pools NOT loaded");
            return;
        }
        LoadFromPath(path, addSettlement, addSettlementConditional, logger);
    }

    // Test seam — accepts an explicit JSON path so unit tests can point at fixture files
    // without depending on BasePath.Name (which resolves to the test runner's directory).
    internal static void LoadFromPath(
        string path,
        Action<string, (string, int)[]> addSettlement,
        Action<string, Func<VolunteerContext, bool>, (string, int)[]> addSettlementConditional,
        IModLogger logger)
    {
        if (!File.Exists(path))
        {
            logger?.LogWarning($"GondorRecruitmentJsonLoader: {path} not found; Gondor falls back to vanilla DefaultVolunteerModel");
            return;
        }

        GondorRecruitmentJsonRoot root;
        try
        {
            var json = File.ReadAllText(path);
            root = JsonConvert.DeserializeObject<GondorRecruitmentJsonRoot>(json);
        }
        catch (Exception ex)
        {
            logger?.LogError($"GondorRecruitmentJsonLoader: failed to parse {path}: {ex.Message}; Gondor falls back to vanilla DefaultVolunteerModel");
            return;
        }

        if (root?.ChanceGroups == null)
        {
            logger?.LogWarning("GondorRecruitmentJsonLoader: gondor.json missing chance_groups; skipping");
            return;
        }

        int groupsApplied = 0;
        int settlementsApplied = 0;
        for (int g = 0; g < root.ChanceGroups.Count; g++)
        {
            var group = root.ChanceGroups[g];
            var label = !string.IsNullOrEmpty(group.Description) ? group.Description : $"group#{g}";

            if (group.Settlements == null || group.Settlements.Count == 0)
            {
                logger?.LogWarning($"GondorRecruitmentJsonLoader: {label} has no settlements; skipping");
                continue;
            }

            if (group.Troops == null || group.Troops.Count == 0)
            {
                logger?.LogWarning($"GondorRecruitmentJsonLoader: {label} has no troops; skipping");
                continue;
            }

            var entries = BuildEntries(label, group.Troops, logger);
            if (entries == null) continue;

            Func<VolunteerContext, bool> conditionFn = null;
            if (!string.IsNullOrWhiteSpace(group.Condition))
            {
                conditionFn = ResolveCondition(group.Condition, label, logger);
                if (conditionFn == null) continue;
            }

            foreach (var settlementId in group.Settlements)
            {
                if (string.IsNullOrWhiteSpace(settlementId))
                {
                    logger?.LogWarning($"GondorRecruitmentJsonLoader: {label} has a blank settlement id; skipping");
                    continue;
                }

                try
                {
                    if (conditionFn != null)
                        addSettlementConditional(settlementId, conditionFn, entries);
                    else
                        addSettlement(settlementId, entries);
                    settlementsApplied++;
                }
                catch (Exception ex)
                {
                    logger?.LogError($"GondorRecruitmentJsonLoader: {label}/{settlementId} failed to register: {ex.Message}");
                }
            }
            groupsApplied++;
        }

        logger?.LogInfo($"GondorRecruitmentJsonLoader: loaded {groupsApplied} group(s) across {settlementsApplied} settlement(s) from {path}");
    }

    private static (string troopId, int weight)[] BuildEntries(string label, Dictionary<string, double> troops, IModLogger logger)
    {
        var entries = new List<(string, int)>(troops.Count);
        double totalPercent = 0;
        foreach (var kv in troops)
        {
            var troopId = kv.Key;
            var percent = kv.Value;

            if (string.IsNullOrWhiteSpace(troopId))
            {
                logger?.LogWarning($"GondorRecruitmentJsonLoader: {label} contains a blank troop id; skipping entry");
                continue;
            }
            if (double.IsNaN(percent) || double.IsInfinity(percent))
            {
                logger?.LogWarning($"GondorRecruitmentJsonLoader: {label}/{troopId} weight is NaN/Infinity; skipping entry");
                continue;
            }
            if (percent <= 0)
            {
                logger?.LogWarning($"GondorRecruitmentJsonLoader: {label}/{troopId} weight {percent} <= 0; skipping entry");
                continue;
            }

            // Convert percentage → integer weight (preserves 4-digit precision).
            // E.g. 33.3334 → 333334. BuildPool requires int weights > 0.
            int weight = (int)Math.Round(percent * PercentToWeightScale);
            if (weight <= 0)
            {
                logger?.LogWarning($"GondorRecruitmentJsonLoader: {label}/{troopId} percent {percent} rounded to weight 0; bumping to 1");
                weight = 1;
            }

            entries.Add((troopId, weight));
            totalPercent += percent;
        }

        if (entries.Count == 0)
        {
            logger?.LogWarning($"GondorRecruitmentJsonLoader: {label} produced no valid entries after sanitization; skipping group");
            return null;
        }

        // The file's own notes state "Percentages total to 100 per settlement group". PickWeighted
        // normalises cumulatively, so an off-100 group never throws — it just silently delivers a
        // distribution the file does not describe. One group shipped at 120% exactly that way. The unit
        // tests gate the COMMITTED file; this warns for a hand-edited install that never ran them.
        // Warn rather than reject: normalisation keeps the pool playable, and dropping a group here would
        // fall the settlement through to a coarser pool for a mistake that is merely cosmetic in effect.
        if (Math.Abs(totalPercent - 100.0) > 0.01)
        {
            logger?.LogWarning(
                $"GondorRecruitmentJsonLoader: {label} totals {totalPercent:0.####}%, not 100 — weights still " +
                "normalise, so the pool works, but the delivered distribution will not match the authored percentages");
        }

        return entries.ToArray();
    }

    private static Func<VolunteerContext, bool> ResolveCondition(string condition, string label, IModLogger logger)
    {
        // The only condition currently understood is the Ithil Guard rule. Match permissively (case-insensitive
        // substring) because the JSON `condition` field is descriptive English, not a DSL.
        var lower = condition.ToLowerInvariant();
        if (lower.Contains("town_es2") && lower.Contains("gondor"))
            return ctx => ctx.OwnerCultureId == "gondor";

        logger?.LogWarning($"GondorRecruitmentJsonLoader: {label} has unrecognised condition '{condition}'; skipping group (fail-closed)");
        return null;
    }
}

#pragma warning disable CS0649 // assigned by Newtonsoft via reflection
internal sealed class GondorRecruitmentJsonRoot
{
    [JsonProperty("chance_groups")]
    public List<GondorRecruitmentChanceGroup> ChanceGroups;
}

internal sealed class GondorRecruitmentChanceGroup
{
    [JsonProperty("description")]
    public string Description;

    [JsonProperty("settlements")]
    public List<string> Settlements;

    [JsonProperty("troops")]
    public Dictionary<string, double> Troops;

    [JsonProperty("condition")]
    public string Condition;
}
#pragma warning restore CS0649

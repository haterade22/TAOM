using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Pure LOTR-issue decision logic. Loads definitions once from the config provider, then answers
/// eligibility / scaling / completion / reward queries with no TaleWorlds dependencies. Singleton.
/// </summary>
public class LotrIssueService : ILotrIssueService
{
    private readonly ILotrIssueConfigProvider _configProvider;
    private readonly IModLogger _logger;

    private List<LotrIssueDefinition> _all;
    private Dictionary<string, LotrIssueDefinition> _byId;

    public LotrIssueService(ILotrIssueConfigProvider configProvider, IModLogger logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public IReadOnlyList<LotrIssueDefinition> GetEligibleIssues(ILotrIssueGiverAdapter giver)
    {
        var result = new List<LotrIssueDefinition>();
        if (giver == null || !giver.IsValid || giver.Occupation == null) return result;

        EnsureLoaded();
        foreach (var def in _all)
        {
            if (def.GiverOccupation != giver.Occupation.Value) continue;
            if (!def.AppliesToCulture(giver.CultureStringId)) continue;
            if (giver.RelationWithPlayer < def.RelationMin) continue;
            result.Add(def);
        }
        return result;
    }

    public LotrIssueDefinition GetIssueById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureLoaded();
        return _byId.TryGetValue(id, out var def) ? def : null;
    }

    public int ComputeTargetCount(LotrIssueDefinition def, float difficulty)
    {
        if (def == null) return 0;
        return def.Count + (int)Math.Round(def.CountPerDifficulty * Clamp01(difficulty));
    }

    public int ComputeRewardGold(LotrIssueDefinition def, float difficulty)
    {
        if (def == null) return 0;
        return def.RewardGoldBase + (int)Math.Round(def.RewardGoldPerDifficulty * Clamp01(difficulty));
    }

    public bool IsObjectiveSatisfied(int progress, int target) => target > 0 && progress >= target;

    public int CountDeliverableCaptives(IReadOnlyList<LotrCaptiveStack> stacks, int cap)
    {
        if (stacks == null || cap <= 0) return 0;
        var sum = 0;
        for (var i = 0; i < stacks.Count && sum < cap; i++)
            if (IsDeliverable(stacks[i])) sum += stacks[i].Number;
        return sum <= cap ? sum : cap;
    }

    public IReadOnlyList<int> PlanCaptiveHandover(IReadOnlyList<LotrCaptiveStack> stacks, int count)
    {
        if (stacks == null) return new int[0];
        var plan = new int[stacks.Count];
        var remaining = count > 0 ? count : 0;
        for (var i = 0; i < stacks.Count && remaining > 0; i++)
        {
            if (!IsDeliverable(stacks[i])) continue;
            var take = Math.Min(remaining, stacks[i].Number);
            plan[i] = take;
            remaining -= take;
        }
        return plan;
    }

    // The whole rule, in one place: any non-empty non-hero stack. Heroes are excluded because the
    // handover removes troops straight off the roster, which would skip EndCaptivityAction for a Hero.
    private static bool IsDeliverable(LotrCaptiveStack stack) => !stack.IsHero && stack.Number > 0;

    public void ApplyRewards(LotrIssueDefinition def, float difficulty, ILotrIssueRewardAdapter hero)
    {
        if (def == null) return;
        if (hero == null || !hero.IsValid)
        {
            _logger.LogWarning($"LotrIssues '{def.Id}': cannot apply rewards — hero adapter invalid");
            return;
        }

        var gold = ComputeRewardGold(def, difficulty);
        if (gold > 0) hero.AddGold(gold);
        if (def.RewardRenown > 0) hero.AddRenown(def.RewardRenown);
        if (!string.IsNullOrEmpty(def.RewardItem)) hero.AddItemToInventory(def.RewardItem, 1);
        _logger.LogInfo($"LotrIssues '{def.Id}': applied reward (gold {gold}, renown {def.RewardRenown}, item '{def.RewardItem}')");
    }

    private void EnsureLoaded()
    {
        if (_all != null) return;
        _all = new List<LotrIssueDefinition>(_configProvider.LoadIssues());
        _byId = new Dictionary<string, LotrIssueDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in _all)
        {
            if (_byId.ContainsKey(def.Id))
            {
                _logger.LogWarning($"LotrIssues: duplicate issue id '{def.Id}' from provider — keeping first");
                continue;
            }
            _byId[def.Id] = def;
        }
    }

    // Non-finite difficulty (NaN/±Inf) collapses to 0 before the clamp (FiniteFloat rule).
    private static float Clamp01(float v)
    {
        if (!FiniteFloatValidator.IsFinite(v)) return 0f;
        if (v < 0f) return 0f;
        if (v > 1f) return 1f;
        return v;
    }
}

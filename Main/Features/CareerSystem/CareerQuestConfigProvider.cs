using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

/// <summary>
/// Loads career-quest definitions from <c>career_system/taom_career_quests.xml</c> and validates
/// them per .claude/rules/csharp-architecture.md "Config Providers MUST Validate": an invalid
/// quest / objective / reward is SKIPPED with a warning rather than loaded broken. A quest with no
/// valid objectives is dropped (a quest you can never complete would soft-lock its tier).
/// </summary>
public class CareerQuestConfigProvider : ICareerQuestConfigProvider
{
    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    private List<CareerQuestDefinition> _quests;

    public CareerQuestConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<CareerQuestDefinition> LoadQuests()
    {
        if (_quests != null) return _quests;

        _quests = new List<CareerQuestDefinition>();
        var path = Path.Combine(_pathService.ModuleDataPath, "career_system", "taom_career_quests.xml");
        if (!File.Exists(path))
        {
            // Not an error: careers with no authored quest fall through to the level gate.
            _logger.LogInfo($"CareerQuest: no quest file at '{path}' — career quests inactive (level gate only)");
            return _quests;
        }

        try
        {
            _quests = ParseQuests(XDocument.Load(path));
            _logger.LogInfo($"CareerQuest: loaded {_quests.Count} valid career quest(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError($"CareerQuest: failed to load quests XML: {ex.Message}");
            _quests = new List<CareerQuestDefinition>();
        }

        return _quests;
    }

    /// <summary>Parse + validate quests from an in-memory document. Internal for unit testing.</summary>
    internal List<CareerQuestDefinition> ParseQuests(XDocument doc)
    {
        var result = new List<CareerQuestDefinition>();
        var root = doc?.Root;
        if (root == null) return result;

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var el in root.Elements("CareerQuest"))
        {
            var quest = ParseQuest(el, seenIds);
            if (quest != null)
                result.Add(quest);
        }
        return result;
    }

    private CareerQuestDefinition ParseQuest(XElement el, HashSet<string> seenIds)
    {
        var id = el.Attribute("id")?.Value ?? "";
        var careerId = el.Attribute("career_id")?.Value ?? "";

        if (string.IsNullOrEmpty(id))
        {
            _logger.LogWarning("CareerQuest: skipping a <CareerQuest> with no id");
            return null;
        }
        if (!seenIds.Add(id))
        {
            _logger.LogWarning($"CareerQuest: skipping duplicate quest id '{id}'");
            return null;
        }
        if (string.IsNullOrEmpty(careerId))
        {
            _logger.LogWarning($"CareerQuest: skipping quest '{id}' — missing career_id");
            return null;
        }

        var tier = ParseInt(el, "tier", 0);
        if (tier < 1 || tier > 3)
        {
            _logger.LogWarning($"CareerQuest: skipping quest '{id}' — tier {tier} out of range 1-3");
            return null;
        }

        var objectives = new List<CareerQuestObjectiveDefinition>();
        var objsEl = el.Element("Objectives");
        if (objsEl != null)
        {
            foreach (var objEl in objsEl.Elements("Objective"))
            {
                var obj = ParseObjective(objEl, id);
                if (obj != null) objectives.Add(obj);
            }
        }
        if (objectives.Count == 0)
        {
            _logger.LogWarning($"CareerQuest: skipping quest '{id}' — no valid objectives (a quest with no objectives could never complete)");
            return null;
        }

        var rewards = new List<CareerQuestRewardDefinition>();
        var rewEl = el.Element("Rewards");
        if (rewEl != null)
        {
            foreach (var r in rewEl.Elements("Reward"))
            {
                var reward = ParseReward(r, id, tier);
                if (reward != null) rewards.Add(reward);
            }
        }

        return new CareerQuestDefinition(
            id: id,
            careerId: careerId,
            tier: tier,
            titleKey: el.Attribute("title_key")?.Value ?? "",
            descriptionKey: el.Attribute("description_key")?.Value ?? "",
            objectives: objectives,
            rewards: rewards);
    }

    private CareerQuestObjectiveDefinition ParseObjective(XElement el, string questId)
    {
        var typeRaw = el.Attribute("type")?.Value;
        if (!Enum.TryParse<CareerQuestObjectiveType>(typeRaw, true, out var type))
        {
            _logger.LogWarning($"CareerQuest '{questId}': skipping objective with unknown type '{typeRaw}'");
            return null;
        }

        var target = ParseInt(el, "target", 0);
        if (target <= 0)
        {
            _logger.LogWarning($"CareerQuest '{questId}': skipping {type} objective — target must be > 0 (was {target})");
            return null;
        }

        var param = el.Attribute("param")?.Value ?? "";
        // Objective types that need a string param to mean anything.
        if ((type == CareerQuestObjectiveType.SkillThreshold || type == CareerQuestObjectiveType.VisitSettlementType)
            && string.IsNullOrEmpty(param))
        {
            _logger.LogWarning($"CareerQuest '{questId}': skipping {type} objective — missing required 'param'");
            return null;
        }
        // VisitSettlementType only ever matches Town/Castle/Village (the runtime emits exactly these);
        // anything else could never progress, so reject it at load (Codex F3a).
        if (type == CareerQuestObjectiveType.VisitSettlementType
            && !(param.Equals("Town", StringComparison.OrdinalIgnoreCase)
                 || param.Equals("Castle", StringComparison.OrdinalIgnoreCase)
                 || param.Equals("Village", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning($"CareerQuest '{questId}': skipping VisitSettlementType objective — param '{param}' must be Town, Castle, or Village");
            return null;
        }

        return new CareerQuestObjectiveDefinition(type, target, param, el.Attribute("task_key")?.Value ?? "");
    }

    private CareerQuestRewardDefinition ParseReward(XElement el, string questId, int questTier)
    {
        var typeRaw = el.Attribute("type")?.Value;
        if (!Enum.TryParse<CareerQuestRewardType>(typeRaw, true, out var type))
        {
            _logger.LogWarning($"CareerQuest '{questId}': skipping reward with unknown type '{typeRaw}'");
            return null;
        }

        var amount = ParseInt(el, "amount", 0);
        var param = el.Attribute("param")?.Value ?? "";

        switch (type)
        {
            case CareerQuestRewardType.UnlockTier:
                if (amount < 1 || amount > 3)
                {
                    _logger.LogWarning($"CareerQuest '{questId}': skipping UnlockTier reward — tier {amount} out of range 1-3");
                    return null;
                }
                // A tier quest unlocks its OWN tier — coerce a mismatched amount so it can never unlock
                // a different tier than the one it gates (Codex F3b).
                if (amount != questTier)
                {
                    _logger.LogWarning($"CareerQuest '{questId}': UnlockTier amount {amount} != quest tier {questTier} — coercing to {questTier}.");
                    amount = questTier;
                }
                break;
            case CareerQuestRewardType.GrantItem:
                if (string.IsNullOrEmpty(param))
                {
                    _logger.LogWarning($"CareerQuest '{questId}': skipping GrantItem reward — missing item id 'param'");
                    return null;
                }
                if (amount <= 0) amount = 1;
                break;
            case CareerQuestRewardType.GrantRenown:
            case CareerQuestRewardType.GrantInfluence:
                if (amount <= 0)
                {
                    _logger.LogWarning($"CareerQuest '{questId}': skipping {type} reward — amount must be > 0 (was {amount})");
                    return null;
                }
                break;
            case CareerQuestRewardType.GrantAttributeFlag:
                if (string.IsNullOrEmpty(param))
                {
                    _logger.LogWarning($"CareerQuest '{questId}': skipping GrantAttributeFlag reward — missing flag 'param'");
                    return null;
                }
                break;
        }

        return new CareerQuestRewardDefinition(type, amount, param);
    }

    private static int ParseInt(XElement el, string attrName, int defaultValue)
    {
        var val = el.Attribute(attrName)?.Value;
        if (val == null) return defaultValue;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
    }
}

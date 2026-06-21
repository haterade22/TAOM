using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Loads LOTR issue definitions from <c>lotr_issues/taom_lotr_issues.xml</c> and validates them per
/// .claude/rules/csharp-architecture.md "Config Providers MUST Validate": an invalid issue is SKIPPED
/// with a warning; a recoverable bad field (non-finite/negative scalar, bad source scheme) is COERCED to
/// the compiled default with a warning. A summary warning fires when any entry was coerced. Singleton —
/// cached for the whole process, so retuning needs an application restart.
/// </summary>
public class LotrIssueConfigProvider : ILotrIssueConfigProvider
{
    private static readonly string[] ValidTroopSources = { "basic", "elite", "bandit", "mount", "prisoners" };
    private static readonly string[] ValidCombatVariants = { "DefeatRaids", "CaptureLords", "WinTournaments" };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    private List<LotrIssueDefinition> _issues;

    public LotrIssueConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public IReadOnlyList<LotrIssueDefinition> LoadIssues()
    {
        if (_issues != null) return _issues;

        _issues = new List<LotrIssueDefinition>();
        var path = Path.Combine(_pathService.ModuleDataPath, "lotr_issues", "taom_lotr_issues.xml");
        if (!File.Exists(path))
        {
            _logger.LogInfo($"LotrIssues: no issue file at '{path}' — no custom issues will spawn (vanilla issues are still suppressed)");
            return _issues;
        }

        try
        {
            _issues = ParseIssues(XDocument.Load(path));
            _logger.LogInfo($"LotrIssues: loaded {_issues.Count} valid issue definition(s)");
        }
        catch (Exception ex)
        {
            _logger.LogError($"LotrIssues: failed to load issue XML: {ex.Message}");
            _issues = new List<LotrIssueDefinition>();
        }

        return _issues;
    }

    /// <summary>Parse + validate issues from an in-memory document. Internal for unit testing.</summary>
    internal List<LotrIssueDefinition> ParseIssues(XDocument doc)
    {
        var result = new List<LotrIssueDefinition>();
        var root = doc?.Root;
        if (root == null) return result;

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coerced = 0;

        foreach (var el in root.Elements("LotrIssue"))
        {
            var issue = ParseIssue(el, seenIds, ref coerced);
            if (issue != null) result.Add(issue);
        }

        if (coerced > 0)
            _logger.LogWarning($"LotrIssues: {coerced} issue field(s) were invalid and reverted to defaults — see prior warnings.");

        return result;
    }

    private LotrIssueDefinition ParseIssue(XElement el, HashSet<string> seenIds, ref int coerced)
    {
        var id = el.Attribute("id")?.Value ?? "";
        if (string.IsNullOrEmpty(id))
        {
            _logger.LogWarning("LotrIssues: skipping a <LotrIssue> with no id");
            return null;
        }
        if (!seenIds.Add(id))
        {
            _logger.LogWarning($"LotrIssues: skipping duplicate issue id '{id}'");
            return null;
        }

        if (!Enum.TryParse<LotrIssueTemplate>(el.Attribute("template")?.Value, true, out var template))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — unknown template '{el.Attribute("template")?.Value}'");
            return null;
        }
        if (!Enum.TryParse<IssueGiverOccupation>(el.Attribute("giver_occupation")?.Value, true, out var giver))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — unknown giver_occupation '{el.Attribute("giver_occupation")?.Value}'");
            return null;
        }
        if (!Enum.TryParse<IssueFrequencyTier>(el.Attribute("frequency")?.Value, true, out var frequency))
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — unknown/absent frequency '{el.Attribute("frequency")?.Value}', defaulting to Common");
            frequency = IssueFrequencyTier.Common;
            coerced++;
        }

        var count = ParseInt(el, "count", 1);
        if (count <= 0)
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — count must be > 0 (was {count}); an uncompletable issue would soft-lock");
            return null;
        }

        var titleKey = el.Attribute("title_key")?.Value ?? "";
        var descKey = el.Attribute("description_key")?.Value ?? "";
        if (string.IsNullOrEmpty(titleKey) || string.IsNullOrEmpty(descKey))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — title_key and description_key are required");
            return null;
        }

        var countPerDiff = CoerceNonNegFloat(el, "count_per_difficulty", id, ref coerced);
        var rewardGoldPerDiff = CoerceNonNegFloat(el, "reward_gold_per_difficulty", id, ref coerced);
        var rewardGoldBase = CoerceNonNegInt(el, "reward_gold_base", id, ref coerced);
        var rewardRenown = CoerceNonNegInt(el, "reward_renown", id, ref coerced);

        var relationMin = ParseInt(el, "relation_min", -10);
        if (relationMin < -100 || relationMin > 100)
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — relation_min {relationMin} out of [-100,100], reverting to -10");
            relationMin = -10;
            coerced++;
        }

        var itemSource = ValidateItemSource(el.Attribute("item_source")?.Value ?? "", id, ref coerced);
        var troopSource = ValidateTroopSource(el.Attribute("troop_source")?.Value ?? "", id, ref coerced);

        // Wave 0: the DeliverGoods template only resolves "item:<id>" sources. A "category:" (or empty)
        // source would spawn an uncompletable issue that silently never stays alive, so skip it at load
        // with a clear warning until a later wave implements category sourcing in the template.
        if (template == LotrIssueTemplate.DeliverGoods && !itemSource.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — DeliverGoods requires an 'item:<id>' source (got '{el.Attribute("item_source")?.Value}'); 'category:' is not implemented yet.");
            return null;
        }

        var cultures = ParseCultures(el.Attribute("cultures")?.Value);

        var variant = el.Attribute("variant")?.Value ?? "";
        if (template == LotrIssueTemplate.Combat && !string.IsNullOrEmpty(variant) && !IsValidCombatVariant(variant))
        {
            _logger.LogWarning($"LotrIssues: skipping issue '{id}' — Combat variant '{variant}' must be one of DefeatRaids|CaptureLords|WinTournaments (empty defaults to DefeatRaids).");
            return null;
        }

        var text = new LotrIssueText(
            titleKey,
            descKey,
            el.Attribute("brief_key")?.Value ?? "",
            el.Attribute("accept_key")?.Value ?? "",
            el.Attribute("explanation_key")?.Value ?? "",
            el.Attribute("solution_accept_key")?.Value ?? "",
            el.Attribute("task_key")?.Value ?? "",
            el.Attribute("success_key")?.Value ?? "",
            el.Attribute("fail_key")?.Value ?? "");

        return new LotrIssueDefinition(
            id, template, giver, frequency, cultures, count, countPerDiff,
            itemSource, troopSource, rewardGoldBase, rewardGoldPerDiff, rewardRenown,
            el.Attribute("reward_item")?.Value ?? "", variant, relationMin, text);
    }

    private static bool IsValidCombatVariant(string v)
    {
        foreach (var x in ValidCombatVariants)
            if (string.Equals(x, v, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private string ValidateItemSource(string raw, string id, ref int coerced)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        if (raw.StartsWith("category:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("item:", StringComparison.OrdinalIgnoreCase))
            return raw;
        _logger.LogWarning($"LotrIssues: issue '{id}' — item_source '{raw}' must be 'category:<X>' or 'item:<X>', clearing");
        coerced++;
        return "";
    }

    private string ValidateTroopSource(string raw, string id, ref int coerced)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        foreach (var v in ValidTroopSources)
            if (string.Equals(v, raw, StringComparison.OrdinalIgnoreCase)) return v.ToLowerInvariant();
        _logger.LogWarning($"LotrIssues: issue '{id}' — troop_source '{raw}' must be one of basic|elite|bandit|mount|prisoners, clearing");
        coerced++;
        return "";
    }

    private float CoerceNonNegFloat(XElement el, string attr, string id, ref int coerced)
    {
        var raw = el.Attribute(attr)?.Value;
        if (raw == null) return 0f;
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || !FiniteFloatValidator.IsFiniteAtLeast(v, 0f))
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — {attr} '{raw}' is not a finite value >= 0, reverting to 0");
            coerced++;
            return 0f;
        }
        return v;
    }

    private int CoerceNonNegInt(XElement el, string attr, string id, ref int coerced)
    {
        var v = ParseInt(el, attr, 0);
        if (v < 0)
        {
            _logger.LogWarning($"LotrIssues: issue '{id}' — {attr} {v} < 0, reverting to 0");
            coerced++;
            return 0;
        }
        return v;
    }

    private static List<string> ParseCultures(string raw)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return list;
        foreach (var token in raw.Split(','))
        {
            var t = token.Trim();
            if (!string.IsNullOrEmpty(t)) list.Add(t);
        }
        return list;
    }

    private static int ParseInt(XElement el, string attr, int defaultValue)
    {
        var raw = el.Attribute(attr)?.Value;
        if (raw == null) return defaultValue;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }
}

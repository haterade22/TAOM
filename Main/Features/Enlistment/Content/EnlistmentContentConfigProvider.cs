using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

public class EnlistmentContentConfigProvider : IEnlistmentContentConfigProvider
{
    private static readonly HashSet<string> KnownSkills = new HashSet<string>(StringComparer.Ordinal)
    {
        // Engine StringIds (DefaultSkills) — note smithing's id is "Crafting" on 1.4.x.
        "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing",
        "Riding", "Athletics", "Crafting", "Scouting", "Tactics", "Roguery",
        "Charm", "Leadership", "Trade", "Steward", "Medicine", "Engineering",
    };

    private static readonly HashSet<string> KnownContexts = new HashSet<string>(StringComparer.Ordinal)
    {
        "siege", "naval", "blockade", "army", "garrison", "march",
    };

    private static readonly HashSet<string> KnownAltCompletions = new HashSet<string>(StringComparer.Ordinal)
    {
        "", "EnemyContact",
    };

    private static readonly HashSet<string> KnownIncidentEffects = new HashSet<string>(StringComparer.Ordinal)
    {
        "", "ReleaseDeferredPay",
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<EnlistmentContentConfig> _config;
    private readonly Lazy<EnlistmentDutiesConfig> _duties;

    public EnlistmentContentConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<EnlistmentContentConfig>(LoadConfig);
        _duties = new Lazy<EnlistmentDutiesConfig>(LoadDuties);
    }

    public EnlistmentContentConfig GetConfig() => _config.Value;

    public EnlistmentDutiesConfig GetDuties() => _duties.Value;

    internal static EnlistmentContentConfig BuildDefaults()
    {
        return new EnlistmentContentConfig
        {
            MeritBands = DefaultMeritBands(),
            Promotions = DefaultPromotions(),
        };
    }

    private static List<MeritBand> DefaultMeritBands() => new List<MeritBand>
    {
        new MeritBand { MinScore = 80, ServiceXp = 30, Gold = 20, Trust = 2, RepDomain = ReputationDomain.Field, RepAmount = 2, GradeKey = "distinguished" },
        new MeritBand { MinScore = 60, ServiceXp = 20, Gold = 10, Trust = 1, RepDomain = ReputationDomain.Field, RepAmount = 1, GradeKey = "strong" },
        new MeritBand { MinScore = 40, ServiceXp = 12, Gold = 5, Trust = 0, RepDomain = ReputationDomain.Field, RepAmount = 1, GradeKey = "solid" },
        new MeritBand { MinScore = 0, ServiceXp = 4, Gold = 0, Trust = 0, RepDomain = ReputationDomain.None, RepAmount = 0, GradeKey = "rough" },
    };

    private static List<PromotionRequirement> DefaultPromotions() => new List<PromotionRequirement>
    {
        new PromotionRequirement { ToRank = ServiceRank.Soldier, MinDaysServed = 7, MinServiceXp = 100, MinLeadershipSkill = 0, MinDutySuccesses = 0, MinTrust = -10 },
        new PromotionRequirement { ToRank = ServiceRank.Veteran, MinDaysServed = 25, MinServiceXp = 350, MinLeadershipSkill = 20, MinDutySuccesses = 2, MinTrust = 0 },
        new PromotionRequirement { ToRank = ServiceRank.Sergeant, MinDaysServed = 60, MinServiceXp = 800, MinLeadershipSkill = 50, MinDutySuccesses = 5, MinTrust = 6 },
    };

    private EnlistmentContentConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "enlistment", "enlistment_config.json");
        var defaults = BuildDefaults();

        if (!File.Exists(path))
        {
            _logger.LogWarning($"EnlistmentContentConfigProvider: enlistment_config.json not found at {path}, using defaults");
            return defaults;
        }

        EnlistmentContentConfig parsed;
        try
        {
            // ObjectCreationHandling.Replace is REQUIRED, not a preference. The config model gives
            // its list properties initializers (e.g. DailyWageByRank = { 5, 8, 14, 22 }), and
            // Newtonsoft's default (Auto) APPENDS the JSON entries to that existing collection
            // instead of replacing it. A well-formed 4-entry table therefore deserialized to 8
            // entries, failed the "exactly 4" rank-table check, and silently reverted to the
            // compiled defaults — so every retune of these tables was discarded with only a
            // warning. Observed in-game 2026-08-07.
            parsed = JsonConvert.DeserializeObject<EnlistmentContentConfig>(
                File.ReadAllText(path),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        }
        catch (Exception ex)
        {
            _logger.LogError($"EnlistmentContentConfigProvider: failed to parse enlistment_config.json: {ex.Message}");
            return defaults;
        }

        if (parsed == null)
            return defaults;

        if (parsed.MeritBands == null || parsed.MeritBands.Count == 0)
            parsed.MeritBands = DefaultMeritBands();
        if (parsed.Promotions == null || parsed.Promotions.Count == 0)
            parsed.Promotions = DefaultPromotions();

        return ValidateConfig(parsed, defaults);
    }

    private EnlistmentContentConfig ValidateConfig(EnlistmentContentConfig config, EnlistmentContentConfig defaults)
    {
        var rejected = false;

        rejected |= ValidateRankTable(config.Progression.DailyWageByRank, defaults.Progression.DailyWageByRank, "progression.dailyWageByRank", v => config.Progression.DailyWageByRank = v);
        rejected |= ValidateRankTable(config.Progression.DailyServiceXpByRank, defaults.Progression.DailyServiceXpByRank, "progression.dailyServiceXpByRank", v => config.Progression.DailyServiceXpByRank = v);

        rejected |= RevertIfNegative(() => config.Progression.DailyLeadershipXp, v => config.Progression.DailyLeadershipXp = v, defaults.Progression.DailyLeadershipXp, "progression.dailyLeadershipXp");
        rejected |= RevertIfNegative(() => config.Progression.DailyAssignmentXp, v => config.Progression.DailyAssignmentXp = v, defaults.Progression.DailyAssignmentXp, "progression.dailyAssignmentXp");
        rejected |= RevertIfNegative(() => config.Progression.XpPerKill, v => config.Progression.XpPerKill = v, defaults.Progression.XpPerKill, "progression.xpPerKill");
        rejected |= RevertIfNegative(() => config.Progression.KillXpCap, v => config.Progression.KillXpCap = v, defaults.Progression.KillXpCap, "progression.killXpCap");
        rejected |= RevertIfNegative(() => config.WagePolicy.CommanderGoldFloor, v => config.WagePolicy.CommanderGoldFloor = v, defaults.WagePolicy.CommanderGoldFloor, "wagePolicy.commanderGoldFloor");
        rejected |= RevertIfNegative(() => config.WagePolicy.MaxDeferredWages, v => config.WagePolicy.MaxDeferredWages = v, defaults.WagePolicy.MaxDeferredWages, "wagePolicy.maxDeferredWages");

        if (!FiniteFloatValidator.IsFiniteInRange(config.Scheduler.BaseOfferChance, 0f, 1f))
        {
            _logger.LogWarning($"EnlistmentContentConfigProvider: scheduler.baseOfferChance={config.Scheduler.BaseOfferChance} must be finite in [0,1], reverting to {defaults.Scheduler.BaseOfferChance}");
            config.Scheduler.BaseOfferChance = defaults.Scheduler.BaseOfferChance;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(config.Scheduler.MaxOfferChance, 0f, 1f)
            || config.Scheduler.MaxOfferChance < config.Scheduler.BaseOfferChance)
        {
            _logger.LogWarning("EnlistmentContentConfigProvider: scheduler.maxOfferChance must be finite in [baseOfferChance,1], reverting");
            config.Scheduler.MaxOfferChance = defaults.Scheduler.MaxOfferChance;
            rejected = true;
        }

        if (!FiniteFloatValidator.IsFiniteInRange(config.MeritScoring.SampleIntervalSeconds, 0.5f, 30f))
        {
            _logger.LogWarning("EnlistmentContentConfigProvider: meritScoring.sampleIntervalSeconds must be finite in [0.5,30], reverting");
            config.MeritScoring.SampleIntervalSeconds = defaults.MeritScoring.SampleIntervalSeconds;
            rejected = true;
        }

        if (!IsValidPromotionLadder(config.Promotions))
        {
            _logger.LogWarning("EnlistmentContentConfigProvider: promotions must be exactly 3 entries (Soldier/Veteran/Sergeant) with strictly increasing days+XP and non-negative fields, reverting to the default ladder");
            config.Promotions = DefaultPromotions();
            rejected = true;
        }

        if (!IsValidBandLadder(config.MeritBands))
        {
            _logger.LogWarning("EnlistmentContentConfigProvider: meritBands must have strictly descending unique minScore ending at 0 with non-negative rewards, reverting to defaults");
            config.MeritBands = DefaultMeritBands();
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("EnlistmentContentConfigProvider: enlistment_config.json contained invalid values. See prior warnings.");
        else
            _logger.LogInfo("EnlistmentContentConfigProvider: loaded enlistment_config.json");

        return config;
    }

    private bool ValidateRankTable(List<int> table, List<int> defaultTable, string name, Action<List<int>> assign)
    {
        if (table != null && table.Count == 4 && table.All(v => v >= 0))
            return false;
        _logger.LogWarning($"EnlistmentContentConfigProvider: {name} must be 4 non-negative entries (one per rank) but was [{(table == null ? "null" : string.Join(",", table))}] (count {table?.Count ?? 0}) — reverting to defaults");
        assign(new List<int>(defaultTable));
        return true;
    }

    private bool RevertIfNegative(Func<int> get, Action<int> set, int defaultValue, string name)
    {
        if (get() >= 0)
            return false;
        _logger.LogWarning($"EnlistmentContentConfigProvider: {name}={get()} must be >= 0, reverting to {defaultValue}");
        set(defaultValue);
        return true;
    }

    private static bool IsValidPromotionLadder(List<PromotionRequirement> promotions)
    {
        if (promotions == null || promotions.Count != 3)
            return false;
        for (var i = 0; i < promotions.Count; i++)
        {
            var p = promotions[i];
            if (p.MinDaysServed < 0 || p.MinServiceXp < 0 || p.MinLeadershipSkill < 0 || p.MinDutySuccesses < 0)
                return false;
            if (p.ToRank != (ServiceRank)(i + 1))
                return false;
            if (i > 0 && (p.MinDaysServed <= promotions[i - 1].MinDaysServed || p.MinServiceXp <= promotions[i - 1].MinServiceXp))
                return false;
        }
        return true;
    }

    private static bool IsValidBandLadder(List<MeritBand> bands)
    {
        if (bands == null || bands.Count < 2 || bands[bands.Count - 1].MinScore != 0)
            return false;
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            if (band.MinScore < 0 || band.ServiceXp < 0 || band.Gold < 0 || band.RepAmount < 0)
                return false;
            if (i > 0 && band.MinScore >= bands[i - 1].MinScore)
                return false;
        }
        return true;
    }

    private EnlistmentDutiesConfig LoadDuties()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "enlistment", "enlistment_duties.json");

        if (!File.Exists(path))
        {
            _logger.LogWarning($"EnlistmentContentConfigProvider: enlistment_duties.json not found at {path} — no duties will be offered");
            return new EnlistmentDutiesConfig();
        }

        EnlistmentDutiesConfig parsed;
        try
        {
            // Same Replace requirement as the config loader above — any list property with an
            // initializer would otherwise accumulate the file's entries on top of the defaults.
            parsed = JsonConvert.DeserializeObject<EnlistmentDutiesConfig>(
                File.ReadAllText(path),
                new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        }
        catch (Exception ex)
        {
            _logger.LogError($"EnlistmentContentConfigProvider: failed to parse enlistment_duties.json: {ex.Message}");
            return new EnlistmentDutiesConfig();
        }

        if (parsed == null)
            return new EnlistmentDutiesConfig();

        var result = new EnlistmentDutiesConfig();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var duty in parsed.FieldDuties ?? new List<DutyDefinition>())
        {
            if (RowInvalid(duty?.Id, seenIds, out var reason)
                || duty.DeadlineDays < 1 && Warn(duty.Id, $"deadlineDays={duty.DeadlineDays} must be >= 1", out reason)
                || !KnownAltCompletions.Contains(duty.AltCompletion ?? "") && Warn(duty.Id, $"unknown altCompletion '{duty.AltCompletion}'", out reason)
                || !RewardValid(duty.ReportReward) && Warn(duty.Id, "negative or unknown-skill reportReward", out reason)
                || !GatesValid(duty.Gates) && Warn(duty.Id, $"unknown context '{FirstUnknownContext(duty.Gates)}' in gates", out reason)
                || duty.SupportSkills != null && duty.SupportSkills.Any(s => !KnownSkills.Contains(s)) && Warn(duty.Id, $"unknown supportSkill '{duty.SupportSkills.First(s => !KnownSkills.Contains(s))}'", out reason))
            {
                _ = reason;
                continue;
            }
            seenIds.Add(duty.Id);
            result.FieldDuties.Add(duty);
        }

        foreach (var duty in parsed.InteractiveDuties ?? new List<InteractiveDutyDefinition>())
        {
            if (RowInvalid(duty?.Id, seenIds, out var reason)
                || !OptionValid(duty.OptionA) && Warn(duty.Id, OptionProblem(duty.OptionA), out reason)
                || !OptionValid(duty.OptionB) && Warn(duty.Id, OptionProblem(duty.OptionB), out reason)
                || !GatesValid(duty.Gates) && Warn(duty.Id, $"unknown context '{FirstUnknownContext(duty.Gates)}' in gates", out reason))
            {
                _ = reason;
                continue;
            }
            seenIds.Add(duty.Id);
            result.InteractiveDuties.Add(duty);
        }

        foreach (var incident in parsed.Incidents ?? new List<IncidentDefinition>())
        {
            if (RowInvalid(incident?.Id, seenIds, out var reason)
                || !FiniteFloatValidator.IsFiniteInRange(incident.Chance, 0f, 1f) && Warn(incident.Id, $"chance={incident.Chance} must be finite in [0,1]", out reason)
                || !KnownIncidentEffects.Contains(incident.Effect ?? "") && Warn(incident.Id, $"unknown effect '{incident.Effect}'", out reason)
                || !OptionValid(incident.OptionA) && Warn(incident.Id, OptionProblem(incident.OptionA), out reason)
                || !OptionValid(incident.OptionB) && Warn(incident.Id, OptionProblem(incident.OptionB), out reason))
            {
                _ = reason;
                continue;
            }
            seenIds.Add(incident.Id);
            result.Incidents.Add(incident);
        }

        _logger.LogInfo($"EnlistmentContentConfigProvider: loaded {result.FieldDuties.Count} field duties, {result.InteractiveDuties.Count} interactive duties, {result.Incidents.Count} incidents");
        return result;
    }

    private bool RowInvalid(string id, HashSet<string> seenIds, out string reason)
    {
        reason = null;
        if (string.IsNullOrEmpty(id))
        {
            _logger.LogWarning("EnlistmentContentConfigProvider: duty row with missing id skipped");
            return true;
        }
        if (seenIds.Contains(id))
        {
            _logger.LogWarning($"EnlistmentContentConfigProvider: duplicate duty id '{id}' skipped");
            return true;
        }
        return false;
    }

    private bool Warn(string id, string problem, out string reason)
    {
        reason = problem;
        _logger.LogWarning($"EnlistmentContentConfigProvider: duty '{id}' skipped — {problem}");
        return true;
    }

    private bool RewardValid(RewardSpec reward)
    {
        if (reward == null)
            return true;
        if (reward.ServiceXp < 0 || reward.Gold < 0 || reward.SkillXp < 0 || reward.RepAmount < 0)
            return false;
        return string.IsNullOrEmpty(reward.SkillId) || KnownSkills.Contains(reward.SkillId);
    }

    private bool GatesValid(GateSpec gates)
    {
        if (gates == null)
            return true;
        return (gates.RequiredContexts ?? new List<string>()).All(c => KnownContexts.Contains(c))
            && (gates.ExcludedContexts ?? new List<string>()).All(c => KnownContexts.Contains(c));
    }

    private static string FirstUnknownContext(GateSpec gates)
    {
        if (gates == null)
            return "";
        return (gates.RequiredContexts ?? new List<string>()).FirstOrDefault(c => !KnownContexts.Contains(c))
            ?? (gates.ExcludedContexts ?? new List<string>()).FirstOrDefault(c => !KnownContexts.Contains(c))
            ?? "";
    }

    private bool OptionValid(DutyOptionSpec option)
    {
        if (option == null || string.IsNullOrEmpty(option.Key))
            return false;
        if (string.IsNullOrEmpty(option.SkillId) || !KnownSkills.Contains(option.SkillId))
            return false;
        if (!string.IsNullOrEmpty(option.SecondarySkillId) && !KnownSkills.Contains(option.SecondarySkillId))
            return false;
        if (option.Difficulty < 1 || option.Difficulty > 200)
            return false;
        return RewardValid(option.SuccessReward) && RewardValid(option.FailureReward);
    }

    private static string OptionProblem(DutyOptionSpec option)
    {
        if (option == null || string.IsNullOrEmpty(option.Key))
            return "option missing key";
        if (string.IsNullOrEmpty(option.SkillId))
            return "option missing skillId";
        if (!KnownSkills.Contains(option.SkillId))
            return $"unknown skill '{option.SkillId}'";
        if (!string.IsNullOrEmpty(option.SecondarySkillId) && !KnownSkills.Contains(option.SecondarySkillId))
            return $"unknown skill '{option.SecondarySkillId}'";
        return "invalid option (difficulty or rewards)";
    }
}

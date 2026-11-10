using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;

namespace TAOM.Features.WarPledge;

public interface IWarPledgeConfigProvider
{
    WarPledgeConfig LoadConfig();
}

/// <summary>
/// Loads and validates <c>diplomacy/war_pledge.json</c> (#1113). A number outside its range reverts to the
/// compiled default with a warning. A lean row is dropped with a warning when it has no kingdom, repeats
/// one (the first row stands), or names a lean that is not Free, Evil or Neutral: a mistyped lean never
/// becomes a side. A missing or unreadable file gives the defaults and an empty table, so the AI courts
/// no realm. The provider cannot see the campaign, so a kingdom id that no live realm has is found by
/// the shipped-data test and, in play, by the service, which skips it.
/// </summary>
public sealed class WarPledgeConfigProvider : IWarPledgeConfigProvider
{
    private const string FileName = "war_pledge.json";

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;

    public WarPledgeConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
    }

    public WarPledgeConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "diplomacy", FileName);
        if (!File.Exists(path))
        {
            _logger.LogWarning($"[WarPledge] {FileName} not found: {path}. No realm has a lean, so the AI courts none.");
            return WarPledgeConfig.Defaults;
        }

        try
        {
            // A file that holds the literal null deserializes to null.
            var file = JsonConvert.DeserializeObject<FileShape>(File.ReadAllText(path)) ?? new FileShape();
            return Validate(file);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[WarPledge] {FileName} could not be read ({ex.GetType().Name}: {ex.Message}). No realm has a lean, so the AI courts none.");
            return WarPledgeConfig.Defaults;
        }
    }

    private WarPledgeConfig Validate(FileShape file)
    {
        var reverted = 0;
        var losingBalance = InRange(
            "losingBalance", file.LosingBalance, WarPledgeConfig.DefaultLosingBalance,
            WarPledgeConfig.MinLosingBalance, WarPledgeConfig.MaxLosingBalance, ref reverted);
        var days = InRange(
            "daysBetweenAiJoins", file.DaysBetweenAiJoins, WarPledgeConfig.DefaultDaysBetweenAiJoins,
            WarPledgeConfig.MinDaysBetweenAiJoins, WarPledgeConfig.MaxDaysBetweenAiJoins, ref reverted);

        var leans = new List<WarPledgeLean>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (file.Leans == null)
        {
            _logger.LogWarning($"[WarPledge] {FileName}: 'leans' is missing; no realm has a lean.");
            reverted++;
        }
        else
        {
            foreach (var row in file.Leans)
            {
                if (TryReadLean(row, seen, out var lean))
                    leans.Add(lean);
                else
                    reverted++;
            }
        }

        if (reverted > 0)
            _logger.LogWarning($"[WarPledge] {FileName}: {reverted} value(s) were reverted or dropped; see the warnings above.");
        _logger.LogInfo($"[WarPledge] Loaded {FileName}: {leans.Count} lean rows, losing balance {losingBalance}, {days} days between AI joins.");
        return new WarPledgeConfig(losingBalance, days, leans);
    }

    // An absent key takes the default with no warning; a present value outside the range warns.
    private int InRange(string name, int? value, int fallback, int min, int max, ref int reverted)
    {
        if (!value.HasValue)
            return fallback;
        if (value.Value >= min && value.Value <= max)
            return value.Value;

        _logger.LogWarning($"[WarPledge] {FileName}: {name} ({value.Value}) is outside {min} to {max}; using {fallback}.");
        reverted++;
        return fallback;
    }

    private bool TryReadLean(LeanShape? row, HashSet<string> seen, out WarPledgeLean lean)
    {
        lean = null!;
        if (row == null)
        {
            _logger.LogWarning($"[WarPledge] {FileName}: a lean row is null; dropping it.");
            return false;
        }

        var kingdom = row.Kingdom?.Trim();
        if (string.IsNullOrEmpty(kingdom))
        {
            _logger.LogWarning($"[WarPledge] {FileName}: a lean row has no kingdom; dropping it.");
            return false;
        }

        if (!TryReadSide(row.Lean, out var side))
        {
            _logger.LogWarning($"[WarPledge] {FileName}: lean '{row.Lean}' of '{kingdom}' is not Free, Evil or Neutral; dropping the row.");
            return false;
        }

        if (!seen.Add(kingdom!))
        {
            _logger.LogWarning($"[WarPledge] {FileName}: '{kingdom}' has a second lean row; the first one stands.");
            return false;
        }

        lean = new WarPledgeLean(kingdom!, side);
        return true;
    }

    // By name only: Enum.TryParse also accepts "1", and a number is not a lean an author meant.
    private static bool TryReadSide(string? text, out FactionSide side)
    {
        side = FactionSide.Neutral;
        var name = text?.Trim();
        if (string.Equals(name, nameof(FactionSide.Free), StringComparison.OrdinalIgnoreCase))
            side = FactionSide.Free;
        else if (string.Equals(name, nameof(FactionSide.Evil), StringComparison.OrdinalIgnoreCase))
            side = FactionSide.Evil;
        else if (!string.Equals(name, nameof(FactionSide.Neutral), StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private sealed class FileShape
    {
        [JsonProperty("losingBalance")]
        public int? LosingBalance { get; set; }

        [JsonProperty("daysBetweenAiJoins")]
        public int? DaysBetweenAiJoins { get; set; }

        [JsonProperty("leans")]
        public List<LeanShape?>? Leans { get; set; }
    }

    private sealed class LeanShape
    {
        [JsonProperty("kingdom")]
        public string? Kingdom { get; set; }

        [JsonProperty("lean")]
        public string? Lean { get; set; }
    }
}

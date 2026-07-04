using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Flat-dictionary persistence for the momentum war state. Key shapes:
///   version, warStarted, warEnded, victor,
///   {side}.momentum, {side}.kingdoms, {side}.stats  (kills|raids|captures),
///   {side}.ev.{ActionType}.{n} = value|endHoursR|description  (description last — pipes inside it are safe),
///   player.events = comma-joined action-type names.
/// Deserialize hardening per the Messengers precedent: NaN/Infinity end-hours, unparseable
/// ints, and unknown enum names are skipped with a warning; queues re-cap on restore;
/// null/absent dict → fresh state. Fixes LOTRAOM's unpersisted player victory gate.
/// </summary>
public class MomentumStateStore : IMomentumStateStore
{
    private const string Version = "1";
    private const string FreePrefix = "free";
    private const string EvilPrefix = "evil";

    private readonly IModLogger _logger;

    public MomentumStateStore(IModLogger logger)
    {
        _logger = logger;
    }

    public MomentumWarState State { get; private set; } = new();

    public List<MomentumActionType> PlayerEvents { get; } = new();

    public event Action MomentumChanged;

    public void NotifyMomentumChanged() => MomentumChanged?.Invoke();

    public void ResetForNewGame()
    {
        State = new MomentumWarState();
        PlayerEvents.Clear();
    }

    public Dictionary<string, string> Serialize()
    {
        var data = new Dictionary<string, string>
        {
            ["version"] = Version,
            ["warStarted"] = State.HasWarStarted ? "1" : "0",
            ["warEnded"] = State.HasWarEnded ? "1" : "0",
            ["victor"] = State.Victor.ToString(),
            ["player.events"] = string.Join(",", PlayerEvents),
        };

        SerializeSide(data, FreePrefix, State.Free);
        SerializeSide(data, EvilPrefix, State.Evil);
        return data;
    }

    private static void SerializeSide(Dictionary<string, string> data, string prefix, MomentumSideData side)
    {
        data[$"{prefix}.momentum"] = side.SideMomentum.ToString(CultureInfo.InvariantCulture);
        data[$"{prefix}.kingdoms"] = string.Join(",", side.KingdomIds);
        data[$"{prefix}.stats"] = string.Join("|",
            side.TotalStats.TotalKills.ToString(CultureInfo.InvariantCulture),
            side.TotalStats.TotalVillagesRaided.ToString(CultureInfo.InvariantCulture),
            side.TotalStats.TotalSettlementsCaptured.ToString(CultureInfo.InvariantCulture));

        foreach (MomentumActionType type in Enum.GetValues(typeof(MomentumActionType)))
        {
            int i = 0;
            foreach (var ev in side.GetEvents(type))
            {
                data[$"{prefix}.ev.{type}.{i}"] = string.Join("|",
                    ev.Value.ToString(CultureInfo.InvariantCulture),
                    ev.EndTimeHours.ToString("R", CultureInfo.InvariantCulture),
                    ev.Description);
                i++;
            }
        }
    }

    public void Deserialize(Dictionary<string, string> data)
    {
        State = new MomentumWarState();
        PlayerEvents.Clear();

        if (data == null || data.Count == 0)
            return;

        State.RestoreFlags(
            warStarted: Get(data, "warStarted") == "1",
            warEnded: Get(data, "warEnded") == "1",
            victor: ParseEnumOrDefault(Get(data, "victor"), WarOutcome.None, "victor"));

        DeserializeSide(data, FreePrefix, State.Free);
        DeserializeSide(data, EvilPrefix, State.Evil);

        foreach (var name in SplitList(Get(data, "player.events")))
        {
            if (TryParseDefinedEnum<MomentumActionType>(name, out var type))
                PlayerEvents.Add(type);
            else
                _logger.LogWarning($"MomentumStateStore: unknown player event type '{name}' skipped");
        }
    }

    private void DeserializeSide(Dictionary<string, string> data, string prefix, MomentumSideData side)
    {
        var momentumRaw = Get(data, $"{prefix}.momentum");
        if (momentumRaw != null)
        {
            if (int.TryParse(momentumRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var momentum))
                side.RestoreMomentum(momentum);
            else
                _logger.LogWarning($"MomentumStateStore: unparseable {prefix}.momentum='{momentumRaw}', defaulting to 0");
        }

        foreach (var kingdomId in SplitList(Get(data, $"{prefix}.kingdoms")))
            side.AddKingdom(kingdomId);

        var statsRaw = Get(data, $"{prefix}.stats");
        if (statsRaw != null)
        {
            var parts = statsRaw.Split('|');
            if (parts.Length == 3
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var kills)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var raids)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var captures))
            {
                side.TotalStats.Restore(kills, raids, captures);
            }
            else
            {
                _logger.LogWarning($"MomentumStateStore: unparseable {prefix}.stats='{statsRaw}', defaulting to 0s");
            }
        }

        // Events: collect per type, sort by end time (FIFO order == expiry order because
        // same-type events share a duration), then restore without momentum side effects.
        var eventPrefix = $"{prefix}.ev.";
        var restored = new List<MomentumEvent>();
        foreach (var entry in data)
        {
            if (!entry.Key.StartsWith(eventPrefix, StringComparison.Ordinal))
                continue;

            var ev = ParseEvent(entry.Key, eventPrefix, entry.Value);
            if (ev != null)
                restored.Add(ev);
        }

        foreach (var ev in restored.OrderBy(e => e.EndTimeHours))
            side.RestoreEvent(ev);
    }

    private MomentumEvent ParseEvent(string key, string eventPrefix, string value)
    {
        // Key tail: {ActionType}.{index}
        var tail = key.Substring(eventPrefix.Length);
        var dot = tail.IndexOf('.');
        var typeName = dot >= 0 ? tail.Substring(0, dot) : tail;

        if (!TryParseDefinedEnum<MomentumActionType>(typeName, out var type))
        {
            _logger.LogWarning($"MomentumStateStore: unknown action type in key '{key}' skipped");
            return null;
        }

        var parts = value.Split(new[] { '|' }, 3);
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var eventValue)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var endHours)
            || double.IsNaN(endHours) || double.IsInfinity(endHours))
        {
            _logger.LogWarning($"MomentumStateStore: malformed event '{key}'='{value}' skipped");
            return null;
        }

        return new MomentumEvent(eventValue, parts[2], type, endHours);
    }

    private TEnum ParseEnumOrDefault<TEnum>(string raw, TEnum fallback, string field) where TEnum : struct
    {
        if (string.IsNullOrEmpty(raw))
            return fallback;
        if (TryParseDefinedEnum<TEnum>(raw, out var parsed))
            return parsed;

        _logger.LogWarning($"MomentumStateStore: unknown {field} '{raw}', defaulting to {fallback}");
        return fallback;
    }

    // Enum.TryParse alone accepts numeric strings ("999" → undefined value); require IsDefined.
    private static bool TryParseDefinedEnum<TEnum>(string raw, out TEnum result) where TEnum : struct
    {
        return Enum.TryParse(raw, out result) && Enum.IsDefined(typeof(TEnum), result);
    }

    private static string Get(Dictionary<string, string> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value : null;
    }

    private static IEnumerable<string> SplitList(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return Enumerable.Empty<string>();
        return raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// factions.json: <c>factions</c> is an object keyed by faction id, each with a display <c>name</c> (a <c>{=TAOM_FOW_...}English</c>
/// key), a <c>culture</c> id, <c>units</c>, <c>heroes</c> and <c>freeWaves</c>, and optionally <c>feats</c> (factors that change the rules for
/// this faction), <c>traits</c> (the lines that tell a player about them), <c>rotation</c> (regions whose bands show in turn) and <c>creatures</c>
/// (a call on a shared timer). A unit offer names its price outright, or a
/// <c>level</c> (and optional <c>eliteWeight</c>) that <see cref="TroopPricing"/> turns into one; a cavalry or horse archer role
/// prices as mounted. A bad entry is skipped with one warning that names the faction and the entry; a faction left with no unit
/// offer is skipped. Whether a troop or hero id exists is not checked here (the shipped-config test does it).
/// </summary>
internal static class FordsFactionsReader
{
    private const int MaxPrice = 10_000_000;
    private const int MaxPackSize = 200;

    private static readonly string RoleNames = string.Join(", ",
        Enum.GetValues(typeof(UnitRole)).Cast<UnitRole>().Where(r => r != UnitRole.Hero));

    public static FordsFactionSet Read(string? text, FordsConfigScope s)
    {
        var root = s.OpenRoot(text, "was not found or could not be read; no factions");
        if (root == null)
            return FordsFactionSet.Empty;

        if (!(root["factions"] is JObject factions))
        {
            s.Warn("'factions' is missing or not an object; no factions");
            return FordsFactionSet.Empty;
        }

        var set = new FordsFactionSet();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in factions.Properties())
        {
            var id = property.Name.Trim();
            if (id.Length == 0)
            {
                s.Warn("a faction has an empty id; skipped");
                continue;
            }

            if (!seen.Add(id))
            {
                s.Warn($"faction '{id}' skipped (duplicate id, the first one is kept)");
                continue;
            }

            if (TryReadFaction(id, property.Value, s, out var faction, out var name))
                set.Add(faction, name);
        }

        s.Info($"loaded, {set.Factions.Count} faction(s)");
        return set;
    }

    private static bool TryReadFaction(string id, JToken token, FordsConfigScope s, out FactionCatalog faction, out string name)
    {
        faction = null!;
        name = id;
        if (!(token is JObject f))
        {
            s.Warn($"faction '{id}' skipped (it is not an object)");
            return false;
        }

        var culture = FordsConfigScope.Text(f["culture"]);
        if (culture == null)
        {
            s.Warn($"faction '{id}' skipped (it has no 'culture')");
            return false;
        }

        var units = ReadUnits(id, f["units"], s);
        if (units.Count == 0)
        {
            s.Warn($"faction '{id}' skipped (no valid unit offers)");
            return false;
        }

        name = FordsConfigScope.Text(f["name"]) ?? id;
        faction = new FactionCatalog(id, culture, units, ReadHeroes(id, f["heroes"], s), ReadFreeWaves(id, f["freeWaves"], s),
            ReadFeats(id, f["feats"], s), ReadTraits(id, f["traits"], s), ReadRotation(id, f["rotation"], s), ReadCreatures(id, f["creatures"], s), name, ReadBuyCycle(id, f["aiBuyCycle"], s), ReadUpgradeCost(id, f["upgradeCost"], s));
        return true;
    }

    private static Dictionary<string, float>? ReadUpgradeCost(string faction, JToken? token, FordsConfigScope s)
    {
        if (token == null)
            return null;
        if (!(token is JObject o))
        {
            s.Warn($"faction '{faction}': 'upgradeCost' is not an object; ignored");
            return null;
        }

        var costs = new Dictionary<string, float>();
        foreach (var property in o.Properties())
        {
            if (FordsConfigScope.TryFloat(property.Value, 0.01f, 100f, out var value, out var why))
                costs[property.Name] = value;
            else
                s.Warn($"faction '{faction}' upgradeCost '{property.Name}' {why}; ignored");
        }

        return costs;
    }

    private static List<string>? ReadBuyCycle(string faction, JToken? token, FordsConfigScope s)
    {
        if (token == null)
            return null;
        if (!(token is JArray array))
        {
            s.Warn($"faction '{faction}': 'aiBuyCycle' is not an array; ignored");
            return null;
        }

        var cycle = array.Select(FordsConfigScope.Text).Where(e => e != null).Select(e => e!).ToList();
        return cycle.Count == 0 ? null : cycle;
    }

    private static FactionFeats? ReadFeats(string faction, JToken? token, FordsConfigScope s)
    {
        if (token == null)
            return null;
        if (!(token is JObject o))
        {
            s.Warn($"faction '{faction}': 'feats' is not an object; ignored");
            return null;
        }

        float Factor(string key, float fallback)
        {
            if (o[key] == null)
                return fallback;
            if (FordsConfigScope.TryFloat(o[key], 0f, 100f, out var value, out var why))
                return value;
            s.Warn($"faction '{faction}' feat '{key}' {why}; using {fallback}");
            return fallback;
        }

        int Bonus(string key)
        {
            if (o[key] == null)
                return 0;
            if (FordsConfigScope.TryInt(o[key], -1000, 1000, out var value, out var why))
                return value;
            s.Warn($"faction '{faction}' feat '{key}' {why}; using 0");
            return 0;
        }

        return new FactionFeats
        {
            TowerFireRate = Factor("towerFireRate", 1f),
            TowerRange = Factor("towerRange", 1f),
            TowerHitPoints = Factor("towerHitPoints", 1f),
            TowerRegen = Factor("towerRegen", 1f),
            EnemyTowerFireRate = Factor("enemyTowerFireRate", 1f),
            RangedDamageToTowers = Factor("rangedDamageToTowers", 1f),
            OrderCooldown = Factor("orderCooldown", 1f),
            CampReward = Factor("campReward", 1f),
            CaptureIncome = Factor("captureIncome", 1f),
            DamageToTowers = Factor("damageToTowers", 1f),
            MountedDamageToTowers = Factor("mountedDamageToTowers", 1f),
            WaveInterval = Factor("waveInterval", 1f),
            UnitCapBonus = Bonus("unitCapBonus"),
            HeroSlotBonus = Bonus("heroSlotBonus"),
            Income = Factor("income", 1f),
            Bounty = Factor("bounty", 1f),
        };
    }

    private static List<FactionTrait>? ReadTraits(string faction, JToken? token, FordsConfigScope s)
    {
        if (token == null)
            return null;
        if (!(token is JArray array))
        {
            s.Warn($"faction '{faction}': 'traits' is not an array; ignored");
            return null;
        }

        var traits = new List<FactionTrait>();
        foreach (var entry in array)
        {
            var text = FordsConfigScope.Text(entry["text"]);
            if (text == null)
            {
                s.Warn($"faction '{faction}': a trait has no 'text'; skipped");
                continue;
            }

            traits.Add(new FactionTrait(text, entry["strength"]?.Type == JTokenType.Boolean && (bool)entry["strength"]!));
        }

        return traits;
    }

    private static OfferRotation? ReadRotation(string faction, JToken? token, FordsConfigScope s)
    {
        if (token == null)
            return null;
        if (!(token is JObject o) || !(o["regions"] is JArray array))
        {
            s.Warn($"faction '{faction}': 'rotation' needs a 'regions' array; ignored");
            return null;
        }

        if (!FordsConfigScope.TryFloat(o["seconds"], 1f, 3600f, out var seconds, out var secondsWhy)
            || !FordsConfigScope.TryInt(o["callFee"], 0, MaxPrice, out var fee, out secondsWhy))
        {
            s.Warn($"faction '{faction}': 'rotation' ignored ({secondsWhy})");
            return null;
        }

        var regions = new List<RegionBand>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in array)
        {
            var id = FordsConfigScope.Text(entry["id"]);
            if (id == null || !seen.Add(id))
            {
                s.Warn($"faction '{faction}': a region has no 'id' or repeats one; skipped");
                continue;
            }

            var units = ReadUnits($"{faction}/{id}", entry["units"], s);
            if (units.Count > 0)
                regions.Add(new RegionBand(id, FordsConfigScope.Text(entry["name"]) ?? id, units));
            else
                s.Warn($"faction '{faction}': region '{id}' skipped (no valid unit offers)");
        }

        return regions.Count == 0 ? null : new OfferRotation(seconds, fee, regions);
    }

    private static CreatureCall? ReadCreatures(string faction, JToken? token, FordsConfigScope s)
    {
        if (token == null)
            return null;
        if (!(token is JObject o) || !FordsConfigScope.TryFloat(o["cooldownSeconds"], 1f, 3600f, out var cooldown, out var why))
        {
            s.Warn($"faction '{faction}': 'creatures' needs an object with 'cooldownSeconds'; ignored");
            return null;
        }

        var units = ReadUnits($"{faction}/creatures", o["units"], s);
        return units.Count == 0 ? null : new CreatureCall(cooldown, units);
    }

    private static List<UnitOffer> ReadUnits(string faction, JToken? token, FordsConfigScope s)
    {
        var result = new List<UnitOffer>();
        if (token == null)
            return result;
        if (!(token is JArray array))
        {
            s.Warn($"faction '{faction}': 'units' is not an array");
            return result;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < array.Count; i++)
        {
            if (!(array[i] is JObject entry))
            {
                s.Warn($"faction '{faction}' unit #{i} skipped (it is not an object)");
                continue;
            }

            var label = $"faction '{faction}' unit '{FordsConfigScope.Text(entry["id"]) ?? "#" + i}'";

            if (!TryReadUnit(entry, out var offer, out var why))
            {
                s.Warn($"{label} skipped ({why})");
                continue;
            }

            if (!seen.Add(offer.OfferId))
            {
                s.Warn($"{label} skipped (duplicate offer id, the first one is kept)");
                continue;
            }

            result.Add(offer);
        }

        return result;
    }

    private static bool TryReadUnit(JObject o, out UnitOffer offer, out string why)
    {
        offer = null!;
        var id = FordsConfigScope.Text(o["id"]);
        var troop = FordsConfigScope.Text(o["troop"]);
        if (id == null)
            return Fail("it has no 'id'", out why);
        if (troop == null)
            return Fail("it has no 'troop'", out why);
        if (!TryReadRole(o["role"], out var role, out why))
            return false;
        if (!FordsConfigScope.TryInt(o["packSize"], 1, MaxPackSize, out var packSize, out var sizeWhy))
            return Fail($"'packSize' {sizeWhy}", out why);

        int price;
        if (o["price"] is JToken priceToken)
        {
            if (!FordsConfigScope.TryInt(priceToken, 1, MaxPrice, out price, out var priceWhy))
                return Fail($"'price' {priceWhy}", out why);
        }
        else if (o["level"] is JToken levelToken)
        {
            if (!FordsConfigScope.TryInt(levelToken, 1, 60, out var level, out var levelWhy))
                return Fail($"'level' {levelWhy}", out why);
            var weight = 1f;
            if (o["eliteWeight"] is JToken weightToken
                && !FordsConfigScope.TryFloat(weightToken, 0.01f, 50f, out weight, out var weightWhy))
                return Fail($"'eliteWeight' {weightWhy}", out why);
            price = TroopPricing.PackPrice(level, role == UnitRole.Cavalry || role == UnitRole.HorseArcher, weight, packSize);
        }
        else
        {
            return Fail("it needs a 'price' or a 'level'", out why);
        }

        offer = new UnitOffer(id, troop, role, packSize, price);
        why = "";
        return true;
    }

    private static bool TryReadRole(JToken? token, out UnitRole role, out string why)
    {
        role = UnitRole.Infantry;
        var text = FordsConfigScope.Text(token);
        if (text == null)
            return Fail("it has no 'role'", out why);
        if (!Enum.TryParse(text, true, out role) || !Enum.IsDefined(typeof(UnitRole), role) || role == UnitRole.Hero)
            return Fail($"role '{text}' is not one of {RoleNames}", out why);
        why = "";
        return true;
    }

    private static List<HeroOffer> ReadHeroes(string faction, JToken? token, FordsConfigScope s)
    {
        var result = new List<HeroOffer>();
        if (token == null)
            return result;
        if (!(token is JArray array))
        {
            s.Warn($"faction '{faction}': 'heroes' is not an array");
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < array.Count; i++)
        {
            var entry = array[i] as JObject;
            var id = FordsConfigScope.Text(entry?["id"]);
            var label = $"faction '{faction}' hero '{id ?? "#" + i}'";
            if (entry == null || id == null)
            {
                s.Warn($"{label} skipped (it needs an object with an 'id')");
                continue;
            }

            if (!FordsConfigScope.TryInt(entry["price"], 1, MaxPrice, out var price, out var why))
            {
                s.Warn($"{label} skipped ('price' {why})");
                continue;
            }

            if (!seen.Add(id))
            {
                s.Warn($"{label} skipped (duplicate hero id, the first one is kept)");
                continue;
            }

            result.Add(new HeroOffer(id, price));
        }

        return result;
    }

    private static List<FreeWaveTier> ReadFreeWaves(string faction, JToken? token, FordsConfigScope s)
    {
        var result = new List<FreeWaveTier>();
        if (token == null)
            return result;
        if (!(token is JArray array))
        {
            s.Warn($"faction '{faction}': 'freeWaves' is not an array");
            return result;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var path = $"faction '{faction}' freeWaves[{i}]";
            if (!(array[i] is JObject tier))
            {
                s.Warn($"{path} skipped (it is not an object)");
                continue;
            }

            if (!FordsConfigScope.TryFloat(tier["fromMinute"], 0f, 600f, out var minute, out var why))
            {
                s.Warn($"{path} skipped ('fromMinute' {why})");
                continue;
            }

            if (result.Any(t => t.FromMinute == minute))
            {
                s.Warn($"{path} skipped (a second tier at minute {FordsConfigScope.Format(minute)}, the first one is kept)");
                continue;
            }

            var units = ReadWaveUnits(path, tier["units"], s, out var warned);
            if (units.Count == 0)
            {
                if (!warned)
                    s.Warn($"{path} skipped (it has no units)");
                continue;
            }

            result.Add(new FreeWaveTier(minute, units));
        }

        return result.OrderBy(t => t.FromMinute).ToList();
    }

    private static List<UnitCount> ReadWaveUnits(string path, JToken? token, FordsConfigScope s, out bool warned)
    {
        warned = false;
        var result = new List<UnitCount>();
        if (!(token is JArray array))
            return result;

        for (var i = 0; i < array.Count; i++)
        {
            var entry = array[i] as JObject;
            var troop = FordsConfigScope.Text(entry?["troop"]);
            if (troop == null)
            {
                s.Warn($"{path}.units[{i}] skipped (it needs an object with a 'troop')");
                warned = true;
                continue;
            }

            if (!FordsConfigScope.TryInt(entry!["count"], 1, 100, out var count, out var why))
            {
                s.Warn($"{path}.units[{i}] skipped ('count' {why})");
                warned = true;
                continue;
            }

            UnitRole? role = null;
            if (entry["role"] != null)
            {
                if (TryReadRole(entry["role"], out var parsed, out var roleWhy))
                    role = parsed;
                else
                    s.Warn($"{path}.units[{i}] role ignored ({roleWhy})");
            }

            result.Add(new UnitCount(troop, count, role));
        }

        return result;
    }

    private static bool Fail(string reason, out string why)
    {
        why = reason;
        return false;
    }
}

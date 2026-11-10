using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.HeroDefence.Domain;
using TAOM.Features.LongDefeat;

namespace TAOM.Features.HeroDefence;

/// <summary>The signatures and the cultures that survived validation. A signature carries its count factor and first power, which every culture of it shares.</summary>
internal sealed record HeroDefenceCulturesFile(IReadOnlyDictionary<string, SignatureDef> Signatures, IReadOnlyList<CultureDef> Cultures)
{
    public static HeroDefenceCulturesFile Empty { get; } = new(new Dictionary<string, SignatureDef>(), Array.Empty<CultureDef>());
}

/// <summary>Reads hero_defence/cultures.json against the rosters. A bad culture leaves the rotation with one warning, and the others stay.</summary>
internal static class HeroDefenceCulturesReader
{
    private const int LeaderCount = 3;
    private const int MixTotal = 100;

    private static readonly Dictionary<string, CreepClass> MixKeys = new(StringComparer.Ordinal)
    {
        ["infantry"] = CreepClass.Infantry,
        ["ranged"] = CreepClass.Ranged,
        ["cavalry"] = CreepClass.Cavalry,
        ["horseArcher"] = CreepClass.HorseArcher,
    };

    private sealed record SignatureRow(SignatureDef Def, float CountFactor, string FirstPower);

    public static HeroDefenceCulturesFile Read(string? text, IReadOnlyList<string> powerPool, IReadOnlyDictionary<string, IReadOnlyList<RosterRow>> rosters, HeroDefenceConfigScope s)
    {
        var root = s.OpenRoot(text, "is missing; no culture can appear");
        if (root == null)
            return HeroDefenceCulturesFile.Empty;

        var signatures = ReadSignatures(s.Group(root, "signatures", ""), powerPool, s);
        var cultures = ReadCultures(s.Group(root, "cultures", ""), signatures, rosters, s);
        if (cultures.Count == 0)
            s.Warn("has no valid culture; the mode cannot start");
        s.Loaded($"{cultures.Count} cultures");
        return new HeroDefenceCulturesFile(signatures.ToDictionary(pair => pair.Key, pair => pair.Value.Def), cultures);
    }

    private static Dictionary<string, SignatureRow> ReadSignatures(JObject? group, IReadOnlyList<string> powerPool, HeroDefenceConfigScope s)
    {
        var read = new Dictionary<string, SignatureRow>(StringComparer.Ordinal);
        if (group == null)
            return read;

        foreach (var property in group.Properties())
        {
            if (TryReadSignature(property.Name, property.Value as JObject, powerPool, s) is { } row)
                read[property.Name] = row;
        }

        return read;
    }

    private static SignatureRow? TryReadSignature(string id, JObject? o, IReadOnlyList<string> powerPool, HeroDefenceConfigScope s)
    {
        var path = "signatures." + id;
        if (o == null)
        {
            s.Warn($"'{path}' is not an object; skipped");
            return null;
        }

        if (!TryOrder(o["order"], out var order))
        {
            s.Warn($"'{path}.order' must name Infantry, Ranged, Cavalry and HorseArcher once each; skipped");
            return null;
        }

        if (!LongDefeatConfigScope.TryFloat(o["countFactor"], 0.25f, 2f, out var factor, out var why))
        {
            s.Warn($"'{path}.countFactor' {why}; skipped");
            return null;
        }

        var firstPower = LongDefeatConfigScope.Text(o["firstPower"]);
        if (firstPower == null || !powerPool.Contains(firstPower))
        {
            s.Warn($"'{path}.firstPower' is '{firstPower}', which is not in powers.pool of waves.json; skipped");
            return null;
        }

        var shuffled = o["shuffled"];
        if (shuffled != null && shuffled.Type != JTokenType.Boolean)
        {
            s.Warn($"'{path}.shuffled' is not true or false; skipped");
            return null;
        }

        return new SignatureRow(new SignatureDef(id, order, shuffled?.Value<bool>() ?? false), factor, firstPower);
    }

    private static bool TryOrder(JToken? token, out IReadOnlyList<CreepClass> order)
    {
        order = Array.Empty<CreepClass>();
        if (token is not JArray array)
            return false;

        var classes = new List<CreepClass>();
        foreach (var item in array)
        {
            if (!HeroDefenceRosterReader.TryClass(LongDefeatConfigScope.Text(item), out var cls))
                return false;
            classes.Add(cls);
        }

        if (classes.Count != MixKeys.Count || classes.Distinct().Count() != classes.Count)
            return false;

        order = classes;
        return true;
    }

    private static List<CultureDef> ReadCultures(JObject? group, Dictionary<string, SignatureRow> signatures, IReadOnlyDictionary<string, IReadOnlyList<RosterRow>> rosters, HeroDefenceConfigScope s)
    {
        var cultures = new List<CultureDef>();
        if (group == null)
            return cultures;

        foreach (var property in group.Properties())
        {
            if (TryReadCulture(property.Name, property.Value as JObject, signatures, rosters, s) is { } culture)
                cultures.Add(culture);
        }

        foreach (var orphan in rosters.Keys.Where(id => group[id] == null))
            s.Warn($"rosters.json has the culture '{orphan}', and this file does not; it does not appear");

        return cultures;
    }

    private static CultureDef? TryReadCulture(string id, JObject? o, Dictionary<string, SignatureRow> signatures, IReadOnlyDictionary<string, IReadOnlyList<RosterRow>> rosters, HeroDefenceConfigScope s)
    {
        var path = "cultures." + id;
        if (o == null)
        {
            s.Warn($"'{path}' is not an object; skipped");
            return null;
        }

        var signatureId = LongDefeatConfigScope.Text(o["signature"]);
        if (signatureId == null || !signatures.TryGetValue(signatureId, out var signature))
        {
            s.Warn($"'{path}.signature' is '{signatureId}', which names no valid signature; skipped");
            return null;
        }

        if (!TryMix(o["mix"] as JObject, out var mix, out var why))
        {
            s.Warn($"'{path}.mix' {why}; skipped");
            return null;
        }

        if (!TryLeaders(o["leaders"], out var leaders))
        {
            s.Warn($"'{path}.leaders' must name {LeaderCount} different characters: captain, lieutenant, general; skipped");
            return null;
        }

        if (!rosters.TryGetValue(id, out var roster))
        {
            s.Warn($"'{path}' has no rows in rosters.json; skipped");
            return null;
        }

        if (!roster.Any(row => row.Class == CreepClass.Infantry))
        {
            s.Warn($"'{path}' has no infantry in rosters.json, and a wave needs foot soldiers; skipped");
            return null;
        }

        return new CultureDef(id, signatureId, mix, signature.CountFactor, signature.FirstPower, leaders, roster);
    }

    private static bool TryMix(JObject? o, out IReadOnlyDictionary<CreepClass, int> mix, out string why)
    {
        mix = new Dictionary<CreepClass, int>();
        if (o == null)
        {
            why = "is not an object";
            return false;
        }

        var read = new Dictionary<CreepClass, int>();
        foreach (var property in o.Properties())
        {
            if (!MixKeys.TryGetValue(property.Name, out var cls))
            {
                why = $"has the unknown class '{property.Name}'";
                return false;
            }

            if (!LongDefeatConfigScope.TryInt(property.Value, 0, MixTotal, out var share, out why))
            {
                why = $"'{property.Name}' {why}";
                return false;
            }

            read[cls] = share;
        }

        if (read.Values.Sum() != MixTotal || !read.TryGetValue(CreepClass.Infantry, out var infantry) || infantry <= 0)
        {
            why = $"must add up to {MixTotal} and give infantry a share";
            return false;
        }

        mix = read;
        why = "";
        return true;
    }

    private static bool TryLeaders(JToken? token, out IReadOnlyList<string> leaders)
    {
        leaders = Array.Empty<string>();
        if (token is not JArray array)
            return false;

        var ids = array.Select(LongDefeatConfigScope.Text).ToList();
        if (ids.Count != LeaderCount || ids.Any(id => id == null) || ids.Distinct(StringComparer.Ordinal).Count() != LeaderCount)
            return false;

        leaders = ids!;
        return true;
    }
}

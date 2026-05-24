using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Features.FactionMap;

public static class FactionDataParser
{
    public static FactionBonus[] ParseBonuses(JArray? arr)
    {
        if (arr == null) return Array.Empty<FactionBonus>();
        var list = new List<FactionBonus>();
        foreach (var item in arr)
        {
            if (item is JObject obj)
                list.Add(new FactionBonus
                {
                    Text = obj.Value<string>("text") ?? "",
                    Positive = obj.Value<bool?>("positive") ?? true,
                });
        }
        return list.ToArray();
    }

    public static FactionPerk[] ParsePerks(JArray? arr)
    {
        if (arr == null) return Array.Empty<FactionPerk>();
        var list = new List<FactionPerk>();
        foreach (var item in arr)
        {
            if (item is JObject obj)
                list.Add(new FactionPerk
                {
                    Name = obj.Value<string>("name") ?? "",
                    Description = obj.Value<string>("description") ?? "",
                });
        }
        return list.ToArray();
    }

    // Parses either the new "special_units": [...] array form OR the legacy "special_unit": {...} single-object form.
    // Returns empty array if both are null/absent.
    public static FactionSpecialUnit[] ParseSpecialUnits(JArray? arr, JObject? legacyObj)
    {
        if (arr != null)
        {
            var list = new List<FactionSpecialUnit>();
            foreach (var item in arr)
            {
                if (item is JObject obj)
                    list.Add(new FactionSpecialUnit
                    {
                        Name = obj.Value<string>("name") ?? "",
                        Description = obj.Value<string>("description") ?? "",
                    });
            }
            return list.ToArray();
        }
        if (legacyObj != null)
        {
            return new[]
            {
                new FactionSpecialUnit
                {
                    Name = legacyObj.Value<string>("name") ?? "",
                    Description = legacyObj.Value<string>("description") ?? "",
                }
            };
        }
        return Array.Empty<FactionSpecialUnit>();
    }
}

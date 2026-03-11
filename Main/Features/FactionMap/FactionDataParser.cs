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

    public static FactionSpecialUnit? ParseSpecialUnit(JObject? obj)
    {
        if (obj == null) return null;
        return new FactionSpecialUnit
        {
            Name = obj.Value<string>("name") ?? "",
            Description = obj.Value<string>("description") ?? "",
        };
    }
}

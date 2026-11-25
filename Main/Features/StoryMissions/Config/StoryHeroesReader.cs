using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.StoryMissions.Config;

/// <summary>
/// A story mission's <c>heroes</c> list (#1366): the heroes who stand free with the garrison, at no price. Each row is the shop's hero row without a
/// <c>price</c>: <c>id</c>, <c>troop</c> and an optional <c>healthFactor</c> (1 to 10; 1 when absent), read by <c>HeroesReader.ReadRow</c>, so
/// <c>hero=aragorn</c> reads the same in both modes. A missing key is no hero and no warning; a value that is not an array gives no hero and one warning; a bad
/// or repeated row is skipped with one warning; past <see cref="MaxHeroes"/> valid rows the rest are dropped with one warning. Whether a troop exists is the
/// launch's check (<c>RunPlanTroops.AllTroopIds</c>).
/// </summary>
internal static class StoryHeroesReader
{
    public const int MaxHeroes = 4;

    public static IReadOnlyList<HeroSpawn> Read(JToken? token, string label, LongDefeatConfigScope s)
    {
        var heroes = new List<HeroSpawn>();
        if (token == null || token.Type == JTokenType.Null)
            return heroes;
        if (!(token is JArray rows))
        {
            s.Warn($"'{label}.heroes' is not an array; this mission has no free hero");
            return heroes;
        }

        var capped = false;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = HeroesReader.ReadRow(rows[i], $"{label}.heroes[{i}]", heroes.ConvertAll(h => (h.Id, h.TroopId)), s);
            if (row == null)
                continue;
            if (heroes.Count == MaxHeroes)
            {
                capped = true;
                continue;
            }

            heroes.Add(new HeroSpawn(row.Id, row.TroopId, row.HealthFactor));
        }

        if (capped)
            s.Warn($"'{label}.heroes' lists more than {MaxHeroes} heroes; the first {MaxHeroes} are kept");
        return heroes;
    }
}

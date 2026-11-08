using System;
using Newtonsoft.Json.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// A faction's <c>tactics</c> block: <c>march</c> and <c>close</c> name a <see cref="LaneArrangement"/>, <c>closeRange</c> and
/// <c>chargeRange</c> are metres from 1 to 100. Every key is optional. A block with a bad value is dropped whole with one warning,
/// so the faction fights as a faction without tactics rather than with half of one.
/// </summary>
internal static class FordsTacticsReader
{
    public static LaneTactic? Read(string faction, JToken? token, FordsConfigScope s)
    {
        if (token == null)
            return null;

        if (!(token is JObject o))
        {
            s.Warn($"faction '{faction}': 'tactics' is not an object; ignored");
            return null;
        }

        if (!TryArrangement(o["march"], out var march, out var why) || !TryArrangement(o["close"], out var close, out why)
            || !TryRange(o, "closeRange", out var closeRange, out why) || !TryRange(o, "chargeRange", out var chargeRange, out why))
        {
            s.Warn($"faction '{faction}' tactics {why}; ignored");
            return null;
        }

        return new LaneTactic(march, close, closeRange, chargeRange);
    }

    private static bool TryArrangement(JToken? token, out LaneArrangement? value, out string why)
    {
        value = null;
        why = string.Empty;
        if (token == null)
            return true;

        // One exact name: Enum.TryParse would also take "2" and "Loose, ShieldWall" (1 | 2, which is Square).
        var text = FordsConfigScope.Text(token);
        if (text != null && Array.IndexOf(Enum.GetNames(typeof(LaneArrangement)), text) >= 0)
        {
            value = (LaneArrangement)Enum.Parse(typeof(LaneArrangement), text);
            return true;
        }

        why = $"arrangement '{token}' is not one of {string.Join(", ", Enum.GetNames(typeof(LaneArrangement)))}";
        return false;
    }

    private static bool TryRange(JObject o, string key, out float? value, out string why)
    {
        value = null;
        why = string.Empty;
        if (o[key] == null)
            return true;

        if (!FordsConfigScope.TryFloat(o[key], 1f, 100f, out var metres, out var reason))
        {
            why = $"'{key}' {reason}";
            return false;
        }

        value = metres;
        return true;
    }
}

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The fortification points of a fortress row (phase3-plan.md section 5), the blocks <c>taom.ld_export</c> writes:
/// <c>towers</c>, a list of <c>{id, posts: [{x,y,z,fx,fy}], volley: [{x,y,z}]}</c>, and <c>volley</c>, <c>{origins: [{x,y,z}]}</c>.
/// An absent or null block is nothing and no warning. A bad point is dropped on its own with one warning; a tower with no id, a bad id or a repeated id is skipped
/// with one warning, and so is a tower left with no usable point; a block of the wrong shape is dropped whole with one warning. The fortress is never skipped for them.
/// The <c>armoury</c> and <c>yard</c> places (phase4-plan.md section 5) are read here too, each through the muster point's reader: absent or null is no place, a bad one is one warning and no place.
/// </summary>
internal static partial class FortressesReader
{
    private const string PostFallback = "the post is dropped";
    private const string SpotFallback = "the point is dropped";

    /// <summary>A tower that records fewer posts than the shop's top tower level asks fills what it has (phase3-plan.md section 5); this says so once, naming the tower.</summary>
    private static void WarnShortTowers(IReadOnlyList<LdTower>? towers, ShopPlan shop, string fortress, LongDefeatConfigScope s)
    {
        var asked = shop.Fort?.Towers?.Levels.Max(l => l.Posts) ?? 0;
        foreach (var tower in towers ?? new List<LdTower>())
        {
            if (tower.Posts.Count < asked)
                s.Warn($"fortress '{fortress}': tower '{tower.Id}' records {tower.Posts.Count} post(s) but the shop's top tower level asks {asked}; it fills the {tower.Posts.Count} it has");
        }
    }

    /// <summary>A tower id is one or more lowercase ASCII letters or underscores, so it can be typed after <c>taom.ld_record post</c> and read back as a JSON string.</summary>
    internal static bool IsTowerId(string id)
    {
        if (id.Length == 0)
            return false;
        foreach (var c in id)
        {
            if (!(c >= 'a' && c <= 'z') && c != '_')
                return false;
        }

        return true;
    }

    private static IReadOnlyList<LdTower>? ReadTowers(JToken? token, string fortress, LongDefeatConfigScope s)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        var label = $"fortress '{fortress}' towers";
        if (!(token is JArray array))
        {
            s.Warn($"{label} is not an array; the fortress has no tower points");
            return null;
        }

        var towers = new List<LdTower>();
        var seen = new HashSet<string>();
        for (var i = 0; i < array.Count; i++)
        {
            var tower = ReadTower(array[i], $"{label}[{i}]", label, seen, s);
            if (tower != null)
                towers.Add(tower);
        }

        return towers.Count == 0 ? null : towers;
    }

    private static LdTower? ReadTower(JToken? token, string position, string label, HashSet<string> seen, LongDefeatConfigScope s)
    {
        if (!(token is JObject o))
        {
            s.Warn($"{position} is not an object; tower skipped");
            return null;
        }

        var id = LongDefeatConfigScope.Text(o["id"]);
        if (id == null || !IsTowerId(id))
        {
            s.Warn($"{position} needs an 'id' of lowercase letters and underscores; tower skipped");
            return null;
        }

        var named = $"{label} '{id}'";
        if (seen.Contains(id))
        {
            s.Warn($"{named} skipped (duplicate id, the first one is kept)");
            return null;
        }

        var posts = new List<MusterFrame>();
        ReadList(o["posts"], named + " posts", s, (JToken? t, string name, out MusterFrame post) => TryReadPost(t, name, s, out post), posts);
        var spots = new List<LdPoint>();
        ReadList(o["volley"], named + " volley", s, (JToken? t, string name, out LdPoint spot) => TryReadSpot(t, name, s, out spot), spots);
        if (posts.Count == 0 && spots.Count == 0)
        {
            s.Warn($"{named} has no usable post or volley spot; tower skipped");
            return null;
        }

        seen.Add(id);
        return new LdTower(id, posts, spots);
    }

    private static LdVolleyOrigins? ReadVolley(JToken? token, string fortress, LongDefeatConfigScope s)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        var label = $"fortress '{fortress}' volley";
        if (!(token is JObject o))
        {
            s.Warn($"{label} is not an object; the fortress has no volley origins");
            return null;
        }

        var origins = new List<LdPoint>();
        ReadList(o["origins"], label + " origins", s, (JToken? t, string name, out LdPoint spot) => TryReadSpot(t, name, s, out spot), origins);
        return origins.Count == 0 ? null : new LdVolleyOrigins(origins);
    }

    private const string ArmouryFallback = "the armoury is hidden";
    private const string YardFallback = "the engineer's yard is hidden";

    // The armoury and the yard (phase4-plan.md section 5), each recorded as the muster point is: {x,y,z,fx,fy}. Absent or null is no place and no warning; a bad one is one warning and no place, and the fortress stays.
    private static MusterFrame? ReadPlace(JToken? token, string fortress, string key, string fallback, LongDefeatConfigScope s)
    {
        if (token == null || token.Type == JTokenType.Null)
            return null;

        return TryReadMusterFrame(token, $"fortress '{fortress}' {key}", s, out var frame, fallback) ? frame : null;
    }

    private delegate bool TryReadPoint<T>(JToken? token, string label, out T value);

    // Reads each element of an optional list into <paramref name="into"/>, skipping one the reader refused (it has warned); an absent or null list is empty and silent, a list of the wrong type is one warning.
    private static void ReadList<T>(JToken? token, string label, LongDefeatConfigScope s, TryReadPoint<T> read, List<T> into)
    {
        if (token == null || token.Type == JTokenType.Null)
            return;

        if (!(token is JArray array))
        {
            s.Warn($"{label} is not an array; its points are dropped");
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (read(array[i], $"{label}[{i}]", out var item))
                into.Add(item);
        }
    }

    private static bool TryReadPost(JToken? token, string label, LongDefeatConfigScope s, out MusterFrame post)
    {
        post = null!;
        if (token == null || token.Type == JTokenType.Null)
        {
            s.Warn($"{label} is null; {PostFallback}");
            return false;
        }

        if (!TryReadMusterFrame(token, label, s, out var frame, PostFallback) || frame == null)
            return false;
        post = frame;
        return true;
    }

    private static bool TryReadSpot(JToken? token, string label, LongDefeatConfigScope s, out LdPoint spot)
    {
        spot = default;
        if (!(token is JObject o))
        {
            s.Warn($"{label} is not an object; {SpotFallback}");
            return false;
        }

        var axes = new float[3];
        var names = new[] { "x", "y", "z" };
        for (var i = 0; i < 3; i++)
        {
            if (!LongDefeatConfigScope.TryFloat(o[names[i]], -MaxCoordinate, MaxCoordinate, out axes[i], out var why))
            {
                s.Warn($"{label}.{names[i]} {why}; {SpotFallback}");
                return false;
            }
        }

        spot = new LdPoint(axes[0], axes[1], axes[2]);
        return true;
    }
}

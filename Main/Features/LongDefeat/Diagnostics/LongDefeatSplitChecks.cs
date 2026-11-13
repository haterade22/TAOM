using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat.Diagnostics;

/// <summary>
/// The <c>split</c> verdict of the opening split (Mike, 2026-11-12), key <c>w&lt;N&gt;</c> for the wave the split stood before. It is written when the hold ends at the first contact. Pass needs every
/// group the split made to hold soldiers and to stand at its place: the median ground distance within <see cref="FootReach"/> for the infantry, whose line spans the wall, and within
/// <see cref="ArcherReach"/> for the archers, whom Scatter spreads over the archer points within 40 m of the order position on each ground axis (the <c>strategic_archer_point</c> prefab's
/// <c>_distanceToCheck</c>). A skip proves nothing and names why: <c>short-hold</c> (under <see cref="MinHoldSeconds"/>, no time to walk), <c>engine-took</c> (no group was still off AI control at the
/// release), <c>charge</c>, <c>no-contact</c> (the wave cleared first), <c>run-ended</c> or <c>no-groups</c>. A split the scene gives no places for fails at once with <c>reason=no-places</c>. Every word of the detail is <c>name=value</c> (the sanitiser rule
/// in <see cref="LongDefeatPostsChecks"/>).
/// </summary>
internal static class LongDefeatSplitChecks
{
    internal const string SplitId = "taom.long-defeat.split";
    internal const float FootReach = 25f;
    internal const float ArcherReach = 40f;
    internal const float MinHoldSeconds = 20f;

    internal static CheckVerdict Stand(int wave, IReadOnlyList<LdSplitGroup> groups, float heldSeconds, LdSplitRelease release, IReadOnlyList<int> close)
    {
        var detail = $"w={N(wave)} " + string.Join(" ", groups.Select(Describe)) + $" held_s={F1(heldSeconds)} released={N(release.Released)} kept={N(release.KeptByPlayer)} close={string.Join(",", close)}";
        if (groups.Count == 0)
            return Skip(detail, "no-groups");
        if (release.Released + release.KeptByPlayer == 0)
            return Skip(detail, "engine-took");
        if (!(heldSeconds >= MinHoldSeconds))
            return Skip(detail, "short-hold");

        var ok = groups.All(g => g.Count > 0 && g.Median <= (g.Archers ? ArcherReach : FootReach));
        return LongDefeatChecks.Verdict(ok, detail);
    }

    internal static CheckVerdict Closed(int wave, IReadOnlyList<LdSplitGroup> groups, string reason) => Skip($"w={N(wave)} " + string.Join(" ", groups.Select(Describe)), reason);

    internal static CheckVerdict NoPlaces(int wave, string problem) => new(CheckResult.Fail, $"w={N(wave)} reason=no-places problem=" + problem.Replace(' ', '_'));

    private static string Describe(LdSplitGroup g) => $"{g.Name}={N(g.Count)} {g.Name}_median={F1(g.Median)} {g.Name}_high={N(g.High)}";

    private static CheckVerdict Skip(string detail, string reason) => new(CheckResult.Skip, (detail + " reason=" + reason).Trim());

    private static string N(int v) => v.ToString(CultureInfo.InvariantCulture);

    private static string F1(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
}

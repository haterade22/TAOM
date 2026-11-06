using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>How a posted archer is kept at his post (spike S6): <c>A</c> a scripted hold, <c>B</c> A and a speed limit of 0, <c>C</c> A and the formation held by <c>SetControlledByAI(true, enforceNotSplittableByAI: true)</c>.</summary>
internal enum PostsVariant
{
    A,
    B,
    C,
}

/// <summary>The variant in force and how a run names another (an environment variable of the game process; the wrapper's <c>--posts-variant</c> sets it).</summary>
internal static class PostsVariants
{
    /// <summary>The environment variable that picks the variant for a run: <c>A</c>, <c>B</c> or <c>C</c>, in either case. Anything else, or nothing, keeps <see cref="Default"/>.</summary>
    internal const string EnvironmentName = "TAOM_LD_POSTS_VARIANT";

    /// <summary>The plan's first choice (variant A).</summary>
    internal const PostsVariant Default = PostsVariant.A;

    /// <summary>The named variant, or null when the text names none.</summary>
    internal static PostsVariant? Parse(string? text)
    {
        switch (text?.Trim().ToUpperInvariant())
        {
            case "A": return PostsVariant.A;
            case "B": return PostsVariant.B;
            case "C": return PostsVariant.C;
            default: return null;
        }
    }
}

/// <summary>The numbers the posts check and the hold hold to.</summary>
internal static class PostsRules
{
    /// <summary>A posted archer is held when he stands within this many metres of his post, measured on the ground plane (the Fords <c>GarrisonLook</c> shape).</summary>
    internal const float HoldMetres = 1.5f;

    /// <summary>... and within this many metres of its height.</summary>
    internal const float HoldHeight = 2f;

    /// <summary>The wave's seconds at which the held sample is taken: the archers must still be at their posts then.</summary>
    internal const float SampleSeconds = 30f;

    /// <summary>The posts hold is re-asserted this often (D30a).</summary>
    internal const float ReassertSeconds = 1f;

    /// <summary>Whether an archer's drift and height difference from his post keep him held.</summary>
    internal static bool Held(float drift, float dz) => drift <= HoldMetres && dz <= HoldHeight;
}

/// <summary>
/// What a fill did for one tower: the posts it should hold, the archers already standing there, those it spawned, the posts the garrison cap left empty (<c>Short</c>, named and never failed) and the spawns
/// that threw or had no troop (<c>Failed</c>, which the check fails: a failure is not a cap).
/// </summary>
internal readonly record struct LdTowerFill(string TowerId, int Wanted, int Standing, int Spawned, int Short, int Failed);

/// <summary>
/// What the field did with <see cref="LdPostFill"/>. <c>MovedOut</c> counts the soldiers who were not post archers that stood in the posts formation (the tactic splits units into any empty index, E21) and were moved
/// to the Ranged formation before the spawn, as the hold does; the fill goes on.
/// </summary>
internal sealed record LdPostsFilled(IReadOnlyList<LdTowerFill> Towers, int MovedOut)
{
    public int Spawned => Towers.Sum(t => t.Spawned);

    public int Short => Towers.Sum(t => t.Short);

    public int Failed => Towers.Sum(t => t.Failed);

    public static LdPostsFilled Nothing { get; } = new(Array.Empty<LdTowerFill>(), 0);
}

/// <summary>
/// One tower's posted archers as the field reads them: how many archers stand (live, in the posts), how many of those are within <see cref="PostsRules.HoldMetres"/> and
/// <see cref="PostsRules.HoldHeight"/> of their own post, the worst drift and height difference, and the arrows the tower's archers have loosed since the mission began (cumulative).
/// </summary>
internal readonly record struct LdTowerPosts(string TowerId, int Standing, int Held, float MaxDrift, float MaxDz, int Shots);

/// <summary>Every tower's posts, read now.</summary>
internal sealed record LdPostsReading(IReadOnlyList<LdTowerPosts> Towers)
{
    public static LdPostsReading None { get; } = new(Array.Empty<LdTowerPosts>());

    public int Standing
    {
        get
        {
            var total = 0;
            for (var i = 0; i < Towers.Count; i++)
                total += Towers[i].Standing;
            return total;
        }
    }
}

/// <summary>What one pass of the hold did (D30a): archers taken out of the posts formation that are not post archers, post archers put back in it, and whether it found the formation under AI and took it off.</summary>
internal readonly record struct LdPostsHold(int MovedOut, int MovedBack, bool AiRegained)
{
    public static LdPostsHold Nothing { get; } = new(0, 0, false);

    public int Moved => MovedOut + MovedBack;
}

/// <summary>
/// Which posts a fill spawns for one tower (D20, OQ1): the post indexes below <c>wanted</c> that no live archer holds, in order, as many as the garrison cap leaves room for; the rest are short, and a short fill is
/// named on the hold line (<c>short=N</c>) and never fails the check (a spawn that failed is <c>failed=N</c> and does). A tower fills the posts it has, so <c>wanted</c> is already capped by the recorded count.
/// </summary>
internal static class PostsFillPlan
{
    internal readonly record struct Result(IReadOnlyList<int> Spawn, int Short);

    public static Result For(int wanted, IReadOnlyCollection<int> taken, int room)
    {
        var spawn = new List<int>();
        var shortBy = 0;
        var left = Math.Max(0, room);
        for (var i = 0; i < wanted; i++)
        {
            if (taken.Contains(i))
                continue;

            if (left > 0)
            {
                spawn.Add(i);
                left--;
            }
            else
            {
                shortBy++;
            }
        }

        return new Result(spawn, shortBy);
    }
}

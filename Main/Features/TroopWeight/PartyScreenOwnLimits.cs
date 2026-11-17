using System.Runtime.CompilerServices;

namespace TAOM.Features.TroopWeight;

/// <summary>
/// Whether each side of a party screen shows the owning party's own size limit, decided once when the screen opens
/// (#1430). <c>PartyScreenLogic.Initialize</c> copies each side's limit once (v1.5.7 :491, :494), but the normal screen
/// edits the live roster (<c>PartyScreenHelper.OpenPartyScreen</c> :53), so the party's weight-deflated limit moves with
/// the first heavy troop moved and a live comparison fails from then on. A quest or custom screen that passes another
/// limit is recorded as not the party's own and keeps vanilla's numbers.
///
/// Keyed by the screen object in a <see cref="ConditionalWeakTable{TKey,TValue}"/>: a closed screen's entry goes with
/// it, and two screens never share one.
/// </summary>
public static class PartyScreenOwnLimits
{
    private sealed class Verdict
    {
        public bool Left;
        public bool Right;
    }

    private static readonly ConditionalWeakTable<object, Verdict> AtOpen = new();

    public static void Record(object screen, bool leftIsOwn, bool rightIsOwn)
    {
        AtOpen.Remove(screen);
        AtOpen.Add(screen, new Verdict { Left = leftIsOwn, Right = rightIsOwn });
    }

    /// <summary>The verdict recorded when the screen opened, or null when none was recorded.</summary>
    public static bool? IsOwnLimit(object screen, bool rightSide)
        => AtOpen.TryGetValue(screen, out var verdict) ? (rightSide ? verdict.Right : verdict.Left) : (bool?)null;
}

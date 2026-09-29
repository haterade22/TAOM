using System.Threading;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// The player's hero's kills since the campaign last took them, for the lord's gear ladder (#693). The kill
/// counter adds each one as it happens (agent removals can arrive off the main thread, #634, hence the
/// interlocked add); the campaign takes the total once the battle ends, never while a mission runs. A process
/// singleton: the campaign behavior empties it when a campaign starts.
/// </summary>
public sealed class HeroKillTally
{
    private int _pending;

    public void Add(int kills)
    {
        if (kills > 0)
            Interlocked.Add(ref _pending, kills);
    }

    /// <summary>Everything added since the last take; the tally is empty afterwards.</summary>
    public int Take() => Interlocked.Exchange(ref _pending, 0);

    /// <summary>
    /// Whether an enemy the hero removed counts as struck down: killed, or knocked out when the ladder counts
    /// knockouts. Never a routed or deleted agent. Who removed whom is the kill counter's to check first.
    /// </summary>
    public static bool Counts(bool killed, bool knockedOut, bool countsKnockouts) =>
        killed || (countsKnockouts && knockedOut);
}

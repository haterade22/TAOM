using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

internal enum PurseSource
{
    Bounty,
    WaveBonus,
    EarlyStart,
    Boon,
    Commander,
}

/// <summary>What gold was spent on, for the report's <c>gold.spentOn</c>: packs, the gate (levels and repairs), the high towers, field care, the Elven Volley and soldier upgrades (#1161).</summary>
internal enum PurseSink
{
    Packs,
    Gate,
    Towers,
    FieldCare,
    Volley,
    Upgrades,
}

/// <summary>The player's gold. The balance is always start plus earned minus spent, which the report checks.</summary>
internal sealed class Purse
{
    private readonly Dictionary<PurseSource, long> _earned = new();
    private readonly Dictionary<PurseSink, long> _spentOn = new();

    public Purse(int startingGold)
    {
        Start = startingGold;
        Balance = startingGold;
    }

    public int Start { get; }
    public int Balance { get; private set; }
    public long TotalEarned { get; private set; }
    public long Spent { get; private set; }

    public long EarnedFrom(PurseSource source) => _earned.TryGetValue(source, out var gold) ? gold : 0;

    public long SpentOn(PurseSink sink) => _spentOn.TryGetValue(sink, out var gold) ? gold : 0;

    /// <summary>Adds gold; a zero or negative amount changes nothing.</summary>
    public void Earn(PurseSource source, int amount)
    {
        if (amount <= 0)
            return;

        Balance += amount;
        TotalEarned += amount;
        _earned[source] = EarnedFrom(source) + amount;
    }

    /// <summary>Takes the price if the balance covers it, booking it to <paramref name="sink"/>; refuses a price that is not positive. The sinks always sum to <see cref="Spent"/>.</summary>
    public bool TrySpend(int amount, PurseSink sink)
    {
        if (amount <= 0 || amount > Balance)
            return false;

        Balance -= amount;
        Spent += amount;
        _spentOn[sink] = SpentOn(sink) + amount;
        return true;
    }

    /// <summary>
    /// Gives back gold spent on <paramref name="sink"/> that bought nothing (an upgrade whose soldier was gone when it went out), as if it had never been spent, so the sinks still sum to
    /// <see cref="Spent"/>. A refund that is not positive, or larger than the sink holds, changes nothing.
    /// </summary>
    public void Refund(int amount, PurseSink sink)
    {
        if (amount <= 0 || amount > SpentOn(sink))
            return;

        Balance += amount;
        Spent -= amount;
        _spentOn[sink] = SpentOn(sink) - amount;
    }
}

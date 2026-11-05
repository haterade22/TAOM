using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

internal enum PurseSource
{
    Bounty,
    WaveBonus,
    EarlyStart,
    Boon,
}

/// <summary>The player's gold. The balance is always start plus earned minus spent, which the report checks.</summary>
internal sealed class Purse
{
    private readonly Dictionary<PurseSource, long> _earned = new();

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

    /// <summary>Adds gold; a zero or negative amount changes nothing.</summary>
    public void Earn(PurseSource source, int amount)
    {
        if (amount <= 0)
            return;

        Balance += amount;
        TotalEarned += amount;
        _earned[source] = EarnedFrom(source) + amount;
    }

    /// <summary>Takes the price if the balance covers it; refuses a price that is not positive.</summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0 || amount > Balance)
            return false;

        Balance -= amount;
        Spent += amount;
        return true;
    }
}

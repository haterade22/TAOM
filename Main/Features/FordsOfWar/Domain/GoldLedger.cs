using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Gold per commander slot. Every slot starts with the starting purse and never holds more than MaxGold.</summary>
internal sealed class GoldLedger
{
    private readonly FordsEconomyConfig _economy;
    private readonly Dictionary<int, int> _balances = new();

    public GoldLedger(FordsEconomyConfig economy, IEnumerable<int> slots)
    {
        _economy = economy;
        foreach (var slot in slots)
            _balances[slot] = Math.Min(economy.StartingGold, economy.MaxGold);
    }

    /// <summary>The slot's gold; 0 for a slot the ledger does not know.</summary>
    public int Balance(int slot) => _balances.TryGetValue(slot, out var gold) ? gold : 0;

    /// <summary>Adds gold up to MaxGold and returns what was actually added.</summary>
    public int Earn(int slot, int amount)
    {
        if (amount <= 0 || !_balances.TryGetValue(slot, out var gold))
            return 0;

        var next = (int)Math.Min((long)gold + amount, _economy.MaxGold);
        _balances[slot] = next;
        return next - gold;
    }

    public bool TrySpend(int slot, int amount)
    {
        if (amount < 0 || !_balances.TryGetValue(slot, out var gold) || gold < amount)
            return false;

        _balances[slot] = gold - amount;
        return true;
    }
}

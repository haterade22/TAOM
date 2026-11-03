using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>What a commander's gold paid for, for the balance lines at the end of a match.</summary>
internal enum GoldUse
{
    Units,
    Creatures,
    Heroes,
    Upgrades,
    Regions,
}

/// <summary>Where a commander's gold came from, for the balance lines at the end of a match.</summary>
internal enum GoldSource
{
    Income,
    Bounty,
    Towers,
    Camps,
    Captures,
    Refund,
    Granted,
}

/// <summary>Gold per commander slot. Every slot starts with the starting purse and never holds more than MaxGold.</summary>
internal sealed class GoldLedger
{
    private readonly FordsEconomyConfig _economy;
    private readonly Dictionary<int, int> _balances = new();
    private readonly Dictionary<int, long> _earned = new();
    private readonly Dictionary<int, long> _spent = new();
    private readonly Dictionary<(int Slot, GoldSource Source), long> _earnedFrom = new();
    private readonly Dictionary<(int Slot, GoldUse Use), long> _spentOn = new();

    public GoldLedger(FordsEconomyConfig economy, IEnumerable<int> slots)
    {
        _economy = economy;
        foreach (var slot in slots)
            _balances[slot] = Math.Min(economy.StartingGold, economy.MaxGold);
    }

    /// <summary>The slot's gold; 0 for a slot the ledger does not know.</summary>
    public int Balance(int slot) => _balances.TryGetValue(slot, out var gold) ? gold : 0;

    /// <summary>All the gold the slot gained since the start, without its starting purse; what MaxGold cut off is not in it.</summary>
    public long Earned(int slot) => _earned.TryGetValue(slot, out var gold) ? gold : 0;

    public long Spent(int slot) => _spent.TryGetValue(slot, out var gold) ? gold : 0;

    /// <summary>The slot's gold by source and by use, as <c>name=gold</c> words for the log; a kind with no gold still shows, so every match reads the same.</summary>
    public string Breakdown(int slot)
    {
        var sources = ((GoldSource[])Enum.GetValues(typeof(GoldSource))).Select(s => Word(s.ToString(), _earnedFrom.TryGetValue((slot, s), out var gold) ? gold : 0));
        var uses = ((GoldUse[])Enum.GetValues(typeof(GoldUse))).Select(u => Word(u.ToString(), _spentOn.TryGetValue((slot, u), out var gold) ? gold : 0));
        return string.Join(" ", sources.Concat(uses));
    }

    private static string Word(string name, long gold) => name.ToLowerInvariant() + "=" + gold;

    /// <summary>Adds gold up to MaxGold and returns what was actually added.</summary>
    public int Earn(int slot, int amount, GoldSource source = GoldSource.Granted)
    {
        if (amount <= 0 || !_balances.TryGetValue(slot, out var gold))
            return 0;

        var next = (int)Math.Min((long)gold + amount, _economy.MaxGold);
        _balances[slot] = next;
        _earned[slot] = Earned(slot) + (next - gold);
        _earnedFrom[(slot, source)] = (_earnedFrom.TryGetValue((slot, source), out var fromSource) ? fromSource : 0) + (next - gold);
        return next - gold;
    }

    public bool TrySpend(int slot, int amount, GoldUse use = GoldUse.Units)
    {
        if (amount < 0 || !_balances.TryGetValue(slot, out var gold) || gold < amount)
            return false;

        _balances[slot] = gold - amount;
        _spent[slot] = Spent(slot) + amount;
        _spentOn[(slot, use)] = (_spentOn.TryGetValue((slot, use), out var onUse) ? onUse : 0) + amount;
        return true;
    }
}

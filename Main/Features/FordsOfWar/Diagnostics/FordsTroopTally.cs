using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// What each troop killed and lost in one match, in units and in gold value, so a balance pass can see which troop earned its price. Printed to the log when the match ends.
/// A kill with no known killer (a tower volley, a mount with no rider) goes to the unknown row.
/// </summary>
internal sealed class FordsTroopTally
{
    private const string Unknown = "(unknown)";

    private sealed class Row
    {
        public int Kills;
        public int Deaths;
        public long ValueKilled;
        public long ValueLost;
    }

    private readonly Dictionary<string, Row> _rows = new(StringComparer.Ordinal);

    public void Death(string victimTroop, string? killerTroop, int value)
    {
        var victim = RowOf(victimTroop);
        victim.Deaths++;
        victim.ValueLost += value;
        var killer = RowOf(killerTroop ?? Unknown);
        killer.Kills++;
        killer.ValueKilled += value;
    }

    public IEnumerable<string> Lines() =>
        _rows.OrderByDescending(r => r.Value.ValueKilled).ThenBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => $"troop={r.Key} kills={r.Value.Kills} deaths={r.Value.Deaths} valueKilled={r.Value.ValueKilled} valueLost={r.Value.ValueLost}");

    private Row RowOf(string troop)
    {
        if (!_rows.TryGetValue(troop, out var row))
            _rows[troop] = row = new Row();
        return row;
    }
}

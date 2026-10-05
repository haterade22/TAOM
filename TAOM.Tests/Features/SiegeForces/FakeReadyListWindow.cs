using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// An <see cref="IReadyListWindow"/> over a plain list. It models what the engine's list does when an entry goes: the
/// entries after it shift down, so a caller that removes in ascending order removes the wrong ones, and it records every
/// removal in call order. <see cref="Side"/> is the engine's <c>BattleSideEnum</c> int: 0 defender, 1 attacker.
/// </summary>
internal sealed class FakeReadyListWindow : IReadyListWindow
{
    public const int Defender = 0;
    public const int Attacker = 1;

    private readonly List<(string? Id, bool IsPlayer)> _entries;
    private readonly int _otherEntries;
    private int _removals;

    public FakeReadyListWindow(string partyId, int side, IEnumerable<(string? Id, bool IsPlayer)> entries, int otherEntries = 0)
    {
        PartyId = partyId;
        Side = side;
        _entries = entries.ToList();
        _otherEntries = otherEntries;
    }

    /// <summary>A window of <paramref name="count"/> entries of one character, then the other stacks.</summary>
    public static FakeReadyListWindow Of(string partyId, int side, int otherEntries = 0, params (string Id, int Count)[] stacks) =>
        new(partyId, side,
            stacks.SelectMany(s => Enumerable.Range(0, s.Count).Select(_ => ((string?)s.Id, false))),
            otherEntries);

    public string PartyId { get; }

    public int Side { get; }

    public int Count => _entries.Count;

    public int ListCount => _otherEntries + _entries.Count;

    /// <summary>The indices passed to <see cref="RemoveAt"/>, in call order.</summary>
    public List<int> RemovedIndices { get; } = new();

    /// <summary>The 1-based removal that throws instead of removing, or 0 for none.</summary>
    public int ThrowOnRemoval { get; set; }

    public IEnumerable<string?> RemainingIds => _entries.Select(e => e.Id);

    public string? CharacterIdAt(int index) => _entries[index].Id;

    public bool IsPlayerCharacterAt(int index) => _entries[index].IsPlayer;

    public void RemoveAt(int index)
    {
        _removals++;
        RemovedIndices.Add(index);
        if (ThrowOnRemoval == _removals)
            throw new InvalidOperationException("the engine's list refused a removal");
        _entries.RemoveAt(index);
    }
}

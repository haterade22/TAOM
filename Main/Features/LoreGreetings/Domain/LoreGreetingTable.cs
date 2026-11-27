using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LoreGreetings.Domain;

/// <summary>One person of the books and every character id that is him or her (a story hero, a named companion, a lord).</summary>
internal sealed record LorePerson(string Id, bool IsFemale, IReadOnlyList<string> Characters);

/// <summary>What one person says to another: at peace, when hostile, and on the first meeting at peace. A null line means none for that case.</summary>
internal sealed record LoreGreetingLines(string? Text, string? Hostile, string? First);

/// <summary>One directed pair of the table, for the shipped-data tests.</summary>
internal sealed record LoreGreetingPair(string Speaker, string Listener, LoreGreetingLines Lines);

/// <summary>
/// The validated persons and pair lines of <c>lore_greetings.json</c>. A character id belongs to one person; a pair is keyed by the
/// speaker's person id and the listener's, one way only.
/// </summary>
internal sealed class LoreGreetingTable
{
    public static readonly LoreGreetingTable Empty =
        new(Array.Empty<LorePerson>(), new Dictionary<(string, string), LoreGreetingLines>());

    private readonly Dictionary<string, LorePerson> _byCharacter = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Speaker, string Listener), LoreGreetingLines> _lines;

    public LoreGreetingTable(IEnumerable<LorePerson> persons, IReadOnlyDictionary<(string Speaker, string Listener), LoreGreetingLines> lines)
    {
        Persons = persons.ToList();
        foreach (var person in Persons)
            foreach (var character in person.Characters)
                if (!_byCharacter.ContainsKey(character))
                    _byCharacter[character] = person;
        _lines = lines.ToDictionary(p => p.Key, p => p.Value);
    }

    public IReadOnlyList<LorePerson> Persons { get; }

    public int PairCount => _lines.Count;

    public IEnumerable<LoreGreetingPair> Pairs => _lines.Select(p => new LoreGreetingPair(p.Key.Speaker, p.Key.Listener, p.Value));

    /// <summary>The person the first of these ids belongs to (a hero id, then its character ids), or null.</summary>
    public LorePerson? PersonOf(IEnumerable<string> ids)
    {
        foreach (var id in ids)
            if (id != null && _byCharacter.TryGetValue(id, out var person))
                return person;
        return null;
    }

    public LoreGreetingLines? LinesFor(string speaker, string listener) =>
        _lines.TryGetValue((speaker, listener), out var lines) ? lines : null;
}

using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Domain;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// An <see cref="IRaceManager"/> that models the real <c>RaceManager</c>'s lookups, fallbacks included:
/// <see cref="GetRaceNameFromId"/> answers "human" for an id it does not know and
/// <see cref="GetRaceIdFromName"/> answers 0 for a name it does not know (Main/Core/Domain/RaceManager.cs).
/// A test that fed these through a bare substitute would have to remember to stub both fallbacks; a fake
/// keeps them honest, so validate-before-lookup is tested against the engine's real trap. It counts every
/// lookup so a test can assert a lookup was never made.
/// </summary>
internal sealed class FakeRaceManager : IRaceManager
{
    public const int Human = 0;
    public const int CaveTroll = 1;
    public const int HillTroll = 2;
    public const int Warg = 3;

    private readonly string[] _names;

    public FakeRaceManager(params string[] orderedRaceNames) => _names = orderedRaceNames;

    /// <summary>human, cave_troll, hill_troll, warg: the four races a siege test needs.</summary>
    public static FakeRaceManager WithTrolls() => new("human", "cave_troll", "hill_troll", "warg");

    /// <summary>Only human: a module set with no troll race registered.</summary>
    public static FakeRaceManager HumansOnly() => new("human");

    public int IdLookups { get; private set; }

    public int NameLookups { get; private set; }

    public List<string> NamesAskedForAnId { get; } = new();

    public List<int> IdsAskedForAName { get; } = new();

    public List<int> GetAllRaceIds() => Enumerable.Range(0, _names.Length).ToList();

    public List<string> GetAllRaceNames() => _names.ToList();

    public IReadOnlyList<string> GetOrderedRaceNames() => _names;

    public bool IsValidRaceName(string name) =>
        !string.IsNullOrEmpty(name) && _names.Contains(name, StringComparer.OrdinalIgnoreCase);

    public bool IsValidRaceId(int id) => id >= 0 && id < _names.Length;

    public int GetRaceIdFromName(string name)
    {
        IdLookups++;
        NamesAskedForAnId.Add(name);
        var index = Array.FindIndex(_names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 0 : index;
    }

    public string GetRaceNameFromId(int id)
    {
        NameLookups++;
        IdsAskedForAName.Add(id);
        return IsValidRaceId(id) ? _names[id] : "human";
    }
}

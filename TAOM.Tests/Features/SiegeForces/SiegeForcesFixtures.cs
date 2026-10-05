using System.Collections.Generic;
using System.Linq;
using TAOM.Features.SiegeForces.Domain;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// Builders for the siege troop picker's snapshot, so a test states only the fields it is about. A troop's
/// <c>Source</c> token defaults to its own id: the real token is an engine CharacterObject the rules never open.
/// </summary>
internal static class SiegeForcesFixtures
{
    public const string Uruk = "uruk_hai";
    public const string HillTrollTroop = "hill_troll";
    public const string CaveTrollTroop = "cave_troll";
    public const string Player = "main_hero";

    public static SiegeTroop Troop(string id, int number = 1, int wounded = 0, int race = FakeRaceManager.Human,
        bool isPlayer = false, object? source = null) =>
        new(source ?? id, id, isPlayer, race, number, wounded);

    public static SiegeParty Party(string id, IEnumerable<SiegeTroop> troops, bool main = false, bool inArmy = false,
        bool garrison = false) =>
        new(id, main, inArmy, garrison, troops.ToList());

    public static SiegeForcesSnapshot Snapshot(IEnumerable<SiegeParty> parties, bool playerIsAttacker = true,
        bool leadsArmy = false, bool ownFief = false, object? token = null) =>
        new(token ?? new object(), playerIsAttacker, leadsArmy, ownFief, parties.ToList());

    /// <summary>The player and a troll-race hero-less stack: the main party of the worked example.</summary>
    public static SiegeParty MainParty() =>
        Party("main",
            new[]
            {
                Troop(Player, 1, isPlayer: true),
                Troop(Uruk, 10),
                Troop(HillTrollTroop, 2, race: FakeRaceManager.HillTroll),
            },
            main: true, inArmy: true);

    /// <summary>A vassal's party in the player's army: 20 Uruk-hai and 3 cave trolls.</summary>
    public static SiegeParty VassalA() =>
        Party("vassalA",
            new[]
            {
                Troop(Uruk, 20),
                Troop(CaveTrollTroop, 3, race: FakeRaceManager.CaveTroll),
            },
            inArmy: true);

    /// <summary>The worked example: the player leads an army with one vassal attached.</summary>
    public static SiegeForcesSnapshot WorkedExample(object? token = null) =>
        Snapshot(new[] { MainParty(), VassalA() }, playerIsAttacker: true, leadsArmy: true, token: token);

    /// <summary>The selection the worked example's player makes: 25 Uruk-hai, one hill troll, no cave troll.</summary>
    public static Dictionary<string, int> WorkedSelection() => new()
    {
        [Player] = 1,
        [Uruk] = 25,
        [HillTrollTroop] = 1,
        [CaveTrollTroop] = 0,
    };

    /// <summary>The selection that leaves nobody out: every healthy troop of the worked example.</summary>
    public static Dictionary<string, int> EveryoneSelected() => new()
    {
        [Player] = 1,
        [Uruk] = 30,
        [HillTrollTroop] = 2,
        [CaveTrollTroop] = 3,
    };
}

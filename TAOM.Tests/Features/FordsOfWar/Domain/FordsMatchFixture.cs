using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// Builds small, exact matches for the aggregate and AI tests: the two-lane test map, gold 1000 each, no passive income
/// and no free waves unless a test turns them on, and no countdown. Slot 1 is West (human), slot 2 is East (AI "hard").
/// </summary>
internal static class FordsMatchFixture
{
    public const int WestSlot = 1;
    public const int EastSlot = 2;

    public static readonly UnitOffer Footmen = new("inf", "footman", UnitRole.Infantry, 10, 100);
    public static readonly UnitOffer Spears = new("spear", "spearman", UnitRole.Spear, 8, 120);
    public static readonly UnitOffer Archers = new("archer", "archer", UnitRole.Ranged, 10, 150);
    public static readonly UnitOffer Troll = new("troll", "cave_troll", UnitRole.Siege, 2, 400);
    public static readonly UnitOffer Elite = new("elite", "elite_guard", UnitRole.Infantry, 5, 1500);

    public static readonly HeroOffer HeroA = new("hero_a", 700);
    public static readonly HeroOffer HeroB = new("hero_b", 400);

    public static FordsMatchConfig Config(System.Func<FordsMatchConfig, FordsMatchConfig>? tweak = null)
    {
        var d = FordsMatchConfig.Default;
        var config = d with
        {
            CountdownSeconds = 0f,
            Economy = d.Economy with { StartingGold = 1000, IncomePerMinute = 0 },
            Waves = d.Waves with { FreeWaveIntervalSeconds = 0f, MaxSpawnsPerTick = 1000 },
            AiProfiles = new Dictionary<string, FordsAiProfile>
            {
                ["hard"] = new("hard", 5f, 1.2f, 0f, 400),
                ["easy"] = new("easy", 12f, 0.8f, 0.25f, 200),
                ["slow"] = new("slow", 1_000_000f, 1.5f, 0f, 300),
                ["fumble"] = new("fumble", 5f, 1f, 1f, 300),
            },
        };
        return tweak == null ? config : tweak(config);
    }

    public static FordsMatchConfig WithFreeWaves(FordsMatchConfig c) =>
        c with { Waves = c.Waves with { FreeWaveIntervalSeconds = 40f } };

    public static MapLayout Layout() =>
        FordsTestLayout.Create() with { CapturePoints = new List<CapturePointSlot> { new("point_0", new Vec3f(500f, 25f, 0f)) } };

    public static IReadOnlyDictionary<MatchSide, FactionCatalog> Factions()
    {
        var units = new List<UnitOffer> { Footmen, Spears, Archers, Troll, Elite };
        var heroes = new List<HeroOffer> { HeroA, HeroB };
        var waves = new List<FreeWaveTier> { new(0f, new List<UnitCount> { new("footman", 5) }) };
        return new Dictionary<MatchSide, FactionCatalog>
        {
            [MatchSide.West] = new("gondor", "gondor", units, heroes, waves),
            [MatchSide.East] = new("mordor", "mordor", units, heroes, waves),
        };
    }

    public static List<CommanderSlot> Slots(string? eastProfile = "hard") => new()
    {
        new CommanderSlot(WestSlot, MatchSide.West, true, null, new[] { 0, 1 }),
        new CommanderSlot(EastSlot, MatchSide.East, eastProfile == null, eastProfile, new[] { 0, 1 }),
    };

    public static FordsMatch Create(FordsMatchConfig? config = null, IReadOnlyList<CommanderSlot>? slots = null, int seed = 7) =>
        new(config ?? Config(), Layout(), Factions(), slots ?? Slots(), new SeededFordsRandom(seed));

    public static AgentKey Key(int n) => new(n);

    public static UnitRecord Unit(int key, MatchSide side, int owner, int lane = 0, int value = 100,
        UnitRole role = UnitRole.Infantry, string? heroId = null) =>
        new(Key(key), side, lane, owner, value, role, heroId);

    public static MatchSnapshot Snap(float dt = 1f, IEnumerable<LaneSnapshot>? lanes = null,
        IEnumerable<TowerTargets>? targets = null, IEnumerable<CaptureCounts>? capture = null) =>
        new()
        {
            Dt = dt,
            Lanes = (lanes ?? Enumerable.Empty<LaneSnapshot>()).ToList(),
            TowerTargets = (targets ?? Enumerable.Empty<TowerTargets>()).ToList(),
            Capture = (capture ?? Enumerable.Empty<CaptureCounts>()).ToList(),
        };

    public static LaneSnapshot Live(MatchSide side, int lane, int units, Vec3f? center = null, float nearestEnemy = float.MaxValue) =>
        new(side, lane, units, center ?? new Vec3f(0f, lane * 50f, 0f), nearestEnemy);

    public static TowerTargets Threat(string towerId, int enemies, float distance = 30f) =>
        new(towerId, Enumerable.Range(0, enemies).Select(i => new TargetCandidate(Key(1000 + i), distance, false)).ToList());

    public static MatchView.TowerView Tower(MatchView view, string id) =>
        view.Lanes.SelectMany(l => l.Towers).Concat(view.Keeps).Single(t => t.Id == id);
}

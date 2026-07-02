using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.TroopProgression;

public partial class VolunteerRecruitmentService : IVolunteerRecruitmentService
{
    private readonly IRandomProvider _random;
    private readonly IModLogger _logger;

    private static readonly Dictionary<string, List<VolunteerChance>> SettlementMap = new();
    private static readonly Dictionary<string, List<VolunteerChance>> ClanMap = new();
    private static readonly Dictionary<string, List<VolunteerChance>> CultureMap = new();
    // Conditional pools: checked BEFORE the regular SettlementMap. Predicate receives the live VolunteerContext.
    // Used for state-sensitive pools like Ithil Guard at town_ES2 (only when Gondor-owned).
    private static readonly Dictionary<string, (Func<VolunteerContext, bool> Condition, List<VolunteerChance> Pool)> ConditionalSettlementMap = new();
    // Idempotent guard for instance-side JSON loading. Hand-written Gondor pools (InitializeGondorSettlements + InitializeGondorClans)
    // stay as a safety net; JSON entries OVERWRITE matching settlement keys when present (in-game). Tests where the JSON file
    // is missing fall back to hand-written behaviour automatically.
    private static int _gondorJsonLoadAttempted;

    static VolunteerRecruitmentService()
    {
        InitializeGondorSettlements();
        InitializeGondorClans();
        InitializeGondorCulture();
        InitializeDolGuldurSettlements();
        InitializeDolGuldurClans();
        InitializeDolGuldurCulture();
        InitializeEreborSettlements();
        InitializeEreborClans();
        InitializeEreborCulture();
        InitializeLothlorienSettlements();
        InitializeLothlorienClans();
        InitializeLothlorienCulture();
        InitializeShaghanaClans();
        InitializeShaghânaCulture();
        InitializeAbanissaClans();
        InitializeAbanissaCulture();
        InitializeRhunSettlements();
        InitializeRhunCulture();
        InitializeGundabadCulture();
        InitializeGoblinCulture();
        InitializeMistyMountainOrcsCulture();
        InitializeRivendellCulture();
        InitializeMordorSettlements();
        InitializeMordorCulture();
        // (No InitializeMordorClans — user explicitly chose to skip clan pools.)
        InitializeDaleCulture();
        InitializeDaleSettlements();
        InitializeRohanClans();
        InitializeRohanCulture();
        InitializeHaradCulture();
        InitializeHaradClans();
        InitializeIsengardCulture();
        InitializeDunlandCulture();
        InitializeDunlandClans();
        InitializeMirkwoodCulture();
        InitializeUmbarCulture();
    }

    public VolunteerRecruitmentService(IRandomProvider random, IModLogger logger)
    {
        _random = random;
        _logger = logger;
        EnsureGondorJsonLoaded();
    }

    // Loads the Gondor recruitment-pools JSON once per process. Idempotent. Failures are logged and swallowed —
    // the hand-written InitializeGondorSettlements/InitializeGondorClans remain as the safety net.
    private void EnsureGondorJsonLoaded()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _gondorJsonLoadAttempted, 1, 0) != 0)
            return;
        try
        {
            GondorRecruitmentJsonLoader.Load(AddSettlement, AddSettlementConditional, _logger);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"VolunteerRecruitmentService: Gondor JSON loader threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    public string GetVolunteerTroopId(VolunteerContext context)
    {
        List<VolunteerChance> pool;
        if (context.IsConvertedSettlement && context.SettlementCultureId != null)
        {
            // CultureConversion: a converted fief recruits the NEW culture's troops, bypassing the
            // per-settlement/clan pools (which hold the ORIGINAL culture's regional troops). EXCEPTION:
            // conditional pools (e.g. Ithil Guard at town_ES2) gate on the LIVE owner culture, not the
            // settlement's original culture, so they remain valid after conversion and must outrank the
            // generic converted-culture fallback — else converting Minas Morgul to gondor silently drops
            // the Ithil Guard pool for generic Anorien recruits. Conversion is gated on HasCulturePool,
            // so the CultureMap lookup normally hits; the trailing cascade is a safety net.
            pool = ResolveConditionalPool(context.SettlementId, context)
                ?? ResolveConditionalPool(context.BoundSettlementId, context)
                ?? ResolvePool(context.SettlementCultureId, CultureMap)
                ?? ResolveStandardCascade(context);
        }
        else
        {
            pool = ResolveStandardCascade(context);
        }

        if (pool == null || pool.Count == 0)
            return null;

        return PickWeighted(pool);
    }

    // Standard (non-converted) resolution cascade: conditional → per-settlement → per-clan → culture fallback.
    private static List<VolunteerChance> ResolveStandardCascade(VolunteerContext context)
    {
        return ResolveConditionalPool(context.SettlementId, context)
            ?? ResolveConditionalPool(context.BoundSettlementId, context)
            ?? ResolvePool(context.SettlementId, SettlementMap)
            ?? ResolvePool(context.BoundSettlementId, SettlementMap)
            ?? ResolvePool(context.OwnerClanId, ClanMap)
            ?? ResolvePool(context.CultureId, CultureMap);
    }

    public bool HasCulturePool(string cultureId)
        => !string.IsNullOrEmpty(cultureId) && CultureMap.ContainsKey(cultureId);

    private static List<VolunteerChance> ResolveConditionalPool(string key, VolunteerContext context)
    {
        if (key == null) return null;
        if (!ConditionalSettlementMap.TryGetValue(key, out var entry)) return null;
        return entry.Condition(context) ? entry.Pool : null;
    }

    private static List<VolunteerChance> ResolvePool(string key, Dictionary<string, List<VolunteerChance>> map)
    {
        if (key != null && map.TryGetValue(key, out var pool))
            return pool;
        return null;
    }

    private string PickWeighted(List<VolunteerChance> pool)
    {
        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++)
            totalWeight += pool[i].Weight;

        int roll = _random.Next(totalWeight);

        int cumulative = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].Weight;
            if (roll < cumulative)
                return pool[i].CharacterId;
        }

        return pool[pool.Count - 1].CharacterId;
    }

    // Settlement pool (total 18): goblins common, orcs the mid-line meat, a uruk entry, khamul shadow
    // elite, a rare ranged-orc entry, and the spider RIDER at the rare tail (a goblin cavalry troop
    // whose Horse slot carries the Giant Spider mount — vanilla cavalry spawn, no patch).
    private static readonly (string, int)[] DolGuldurSettlementPool =
    {
        ("dg_goblin_slave",           7),
        ("dg_orc_recruit",            4),  // orc line + warg line entry
        ("dg_uruk_foul",              2),  // uruk line entry
        ("dg_khamul_shadow_initiate", 3),
        ("dg_orc_scout",              1),  // ranged-orc line entry (dg_orc_scout -> dg_orc_archer)
        ("taom_spider_creature",      1),  // Giant Spider rider — rare settlement-path tail (keeps pool total 18).
    };

    private static readonly (string, int)[] DolGuldurClanPool =
    {
        ("dg_goblin_slave",           7),
        ("dg_orc_recruit",            4),  // orc line + warg line entry
        ("dg_uruk_foul",              2),  // uruk line entry
        ("dg_khamul_shadow_initiate", 3),
        ("dg_orc_scout",              1),  // ranged-orc line entry
    };

    // --- Erebor Settlement Mappings ---

    // Erebor recruitment is a mix of Erebor + Iron Hills troops (same blend as the culture pool):
    // Erebor-leaning 8:4 weight (miner 5 / noble 3 / Iron Hills recruit 2 / Iron Hills noble 2).
    // Applied to BOTH settlements and clans — settlement pools are checked first, so the mix must
    // live there too or it would never surface in the mapped Erebor towns/castles.
    // Total 15. The two trailing entries are reachability fixes (appended last so the existing
    // cumulative ranges of the first four are unchanged): the Iron Pass line (ironpass_recruit ->
    // warrior -> infantry -> axeman -> ... + the ironpass_arbalest ranged sub-line) and the
    // Erebor Oathsworn elite line (erebor_oathsworn -> legionary -> royal_legionary) were fielded
    // by AI lords but orphaned from every pool. Iron Pass recruit at modest weight (2); the L36
    // Oathsworn elite as a rare line-entry (1).
    private static readonly (string, int)[] EreborMix =
    {
        ("erebor_reg_miner",       5),
        ("erebor_noble",           3),
        ("iron_hills_reg_recruit", 2),
        ("iron_hills_noble",       2),
        ("ironpass_recruit",       2),  // Iron Pass line entry
        ("erebor_oathsworn",       1),  // Oathsworn elite line entry
    };

    // --- Helpers ---

    private static void AddSettlement(string settlementId, params (string troopId, int weight)[] entries)
        => SettlementMap[settlementId] = BuildPool(settlementId, entries);

    private static void AddClan(string clanId, params (string troopId, int weight)[] entries)
        => ClanMap[clanId] = BuildPool(clanId, entries);

    // internal for test reach — see VolunteerRecruitmentServiceTests
    internal static void AddSettlementConditional(
        string settlementId,
        Func<VolunteerContext, bool> condition,
        params (string troopId, int weight)[] entries)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition), $"Conditional pool for '{settlementId}' needs a predicate.");
        ConditionalSettlementMap[settlementId] = (condition, BuildPool(settlementId, entries));
    }

    // Internal accessor for tests — clears the conditional map between test scenarios that re-seed it.
    internal static bool TryRemoveConditionalSettlement(string settlementId)
        => ConditionalSettlementMap.Remove(settlementId);

    // Internal accessor for tests — every troop id the service can offer across ALL pools
    // (settlement + clan + culture + conditional). The reachability guard test
    // (VolunteerRecruitmentServiceTests) flood-fills the troop-XML upgrade graph from these roots
    // and asserts every non-militia / non-boss troop is reachable, so a future orphaned line fails
    // the build instead of silently becoming unrecruitable.
    internal static IEnumerable<string> AllPooledTroopIds()
    {
        var ids = new HashSet<string>();
        foreach (var pool in SettlementMap.Values)
            foreach (var c in pool) ids.Add(c.CharacterId);
        foreach (var pool in ClanMap.Values)
            foreach (var c in pool) ids.Add(c.CharacterId);
        foreach (var pool in CultureMap.Values)
            foreach (var c in pool) ids.Add(c.CharacterId);
        foreach (var entry in ConditionalSettlementMap.Values)
            foreach (var c in entry.Pool) ids.Add(c.CharacterId);
        return ids;
    }

    internal static List<VolunteerChance> BuildPool(string ownerId, (string troopId, int weight)[] entries)
    {
        if (entries == null || entries.Length == 0)
            throw new ArgumentException($"Volunteer pool for '{ownerId}' is empty.", nameof(entries));

        var pool = new List<VolunteerChance>(entries.Length);
        foreach (var (troopId, weight) in entries)
        {
            if (string.IsNullOrWhiteSpace(troopId))
                throw new ArgumentException($"Volunteer pool for '{ownerId}' contains a blank troop id.", nameof(entries));
            if (weight <= 0)
                throw new ArgumentException($"Volunteer pool for '{ownerId}': '{troopId}' has weight {weight}; must be positive.", nameof(entries));
            pool.Add(new VolunteerChance(troopId, weight));
        }
        return pool;
    }
}

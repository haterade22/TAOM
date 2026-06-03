using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM.Features.TroopProgression;

public class VolunteerRecruitmentService : IVolunteerRecruitmentService
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
        InitializeIsengardCulture();
        InitializeDunlandCulture();
        InitializeDunlandClans();
    }

    // --- Rohan Culture Fallback (Culture.vlandia) ---
    // Rohan previously had ONLY clan pools (InitializeRohanClans) and no culture-level fallback, so
    // HasCulturePool("vlandia") was false. That made Rohan an invalid CultureConversion target — a Rohan
    // clan conquering a foreign fief never triggered conversion (Codex adversarial review, 2026-06-02).
    // Mirror the clan pool: all 7 Rohan basic recruits at equal weight 1.
    private static void InitializeRohanCulture()
    {
        CultureMap["vlandia"] = new List<VolunteerChance>
        {
            new VolunteerChance("rohan_wold_recruit",        1),
            new VolunteerChance("rohan_westemnet_recruit",   1),
            new VolunteerChance("rohan_eastemnet_recruit",   1),
            new VolunteerChance("rohan_eastfold_recruit",    1),
            new VolunteerChance("rohan_westfold_recruit",    1),
            new VolunteerChance("rohan_westmarches_recruit", 1),
            new VolunteerChance("rohan_edoras_recruit",      1),
        };
    }

    // --- Harad Culture Fallback (Culture.aserai — the Harad kingdom) ---
    // The Harad kingdom uses engine culture id "aserai" (cheatsheet). It had no CultureMap pool — only the
    // Shaghâna/Âbanissa sub-factions did — so HasCulturePool("aserai") was false and Harad conquests never
    // converted (Codex review, 2026-06-02). Reuse the same harad_levy/harad_noble pool as those sub-factions.
    private static void InitializeHaradCulture()
    {
        CultureMap["aserai"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy",  7),
            new VolunteerChance("harad_noble", 3),
        };
    }

    // --- Dunland (Culture.empire) Culture Fallback ---
    // "Culture will be them all" — every Dunland is_basic_troop=true troop at equal weight 1:
    // the generic Peasant plus the three totem noble sons (Wolf/Blaidd, Boar/Turch, Raven/Cigfran).
    // SettlementMap is intentionally left empty for Dunland (per user spec).
    private static void InitializeDunlandCulture()
    {
        CultureMap["empire"] = new List<VolunteerChance>
        {
            new VolunteerChance("dunland_peasant",          1),
            new VolunteerChance("dunland_noble_son",        1),  // Blaidd-luth (Wolf)
            new VolunteerChance("dunland_boar_noble_son",   1),  // Turch-luth (Boar)
            new VolunteerChance("dunland_raven_noble_son",  1),  // Cigfran-luth (Raven)
        };
    }

    // --- Dunland Clan Pools ---
    // "Clan will be specific troops by name." The three totem clans with a matching noble son
    // recruit the Peasant + their own noble son. The six totem clans WITHOUT a named noble son
    // (Uch/Ox, Arth/Bear, Hebog/Hawk, Draig/Dragon, Caru/Stag, Avanc) get the full roster
    // (Peasant + all three noble sons) so they still field a complete clan army.
    private static void InitializeDunlandClans()
    {
        // Totem clans with a signature noble son:
        AddClan("clan_empire_north_1",  // Blaidd-luth (Wolf)
            ("dunland_peasant", 1), ("dunland_noble_son", 1));
        AddClan("clan_empire_north_2",  // Turch-luth (Boar)
            ("dunland_peasant", 1), ("dunland_boar_noble_son", 1));
        AddClan("clan_empire_north_5",  // Cigfran-luth (Raven)
            ("dunland_peasant", 1), ("dunland_raven_noble_son", 1));

        // Totem clans without a signature noble son — full roster:
        (string, int)[] allDunland =
        {
            ("dunland_peasant",         1),
            ("dunland_noble_son",       1),
            ("dunland_boar_noble_son",  1),
            ("dunland_raven_noble_son", 1),
        };
        AddClan("clan_empire_north_3", allDunland);  // Uch-luth (Ox)
        AddClan("clan_empire_north_4", allDunland);  // Arth-luth (Bear)
        AddClan("clan_empire_north_6", allDunland);  // Hebog-luth (Hawk)
        AddClan("clan_empire_north_7", allDunland);  // Draig-luth (Dragon)
        AddClan("clan_empire_north_8", allDunland);  // Caru-luth (Stag)
        AddClan("clan_empire_north_9", allDunland);  // Avanc-luth
    }

    // --- Isengard Culture Fallback ---
    // Isengard had no recruitment pool (hostile-faction only; GetVolunteerTroopId returned null).
    // Culture-level pool makes the player able to recruit Uruk-Hai. Weighted toward the L6 Recruit
    // root (4), with the crossbow-line entry (Skirmisher) + Warg-Rider cavalry at 2 each, and the
    // melee mid (Warrior) + bow-line entry (Scout) at 1 each. Total weight 10.
    private static void InitializeIsengardCulture()
    {
        CultureMap["isengard"] = new List<VolunteerChance>
        {
            new VolunteerChance("urukhai_recruit",    4),
            new VolunteerChance("urukhai_skirmisher", 2),  // crossbow-line entry
            new VolunteerChance("orc_warg_scout",     2),  // Warg-Rider cavalry
            new VolunteerChance("urukhai_warrior",    1),  // melee mid
            new VolunteerChance("urukhai_scout",      1),  // bow-line entry
        };
    }

    // --- Dale Settlement-Specific Pools ---
    // Only town_S1 (Lake-Town) gets a settlement-specific override per user spec. All
    // other Sturgia settlements fall through to the culture pool in InitializeDaleCulture.
    //
    // Lake-Town pool: heavy weight on the Lake-Town Peasant (9) — it's a fishing-folk
    // settlement, so common recruits are smallfolk. Dalian Levy (1) preserves a rare
    // route into the royal lines for players who specifically want to recruit there.
    private static void InitializeDaleSettlements()
    {
        AddSettlement("town_S1",
            ("dale_recruit", 9),  // Lake-Town Peasant
            ("dale_squire",  1)); // Dalian Levy
    }

    // --- Rohan Clan Pools ---
    // Every Rohan (Culture.vlandia) clan recruits all 7 Rohan basic troops (is_basic_troop=true)
    // at equal weight 1, so the player can recruit the full Rohan T2 lineup from any settlement
    // bound to a Rohan clan regardless of region (Wold, Westemnet, Eastemnet, Eastfold, Westfold,
    // Westmarches, Edoras). Per-clan flavor is intentionally flat — recruitment is uniform across
    // the kingdom so players aren't forced to chase specific clans for specific basic troops.
    //
    // ClanMap is checked AFTER SettlementMap and BEFORE CultureMap in the lookup order, so any
    // future per-settlement Rohan pool would still win. Without a per-settlement entry, the lookup
    // falls through to this clan-level pool.
    private static void InitializeRohanClans()
    {
        (string, int)[] basicTroops =
        {
            ("rohan_wold_recruit",        1),
            ("rohan_westemnet_recruit",   1),
            ("rohan_eastemnet_recruit",   1),
            ("rohan_eastfold_recruit",    1),
            ("rohan_westfold_recruit",    1),
            ("rohan_westmarches_recruit", 1),
            ("rohan_edoras_recruit",      1),
        };
        for (int i = 1; i <= 11; i++)
        {
            AddClan($"clan_vlandia_{i}", basicTroops);
        }
    }

    // --- Dale Culture Fallback ---
    // Dale (vanilla Culture.sturgia renamed to "Barding" via spcultures.xslt) recruits from
    // a single culture-level pool — no per-settlement / per-clan flavor variants in this
    // session. Sturgia kingdom settlements (Lake-town, Dale proper) all draw from this.
    //
    // Pool reflects the user-designed Dale tree: Dalian Levy (weight 4) is the most common
    // recruit and serves as the royal-line root. The 6 other slots (weight 1 each) cover
    // one representative entry troop for each branch + the Lake-Town levy entry. Total weight 10.
    //
    // Branch entry points:
    //   - Dalian Levy        (dale_squire)       → 4 royal branches (riverman/militia/yeoman/crossbow/merchant)
    //   - Dalian Riverman    (dale_riverman)     → Riverman spear+shield line
    //   - Dalian Militia     (dale_man_at_arms)  → Great Infantry line (NOT dale_militia / Lake-Town Militia)
    //   - Dalian Yeoman      (dale_bowman)       → Excellent Archers (bow) line
    //   - Dalian Crossbowman (dale_crossbowman)  → Royal Crossbow line
    //   - Dalian Merchant Guard (dale_outrider)  → Cavalry split (Light + Heavy)
    //   - Lake-Town Peasant  (dale_recruit)      → Lake-Town levy + Watch + Pikeman lines
    private static void InitializeDaleCulture()
    {
        CultureMap["sturgia"] = new List<VolunteerChance>
        {
            new VolunteerChance("dale_squire",       4),
            new VolunteerChance("dale_riverman",     1),
            new VolunteerChance("dale_man_at_arms",  1),
            new VolunteerChance("dale_bowman",       1),
            new VolunteerChance("dale_crossbowman",  1),
            new VolunteerChance("dale_outrider",     1),
            new VolunteerChance("dale_recruit",      1),
        };
    }

    // --- Gundabad Culture Fallback ---
    // Settlements/clans absent — Gundabad in TAOM is hostile faction; volunteer recruitment
    // falls back to culture pool. Per #212 KEYforce troop revamp (Pale Uruk T2–T8).
    private static void InitializeGundabadCulture()
    {
        CultureMap["gundabad"] = new List<VolunteerChance>
        {
            new VolunteerChance("gundabad_snaga", 7),
            new VolunteerChance("gundabad_grunt", 2),
            new VolunteerChance("gundabad_fighter", 1)
        };
    }

    private static void InitializeGoblinCulture()
    {
        // Goblins of the High Pass — swarm faction (mirrors the Gundabad hostile-faction pattern).
        CultureMap["goblin"] = new List<VolunteerChance>
        {
            new VolunteerChance("goblin_snaga", 7),
            new VolunteerChance("goblin_grunt", 2),
            new VolunteerChance("goblin_fighter", 1)
        };
    }

    private static void InitializeMistyMountainOrcsCulture()
    {
        // Orc-host of the Misty Mountains — culture-only pool (hostile faction).
        CultureMap["mistymountainorcs"] = new List<VolunteerChance>
        {
            new VolunteerChance("mistymountainorcs_snaga", 7),
            new VolunteerChance("mistymountainorcs_grunt", 2),
            new VolunteerChance("mistymountainorcs_fighter", 1)
        };
    }

    private static void InitializeRivendellCulture()
    {
        // Noldor pool shared by the Rivendell kingdom and the new Lindon kingdom (both
        // Culture.rivendell). CultureMap["rivendell"] was previously absent, so rivendell-culture
        // settlements with no settlement/clan pool returned null — this fills that gap.
        CultureMap["rivendell"] = new List<VolunteerChance>
        {
            new VolunteerChance("imladris_recruit", 5),
            new VolunteerChance("imladris_infantry", 3),
            new VolunteerChance("imladris_bowman", 2)
        };
    }

    // --- Mordor Settlement Mappings ---
    // Two distinct pools by settlement type:
    //   - Towns (3): canonical Mordor pool with Black Uruks as the rareish elite path (weight 3/10),
    //                orcs as the common path (weight 4 on orc_recruit), plus 3 specialist tier-2 entries.
    //   - Castles (8): town pool MINUS Black Uruks (per user spec: BU recruitable in towns only).
    //
    // town_ES2 (Pelgaur / Minas Morgul) has the existing AddSettlementConditional Ithil Guard rule
    // that fires BEFORE this SettlementMap lookup when Gondor owns the town. When Mordor owns
    // (default), the conditional predicate fails and resolution falls through to this town pool.

    private static void InitializeMordorSettlements()
    {
        (string, int)[] townPool =
        {
            ("mordor_uruk_grunt",   3),  // Black Uruk Grunt — line entry; rareish elite
            ("mordor_orc_recruit",  4),  // Orc Recruit — common baseline
            ("mordor_orc_impaler",  1),  // mid-tier orc polearm specialist
            ("mordor_orc_hunter",   1),  // mid-tier orc ranged
            ("mordor_warg_tamer",   1),  // warg cavalry line entry
        };
        AddSettlement("town_ES1", townPool);  // Danustica
        AddSettlement("town_ES2", townPool);  // Pelgaur — falls through Ithil Guard conditional when Mordor-owned
        AddSettlement("town_ES3", townPool);  // Tharbilid

        (string, int)[] castlePool =
        {
            ("mordor_orc_recruit",  4),
            ("mordor_orc_impaler",  1),
            ("mordor_orc_hunter",   1),
            ("mordor_warg_tamer",   1),
        };
        AddSettlement("castle_ES1", castlePool);  // The Morannon
        AddSettlement("castle_ES2", castlePool);  // Carach Angren
        AddSettlement("castle_ES3", castlePool);  // Cirith Ungol
        AddSettlement("castle_ES4", castlePool);  // Mornaur
        AddSettlement("castle_ES5", castlePool);  // Barad Nûrn
        AddSettlement("castle_ES6", castlePool);  // Cirith Nargil
        AddSettlement("castle_ES7", castlePool);  // Barad Wath
        AddSettlement("castle_ES8", castlePool);  // Lûglurag
    }

    // --- Mordor Culture Fallback ---
    // Matches the town pool (includes Black Uruks). Safety net for any Mordor settlement not
    // explicitly mapped above — none expected, but explicit > implicit per simplicity-criterion.md.
    private static void InitializeMordorCulture()
    {
        CultureMap["mordor"] = new List<VolunteerChance>
        {
            new VolunteerChance("mordor_uruk_grunt",   3),
            new VolunteerChance("mordor_orc_recruit",  4),
            new VolunteerChance("mordor_orc_impaler",  1),
            new VolunteerChance("mordor_orc_hunter",   1),
            new VolunteerChance("mordor_warg_tamer",   1),
        };
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
            // CultureConversion: a conquered fief that has fully converted recruits the NEW culture's
            // troops, bypassing the settlement/clan pools (which hold the ORIGINAL culture's regional
            // troops). SettlementCultureId == the converted Settlement.Culture; conversion is gated on
            // HasCulturePool, so this normally hits. If somehow absent, fall through to the standard
            // cascade rather than returning naked (no regression vs. pre-feature behavior).
            pool = ResolvePool(context.SettlementCultureId, CultureMap) ?? ResolveStandardCascade(context);
        }
        else
        {
            pool = ResolveStandardCascade(context);
        }

        if (pool == null || pool.Count == 0)
            return null;

        var troop = PickWeighted(pool);
        _logger.LogDebug($"Volunteer: settlement={context.SettlementId} clan={context.OwnerClanId} culture={context.CultureId} converted={context.IsConvertedSettlement} → {troop}");
        return troop;
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

    // --- Gondor Settlement Mappings ---

    private static void InitializeGondorSettlements()
    {
        // Towns
        AddSettlement("town_EW1",
            ("gondor_ano_peasant",       6),
            ("gondor_ithilien_ranger",   1),
            ("gondor_mt_fountain_guard", 1),
            ("gondor_mt_trainee",        2));
        AddSettlement("town_EW2",  ("gondor_osg_veteran", 6),     ("gondor_ano_peasant", 4));
        AddSettlement("town_EW3",  ("gondor_osg_veteran", 6),     ("gondor_ano_peasant", 4));
        AddSettlement("town_EW4",  ("gondor_pel_skirmisher", 7),  ("gondor_leb_militia", 3));
        AddSettlement("town_EW5",  ("gondor_da_noble", 7),        ("gondor_bel_recruit", 3));
        AddSettlement("town_EW6",  ("gondor_anf_levy", 7),        ("gondor_anf_guardsman", 3));
        AddSettlement("town_EW7",  ("gondor_leb_militia", 7),     ("gondor_lg_noble", 3));
        AddSettlement("town_EW8",  ("gondor_pg_volunteer", 7),    ("gondor_arn_noble", 3));
        AddSettlement("town_EW9",  ("gondor_cal_noble", 7),       ("gondor_lam_clansman", 3));
        AddSettlement("town_EW10", ("gondor_ser_noble", 7),       ("gondor_anf_levy", 3));
        AddSettlement("town_EW11", ("gondor_met_noble", 7),       ("gondor_ano_peasant", 3));

        // Castles
        AddSettlement("castle_EW1",  ("gondor_ano_peasant", 7),     ("gondor_mt_trainee", 3));
        AddSettlement("castle_EW2",  ("gondor_lam_clansman", 7),    ("gondor_ring_peasant", 3));
        AddSettlement("castle_EW3",  ("gondor_bel_recruit", 7),     ("gondor_da_noble", 3));
        AddSettlement("castle_EW4",  ("gondor_ca_noble", 7),        ("gondor_ano_peasant", 3));
        AddSettlement("castle_EW5",  ("gondor_ano_peasant", 8),     ("gondor_ano_peasant", 2));
        AddSettlement("castle_EW6",  ("gondor_har_conscript", 8),   ("gondor_har_conscript", 2));
        AddSettlement("castle_EW7",  ("gondor_anf_levy", 7),        ("gondor_ser_noble", 3));
        AddSettlement("castle_EW8",  ("gondor_pg_volunteer", 7),    ("gondor_arn_noble", 3));
        AddSettlement("castle_EW9",  ("gondor_tol_arbalest", 7),    ("gondor_bel_recruit", 3));
        AddSettlement("castle_EW10", ("gondor_har_conscript", 7),   ("gondor_met_noble", 3));
        AddSettlement("castle_EW11", ("gondor_bel_recruit", 7),     ("gondor_da_noble", 3));
        AddSettlement("castle_EW12", ("gondor_lin_noble", 7),       ("gondor_leb_militia", 3));
        AddSettlement("castle_EW13", ("gondor_anf_levy", 8),        ("gondor_anf_levy", 2));
        AddSettlement("castle_EW14", ("gondor_leb_militia", 7),     ("gondor_lin_noble", 3));
        AddSettlement("castle_EW15", ("gondor_har_conscript", 7),   ("gondor_met_noble", 3));
        AddSettlement("castle_EW16", ("gondor_har_conscript", 7),   ("gondor_met_noble", 3));
    }

    // --- Gondor Clan Mappings ---

    private static void InitializeGondorClans()
    {
        AddClan("clan_empire_west_1",
            ("gondor_ano_peasant",       6),
            ("gondor_ithilien_ranger",   1),
            ("gondor_mt_fountain_guard", 1),
            ("gondor_mt_trainee",        2));
        AddClan("clan_empire_west_2", ("gondor_bel_recruit", 7),    ("gondor_da_noble", 3));
        AddClan("clan_empire_west_3", ("gondor_leb_militia", 7),    ("gondor_pel_skirmisher", 3));
        AddClan("clan_empire_west_4", ("gondor_lam_clansman", 7),   ("gondor_cal_noble", 3));
        AddClan("clan_empire_west_5", ("gondor_loss_lumberman", 7), ("gondor_loss_axebearer", 3));
        AddClan("clan_empire_west_6", ("gondor_pg_volunteer", 8),   ("gondor_pg_volunteer", 2));
        AddClan("clan_empire_west_7", ("gondor_lam_clansman", 7),   ("gondor_cal_noble", 3));
        AddClan("clan_empire_west_8",
            ("gondor_anf_levy",      5),
            ("gondor_ser_pikeman",   2),
            ("gondor_ser_noble",     2),
            ("gondor_anf_guardsman", 1));
        AddClan("clan_empire_west_9",  ("gondor_brv_bowman", 7),  ("gondor_ano_peasant", 3));
        AddClan("clan_empire_west_10", ("gondor_har_conscript", 7), ("gondor_met_noble", 3));
        AddClan("clan_empire_west_11", ("gondor_ca_noble", 9),    ("gondor_ithilien_ranger", 1));
        AddClan("clan_empire_west_12", ("gondor_lin_noble", 7),   ("gondor_ano_peasant", 3));
        AddClan("clan_empire_west_13", ("gondor_tol_arbalest", 7), ("gondor_bel_recruit", 3));
        AddClan("clan_empire_west_14", ("gondor_anf_levy", 7),    ("gondor_anf_guardsman", 3));
    }

    // --- Gondor Culture Fallback ---

    private static void InitializeGondorCulture()
    {
        CultureMap["gondor"] = new List<VolunteerChance>
        {
            new VolunteerChance("gondor_ano_peasant", 7),
            new VolunteerChance("gondor_bel_recruit", 1),
            new VolunteerChance("gondor_lam_clansman", 1),
            new VolunteerChance("gondor_loss_lumberman", 1)
        };
    }

    // --- Dol Guldur Settlement Mappings ---

    private static void InitializeDolGuldurSettlements()
    {
        AddSettlement("town_DG1",   ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddSettlement("castle_DG1", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddSettlement("castle_DG2", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddSettlement("castle_DG3", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
    }

    // --- Dol Guldur Clan Mappings ---

    private static void InitializeDolGuldurClans()
    {
        AddClan("clan_dolguldur_1", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddClan("clan_dolguldur_2", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddClan("clan_dolguldur_3", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddClan("clan_dolguldur_4", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddClan("clan_dolguldur_5", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
        AddClan("clan_dolguldur_6", ("dg_goblin_slave", 7), ("dg_khamul_shadow_initiate", 3));
    }

    // --- Dol Guldur Culture Fallback ---

    private static void InitializeDolGuldurCulture()
    {
        CultureMap["dolguldur"] = new List<VolunteerChance>
        {
            new VolunteerChance("dg_goblin_slave", 5),
            new VolunteerChance("dg_uruk_warrior", 3),
            new VolunteerChance("dg_khamul_shadow_initiate", 2)
        };
    }

    // --- Erebor Settlement Mappings ---

    // Erebor recruitment is a mix of Erebor + Iron Hills troops (same blend as the culture pool):
    // Erebor-leaning 8:4 weight (miner 5 / noble 3 / Iron Hills recruit 2 / Iron Hills noble 2).
    // Applied to BOTH settlements and clans — settlement pools are checked first, so the mix must
    // live there too or it would never surface in the mapped Erebor towns/castles.
    private static readonly (string, int)[] EreborMix =
    {
        ("erebor_reg_miner",       5),
        ("erebor_noble",           3),
        ("iron_hills_reg_recruit", 2),
        ("iron_hills_noble",       2),
    };

    private static void InitializeEreborSettlements()
    {
        // Towns
        AddSettlement("town_E1", EreborMix);
        AddSettlement("town_E2", EreborMix);
        AddSettlement("town_E3", EreborMix);
        AddSettlement("town_E4", EreborMix);

        // Castles
        AddSettlement("castle_E1", EreborMix);
        AddSettlement("castle_E2", EreborMix);
        AddSettlement("castle_E3", EreborMix);
        AddSettlement("castle_E4", EreborMix);
        AddSettlement("castle_E5", EreborMix);
        AddSettlement("castle_E6", EreborMix);
        AddSettlement("castle_E7", EreborMix);
        AddSettlement("castle_E8", EreborMix);
        AddSettlement("castle_E9", EreborMix);
    }

    // --- Erebor Clan Mappings ---

    private static void InitializeEreborClans()
    {
        AddClan("clan_erebor_1", EreborMix);
        AddClan("clan_erebor_2", EreborMix);
        AddClan("clan_erebor_3", EreborMix);
        AddClan("clan_erebor_4", EreborMix);
        AddClan("clan_erebor_5", EreborMix);
        AddClan("clan_erebor_6", EreborMix);
        AddClan("clan_erebor_7", EreborMix);
    }

    // --- Erebor Culture Fallback ---

    private static void InitializeEreborCulture()
    {
        CultureMap["erebor"] = new List<VolunteerChance>
        {
            new VolunteerChance("erebor_reg_miner", 5),
            new VolunteerChance("erebor_noble", 3),
            new VolunteerChance("iron_hills_reg_recruit", 2),
            // T2 entry-point of the Iron Hills Noble line added in #212 KEYforce revamp.
            // Without this, the 13-troop noble line is fielded by AI but not recruitable in villages.
            new VolunteerChance("iron_hills_noble", 2)
        };
    }

    // --- Lothlorien Settlement Mappings (temporary: borrows Rivendell troops until troops_lothlorien.xml is built) ---

    private static void InitializeLothlorienSettlements()
    {
        AddSettlement("town_L1",   ("imladris_recruit", 5), ("imladris_infantry", 3));
        AddSettlement("castle_L1", ("imladris_recruit", 5), ("imladris_infantry", 3));
        AddSettlement("castle_L2", ("imladris_recruit", 5), ("imladris_infantry", 3));
        AddSettlement("castle_L3", ("imladris_recruit", 5), ("imladris_infantry", 3));
    }

    // --- Lothlorien Clan Mappings ---

    private static void InitializeLothlorienClans()
    {
        AddClan("clan_lothlorien_1", ("imladris_recruit", 5), ("imladris_infantry", 3));
    }

    // --- Lothlorien Culture Fallback ---

    private static void InitializeLothlorienCulture()
    {
        CultureMap["lothlorien"] = new List<VolunteerChance>
        {
            new VolunteerChance("imladris_recruit", 5),
            new VolunteerChance("imladris_infantry", 3),
            new VolunteerChance("imladris_bowman", 2)
        };
    }

    // --- Shaghâna Clan Mappings ---

    private static void InitializeShaghanaClans()
    {
        AddClan("clan_shaghana_1", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_2", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_3", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_4", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_5", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_6", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_7", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_8", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_shaghana_9", ("harad_levy", 7), ("harad_noble", 3));
    }

    // --- Shaghâna Culture Fallback ---

    private static void InitializeShaghânaCulture()
    {
        CultureMap["shaghana"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy", 7),
            new VolunteerChance("harad_noble", 3)
        };
    }

    // --- Âbanissa Clan Mappings ---

    private static void InitializeAbanissaClans()
    {
        AddClan("clan_abanissa_1", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_2", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_3", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_4", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_5", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_6", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_7", ("harad_levy", 7), ("harad_noble", 3));
        AddClan("clan_abanissa_8", ("harad_levy", 7), ("harad_noble", 3));
    }

    // --- Âbanissa Culture Fallback ---

    private static void InitializeAbanissaCulture()
    {
        CultureMap["abanissa"] = new List<VolunteerChance>
        {
            new VolunteerChance("harad_levy", 7),
            new VolunteerChance("harad_noble", 3)
        };
    }

    // --- Rhun Settlement Mappings ---
    // Six regional pools themed by sub-faction (Dragon-Wrath / Balcoth / Far-Rhun / Wain / mixed / Kharaghul).
    // Castle entries cover all bound villages via VolunteerContextAdapter's BoundSettlementId fallback.

    private static void InitializeRhunSettlements()
    {
        // Dragon-Wrath pool: Khûndol + Mârdûn + Tarlat Arlan + Khûsar
        (string, int)[] dragonWrathPool =
        {
            ("dragon_wrath_acolyte",  3),
            ("dragon_wrath_archer",   1),
            ("dragon_wrath_infantry", 1),
            ("dragon_wrath_lancer",   1),
            ("darkhun_recruit",       2),
            ("black_sun_trainee",     1),
            ("loke_rim_initiate",     1),
        };
        AddSettlement("town_RU7",   dragonWrathPool);
        AddSettlement("castle_RU1", dragonWrathPool);
        AddSettlement("castle_RU2", dragonWrathPool);
        AddSettlement("castle_RU3", dragonWrathPool);

        // Balcoth pool: Ûrushban + Nîrakh + Vorgavuld + Castle RU9
        (string, int)[] balcothPool =
        {
            ("balcoth_volunteer", 5),
            ("balcoth_archer",    2),
            ("balcoth_axeman",    1),
            ("loke_rim_initiate", 2),
        };
        AddSettlement("town_RU4",    balcothPool);
        AddSettlement("castle_RU10", balcothPool);
        AddSettlement("town_RU3",    balcothPool);
        AddSettlement("castle_RU9",  balcothPool);

        // Far-Rhun pool: Sârt + Ulbarath + Chêya
        (string, int)[] farRhunPool =
        {
            ("far_rhun_levy",     4),
            ("far_rhun_footman",  2),
            ("far_rhun_horseman", 3),
            ("loke_rim_initiate", 1),
        };
        AddSettlement("town_RU5",    farRhunPool);
        AddSettlement("castle_RU11", farRhunPool);
        AddSettlement("castle_RU12", farRhunPool);

        // Wain pool: Tôrcâin + Kârashûn + Kelepar + Rûartar
        (string, int)[] wainPool =
        {
            ("loke_rim_initiate",  1),
            ("wain_youngblood",    5),
            ("wain_glaiveman",     2),
            ("wainrider_cavalry",  2),
        };
        AddSettlement("castle_RU7", wainPool);
        AddSettlement("castle_RU8", wainPool);
        AddSettlement("town_RU6",   wainPool);
        AddSettlement("castle_RU6", wainPool);

        // Mixed pool: Mistrand + Lest + Samârnûl (identical to culture fallback by design — explicit > implicit)
        (string, int)[] mixedPool =
        {
            ("balcoth_volunteer",    1),
            ("black_sun_trainee",    1),
            ("darkhun_recruit",      1),
            ("dragon_wrath_acolyte", 1),
            ("far_rhun_levy",        1),
            ("kharaghul_youth",      1),
            ("loke_rim_initiate",    1),
            ("sagarun_deckhand",     1),
            ("wain_youngblood",      2),
        };
        AddSettlement("town_RU1",   mixedPool);
        AddSettlement("town_RU2",   mixedPool);
        AddSettlement("castle_RU4", mixedPool);

        // Kharaghul pool: Iôrig + Ulathar
        (string, int)[] kharaghulPool =
        {
            ("loke_rim_initiate",     1),
            ("kharaghul_youth",       5),
            ("kharaghul_raider",      2),
            ("kharaghul_horse_scout", 2),
        };
        AddSettlement("town_RU8",   kharaghulPool);
        AddSettlement("castle_RU5", kharaghulPool);
    }

    // --- Rhun Culture Fallback ---
    // Engine culture id is "khuzait" (per .claude/rules/xml-data.md — XSLT cultures use vanilla engine IDs).

    private static void InitializeRhunCulture()
    {
        CultureMap["khuzait"] = new List<VolunteerChance>
        {
            new VolunteerChance("balcoth_volunteer",    1),
            new VolunteerChance("black_sun_trainee",    1),
            new VolunteerChance("darkhun_recruit",      1),
            new VolunteerChance("dragon_wrath_acolyte", 1),
            new VolunteerChance("far_rhun_levy",        1),
            new VolunteerChance("kharaghul_youth",      1),
            new VolunteerChance("loke_rim_initiate",    1),
            new VolunteerChance("sagarun_deckhand",     1),
            new VolunteerChance("wain_youngblood",      2),
        };
    }

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

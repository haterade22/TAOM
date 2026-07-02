using System.Collections.Generic;

namespace TAOM.Features.TroopProgression;

// Volunteer recruitment pools for Rohan (clan pools + culture fallback) — the data half of
// VolunteerRecruitmentService, registered by the static ctor in the core file (T5 refactor split:
// one file per culture so a culture's pools + design rationale live together; lookup/weighting
// logic stays in VolunteerRecruitmentService.cs). Lookup priority: conditional > settlement >
// clan > culture (troops.md).
public partial class VolunteerRecruitmentService
{
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
}

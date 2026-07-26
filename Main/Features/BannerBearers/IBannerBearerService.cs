using System.Collections.Generic;
using TaleWorlds.Core;
using TAOM.Features.BannerBearers.Domain;

namespace TAOM.Features.BannerBearers;

// All banner-bearer POLICY. Pure: takes primitives and the FormationClass value-type enum,
// never a Formation/Agent (ADR-007). The GameModel and MissionLogic boundaries adapt.
public interface IBannerBearerService
{
    bool IsEnabled { get; }

    // True iff EVERY bearer-candidate in a formation carries renderable heraldry. Each entry is one
    // troop's agent.Origin.Banner.GetBannerDataListCount(); 0 = a null Banner or an empty BannerDataList.
    // SetFormationBanner forces a native banner-tableau rebuild that renders the bearer's heraldry, and
    // the engine picks that bearer by PRIORITY from ALL candidates — not slot 0 — so a single bannerless
    // troop can be the one rendered and access-violate (0xC0000005 @ TaleWorlds.Native.dll+0x28ac0e; the
    // 100%-repro siege OrderOfBattle CTD, rca-banner-bearers-siege-ctd-2026-07-23). Hence ALL candidates
    // must be renderable, and an empty set (nothing to confirm) is treated as not-renderable. The
    // MissionLogic reads the counts off the sealed types; this decides.
    bool HasRenderableHeraldry(IReadOnlyList<int> candidateBannerDataCounts);

    // Read once per mission by the model — see BannerBearerConfig.MinimumFormationTroopCount
    // for why this must not vary mid-mission.
    int MinimumFormationTroopCount { get; }

    // How many bearers a formation of this size/class should field. 0 = none.
    int GetDesiredBearerCount(int formationUnitCount, FormationClass formationClass);

    // False for excluded races AND for any race id the engine doesn't know (fail-closed).
    bool IsRaceAllowed(int raceId);

    // Whether a troop of this formation class may carry a banner (default: Infantry only).
    // Keyed on the troop's DefaultFormationClass, i.e. its default_group XML attribute.
    bool IsFormationGroupAllowed(FormationClass formationClass);

    // Banner ItemObject id for a culture, or null when the culture should field no banner.
    string? ResolveBannerItemId(string? cultureId);

    // The culture a formation should fly, given every unit's culture id. The FIRST unit is not a
    // semantic culture owner (Formation.GetFirstUnit is just Arrangement.GetAllUnits()[0]), so a
    // mixed-culture formation — an allied army, or a mercenary-heavy player party — would fly
    // whichever standard happened to be arranged into slot 0. Majority wins instead, with a
    // deterministic tie-break so the result never depends on arrangement order.
    string? ResolveMajorityCultureId(IReadOnlyList<string?> cultureIds);

    // Patch63 spawn guard (issue #360): does the freshly-spawned bearer's ExtraWeaponSlot hold
    // the expected formation banner? Only Ok permits the engine's native slot-4 entity read —
    // that read has no engine-side guard and AVs on an absent weapon record. Fail closed: an
    // unknown expected id is never Ok.
    BearerSlotVerdict EvaluateSpawnedBearerSlot(string? slot4ItemId, string? expectedBannerItemId);

    // Patch63 eligibility gate for a REINFORCEMENT bearer, folding the master toggle: with the
    // feature disabled this is unconditionally true (vanilla parity — a vanilla hero-captain
    // formation's replacement bearers must not be suppressed by TAOM policy; the engine's own
    // reinforcement path applies no per-agent policy either). Enabled: the same race +
    // formation-group policy the deployment gate applies via CanAgentBecomeBannerBearer.
    bool IsReinforcementBearerAllowed(int raceId, FormationClass formationClass);
}

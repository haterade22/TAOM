# Tavern Mercenaries

## Overview

Every town in TAOM sells a rotating stack of hireable troops in its backstreets menu and spawns one of
them as a walking NPC in the tavern. This feature makes that offer **culture-appropriate**: each town
hires its own people — Minas Morgul sells Mordor orcs, Orthanc sells Uruk-hai, Umbar sells Adûnaim —
drawn from a dedicated set of `*_merc` troops rather than from the vanilla Calradian mercenary pool.

It is a pure data feature. No C# was written; the behaviour is entirely
`RecruitmentCampaignBehavior` reading `<basic_mercenary_troops>` out of `taom_spcultures.xml`.

## Why This Exists

- **Vanilla behaviour:** `CultureObject.BasicMercenaryTroops` is a per-culture list of mercenary
  troops. Calradian cultures point it at `eastern_mercenary` ("Sellsword"), `western_mercenary`
  ("Hired Spear") and `sword_sisters_sister_t3`, all `Culture.neutral_culture`.
- **TAOM requirement:** the tavern offer is one of the few places the player *buys* troops outright,
  so it should read as the settlement's own hired muscle, and it should be worth checking — troops you
  can't easily get from notables.
- **Without this feature:** all 14 town-owning cultures shipped vanilla's list verbatim, so a Mordor
  fortress-city offered *"Recruit 3 Hired Pike (2400 denars)"* — a Vlandian-looking pikeman standing
  in the tavern of Minas Morgul. Reported by the player from a live campaign, 2026-07-26.

## Architecture

### How the engine picks a town's offer

`RecruitmentCampaignBehavior` (decompiled:
`E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\RecruitmentCampaignBehavior.cs`)
keeps one saved record per town:

```csharp
public class TownMercenaryData      // [SaveableProperty] TroopType + Number
```

- `DailyTickTown` → `UpdateCurrentMercenaryTroopAndCount(town, forceUpdate: day % 2 == 0)` (line 341).
  A **full reroll every other day**; on the off days it only tops the count up.
- **70%** of rerolls (`DefaultTavernMercenaryTroopsModel.RegularMercenariesSpawnChance = 0.7f`) draw a
  random entry from `town.Culture.BasicMercenaryTroops`, then call `FindRandomMercenaryTroop` — which
  **randomly walks that troop's `UpgradeTargets`**, each tier deeper weighted `1/1.5×` (lines 384-401).
  This walk is why vanilla's T2 `western_mercenary` surfaced as its T4 upgrade "Hired Pike".
- **30%** fall back to `town.Culture.CaravanGuard` and walk *its* upgrades. TAOM already set that
  per culture (`caravan_guard_mordor` etc.), which is why the tavern looked correct roughly 3 days in 10.
- Count: `(MaxCharacterTier − tier) × 2` … `× 5`, random-weighted (`FindNumberOfMercenariesWillBeAdded`,
  line 403) — **lower-tier troops are offered in larger stacks**. Price = `GetTroopRecruitmentCost` ×
  count.

Three consumers read the same `TownMercenaryData`:

| Consumer | Gate |
|----------|------|
| Backstreet menu option `recruit_mercenaries` (line 658) | `Number > 0` and player gold ≥ unit cost |
| Walking tavern NPC (`SandBox.RecruitmentAgentSpawnBehavior.CreateMercenary`) | `HasAvailableMercenary(NotAssigned)` — spawns for any occupation |
| Its **hire dialogue** (lines 729, 755, 781) | `Occupation` ∈ {`Mercenary`, `CaravanGuard`, `Gangster`} |
| AI lord + caravan hiring (`CheckRecruiting`, lines 419, 464) | `HasAvailableMercenary(Occupation.Mercenary)` |

### Design decisions

**Copies, not repurposed line troops.** The picks are duplicates (`<source>_merc`) rather than the
originals flipped to `Mercenary`. Occupation is not cosmetic: `TaomPartyWageModel` routes it through
[`TroopCostService`](../../Main/Features/TroopProgression/TroopCostService.cs) for a **×2 recruitment
cost and ×1.5 wage** (`MercenaryRecruitMultiplier` / `MercenaryWageMultiplier`, lines 6-7). Flipping a
shared line troop would have repriced it in notable recruitment and in every AI party and garrison
fielding it — `harad_noble` alone appears in 14 party templates. The copies keep the originals at
`Soldier` and confine the mercenary economics to the tavern.

**Leaf copies.** The `_merc` entries carry no `<upgrade_targets>`. Because the engine walks upgrades
from whatever root it draws, an upgrade target would let the offer drift out of the mercenary set and
back onto a normal Soldier troop — reintroducing the original bug in TAOM-flavoured form.

**Rarest-first selection.** Sources are each culture's lowest-`VolunteerChance`-weight entries in
[`Main/Features/TroopProgression/RecruitmentPools/`](../../Main/Features/TroopProgression/RecruitmentPools/),
so the tavern sells the specialists notables rarely offer rather than the troop you can already recruit
in bulk. Mordor's pool is `mordor_orc_recruit:4`, `morannon_recruit:5` (common) against four weight-1
specialists — those four became the mercenaries.

### Data flow

```
Main/Features/TroopProgression/RecruitmentPools/*.cs   (VolunteerChance weights — selection input)
        |
   tools/oneoff/generate_tavern_mercenaries.py         (PICKS table -> copies + culture blocks)
        |
        +--> troops/troops_<culture>.xml               (<source>_merc, occupation="Mercenary", leaf)
        |
        +--> taom_spcultures.xml <basic_mercenary_troops>
                     |
        RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount  (70% branch)
                     |
        TownMercenaryData -> backstreet menu + tavern NPC + AI hiring
```

## Configuration

### `Main/_Module/ModuleData/taom_spcultures.xml`

One `<basic_mercenary_troops>` block per culture:

```xml
<basic_mercenary_troops>
  <template name="NPCCharacter.mordor_uruk_grunt_merc" />
  <template name="NPCCharacter.mordor_orc_impaler_merc" />
  <template name="NPCCharacter.mordor_orc_hunter_merc" />
  <template name="NPCCharacter.mordor_warg_tamer_merc" />
</basic_mercenary_troops>
```

### Current picks (21 troops, 14 cultures)

Sources are weight-1 pool entries unless noted; Lothlórien, Umbar and the two Harad cultures have no
weight-1 entry, so they take their pool's minimum.

| Culture | Source troops → `_merc` copies | Weight |
|---------|-------------------------------|--------|
| `mordor` | `mordor_uruk_grunt`, `mordor_orc_impaler`, `mordor_orc_hunter`, `mordor_warg_tamer` | 1 |
| `gondor` | `gondor_bel_recruit`, `gondor_lam_clansman`, `gondor_loss_lumberman` | 1 |
| `isengard` | `urukhai_warrior`, `urukhai_scout`, `orthanc_chosen` | 1 |
| `gundabad` | `gundabad_fighter`, `gundabad_scout` | 1 |
| `erebor` | `erebor_oathsworn` | 1 |
| `rivendell` | `rivendell_noble` | 1 |
| `mirkwood` | `mirkwood_recruit` | 1 (pool's only entry) |
| `dolguldur` | `dg_orc_scout` | 1 |
| `goblin` | `goblin_fighter` | 1 |
| `mistymountainorcs` | `mistymountainorcs_fighter` | 1 |
| `lothlorien` | `imladris_bowman` | 2 |
| `umbar` | `umbar_elite` | 3 |
| `shaghana`, `abanissa` | `harad_noble` (shared Harad pool; one copy, two references) | 3 |

Excluded by rule: creature mounts (`taom_spider_creature`, Dol Guldur weight 1) and level-51
legendaries (`rivendell_knight_golden_flower`, Rivendell weight 1) — neither reads as a tavern hire.

### Copy anatomy

A `_merc` copy is the source block with exactly four edits:

| Field | Change |
|-------|--------|
| `id` | `<source>` → `<source>_merc` |
| `name` | `{=aom_merc_<source>_name}[Culture] Hired <role>` (English-only, as with every other TAOM troop name) |
| `occupation` | `Soldier` → `Mercenary` |
| `<upgrade_targets>` | removed |

`race`, `level`, `default_group`, `culture`, `<face>`, `<skills>` and `<Equipments>` are copied
verbatim, so a hired troop is mechanically identical to its source.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/taom_spcultures.xml` | 14 `<basic_mercenary_troops>` blocks |
| `Main/_Module/ModuleData/troops/troops_<culture>.xml` | the 21 `_merc` troops, in a `TAVERN MERCENARIES` section at the end of each file |
| `tools/oneoff/generate_tavern_mercenaries.py` | idempotent generator; `PICKS` table is the source of truth for selection |
| `Main/Features/TroopProgression/RecruitmentPools/*.cs` | `VolunteerChance` weights that decide which troops are "rarest" |
| `Main/Features/TroopProgression/TroopCostService.cs` | the ×2 recruit / ×1.5 wage mercenary multipliers |
| `docs/cultures.md` | new-culture checklist entry + the three authoring rules |

## Dependencies

None in C#. The feature depends on data already owned by `TroopProgression` (recruitment-pool weights)
and on vanilla `RecruitmentCampaignBehavior` / `RecruitmentAgentSpawnBehavior` being unpatched by TAOM
— `Patch42` postfixes `HourlyTickParty` in the same class for castle recruitment but does not touch the
mercenary path.

## Tests

- `TAOM.Tests/Features/TroopProgression/TavernMercenaryDataTests.cs` — 4 tests over the shipped XML:
  every referenced troop is TAOM-defined (no `neutral_culture` ids), carries `occupation="Mercenary"`,
  has no upgrade targets, and copies a troop that sits at its culture pool's **minimum** weight (so a
  future pool rebalance can't silently make the tavern offer common).
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` —
  `AllNonMilitiaNonBossTroops_AreReachableFromARecruitmentPoolRoot` exempts `*_merc` alongside
  `*_militia_*` and `*_boss`: mercenaries are bought for gold, not volunteered. Without the exemption
  the 21 copies read as orphaned troops.

## How to add tavern mercenaries for a new culture

1. Find the culture's rarest recruitment-pool entries — the lowest `VolunteerChance` weight in its
   `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs`.
2. Drop creature mounts and level-50+ legendaries from the candidates.
3. Add a row to `PICKS` in `tools/oneoff/generate_tavern_mercenaries.py`:
   `"<culture>": [("<source_id>", "<troop file stem>", "[Culture] Hired <Role>")]`.
4. Run `python tools/oneoff/generate_tavern_mercenaries.py` (idempotent — existing copies are skipped).
5. `python tools/validate_moduledata.py` then `./build.ps1 -RunTests`; `TavernMercenaryDataTests`
   enforces all four invariants.

## Gotchas

- **An existing save keeps its old offer.** `TownMercenaryData.TroopType` is a `[SaveableProperty]`, so
  a campaign saved before this change still shows its stored Calradian troop until that town's next
  reroll (up to two in-game days).
- **The tavern NPC spawns regardless of occupation, but only talks if it's a mercenary.**
  `CreateMercenary` gates on `HasAvailableMercenary(Occupation.NotAssigned)` while the hire dialogue
  gates on `Mercenary`/`CaravanGuard`/`Gangster` — a `Soldier`-occupation offer would put a silent NPC
  in the tavern with no way to hire it. This is the failure mode the occupation edit prevents, and it
  is only visible in-game.
- **Never add `<upgrade_targets>` to a `_merc` troop.** See "Leaf copies" above.
- **Stack size is inverse to tier.** A level-6 Gondor recruit is offered in far larger stacks than a
  level-36 Rivendell noble — that is `FindNumberOfMercenariesWillBeAdded`, not a data error.

## History

| Date | Change |
|------|--------|
| 2026-07-26 | Feature created. 21 `_merc` troops, 14 culture blocks repointed off vanilla's Calradian list. Player report: "Hired Pike" in Minas Morgul. |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/cultures.md](../modding/cultures.md)
- [docs/modding/troops.md](../modding/troops.md)
- [docs/modding/troubleshooting.md](../modding/troubleshooting.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reviews/lessons/data-content-cultures.md](../reviews/lessons/data-content-cultures.md)

<!-- backlinks-end -->

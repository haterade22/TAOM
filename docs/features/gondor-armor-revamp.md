# Gondor Armor Revamp (KEYforce)

## Overview

Wires KEYforce's new Gondor armor meshes — covering 8 previously-uncovered regions — into the LOTRLOME_Armory module and refits 107 Gondor troops across 13 regions to wear the new gear per the artist's per-tier armor + weapon guide.

## Why This Exists

KEYforce delivered 99 new armor pieces (helmets, chests, pauldrons, bracers, greaves) for the southern Gondor fiefs that had been missing custom gear since the mod's initial Gondor pass. Without this feature, Lossarnach axebearers wore generic Anórien chainmail, Serelond pikemen had no distinctive look, and Lamedon hill-wardens were visually indistinguishable from Anórien infantry — none of the regions the artist designed armor for were actually wearing it in-game.

- **Vanilla behavior:** N/A — Gondor in vanilla Bannerlord is `empire_w` with vanilla Battanian gear. TAOM's prior Gondor pass equipped Anórien, Minas Tirith, Osgiliath, Cair Andros, and Minas Ithil with custom `sk_gd_*` items. The 8 southern fiefs reused Anórien generics.
- **TAOM requirement:** Each Gondor region (Lossarnach, Pinnath Gelin, Harondor, Anfalas, Serelond, Lebennin, Belfalas, Lamedon) wears region-specific armor consistent with KEYforce's intent.
- **Without this feature:** Visual identity collapses to generic Anórien for 8 of 13 source-of-truth regions. The artist's mesh work isn't surfaced in-game.

## Architecture

### Design Challenge

Two coupled artifacts — one in TAOM, one in the artist's separate LOTRLOME_Armory module — must move in lockstep:

1. **Item XML in the Armory** — every item must have a matching `<Item id="sk_gd_..." mesh="sk_gd_..." />` definition before any troop can equip it. Missing items cause the "underwear bug" (troops render naked).
2. **Equipment loadouts in `troops_gondor.xml`** — 6,800 lines, 107 troops to update with per-tier loadouts that follow the artist's "low → high" progression tables.

The two must be applied and validated together — neither alone produces a working state.

### Solution Approach

- **Phase A: Armory item authoring.** A sibling generator `tools/generate_gondor_armor_phase2.py` reuses the `STAT_TIERS` table from phase-1 (`tools/generate_gondor_armor.py`) so new items have stat values consistent with existing ones. Idempotent (safe to re-run).
- **Phase B: Troop equipment refit.** Four parallel planning agents read the source-of-truth and produced precise blueprint XML for each troop's new `<EquipmentRoster>`. A consolidator script `tools/apply_gondor_troop_revamp.py` encodes all 107 blueprints as Python data and applies them via regex-based block replacement that preserves indentation and Horse/HorseHarness lines on cavalry.
- **Phase C: Cross-reference validation.** `tools/validate_gondor_refs.py` greps every `Item.sk_gd_*` reference in `troops_gondor.xml` against the Armory's `id="sk_gd_*"` ids. Zero missing references is the gate.

### Component Diagram

```
KEYforce (3D artist)
        |
        | meshes (.tpac) + source-of-truth (gondor_armors_and_troops.txt)
        v
generate_gondor_armor_phase2.py  --apply  -->  LOTRLOME_Armory/.../gondor/*.xml
                                                     |
                                                     | item ids
                                                     v
4 parallel planning agents read source-of-truth
        |
        | per-troop blueprint (slot, item_id) tuples
        v
apply_gondor_troop_revamp.py  --apply  -->  Main/_Module/ModuleData/troops/troops_gondor.xml
                                                     |
                                                     | sk_gd_* references
                                                     v
                            validate_gondor_refs.py  -->  PASS / FAIL gate
```

## Configuration

### Source-of-truth: `E:\repos\lotraom-assets\tools\gondor_armors_and_troops.md`

The artist's authoritative guide. Two halves:

1. **Item lists by region** (lines 1–313): every item id the artist produced, grouped by region and slot.
2. **Per-region armor + weapon guides** (lines 314–1288): unit tree, "low → high" progression for each slot, and weapon loadout per tier.

When the artist ships new gear, this file is updated and the two phase-2 scripts can be extended/re-run.

### Stat tiers: `tools/generate_gondor_armor.py` `STAT_TIERS`

| Slot | light | medium | heavy | elite |
|------|-------|--------|-------|-------|
| head | 15/1.5 | 24/2.5 | 32/3.5 | 40/4.5 |
| body | 20/8.0 | 32/13.0 | 42/18.0 | 50/22.0 |
| shoulder | 5+3/3.0 | 9+6/5.0 | 13+11/7.0 | 19+17/9.0 |
| arm | 8/0.6 | 14/1.0 | 20/1.5 | 26/2.0 |
| leg | 12/1.5 | 20/2.5 | 28/3.5 | 34/4.0 |

(armor / weight; shoulder lists body+arm)

## Key Files

| File | Purpose |
|------|---------|
| `LOTRLOME_Armory/.../gondor/head_armors.xml` | 39 new helmets (Pinnath Gelin, Harondor, Anfalas, Serelond, Lamedon) |
| `LOTRLOME_Armory/.../gondor/body_armors.xml` | 42 new chests (all 8 missing families) |
| `LOTRLOME_Armory/.../gondor/shoulder_armors.xml` | 9 new pauldrons (Lossarnach + Serelond) |
| `LOTRLOME_Armory/.../gondor/arm_armors.xml` | 5 new bracers (Lossarnach + Serelond) |
| `LOTRLOME_Armory/.../gondor/leg_armors.xml` | 4 new Serelond greaves |
| `Main/_Module/ModuleData/troops/troops_gondor.xml` | 107 troops refit; 5 retired |
| `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml` | Castle EW8 guard pool — `_axeguard` → `_vet_axebearer` |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | Castle EW8/EW12 + clan_empire_west_5 elite recruitment — `_noble` → `_axebearer` |
| `tools/generate_gondor_armor_phase2.py` | Idempotent armor item author |
| `tools/apply_gondor_troop_revamp.py` | Equipment blueprint applier (107 troops) |
| `tools/validate_gondor_refs.py` | Cross-reference gate for the underwear bug |
| `tools/generate_gondor_troops.py` | Original troop scaffolder — Lossarnach noble line removed |

## Dependencies

- `LOTRLOME_Armory` module (declared in `SubModule.xml`) — must be loaded for items to resolve at runtime.
- KEYforce mesh assets in `LOTRLOME_Armory/.../gondor/*.tpac` — the item XML references mesh names that must exist in the engine.
- `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` — recruitment chain entry points must reference surviving troop ids.

## Tests

- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs` — 84 tests, all pass after the `_noble` → `_axebearer` rewiring. No new tests added (the change is data-only ID swap).
- Cross-reference validation (`tools/validate_gondor_refs.py`) — equivalent to a runtime smoke test for the underwear bug. Expected output: `PASS: all sk_gd_* / sk_dg_* references resolve.`

## How to add a new region

1. Have KEYforce ship meshes + add the new region's section to `gondor_armors_and_troops.txt`.
2. Extend `tools/generate_gondor_armor_phase2.py`: add `ArmorItem(...)` entries to `HEAD_ARMORS` / `BODY_ARMORS` / etc. for the new family. Run `--dry-run` then `--apply`.
3. Extend `tools/apply_gondor_troop_revamp.py`: add per-troop entries to `EQUIPMENT` dict. Encode `(slot, item_id)` tuples per the artist's tier mapping. Run `--dry-run` then `--apply`.
4. Run `tools/validate_gondor_refs.py` — must show 0 missing references.
5. Run `./build.ps1 -RunTests` and verify recruitment tests pass.
6. In-game spot check: launch a custom battle, select Gondor, scroll to the new region's troops in the troop preview.

## How to retire a troop

This was done for 5 Lossarnach extras. New mod version permits ID changes — pre-1.0 saves are not preserved.

1. Add the troop id to `DELETE_IDS` in `tools/apply_gondor_troop_revamp.py`.
2. Run `--apply`. The script deletes the entire `<NPCCharacter>` block and removes any `<upgrade_target id="NPCCharacter.<id>" />` references.
3. Find external references: `grep -r "<deleted_id>" Main/`. Update:
   - `VolunteerRecruitmentService.cs` — replace with surviving mainline ids
   - `settlement_guards_config.xml` — same
   - `tools/generate_gondor_troops.py` — remove the troop's `Troop(...)` definition so re-runs don't recreate it
4. Run `tools/validate_gondor_refs.py` and `./build.ps1 -RunTests`.

## Performance

No runtime performance impact — this is a pure data refit. Item resolution at troop spawn is unchanged in cost. The 99 new Armory items add ~5 KB to the per-faction item index, negligible.

## Troop-wiring status (southern-Gondor fiefs)

Item defs (#358) and troop *equipment* wiring are separate steps. Current state:

| Line | Troop prefix | Region armour | Status |
|------|--------------|---------------|--------|
| Dol-Amroth / Swan Knight | `gondor_da_*` | `sk_gd_dol_*` | ✅ equipped |
| Arndir (Pinnath noble) | `gondor_arn_*` | `sk_gd_pin_noble_*` | ✅ equipped (20/21; `pin_nob_chest_elite_b` lord-only idle) |
| Blackroot Vale | `gondor_brv_*` | `sk_gd_vale_*` | ✅ equipped (0 idle) |
| Pinnath Gelin (regular) | `gondor_pg_*` | `sk_gd_pin_*` | ✅ equipped (pre-existing) |
| Lamedon | `gondor_lam_*` | `sk_gd_lam_*` | ✅ full variety — all 17 helms + 7 chests (lord pieces on `hill_warden`, balance-flagged) |
| Lossarnach | `gondor_loss_*` | `sk_gd_los_*` | ✅ all 26 pieces used |
| Belfalas | `gondor_bel_*` | `sk_gd_bel_*` (body) + Anórien | ✅ spec-complete (3 capes un-modelled — KEYforce to-do) |
| Lebennin | `gondor_leb_*` | `sk_gd_leb_*` (chest) + Anórien | ✅ complete (chest-only region by design) |
| **Linhir** | `gondor_lin_*` (5 troops) | `sk_gd_lin_*` + DA helmet fallback | ✅ equipped — full self-modelled set; T3/T4 helmet = `sk_gd_dol_helmet_med_a`; 5 lord/cape pieces reserved (DRAFT guide) |
| **Lond-Galen** | `gondor_lg_*` (5 troops) | `sk_gd_lon_*` + Serelond fallback | ✅ equipped — Head/Body `sk_gd_lon_*`, Cape/Gloves/Leg `sk_gd_sere_*` per guide; `chest_lord_a` lord-only |

### Gotchas when wiring a troop line (learned 2026-07-24)

1. **Do NOT run `apply_gondor_troop_revamp.py --apply` wholesale.** Its `EQUIPMENT` dict has drifted from the live `troops_gondor.xml` — a dry-run showed **61/118** troops would be rewritten and `DELETE_IDS` would delete a troop. Apply only your intended ids (import the module's `find_npc_block`/`replace_equipment` and loop) or do a per-roster armour-slot swap directly; always `git diff` after to confirm scope.
2. **A single `Horse`/`HorseHarness` placed OUTSIDE the `<EquipmentRoster>`** (a loose child of `<Equipments>`, after the rosters) is a **valid, intended** pattern for a mounted troop with variant rosters — the engine applies that one mount to EVERY variant, so it need not be copied into each roster (confirmed by KEYforce/lead, 2026-07-24). The real trap is a full-roster *rebuild* (join rosters + civ set) that silently DROPS the loose Horse/HorseHarness → the troop loses its mount. When rebuilding a mounted troop's rosters, **preserve the loose Horse/HorseHarness** — keep it as the single shared entry after the rosters (preferred, cleaner), or equivalently-but-redundantly copy it inside each roster. (`gondor_arn_hill_knight` lost its mount this way and was rebuilt with the mount inside both rosters; `gondor_anf_cavalry` uses the cleaner shared-outside form.)
3. **Multi-roster troops** (variant loadouts) must have EVERY roster's armour swapped, not just the first — the applier's `EQUIP_ROSTER_RE.search` only finds the first.

## Changelog

- 2026-07-24 — **Capital-Gondor armour polish** (Anórien pool + Minas Tirith — beyond the #358 southern revamp; equipment-only, save-safe, validators PASS). Homed idle *own-region* pieces onto the capstones that lacked them: **Osgiliath** Guard + Dome Guard → own `sk_gd_osg_bracer_noble_elite_a` (chosen so `med_a`/`heavy_a` stay used — no new idle); **Minas Ithil** Captain + Sharpshooter → own `sk_gd_ith_noble_helmet_heavy_b`, so all three top Ithil troops wear Ithil's own helm; **Minas Tirith** Fountain Guard → own **elite** Fountain chest `sk_gd_mns_fount_chest_elite_a` + a masked-helm variant, Veteran → masked noble-helm variant. Decisions honoured: Ithil keeps its own look (7 plain `ano_pauld_noble_*` stay idle), Anórien-Regular chest ladder left as-is, Fountain/Citadel Guard use their own `mns_` armour (no `osg_`; osg lord chest reserved). Anórien pool idle 15→13 (rest reserved/deferred by design); `mns_` set 10/11 used (lone idle = the surplus heavy Fountain chest).
- 2026-07-24 (#358) — **Linhir spear line equipped — LAST greenfield Gondor noble line** (equipment-only, save-safe, 187 NPCCharacters unchanged, mesh-exact, validators PASS, adversarially verified). 5 `gondor_lin_*` (T3 Noble → T7 High Guard) refit from generic Anórien to the near-complete self-modelled `sk_gd_lin_*` set per the **DRAFT** Armor Guide; only fallback is the T3/T4 medium helmet → Dol-Amroth `sk_gd_dol_helmet_med_a` (Linhir models no med helmet). Two variant rosters each (bracer `_a/_b` on T3/T4, helmet `_a/_b` on T5–T7). **Lord/cape pieces reserved off the troops** per spec (`chest_elite_a` "Linhir Lord Armour", `helmet_lord_a/b`, both `pauld_cape_noble_*`) → 17/22 used, 5 reserved-idle. Weapons preserved (spear+shield; the draft's sword+tower-shield weapon guide is a separate pass). Leg caps at `grvs_heavy` (no elite greave exists). **All southern + capital Gondor noble lines are now equipped; Minas Tirith `gondor_mt_` remains an optional separate audit.**
- 2026-07-24 (#358) — **Lond-Galen crossbow line equipped + Anfalas idle-fix** (troop-wiring follow-up; equipment-only, save-safe, 187 NPCCharacters unchanged, all refs mesh-exact, `validate_gondor_refs` + `validate_moduledata` PASS):
  - **Lond-Galen** — 5 `gondor_lg_*` (T4 Noble → T8 Haven Guard, a crossbow/pavise line) refit from generic Anórien to the line's own `sk_gd_lon_*` helmets + chests, with **Serelond `sk_gd_sere_*`** pauldron/bracer/greave fallback per the Armor Guide (the spec's explicit fallback for this line — its sibling Serelond Noble line — **not** Anórien). Two variant rosters per troop (helmet `_a`/`_b`); weapons preserved. `sk_gd_lon_chest_lord_a` reserved for the line's lords (worn by 4 Gondor lord equipment sets), so the 10-piece `lon_` set is fully used — 9 on troops + 1 on lords, **0 truly idle**. The two Anfalas veterans keep their shared `lon_` gear (share, not move). Adversarially verified across 3 lenses (spec-parity, save-safety, resolution/idle) — all confirmed. **Remaining greenfield Gondor noble line: Linhir only.**
  - **Anfalas** — the 3 idle `sk_gd_anf_*` pieces (`cav_helmet_heavy_b`, `inf_helmet_heavy_c`, `inf_chest_heavy_b`) homed via variant rosters on `gondor_anf_cavalry` (+`cav_helmet_heavy_b`) and `gondor_anf_infantry` (+`inf_helmet_heavy_c` + `inf_chest_heavy_b`); **all 13 Anfalas pieces now used.** `anf_cavalry`'s mount kept as the single shared loose Horse (see gotcha #2).
- 2026-07-24 (#358) — **More Gondor troop lines wired to their region armour** (continuing the troop-wiring follow-up, all via the same per-roster armour-slot swap; equipment-only, save-safe, 187 NPCCharacters unchanged, all refs mesh-exact, `validate_gondor_refs` + `validate_moduledata` PASS):
  - **Arndir** (Pinnath Gelin noble) — 9 `gondor_arn_*` (T3 Noble → T8 Hill-Knight cav / T7 Foot-Knight inf) → `sk_gd_pin_noble_*` per the Armor Guide (cavalry cape-pauldrons, Hill-Knight the elite chest, Anórien noble bracer/greave fallback). **20 of 21** noble pieces now used; only `pin_nob_chest_elite_b` (spec-marked lord-only) stays idle.
  - **Blackroot Vale** — 7 `gondor_brv_*` archers (Bowman → Shadowbow) → `sk_gd_vale_*` (hoods on scouts, plain capes → pauldron+cape on rangers, `chest_heavy_c` archer-pad on Shadowbow). **All 20 vale pieces used (0 idle).**
  - **Lamedon** — 5 `gondor_lam_*`: variant rosters added so all **17 helmets + 7 chests** are used (were 5 + 5). The 4 lord helms + `chest_lord_a` placed on the top troop `hill_warden` — flagged as a balance choice (move to Lamedon lords if too strong).
  - **Lossarnach** — 14 `gondor_loss_*`: the 2 idle pieces (`noble_helmet_elite_b`, `inf_chest_elite_a`) wired via variant rosters; **all 26 pieces used.**
  - Fixed a horse-loss on `gondor_arn_hill_knight` — its mount sat mis-placed *outside* the `<EquipmentRoster>`, so a variant-roster rebuild dropped it; restored *inside* both rosters. **Remaining greenfield Gondor noble lines: Linhir, Lond-Galen.**
- 2026-07-24 (#358) — **Dol-Amroth / Swan Knight troop line equipped** (the #358 troop-wiring follow-up, partial). The 11-troop line (`gondor_da_noble` → T9 `gondor_da_swan_knight` / T8 `gondor_da_swan_guard`) refit from generic Anórien to the `sk_gd_dol_*` set per the artist Armor Guide — cavalry cape-pauldrons, infantry plain, elite chest only on the Swan Knight, masked elite helm on the pinnacle units. All **17 battle rosters** converted (2–3 variant rosters on some troops); weapons + mounts preserved; equipment-only (save-safe). Authored the modelled-but-un-authored Belfalas boot `sk_gd_bel_boots_a` (T1 recruit). **Applied via a per-roster armour-slot swap, not the applier's whole-file `--apply`** — a dry-run showed `apply_gondor_troop_revamp.py`'s dict has drifted from the live file (**61/118** would be rewritten) and `DELETE_IDS` would delete a troop, so a wholesale apply is unsafe; the 11 entries + generator `DA_*` stubs were filled as a drift-guard. Belfalas already matched its guide (its 3 un-modelled cape pieces stay on Anórien fallback — KEYforce to-do). All 87 armour refs mesh-exact; `validate_gondor_refs` + `validate_moduledata` PASS. (Arndir + Blackroot Vale wired later the same day — see entry above; remaining greenfield: Linhir, Lond-Galen.)

> **Both asset-pipeline caveats in the entries below are RESOLVED as of 2026-09-01, and their
> shared premise was wrong.** They say these meshes render only once the runtime `AssetPackages/`
> bundles are recompiled via the Modding Kit. There are no bundles: `LOTRLOME_Armory` has 0 cooked
> packs against 4,490 loose `Assets/**/*.tpac`, and the loose tree is what the engine reads for
> this module. All 117 of the meshes those entries describe resolve today (`sk_gd_dol_` 40,
> `sk_gd_lin_` 26, `sk_gd_vale_` 25, `sk_gd_lon_` 14, `sk_gd_pin_noble` 12), verified against the
> live tpac TOCs. No recompile is or was required. Corrected model:
> [armory-guide.md](../reference/armory-guide.md) "Two asset trees".
- 2026-07-22 (#358) — KEYforce **noble** drop (2026-07-21 meshes): **106 new `sk_gd_*` item defs** for five southern-Gondor noble lines via the phase-2 generator — **Dol-Amroth** (`sk_gd_dol_*`, 33), **Linhir** (`sk_gd_lin_*`, 22), **Blackroot Vale** (`sk_gd_vale_*`, 20), **Pinnath Gelin "Arndir" noble** (`sk_gd_pin_noble_*` / `_nob_chest_*` / `_pauld_noble_*`, 21), **Lond-Galen** (`sk_gd_lon_*`, 10). **Every id was verified against the geo-tpac mesh TOCs** (`Assets/gondor_assets/{belfalas,pinnath_gelin,anfalas}/*_geo.tpac`), the ground truth — not just the spec. `beard_cover_type` set to `none` (generator default aligned to commit `c4886891`). **Item defs only** — no troop-tree wiring; the four brand-new lines' troop trees (Dol-Amroth up to T9 Swan Knight, Blackroot Vale archer line, Arndir Hill-Knights, Linhir spearmen) are a **tracked follow-up**, so those items read as "unused" in `validate_gondor_refs.py` (PASS, 0 missing; `validate_moduledata.py` PASS; no duplicate ids). **Lond-Galen was a mesh rename, resolved via the tpac:** the drop renamed `sk_gd_anf_lon_helmet_*` → `sk_gd_lon_helmet_*` and `sk_gd_lon_nob_chest_*` → `sk_gd_lon_chest_*` (old names absent from every geo tpac). The generator's Jun-06 "Anfalas Noble" entries were renamed to the verified ids, and the two troops that wore the old gear (`gondor_anf_vet_infantry`, `gondor_anf_vet_cavalry`) were repointed so they stay clothed after the recompile; the 10 old dead-mesh item defs still linger in the live XML, now unused (retire in the follow-up). **Asset caveat unchanged:** the meshes render only once the runtime `AssetPackages/` bundles are recompiled via the Modding Kit (reported handled/coming) — item defs resolve the instant the packs land.
- 2026-06-29 — KEYforce **noble** drop: 28 new `sk_gd_*` items (Anfalas noble 10, Lossarnach noble 18) via the phase-2 generator, mesh ids **verified against the `.tpac` TOCs** (they do NOT follow a clean prefix — Anfalas noble chests are `sk_gd_lon_nob_chest_*`, Lossarnach helmets `sk_gd_los_noble_helmet_*` but its chests `sk_gd_los_nob_chest_*`). Anfalas vet troops swapped to noble Head+Body; the **Lossarnach Noble line restored** (the 5 troops retired below, now that meshes exist) — `gondor_loss_noble`→`_veteran`→`_sergeant`→`_warden`→`_captain`. Player-recruitable from Lossarnach (Bar Melui / `town_EW7`) via the live `recruitment_pools/gondor.json` settlement group (the `clan_empire_west_5` ClanMap entry is only a safety-net — SettlementMap outranks ClanMap in the cascade, so the JSON group is what actually fires; this mirrors how `gondor_ser_noble` is wired). Fielded by `kingdom_hero_party_gondor_lossarnach_template`. **Asset-pipeline caveat:** the drop's meshes are *source* tpacs in `Assets/gondor_assets/` and must be recompiled into the runtime `AssetPackages/` bundles via the Modding Kit before they render. **Ship the recompile atomically with this XML** — the Anfalas vet swap repoints two *currently-clothed* troops onto the unbundled meshes, so XML-without-recompile regresses them to underwear (the new Lossarnach nobles were never clothed, so they only degrade-in-place). The item XML + troop data are correct and resolve the instant the meshes bundle.
- 2026-05-01 — KEYforce Gondor armor revamp (#99): 99 new `sk_gd_*` Armory items across 5 slots for 8 southern regions, 98 troops refit + 5 Lossarnach noble troops retired in `troops_gondor.xml`, recruitment/guard rewiring to the axebearer line, and the phase-2 generator + applier + ref-validator tooling.

## GitHub Issue

- **Issue:** [#99 — feat(gondor): KEYforce armor revamp — add 99 items + restructure 13 regional troop trees](https://github.com/haterade22/TAOM/issues/99)
- **Status:** Closed (after Phase 6 closeout)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/gondor-ithilien-ranger.md](./gondor-ithilien-ranger.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reviews/audit-gondor-armory-2026-08-04.md](../reviews/audit-gondor-armory-2026-08-04.md)

<!-- backlinks-end -->

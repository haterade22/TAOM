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

### Source-of-truth: `E:\repos\lotraom-assets\tools\gondor_armors_and_troops.txt`

The artist's authoritative guide. Two halves:

1. **Item lists by region** (lines 1–313): every item id the artist produced, grouped by region and slot.
2. **Per-region armor + weapon guides** (lines 314–1288): unit tree, "low → high" progression for each slot, and weapon loadout per tier.

When the artist ships new gear, this file is updated and the two phase-2 scripts can be extended/re-run.

### Stat tiers: `tools/generate_gondor_armor.py` `STAT_TIERS`

| Slot | light | medium | heavy | elite |
|------|-------|--------|-------|-------|
| head | 15/1.5 | 24/2.5 | 32/3.5 | 40/4.5 |
| body | 20/8.0 | 32/13.0 | 42/18.0 | 50/22.0 |
| shoulder | 5+5/3.0 | 8+8/5.0 | 12+10/7.0 | 15+12/9.0 |
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

## GitHub Issue

- **Issue:** [#99 — feat(gondor): KEYforce armor revamp — add 99 items + restructure 13 regional troop trees](https://github.com/haterade22/TAOM/issues/99)
- **Status:** Closed (after Phase 6 closeout)

# Troop Tree Revamp (KEYforce spec, Issue #212)

## Overview

Follow-up to #211 (Armory item authoring): #211 shipped 1000+ new armor items per KEYforce's spec but deferred the corresponding troop tree work. This issue closes that gap for 5 cultures — Mordor, Isengard, Dol Guldur, Gundabad, Erebor — by adding 48 new troops, deleting 30 TAOM-fabricated extras not in the spec, and refitting 113 existing troops with spec-compliant equipment. Rhun is handled in a separate session.

## Why This Exists

- **Vanilla behavior:** Bannerlord has no Middle-earth troop trees.
- **Pre-#211 TAOM:** Each culture had a hand-fabricated troop tree shipping in `troops/troops_<culture>.xml`. Many troops were never wired into a Tolkien-canonical role and used legacy equipment IDs predating the LOTRLOME_Armory authoring pass.
- **#211 (KEYforce armor spec):** Authored the per-culture armor lines (`sk_md_orc_*`, `sk_uruk_mordor_*`, `sk_is_orc_*`, etc.) but did NOT consume them at the troop level. Equipment rolls into Bannerlord via the `EquipmentRoster` block on each `NPCCharacter` — adding items to the Armory without referencing them from troops means lords spawn in legacy gear and the new meshes are dead weight.
- **Without this feature:** Mordor orc lords still spawned with no orc race line; Isengard's Section 1 spec orcs (`isengard_orc_grunt` through `isengard_orc_slayer`) didn't exist as NPCCharacters; Gundabad had no `gundabad_bolgs_ironfang` T8 elite; Erebor had no Iron Hills Noble line. KEYforce's spec was unused.

## Architecture

### Design Challenge

The work is **mechanical** (XML edits across 5 culture troop files + 4 downstream config files), but the risk surface is the *cross-file consistency* requirement codified in `.claude/rules/troops.md`:

- New troops must be added to relevant `taom_partyTemplates.xml` blocks or lord armies never spawn them.
- Deleted troops must be purged from `troop_weights.xml`, `troop_resource_costs.xml`, AND every `PartyTemplateStack` reference, or the engine logs `MBObjectManager` lookup warnings on every campaign tick.
- `VolunteerRecruitmentService.cs` settlement / clan / culture fallback pools must reference IDs that still exist post-revamp.
- Race attributes per the rules table must match what `IRaceManager` recognizes — otherwise the engine falls back to `human` race and visuals are wrong.

A single missed reference produces a save-load crash or silent visual bug. Manual editing across 5 troop files + 4 downstream files = high error rate.

### Solution Approach

**Idempotent Python apply scripts per culture, each with `--dry-run` / `--apply` flags, cloned from the established `tools/apply_gondor_troop_revamp.py` pattern (issue #99).**

Each script encodes three operations for its culture:
1. **EQUIPMENT** dict — `troop_id → list of (slot, item_id)` pairs. Refits existing troops in-place.
2. **DELETE_IDS** set — Removes obsolete NPCCharacter blocks AND scrubs them from every `upgrade_targets="..."` attribute in the same file.
3. **NEW_TROOPS_XML** string — Block of new NPCCharacter definitions inserted before `</NPCCharacters>`.

Order: delete first, then insert (avoids naming collisions like `mordor_orc_warrior` which existed as a stub AND as a spec T4 entry).

Two downstream cleanup scripts close the cross-file consistency loop:

- **`tools/cleanup_deleted_troops_212.py`** — Sweeps deleted IDs from `taom_partyTemplates.xml`, `troop_weights.xml`, `troop_resource_costs.xml`.
- **`tools/expand_party_templates_212.py`** — Inserts `PartyTemplateStack` entries for new troops into each culture's `kingdom_hero_party_<culture>_template` block.

C# downstream:

- **`VolunteerRecruitmentService.InitializeGundabadCulture()`** — Adds fallback recruitment pool; Gundabad had ZERO recruitment entries before this issue.

Validation:

- **`tools/validate_all_troop_refs.py`** — Cross-checks every `sk_*_*` reference in every `troops_<culture>.xml` against Armory item XMLs. Final state: all 7 cultures PASS, 0 missing.

### Component Diagram

```
E:\repos\lotraom-assets\tools\<culture>_armors_and_troops.txt   (KEYforce spec)
                              |
                              v
tools/apply_<culture>_troop_revamp.py    (per-culture mechanical script)
                              |
                  --apply ->  | mutates troops_<culture>.xml
                              v
                       troops_<culture>.xml
                              |
                              v
tools/cleanup_deleted_troops_212.py   ->  taom_partyTemplates.xml
                                          troop_weights.xml
                                          troop_resource_costs.xml
tools/expand_party_templates_212.py   ->  taom_partyTemplates.xml (new stacks)
VolunteerRecruitmentService.cs        ->  Gundabad fallback pool
                              |
                              v
tools/validate_all_troop_refs.py      (gate: 0 missing refs)
                              |
                              v
                         ./build.ps1
```

## Configuration

### Spec sources (KEYforce, external repo)

| Culture | File | Lines |
|---------|------|-------|
| Mordor | `E:\repos\lotraom-assets\tools\mordor_armor_and_troops.txt` | 661 |
| Isengard | `E:\repos\lotraom-assets\tools\isengard_armors_and_troops.txt` | 684 |
| Dol Guldur | `E:\repos\lotraom-assets\tools\dol_guldur_armors_and_troops.txt` | 713 |
| Gundabad | `E:\repos\lotraom-assets\tools\gundabad_armors_and_troops.txt` | 282 |
| Erebor | `E:\repos\lotraom-assets\tools\erebor_armors_and_troops.txt` | 1037 |

### Per-culture totals (final state)

| Culture | NPCCharacters | New | Deleted | Refits | Validator |
|---------|---------------|-----|---------|--------|-----------|
| Mordor | 35 | 21 (10 orc + 6 Nurn Warg + 5 Black Uruk variants) | 14 (10 uruk extras + 3 orc stubs + `mordor_black_numenorean`) | 9 | PASS — 123 armor refs, 0 missing |
| Isengard | 51 | 13 (`isengard_orc_*` line — Section 1 of spec) | 0 (`orthanc_*` line kept — Tolkien-canonical) | 30 | PASS — 126 armor refs, 0 missing |
| Dol Guldur | 50 | 0 (Khamul human line already in file) | 12 (6 old stubs + 6 berserker line not in spec) | 17 | PASS — 157 armor refs, 0 missing |
| Gundabad | 27 | 1 (`gundabad_bolgs_ironfang` T8) | 4 (`champion`, `pike_warrior`, `veteran_pike_warrior`, `warg_warrior`) | 16 | PASS — 93 armor refs, 0 missing |
| Erebor | 58 | 13 (Iron Hills Noble line: archer/infantry/shock branches) | 0 | 41 | PASS — 218 armor refs, 0 missing |

**Total: 48 new, 30 deleted, 113 refits.** All counts verified against XML state on 2026-05-23.

### Race attributes (per `.claude/rules/troops.md` table + user direction)

| Culture | Line | `race` attribute |
|---------|------|------------------|
| Mordor | orc, Nurn Warg | `orc` |
| Mordor | Black Uruks | `uruk` |
| Isengard | new orc line | `orc` |
| Isengard | Uruk-Hai (existing) | `uruk_hai` (preserved for save compat) |
| Isengard | berserker (existing) | `berserker` (preserved for save compat) |
| Dol Guldur | Uruk, Warg | `dg_uruk` |
| Dol Guldur | Khamul humans | _no attribute_ (vanilla human) |
| Gundabad | Pale Uruk | `pale_uruk` |
| Erebor | dwarven + Iron Hills | `dwarf` |

## Key Files

| File | Purpose |
|------|---------|
| `tools/apply_mordor_troop_revamp.py` | Mordor mechanical apply script |
| `tools/apply_isengard_troop_revamp.py` | Isengard mechanical apply script |
| `tools/apply_dolguldur_troop_revamp.py` | Dol Guldur mechanical apply script |
| `tools/apply_gundabad_troop_revamp.py` | Gundabad mechanical apply script |
| `tools/apply_erebor_troop_revamp.py` | Erebor mechanical apply script |
| `tools/cleanup_deleted_troops_212.py` | Sweep deleted IDs from 3 downstream XMLs |
| `tools/expand_party_templates_212.py` | Insert new troops into `kingdom_hero_party_*` templates |
| `tools/validate_all_troop_refs.py` | Cross-reference gate (Armory ↔ troops XML) |
| `Main/_Module/ModuleData/troops/troops_<culture>.xml` × 5 | The troop tree files (mutated by apply scripts) |
| `Main/_Module/ModuleData/taom_partyTemplates.xml` | Party stack composition per culture |
| `Main/_Module/ModuleData/TroopWeights/troop_weights.xml` | AI strength weighting |
| `Main/_Module/ModuleData/special_resources/troop_resource_costs.xml` | Per-troop kingdom resource cost |
| `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` | `InitializeGundabadCulture()` recruitment pool |

## Dependencies

- **#211 (KEYforce multi-culture armor authoring)** — Source of the `sk_md_orc_*`, `sk_uruk_mordor_*`, `sk_is_orc_*`, `sk_dg_uruk_*`, `sk_gb_uruk_*`, `sk_dwarf_iron_*` item IDs referenced by the troop loadouts.
- **#99 (Gondor armor + troop revamp)** — Pattern template; all 5 apply scripts in this issue were cloned from `tools/apply_gondor_troop_revamp.py`.
- **LOTRLOME_Armory module** — Owns the actual `<Item id="sk_*">` definitions referenced from troop loadouts.
- **`IRaceManager`** (Main/Features/CharacterCreation/RaceManager.cs) — Resolves `race="orc"`, `race="uruk"`, `race="dg_uruk"`, `race="pale_uruk"`, `race="dwarf"` to engine race IDs.

## Tests

No new test files. The work is data-only XML mutation + 1 method addition to `VolunteerRecruitmentService` (already covered by existing `VolunteerRecruitmentServiceTests`).

Pre-revamp test suite count: 2122 → post: 2144 passing (the +22 delta is from a parallel session, not this issue). Final build: 0 errors, 961 warnings (pre-existing).

## How to Re-Run an Apply Script

The scripts are idempotent — running `--apply` twice is safe (no-op on the second pass).

1. `python tools/apply_<culture>_troop_revamp.py --dry-run` — print intended deltas, no file writes.
2. `python tools/apply_<culture>_troop_revamp.py --apply` — mutate `troops_<culture>.xml`.
3. `python tools/cleanup_deleted_troops_212.py --apply` — sweep any newly-orphaned references.
4. `python tools/expand_party_templates_212.py --apply` — wire new troops into party templates.
5. `python tools/validate_all_troop_refs.py` — gate: must report 0 missing.
6. `./build.ps1` — must exit 0.

## How to Add a New Troop Mid-Revamp

If KEYforce's spec changes and a new troop ID needs to be added to (e.g.) Mordor:

1. Append a `(troop_id, [(slot, item_id), ...])` entry to the `NEW_TROOPS_XML` block in `tools/apply_mordor_troop_revamp.py`.
2. Add a corresponding `PartyTemplateStack` entry to the `ADDITIONS["mordor"]` list in `tools/expand_party_templates_212.py`.
3. If the troop is an elite (tier ≥ 6), add a `<TroopWeight id="..." weight="2.0"/>` to `Main/_Module/ModuleData/TroopWeights/troop_weights.xml`.
4. Re-run the apply + cleanup + expand + validate pipeline above.

## Decisions

- **"Delete if not in spec" applied strictly** to TAOM-fabricated extras (`mordor_uruk_feller/ravager/executioner/...`, `dg_initiate/disciple/infantry/...`, berserker line, Gundabad duplicates).
- **`orthanc_*` line kept** (4 Isengard troops) — Tolkien-canonical Orthanc tower elite uruks, per user direction.
- **`mordor_black_numenorean` deleted** — per user direction (explicit exception to the keep-canonical heuristic).
- **`dg_uruk_veteran_warrior` repurposed in-place** (level 21→16) rather than renamed, to preserve save compat per `troops.md` rule "Never change troop IDs."

## Out of Scope

- **Rhun** — user is handling in a separate session.
- **Lossarnach pauldrons** — Gondor #99 known limitation.
- **Localization XML** for the 47 new troop display names — game falls back to the display name on the NPCCharacter XML directly.
- **Patrol-level / vassal-reward templates** — only `kingdom_hero_party_<culture>_template` was expanded this pass; patrol and reward templates use legacy compositions until a follow-up issue.
- **Dol Guldur Khamul human troops party-template wiring** — already in `kingdom_hero_party_dolguldur_template` per the pre-revamp audit; no new wiring needed.

## GitHub Issue

- **Issue:** #212
- **Status:** Closed
- **Related:** #99 (Gondor), #211 (Armory authoring), Rhun session (separate)

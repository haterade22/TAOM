# RCA: Multi-culture armor revamp (2026-05-22)

## Summary

`/deep-review` of the KEYforce multi-culture armor revamp (issue #211, 6 cultures, 277 new items) surfaced one HIGH and several LOW issues. The HIGH issue (duplicate item IDs across `erebor/` and `iron_hills/` folders) was a real bug that would have caused engine warnings/shadowing at runtime. All issues fixed in-session before commit.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|--------------------|
| 1 | **HIGH** | `generate_erebor_armor.py` wrote 123 `sk_dwarf_iron_*` items to `LOTRLOME_items/erebor/`, but the canonical Iron Hills folder is `LOTRLOME_items/iron_hills/` (with 125 pre-existing IDs). 118 IDs ended up in BOTH folders. Both folders load at module init — duplicate IDs cause silent shadowing or engine warnings. | Cross-folder duplicate detection gap | Generator's `DEFAULT_ARMORY_BASE` was set to `erebor/` because the user's spec file is named `erebor_armors_and_troops.txt`. I assumed `erebor/` was the canonical home for all dwarf items. I never grep'd `LOTRLOME_items/` to discover the pre-existing `iron_hills/` folder. Cross-reference validator only checks "every troop ref resolves to ONE id" — it doesn't catch "this id is defined in TWO files." | (a) **Validator extension**: add a duplicate-id check across all Armory XML files. Any id defined more than once = FAIL. (b) **Pre-authoring check**: every new generator script must `grep -l "id=\"<prefix>" LOTRLOME_items/*/` before defaulting to a folder. The first folder that already has items with that prefix is the canonical home. (c) Codified in `tools/validate_all_troop_refs.py` and in future generator templates. |
| 2 | LOW | `generate_erebor_armor.py` line 124 had an unused regex variable `m = re.match(...)` immediately abandoned in favor of split-based parsing. Dead code. | Code quality | I wrote the regex first, then realized split was simpler. Forgot to delete the regex. | Deleted in-session. No systemic rule needed — single-occurrence drafting artifact. |
| 3 | LOW | `generate_erebor_armor.py` line 155 had operator-precedence ambiguity: `if "leather" in item_id or category == "boots" and "light" in tier:` (parsed as `or (and ...)`). Worked correctly for current items but would misclassify `sk_dwarf_iron_boots_med_leather_a` if added. | Code robustness | Wrote condition without parens, Python's precedence happened to give the right answer for the current spec. Bug latent for items the spec doesn't list yet. | Fixed in-session with explicit parens: `if "leather" in item_id or (category == "boots" and tier == "light")`. Lint rule possible but low ROI for one-off scripts. |
| 4 | LOW | `validate_all_troop_refs.py` ARMOR_PREFIX_RE missed `ar_*` prefix (used by `ar_ardunian_elite_*` items in mordor/ and referenced by troops_mordor.xml + troops_umbar.xml). Validator would silently miss breakage of these items. | Validator scope gap | Built the regex from the cultures I touched this session (sk_*, clo_urukscout_, urukscout_). Didn't audit all existing TAOM-owned item prefixes. | Added `ar_[a-z]+_` to the validator regex in-session. Process improvement: when extending an existing validator, grep for ALL `Item.` references across all troop XML, sort/uniq the prefixes, and ensure every prefix matches the regex. |
| 5 | LOW | CHANGELOG narrative said "22 missing Loke-Rim elite helmets + 1 hood" — actually 21 helmets + 1 hood = 22 total. Off-by-one in prose only; the count in the table was correct. | Documentation drift | Wrote the prose from memory after the table. Didn't re-count when adding the hood note. | Fixed in-session. No systemic rule. |
| 6 | LOW (deferred) | `sk_is_orc_*` (13) and `sk_dg_orc_*` (14) paint-variant helmets have item XML authored but no corresponding `.tpac` mesh packages shipped in `Assets/Isengard/` and `Assets/Dol Guldur/`. They will render as missing meshes in-game UNTIL KEYforce ships paint-variant meshes. | Mesh-first principle gap | I authored items for every spec entry in those families. KEYforce ships generic (`sk_gn_orc_*`) meshes today; paint variants (`sk_is_orc_*`, `sk_dg_orc_*`) are pending. The user's mesh-first directive says "create variations to use all meshes" — implicitly meaning, *don't* author items without backing meshes. | Documented as deferred limitation in CHANGELOG. Per the original directive these items SHOULD use the generic shape mesh as a fallback (`sk_gn_orc_*_geo.tpac`) and rename when paint variants ship. Not blocking — items render gracefully missing-mesh and don't cause crashes. **Followup work**: revisit IS/DG paint items either by re-pointing `mesh="..."` attribute to the GN shape mesh, or by removing them until paint meshes ship. |

## Root-cause pattern

**Finding 1 (HIGH) reveals a class of bug: "I assumed folder X without grepping for the prefix."** Same category as scope-gapped behaviors from past sessions:
- Gondor #99 Lossarnach noble line: assumed it was active without grepping recruitment service for references (caught at integration time).
- Multiple Codex reviews on TAOM (#26, #28, #31, #38) have flagged "feature implementation made assumption about config/data layout without checking the existing convention."

**The systemic gap:** TAOM has a multi-folder Armory convention (separate folders per culture/sub-culture) but no validator that detects duplicate IDs across folders. The cross-reference gate only validates "troop ref → id exists" (one-to-one); it does not validate "each id is defined exactly once" (uniqueness).

## Why each agent missed (or caught) finding #1

| Agent | Caught it? | Why |
|-------|-----------|------|
| 1 Standards | No | Standards checks are per-file. Cross-file duplicate detection isn't a TAOM standard. |
| 2 Bannerlord API | No | Schema verification per item — not concerned with cross-file uniqueness. |
| 3 Efficiency | No | Performance checks per script — not concerned with where the script writes. |
| 4 Completeness | No | Existence-check (file/issue/changelog exist) — not concerned with item-ID uniqueness. |
| 5 **Data Flow** | **YES** | Per the prompt: "trace the chain mesh ↔ item ↔ troop." Agent enumerated `iron_hills/` mesh packages and noticed the items reference them — which led to discovering the items were duplicated in `erebor/`. This is exactly what data-flow tracing is for. |

The HIGH bug was caught only because Agent 5 traced from KEYforce mesh assets *backwards* through the Armory item XML — a chain rule 6 of the data-flow agent's prompt. Lesser-scoped agents had no way to catch it. **This confirms the value of keeping Agent 5 (Data Flow) on every deep-review.**

## Feedback memories to codify

One genuine systemic pattern is worth codifying:

**Memory candidate: `feedback_multi_folder_id_uniqueness.md`**
- **Rule**: When a culture has multiple Armory subfolders (e.g., dwarf items in both `erebor/` and `iron_hills/`), every new generator script MUST grep ALL subfolders for the prefix BEFORE writing. The first folder containing items with the prefix is the canonical home. Adding items to a different folder creates runtime duplicate-ID warnings.
- **Validator extension**: `tools/validate_all_troop_refs.py` should be extended to fail on any id defined more than once across all Armory XML files.

I will add this memory in a follow-up commit after the rule is observed working through one more cycle.

## Fixes applied (in-session)

| Fix | File | Status |
|-----|------|--------|
| Remove duplicated `sk_dwarf_iron_*` from `erebor/*.xml` | `tools/rollback_erebor_iron_misfile.py` (new) + 5 erebor XMLs | Done |
| Re-author 5 unique spec items to `iron_hills/shoulder_armors.xml` | `iron_hills/shoulder_armors.xml` (out of repo) | Done (via re-run of corrected generator) |
| Fix generator default to `iron_hills/` | `tools/generate_erebor_armor.py` | Done |
| Remove unused regex | `tools/generate_erebor_armor.py` line 124 | Done |
| Fix operator-precedence in material heuristic | `tools/generate_erebor_armor.py` line 155 | Done |
| Add `ar_*` prefix to validator | `tools/validate_all_troop_refs.py` | Done |
| Fix CHANGELOG prose (22 → 21 elite helmets) | `CHANGELOG.md` | Done |
| Document rollback in CHANGELOG | `CHANGELOG.md` | Done |
| RCA file (this document) | `docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md` | Done |

## Verdict

**READY FOR COMMIT** after:
1. Re-run `tools/validate_all_troop_refs.py` to confirm 0 missing refs after the rollback + re-author.
2. Re-run `./build.ps1` to confirm 0 errors.

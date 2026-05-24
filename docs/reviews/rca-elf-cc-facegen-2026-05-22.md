# RCA — Elf Character Creation facegen action_set (2026-05-22)

## Top-line summary

Elves (Mirkwood / Rivendell) rendered as a contorted / horizontally-stretched mesh on the Character Creation parent menu — same visible failure mode as the 2026-05-04 "broken custom-race CC parents" bug, but with a different root cause hidden behind it. The original 2026-05-04 fix patched 1.3 action-type aliases onto 12 *pre-existing* facegen action_sets in LOTRLOME_Armory but never authored the missing `as_elf_facegen` / `as_elf_female_facegen` pair. The commit message and CHANGELOG line both listed "elf" as fixed despite the patch not touching it.

The fix shipped in two iterations the same session:

| Iter | Approach | Scope passed | Scope failed |
|---|---|---|---|
| v1 | Slim 14-action elf facegen (only the CC parent action types, `base_set="as_human_warrior"`) | Parent menu — elves stand upright | Early Childhood + every subsequent CC stage — child agent lying down / T-posed |
| v2 | Verbatim copy of `as_dwarf_facegen` / `as_dwarf_female_facegen` (~420 lines per file), `id` + `base_set` attributes renamed only | Every CC stage (confirmed in-game) | — |

Two distinct lessons:

1. **Doc completeness:** the 2026-05-04 snapshot README said the patch covered "12 facegen sets (dwarf, dwarf_female, orc, orc_female, … etc.)". The `etc.` is what hid the elf hole for 18 days — no missing race was named, no present race was claimed comprehensive. Now the README enumerates every required `as_<race>_facegen` ID explicitly.
2. **Engine inheritance semantics:** Bannerlord 1.3's facegen action-lookup does NOT fall through `base_set` for post-parent CC action types (`act_childhood_*`, `act_character_creation_toddler_*`, `act_inventory_*`, `act_stand_*`, `act_sit_*`, `act_rider_story_*`, `act_horse_story_*`). Those types must be declared **directly** in the facegen action_set. LOTRLOME's `as_dwarf_facegen` is the proof-by-existence: it declares all ~106 actions explicitly even though `as_dwarf_warrior` is its base.

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `as_elf_facegen` / `as_elf_female_facegen` never authored in LOTRLOME — engine fell back to default that didn't bind to human skeleton elves use | Cross-mod data gap | The 2026-05-04 patch only touched action_sets that ALREADY existed in LOTRLOME. LOTRLOME was a 1.2-era LOTR armor mod that never had elves as a playable race, so there was nothing for the patch to update. The 2026-05-04 commit message + CHANGELOG line both listed "elf" as fixed, masking the gap. The snapshot README said "12 facegen sets ... etc." — no explicit name list. | **Memory addendum** in `feedback_lotrlome_action_set_aliases.md`: the fix recipe must both (a) PATCH existing facegen action_sets with 1.3 aliases AND (b) CREATE missing facegen action_sets for races that LOTRLOME's authors did not anticipate. The snapshot README now lists every required `as_<race>_facegen` ID by name — no `etc.` |
| 2 | HIGH | v1 fix was a slim 14-action facegen entry; parent menu worked but Early Childhood + all later stages broke (lying-down agent) | Engine semantics gap | I assumed `base_set="as_human_warrior"` inheritance would cover `act_childhood_*` / `act_character_creation_toddler_*` / etc. It does not — those action types must live directly in the facegen action_set. The dwarf block's ~106-action explicit declaration is the evidence: if inheritance worked, LOTRLOME wouldn't bother declaring those in every race's facegen set. I had read the dwarf block before shipping v1 and STILL chose slim because of diff-size aesthetics. | **Memory addendum v2** in `feedback_lotrlome_action_set_aliases.md`: the concrete recipe is "copy LOTRLOME's `as_dwarf_facegen` verbatim, rename `id` + `base_set`, nothing else." The slim form is not "minimum viable" — the dwarf-block form is. Always read existing-working-code to decide what "minimum" means before inventing your own form. |
| 3 | LOW | I told the user "regression is elf-only" without confirming with them — could have been mistaken | Communication / verification | Used an Explore agent's audit to confirm other races still had 106/31 action parity. Was correct, but the user's "the action sets were changed for all races" framing prompted a second guess. | No rule change — the audit was the right move; just be explicit in the response that the audit refuted the framing rather than the user being wrong. |

## Root-cause pattern (#2 deserves its own section)

The slim-vs-full failure mode is the recurring "trust inheritance, fail at runtime" anti-pattern. Bannerlord's action_set / monster / skin XML system is full of `base_set=` / `base_skin=` / `parent=` references that suggest inheritance is the norm — but the engine's behavior is selective about which fields actually fall through. Without decompiling the specific lookup path (and even WITH decompiling — these are native engine calls, hard to fully trace), the only safe heuristic is **"do whatever the proven-working sibling does, verbatim."**

For facegen specifically:
- LOTRLOME's `as_dwarf_facegen`: ~106 actions, works for dwarves at every CC stage. ✓ Proof.
- LOTRLOME's `as_orc_facegen` / `as_uruk_facegen` / etc.: same ~106 actions. ✓ Proof × 12.
- TAOM's v1 `as_elf_facegen`: 14 actions, works only for parent menu. ✗ Counter-example.
- TAOM's v2 `as_elf_facegen`: ~106 actions matching dwarf verbatim. ✓ Works.

The pattern across all 12 LOTRLOME facegens was the evidence. I read it and chose to ignore it for diff-size reasons. v2 corrected the mistake.

**Why this happens generally:** when a new entry must mirror existing entries in a poorly-documented XML schema, the "smaller diff" temptation is strong. The cost of getting it wrong (silent runtime breakage at a different stage than the one being tested) is much higher than the cost of a 420-line verbatim copy. Diff-size is a code-review aesthetic, not a correctness criterion.

## Why each agent / step missed (or caught)

- **Phase-1 Explore agent (2026-05-22, finding #1):** ✅ caught the elf hole. Audited every TAOM `race=` attribute against LOTRLOME's facegen list and reported `elf` as the only missing entry. Specifically called out: "no `as_elf_facegen` ... the engine falls back ... contorted-mesh bug visible on Mirkwood / Rivendell."
- **Phase-1 same agent (finding #2):** ✗ missed the slim-vs-full issue because the audit only checked presence/absence of facegen IDs, not the action-type count per facegen. A "complete" audit at that point would have flagged "elf facegen has 14 actions, dwarf has 106 — wide gap" before I shipped v1.
- **v1 ship decision (mine):** ✗ I had the data — I'd already read the full dwarf block at lines 16812-17134 and saw its ~322-line structure. I chose slim because:
  1. Diff-size aesthetic.
  2. Assumed `base_set="as_human_warrior"` inheritance would cover non-parent action types (untested assumption).
  3. Did not in-game-test BEFORE shipping the slim entry — relied on the user's first-screenshot scope (parent menu only) without anticipating they'd progress to later CC stages.
- **User's in-game test (between v1 and v2):** ✅ caught finding #2 within minutes of shipping v1. Same-day iteration cost ~30 min of additional work + ~420 lines of new diff per file. Acceptable cost relative to the alternative (shipping v1, user reports lying-down child days later, full RCA + reload context cost).
- **Phase-2 Explore agent (between v1 and v2):** ✅ confirmed the engine doesn't fall through `base_set` for `act_childhood_*`, validated XML parse health, and pointed at the dwarf block's full action surface as the correct template. Vindicated the same data the Phase-1 agent had earlier but I hadn't acted on.

## Preventive actions taken

1. **Doc:** `docs/reference/lotrlome-armory-snapshot/README.md` rewritten to list every required `as_<race>_facegen` ID and every required action-type category that must be declared directly in the facegen action_set. No `etc.`
2. **Doc:** `docs/features/character-creation.md` gained a new section ("LOTRLOME `as_<race>_facegen` action_set requirement") with the full recipe + warning about the slim form.
3. **Doc:** `docs/features/race-age-system.md` "How to Add a New Race" now includes a step pointing at the CC facegen requirement.
4. **Memory:** `feedback_lotrlome_action_set_aliases.md` extended with two same-day addenda — the create-missing-not-just-patch rule (from finding #1) and the declare-everything-don't-trust-inheritance rule with concrete recipe (from finding #2).
5. **Code:** the v2 fix itself — `as_elf_facegen` + `as_elf_female_facegen` are now full 106/31-action entries in both the live LOTRLOME and the tracked snapshot, with attribute parity vs `as_dwarf_facegen` confirmed by Python `xml.etree.ElementTree.parse` + action-count audit.

## What this does NOT need

- **No TAOM C# changes** — the fix is pure XML, no Harmony, no GameModel, no SubModule edits. `Patch20_NarrativeHorseGuard`'s race-sync prefix was correct all along; it just needed the engine's lookup target (`as_elf_facegen`) to actually exist with a complete action surface.
- **No startup check / build-time injector** — user explicitly chose "snapshot + doc only" prevention. The audit table + per-race checklist in the README is the safety net.
- **No `/deep-review`** — XML-only change, no C# touched, doesn't meet the deep-review threshold ("≥2 C# files or any feature module").
- **No GitHub issue** — XML-only fix lives entirely in another mod's `ModuleData`; no TAOM-feature change to ticket. Documented via CHANGELOG + this RCA + memory.

## Cost

- v1 → v2 same-day iteration: ~30 min of additional work, ~420 lines of additional diff per file (live LOTRLOME + tracked snapshot).
- Counterfactual cost if v1 had shipped without immediate in-game test and the lying-down-child bug was reported days later: full RCA + context-reload + cold rediscovery of the slim-vs-full distinction. Probably 2-3x.
- Lesson worth carrying forward: when shipping any XML data fix in a poorly-documented engine schema, the in-game test is the only credible verification. Code-only validation (XML parses, schema looks right, action types match what the screenshot suggested) does not catch failure modes at adjacent stages.

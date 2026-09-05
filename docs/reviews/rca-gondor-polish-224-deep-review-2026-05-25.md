# RCA — Gondor polish #224 deep-review findings (2026-05-25)

**Trigger:** `/deep-review` on commit `9649e0f` (the closing commit of #224) surfaced 1 HIGH data-flow gap + 2 MEDIUM script bugs + 1 LOW doc-count discrepancy.

**Verdict:** all confirmed findings fixed in this session per the `/deep-review` "no silent deferrals" rule.

## Findings table

| # | Sev | Bug | Category | Why missed |
|---|-----|-----|----------|------------|
| 1 | HIGH | Lossarnach 1h axe still present across all 6 rosters × 3 troops. CHANGELOG promised "Cleared extra 1h axe slot — keep only 2h axe + thrown axe per user spec." Actual XML: Item1=`wm_gondor_lossarnach_1h_axe_*` retained everywhere. | Data flow — promise-vs-implementation gap | Wrong slot in `EQUIPMENT_DELTAS`: script targeted `Item2`/`Item3` instead of `Item1`. Author looked at the spec ("drop 1h axe") and the roster layout (`Item0=throwing, Item1=1h, Item2=2h`) but transposed the slot number in code. **Compounding bug**: `apply_clear` used `count=1`, so even when the wrong slot existed it was only cleared from the first roster (5 of 6 rosters per troop went untouched). |
| 1b | HIGH (collateral) | Buggy `clear Item2` op REMOVED `wm_gondor_lossarnach_2h_axe_a` from roster 0 of all 3 troops (the 2h axe we WANTED to keep). | Data flow | Same wrong-slot error as #1. The deep-review's "before/after Item2 count" was 6→5 per troop, revealing the lost 2h axe. |
| 2 | MEDIUM | `apply_set` for non-roster slots (`Horse`, `HorseHarness`) inserts INSIDE `</EquipmentRoster>` rather than at the parent `<Equipments>` level. | Latent script defect | Author wrote a single insertion path that always anchors on `</EquipmentRoster>`. Didn't fire this commit (all cavalry troops already had HorseHarness slots; ops were replace-in-place). Would fire on future polish ops that add HorseHarness to a previously non-cavalry troop. |
| 3 | MEDIUM | `EQUIPMENT_DELTAS` has `gondor_leb_militia` defined twice. Python dict overwrite means the first entry (boots only) is silently lost; only the second (boots + sword) survives. Result is correct by accident; code is misleading. | Script hygiene | Author edited the script in two passes (boots fix first, then sword fix) and didn't merge into a single entry. Dict-overwrite is silent — no Python warning. |
| 4 | LOW | CHANGELOG claims "94 equipment ops applied" but actual count varies by counting method (script's reporter says 94; manual category sum says ~99). Cosmetic. | Doc accuracy | Author used the script's own reporter number; the reporter counts only `applied` ops (those that actually changed XML), not `attempted`. The CHANGELOG bucket sum counts attempted ops including no-ops on already-correct slots. |

## Why each deep-review agent missed (or caught) these findings

**Agent 1 (Standards) — caught finding #2** (latent `apply_set` insertion path bug). This is the agent's normal scope: code-quality / pattern conformance. ✓

**Agent 2 (Bannerlord API)** — SKIPPED. No C# / TaleWorlds API surface in the changeset.

**Agent 3 (Efficiency) — caught finding #3** (duplicate dict key). The agent reads the actual script and reports correctness bugs that look like efficiency anti-patterns. ✓

**Agent 4 (Completeness) — caught finding #4** (op-count discrepancy). The agent's job is to count promised-vs-delivered, and noticed the 94/99/104 mismatch when grep-counting operations in the script. ✓

**Agent 5 (Data Flow) — caught finding #1 (and by extension #1b)**. This is the data-flow agent's core competency: trace every CHANGELOG promise to actual XML state. The agent did the manual cross-check that Item1 still contained the 1h axe across all 6 rosters × 3 troops, which the script reporter (and the author's own sanity check) had missed. **This is exactly the agent's intended catch — "every HIGH bug found by Codex in this project was a data flow gap"; Agent 5 caught one without needing Codex.** ✓

**Why was the bug not caught at apply-time:**
- The script's dry-run reporter said "Ops applied: 94/100" — a 6-op shortfall, but the author interpreted those as expected no-ops for `clear` operations on slots that don't exist on every troop (correct in general, wrong here).
- The author's post-apply spot-check (`awk` to inspect 4 troops) didn't include any Lossarnach axe-thrower.
- The validator (`validate_all_troop_refs.py`) only checks armor-reference resolution, not equipment-loadout correctness. PASS at validator does not mean the loadout matches the spec.

## Root-cause pattern: "off-by-one slot index"

Findings #1 + #1b share a single root cause: the author looked at the spec, then at the XML, then wrote `("clear", "Item2"), ("clear", "Item3")` when the actual semantic was `("clear", "Item1")`. The slot indices are short, similar-looking strings — a one-character typo (`1` → `2`) flipped the entire intent. The mistake then compounded because:

1. The script's reporter said "94/100 ops applied" but doesn't reveal that 3 of those ops actually REMOVED the wrong thing.
2. The validator doesn't sanity-check that the troop's actual loadout matches the spec — only that armor IDs resolve.
3. The `apply_clear` `count=1` limit hid the multi-roster damage from a one-line grep — only 1 of 6 rosters showed the change, so it was easy to miss.

## Preventive actions

### 1. `apply_clear` clears ALL roster occurrences (FIXED in this session)

Removed `count=1`. New semantic: "remove this slot from this troop's every roster." If a future polish op needs per-roster precision, add a different op type (e.g., `clear_in_roster_n`) — don't reuse `clear` with a count limit.

### 2. `apply_set` insertion path knows about non-roster slots (FIXED in this session)

Added `NON_ROSTER_SLOTS = {"Horse", "HorseHarness"}` set + branch in `apply_set`. Insertions for these slots now anchor on `</Equipments>` not `</EquipmentRoster>`.

### 3. De-duplicated `gondor_leb_militia` entry (FIXED in this session)

Merged the two delta entries into one. Added a header comment noting the troop's boots are wired in the Lebennin block.

### 4. Lossarnach deltas now clear `Item1` (FIXED in this session)

Fixed the slot index. Also: restored the lost `wm_gondor_lossarnach_2h_axe_a` in roster 0 of all 3 troops via a one-shot Python restore (anchored on each troop's unique Item0 throwing-axe ID).

### 5. Generalized lesson — deep-review WAS the safety net

The deep-review caught this BEFORE the bug shipped to in-game testing. The "Phase 3e RCA mandatory for every confirmed finding" rule worked exactly as designed: not as a post-mortem after the bug bit a user, but as a pre-merge gate. The lesson for future polish work is:

- **Run `/deep-review` after every apply-script-generated commit, not just the closing commit of a feature.** The apply scripts generate large diffs; visual review (the author's own spot-checks) consistently misses gaps that Agent 5's spec-vs-XML trace finds.
- **The script's "ops applied: N/M" reporter is necessary but not sufficient.** If the M is from `(op, slot, args)` tuples that named the wrong slot, the report says "applied" while the work is wrong. Treat the reporter as a sanity check for "did the script run," not for "did the script do the right thing."

No new feedback-memory entry needed — this is a one-session author error, not a systemic pattern.

## What this RCA does NOT cover

- The CHANGELOG entry was substantively correct (it promised the right outcome — drop 1h axe). The implementation was wrong. The fix patches the implementation; CHANGELOG stays.
- The 2 MEDIUM script bugs were latent / cosmetically wrong but didn't damage data on the original commit. Fixes prevent future damage.
- The LOW op-count discrepancy is a counting-methodology difference, not a delivery gap. No fix needed.

## Verification

- Validator: PASS, 7/7 cultures, 0 missing refs (Gondor: 181 troops unchanged).
- Build: 0 errors, 1042 warnings (pre-existing).
- Lossarnach state per direct grep:
  - `gondor_loss_axe_thrower`: Item1=0 occurrences, Item2=6 occurrences ✓
  - `gondor_loss_skirmisher`: Item1=0, Item2=6 ✓
  - `gondor_loss_vet_axe_thrower`: Item1=0, Item2=6 ✓
- Script: idempotent re-run produces no further changes (verified via post-fix dry-run).

## References

- Commit `9649e0f` — original #224 work (introduced the bug)
- This commit (TBD after this RCA lands) — fixes the bug
- Issue #224 — closed; will not be reopened (this is a follow-up patch, not a regression of scope)
- Skill `.claude/skills/deep-review/SKILL.md` Phase 3e (mandatory RCA gate)
- `.claude/rules/harness-facts.md` (Phase 3e applies to ANY confirmed finding)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->

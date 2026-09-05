# RCA — Dwarf action-set parity fix (water-fall CTD)

**Date:** 2026-06-25
**Feature/fix:** Restore full action-type parity to standalone `as_dwarf_warrior` (issue #300)
**Review:** `/deep-review` — 5 agents (tooling correctness, independent parity recompute, engine-behavior decompile, docs accuracy, adversarial blast-radius)

## Summary

A dwarf falling into water CTD'd because the standalone `as_dwarf_warrior` action set (no `base_set`, seeded from Native 1.3) was missing 423 active action types that Native 1.4.6's `as_human_warrior` has — including the engine's `act_dive_*`. Fix: a comment-safe, idempotent text-splice tool (`tools/patch_dwarf_action_parity.py`) adds every missing active type as an explicit entry, applied to the LIVE LOTRLOME file + the tracked snapshot.

**The data/code fix was confirmed correct by all five agents** — independent parity recompute (0 missing, 0 dupes, additions-only), tooling correctness (10/10), and an engine decompile that VERIFIED the cross-module field-merge (`Module.cs` `CreateProcessedActionSetsXMLForNative`) and the missing-type=hard-crash / missing-clip=soft-fail distinction. The review's value was catching a **documentation** error, not a code error.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | Docs (CHANGELOG/LESSONS-LEARNED/README/engine-bump) claimed "trolls (`as_cave_troll_*`/`as_hill_troll_*`) are the next standalone sets at risk." **False** — both troll roots use `base_set="as_human_warrior"` and inherit Native's dive via the field-merge. | Docs / accuracy | Asserted the blast radius **from memory** (the troll-race memory mentions a standalone `cave_troll`) without enumerating the LIVE file's actual standalone sets. | Enumerate standalone sets (`skeleton=` set AND no `base_set`) **programmatically** before claiming blast radius. The LIVE file has only 5; `as_dwarf_warrior` is the only standalone *humanoid* set. Corrected all 5 docs; codified in LESSONS-LEARNED + engine-bump Phase 4. |
| 2 | MED | Snapshot README "Snapshot date" section not bumped to 2026-06-25. | Docs / completeness | Edited the README's new parity section; missed the dated section at the file bottom (the README's own instruction is to bump it when the snapshot file changes). | Bumped the date with a note that the patch was in-place (not a full re-snapshot). |
| 3 | LOW | `patch_dwarf_action_parity.py` had no error handling on the **target** read (the base read was guarded) — a wrong path or game-locked LIVE file would stack-trace. | Tooling robustness | Focused on the happy path; the script is a permanent re-run tool (engine bumps), so a clean error matters. | Wrapped the target read in `try/except OSError → sys.exit`. Verified the clean-error path. |
| 4 | LOW (pre-existing — NOT this change) | The tracked snapshot has drifted from LIVE by 10 `action_set`s (the spider/elephant/chariot creature sets added during June 2026 mount work). | Repo hygiene | Snapshot wasn't re-taken after the mount features landed. | Noted in the README snapshot-date section. A full re-snapshot is out of scope for this fix (would be a large unrelated diff); flagged for a separate pass. |

## Root-cause pattern

The single real finding (#1) is a **"claim from memory vs. verify from source"** miss — the same family as `evidence-over-claims.md` §C. The fix's *conclusion* (dwarf-only) was correct; the *follow-up risk I documented* (trolls next) was an unverified inference that the adversarial agent refuted by listing the LIVE file's standalone sets. Findings #2–#4 are minor hygiene, not a shared theme.

Notably, the fix's load-bearing engine assumptions (field-merge, base_set non-traversal, clip safety) were the highest-risk claims — and those I *did* verify (in-file comment + working orcs + the engine decompile). The miss was on a lower-stakes blast-radius aside I let ride on memory. Lesson: apply the same enumerate/decompile rigor to the "what else is affected" claim as to the core fix.

## Why each agent did / didn't catch finding #1

- **Agent 1/2/3 (tooling, parity, engine-behavior):** scoped to the fix's *correctness* (the dwarf set + the engine merge) — which was fine. The trolls claim lives only in prose, outside their scope. Correctly silent.
- **Agent 4 (docs accuracy):** cross-checked numbers/commands/links and caught #2 (snapshot date), but did not independently re-enumerate standalone sets to test the trolls claim.
- **Agent 5 (adversarial blast-radius):** **caught #1.** Its task A explicitly enumerated every standalone `action_set` and reported the true set of 5 — directly refuting the trolls claim. This is why the adversarial "enumerate the full blast radius" agent is high-value: it tests the *aside*, not just the fix.

## Lesson codified

`docs/reviews/LESSONS-LEARNED.md` → Animation & Skeleton: the existing standalone-set lesson's **Prevent** line now mandates *enumerating* standalone sets programmatically (not inferring from memory) to bound the blast radius, and records the corrected trolls fact. No new feature/topic memory file needed (the canonical record is LESSONS-LEARNED).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->

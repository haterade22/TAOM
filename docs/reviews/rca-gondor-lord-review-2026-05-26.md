# RCA — Gondor Lord Review (2026-05-26)

## Top-line summary

Session shipped a 4-fix Gondor noble-roster cleanup (Amrothos clan, 3 noblewomen rename + culture flip, Ciriel body = Dorwen body, Fix 4 round-robin body keys + 8 Tier 2 culture flips). `/deep-review` surfaced 2 confirmed bugs introduced/missed by the session work, plus 1 pre-existing TAOM inconsistency unrelated to this work. Net: minor — both confirmed bugs are cosmetic and either fixed in-session (settlements.xml) or pending user authorization (GitHub issue).

## Findings table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `Main/_Module/ModuleData/settlements.xml:2904` flavor text retained "Lady Vanyalos" after `lord_1_45_1` was renamed to Berethiel. Player sees the old name in the town_EW7 (Bar Melui / Arnach) encyclopedia entry. | Stale reference after rename | Plan-phase data-flow trace was scoped to `lords.xml + heroes.xslt + Languages/*.xml`. `settlements.xml` flavor text was outside that scope. The pattern — "rename an NPC, grep ONLY the NPC-data files for stale references" — has a blind spot for HUMAN-AUTHORED lore text that mentions characters by name. | When renaming any NPC, grep ALL of `Main/_Module/ModuleData/**/*.xml` for the OLD name (not just `characters/`, `heroes.xslt`, `Languages/`). Add this as a step in the [new-culture-authoring.md](../ai-includes/new-culture-authoring.md) flow. Memorize as `feedback_rename_grep_all_moduledata.md`. |
| 2 | HIGH (per CLAUDE.md mandatory rule) | No GitHub issue exists for the work. Closing commit would have violated the "Issue must exist BEFORE the closing commit" gate. | Process miss | Plan + implementation did not include "create issue" as a step. The Completion Workflow in CLAUDE.md is mandatory but the plan template didn't reflect it. | Add `gh issue create` as Phase-0 in any future TAOM fix plan that touches user-visible code/data. Skill `/issue` exists for this exact purpose — invoke it before implementation when the scope is non-trivial. |

## Root-cause pattern

Both findings are **scope-discipline** failures, not technical bugs:

1. F1 (settlements.xml) — the rename's "trace consumers" step was scoped narrowly. The bug class is "after renaming an entity, find every place its OLD name appears as a literal string in human-authored text." Three previous TAOM bug classes share this shape:
   - `feedback_classify_by_grep_not_by_assumption.md` (RCA shaghana/abanissa, 2026-05) — assumed an ID's culture from naming convention without grepping memory + XMLs.
   - `feedback_enumerate_from_source_of_truth.md` (RCA player-startup-gold, 2026-05) — extended a config from the existing config rather than from the upstream source-of-truth.
   - `feedback_substring_keyword_matches_external_data.md` (RCA SiegeDismount, 2026-05-06) — substring keyword matching against engine state didn't grep ALL ModuleData/*.xml for collisions.

   The current bug is a fourth instance of "scope your grep to the narrowest reasonable set and miss a wider blast radius." Generalisation: **when renaming any entity (NPC, settlement, item, faction, kingdom), the default grep target is `Main/_Module/ModuleData/**/*.xml` AND `Main/_Module/ModuleData/Languages/**/*.xml`, not just the files that directly define the entity.**

2. F2 (GitHub issue) — pure process miss. The mandatory rule lives in CLAUDE.md and is enforced only by reviewer discipline; no hook blocks the commit when an issue is absent (the CHANGELOG hook exists, but no issue-existence hook does). Out of scope for this RCA to add a hook, but worth tracking as a candidate harness improvement.

## Why each agent missed (or partially caught) these

| Agent | What it found | What it missed | Why |
|---|---|---|---|
| Agent 1 — Standards | Nothing missed; correctly passed XSLT passthrough + XML formatting | n/a (its scope is structural rules, not stale refs) | Working as intended |
| Agent 2 — Vanilla faction cross-ref | Confirmed all 11 culture flips correct, no missed Gondor lords | n/a (its scope is the vanilla `heroes.xml` faction assignments, not text in `settlements.xml`) | Working as intended |
| Agent 3 — Python script audit | Script idempotent + lambda-safe + round-robin matches plan | n/a (script-only scope) | Working as intended |
| Agent 4 — Completeness | **Caught both F1 (stale "Lady Vanyalos") and F2 (missing GitHub issue)** | n/a — this is the agent that did its job | Loc-consistency grep was widened to include settlements.xml in the prompt; the agent followed through |
| Agent 5 — Data Flow | Caught F1 independently AND raised an incorrect "lords.xslt wins" claim | Initially misclassified Fix-4 body keys as dead code, retracted after verification of SubModule.xml load order | Worked from XSLT-semantics assumption without checking `SubModule.xml` registration order. The Bannerlord modding convention is "last-loaded wins among additive XML sources" — that's load-order dependent, not XSLT-semantics dependent. **Future agent prompt fix:** when an Explore agent reasons about XSLT vs additive XML precedence, it must check `SubModule.xml` `XmlNode` ordering BEFORE asserting one source wins. |

The compound lesson is: Agent 4 + Agent 5 both caught F1, which is good — redundancy worked. Agent 5's process error (asserting "lords.xslt wins" without verifying load order) was caught only because Claude verified the claim against the user's prior screenshots and `SubModule.xml`. **If the user hadn't already shown screenshots proving lords.xml wins, the agent's misread might have prompted unnecessary lords.xslt edits.**

## Feedback memories to codify

| Memory | Status |
|---|---|
| `feedback_rename_grep_all_moduledata.md` — when renaming any TAOM entity, grep `Main/_Module/ModuleData/**/*.xml` not just the directly-defining files | TO ADD |
| `feedback_re_sub_backref_followed_by_digit.md` — Python `re.sub` with `r'\1' + digit_string` corrupts output (parses as `\10` backref); use lambda or `\g<N>` | ALREADY ADDED earlier this session |
| `feedback_submodule_xml_load_order_decides_winning_source.md` — for any XML source overlap question in TAOM, the answer is in `Main/_Module/SubModule.xml` `XmlNode` ordering, NOT in XSLT semantics. Last-loaded wins among additive sources with the same `XmlName id=`. | CONSIDER ADDING (would help future Data Flow agents) |

## Pre-existing TAOM inconsistency (out of session scope but worth follow-up)

`Main/_Module/ModuleData/lords.xslt` lines 4593-4827 have 7 NPC name overrides (Caldamir, Rúmil, Calathiel, Imloth, Anariel, Barandor, Belwen) that NEVER appear in-game because TAOM's `lords.xml` (loaded second per SubModule.xml line 123) wins. These were authored to give Tolkien-flavor names to Forlong/Hirluin/Lossarnach family members but are shadowed.

**Options for future work:**
- Update `lords.xml` to match the lords.xslt names (Brandir→Caldamir, etc.) — preserves the Tolkien-flavor intent
- Delete the dead-code lords.xslt overrides — cleanup
- Investigate whether the SubModule.xml load order was intentional or accidental

Open a separate GitHub issue if this is worth pursuing.

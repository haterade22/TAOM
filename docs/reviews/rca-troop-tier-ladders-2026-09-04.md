# RCA: the troop tier-ladder pass (#541) deep review

**Scope.** Six review agents (standards, engine compatibility, efficiency, completeness, cross-system
data flow, tooling correctness) over the uncommitted tier-ladder changeset: 14 `troops_*.xml` files
rewritten by the new `tools/fix_upgrade_armour_regressions.py`, the `UPGRADE_ARMOUR_REGRESSION`
validator gate, the Dale and Dunland Armory restats, and the docs. No C# changed. Standards,
compatibility (5 of 5 engine assumptions verified on the installed 1.4.8 DLLs), efficiency and
completeness passed. The data-flow agent raised one inconsistency that did not survive
verification. The tooling agent returned one latent HIGH, one confirmed MEDIUM and four LOW items,
all of which are fixed in the same changeset.

**Fix state.** Every confirmed finding below is fixed; the suite is 952 tests green, the validator
reports 0 errors and 0 `UPGRADE_ARMOUR_REGRESSION`, and the clamp is idempotent (0 edges, 0
replacements on a re-run).

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (latent) | `_SET_RE` in the clamp's writer had no `/>` alternation. A self-closing `<EquipmentSet ... />` civilian-template reference matched on its own `>` and would run forward to an unrelated close tag, swallowing the next set into its body. Silent today only because no troop block carries a later `</EquipmentSet>` (0 at-risk blocks repo-wide); when it does trigger the failure is an uncaught `RuntimeError` from the parse-before-write gate, not a report. | regex, writer | The sibling regex in `taom_schema.py` (`_INLINE_ROSTER_RE`) documents exactly this hazard, and the writer was authored the same day without reading it. The `.claude/rules/moduledata-validation.md` block asks for the parse gate, which was present, but not for the sibling-predicate mirror. | The alternation is ported and a test pins that a self-closing set inside a block is left intact and does not swallow its neighbour. Lesson: mirror a sibling validator's predicate, never a near-equivalent (this is already lesson "Mirror a validator's PREDICATE across languages" in `lessons/build-tooling-workflow.md`; it now has a second instance). |
| 2 | MEDIUM (confirmed) | `family()` did not know the Rivendell and Lindon token vocabulary (`tierN`, `silver`, `silvergold`, `gold`), so `rivendell_helmet_archer_tier1_silver` collapsed to itself, no candidate matched, and the fallback handed two archer troops (`imladris_horse_archer`, `imladris_outrider`, plus the Lindon twins) the parent's cavalry or infantry helmet, against the tool's own stated contract of keeping visual identity. | data heuristic | The token list was written from the Armory ids I had looked at (Rhun, Dol Guldur, Dale, Gundabad) and never checked against the 93 live items carrying `_tier`. The dry-run review looked at values, not at whether a swap crossed a mesh family. | Vocabulary extended; a test pins the elf ids; the six affected Head slots were restored to their pre-clamp items and re-picked under the corrected rule (`rivendell_helmet_archer_tier3`, same 60 armour, same family). Lesson: a name-family heuristic must be checked against the full id inventory of every culture it will run over, by counting the ids it fails to strip. |
| 3 | LOW | `demote_hero_kit` skipped silently when a troop wore only hero kit in a slot, unlike the family path which reports `UNRESOLVED`. | reporting asymmetry | Two failure paths in one tool, written at different moments, with different reporting shapes. | A `NOTE:` line is printed. |
| 4 | LOW | The script's module docstring never mentioned the unconditional DEMOTE pass, though CHANGELOG and README did. | docs | Feature added mid-session after the docstring was written. | Docstring paragraph added. |
| 5 | LOW | `write_changes` opened files without `with`. | hygiene | Copied the shape of `rebalance_troops.apply_skills_via_regex`, which has the same shape. | Fixed here; the sibling is left as is (edit scope discipline). |
| 6 | LOW | CHANGELOG said "13 troop files"; 14 were written (`troops_goblin.xml` carries one in-scope line). | count | The count was typed from the pre-apply plan table, not from `git diff --stat` after the apply. | Corrected. Evidence-over-claims already covers this: the number is read from the proving output, after it exists. |
| 7 | LOW (risk) | The clamp's `BODYLESS_BY_DESIGN` fallback set duplicates the validator's allowlist; nothing pinned them together. | drift | A fallback written for the bare-checkout case with no test. | Test added: the fallback must equal `Validator._BODYLESS_BY_DESIGN`. |

**Not a finding.** The data-flow agent reported `taom_enlistment_equipment.xml` as stale relative to
the Dale roster moves. A dry-run of `generate_enlistment_rosters.py --culture sturgia` shows the Dale
donors are the militia spearman, man-at-arms, guardsman, river warden, militia archer, longbowman,
marksman, outrider, knight and king's guard; none of the three moved troops (squire, the two militia
veterans) donates, so nothing changed. The same agent placed the `dg_uruk_*` troops in the Mordor
file; they are in `troops_dolguldur.xml`. Both were relayed as claims and checked, per
`evidence-over-claims.md` A.4.

## Root-cause pattern

Findings 1 and 2 share one shape: **a text heuristic authored against the data in front of the
author, not against the whole inventory it would run over.** The regex was fine for the rosters
read that afternoon; the token list was fine for the four cultures inspected. Both broke on shapes
that exist elsewhere in the same tree (self-closing template references in Gondor and Rhun, elf
`tierN_silver` ids). The existing lesson "Mirror a validator's PREDICATE across languages" covers
the first; the second needs its own rule, appended below.

## Why each agent missed or caught these

- **Standards** checked the I/O idiom, the parse gate and the dash ban and passed correctly; the
  regex hazard is outside its checklist.
- **Compatibility** verified the five engine assumptions the tooling encodes and was the right
  place for that; it has no view of regex shape.
- **Efficiency** found the bare `open()` (finding 5) and nothing else, correctly.
- **Completeness** enumerated the untested branches (OVERRIDES, UNRESOLVED, exit codes) and was
  otherwise clean.
- **Data flow** traced item ids, slot families, demotion scope, restat monotonicity and the MCP
  wiring, all correctly; its one inconsistency was a claim about a generated file that a dry-run
  disproved.
- **Tooling correctness** caught findings 1, 2, 3, 4 and 6. This is the agent the skill adds for
  data-mutating scripts precisely because the core five are C#-centric, and this review is another
  data point that it earns its place.

## Feedback memories to codify

One new lesson, appended to `docs/reviews/lessons/build-tooling-workflow.md`: a name-family
heuristic is validated by counting the ids it fails to strip across every culture, before it picks
anything. No new memory file; the tooling-agent requirement already lives in the skill.

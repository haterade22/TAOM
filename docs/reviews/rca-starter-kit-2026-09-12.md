# RCA: starter kit (#569), deep review of 2026-09-12

## Summary

Six review agents (standards, engine compatibility, efficiency, completeness, data flow, tooling
correctness) over the starter-kit change: two new data-mutating tools, three roster files, one
new override file, a SubModule registration and two C# test files. One HIGH finding, reproduced
live by the tooling agent, and four LOW or documentation findings. The HIGH was fixed the same
session with two tests; the data flow agent's latent gap is documented as an accepted blind spot.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `generate_starter_kit.py` derived its donor set from the roster files, and its own wiring step rewrites those rosters to the `starter_` ids, which the donor scan skipped. After wiring, a re-run planned 0 clones, and `--apply` would have collapsed the crafting-pieces marker block (39 blades), every stylesheet marker block and every `starter_kit.xml` to the empty plan; `--verify` reported no drift because it checked only the plan's own entries. | Tooling: a generator whose input is rewritten by its own second step | The 38 unit tests exercised the generator on un-wired fixtures only; the live `--apply` and the idempotency check both ran BEFORE the rosters were rewired, so the second run was a true no-op at that moment and looked like proof. The workflow in the feature doc ("re-run the generator whenever a roster gains a donor, then the wiring tool") is the exact sequence that walks into it. | `collect_donors` maps a `starter_` id back to its donor in place (the `_starter` spelling first, since the Armory already had six `*_starter` bows); `apply_plan` refuses to drop any starter id already on disk unless `--allow-shrink` is passed; a test runs the plan over rewired rosters and asserts the same clones, pieces and registrations; a test asserts the shrink refusal. Lesson appended to `docs/reviews/lessons/build-tooling-workflow.md`. |
| 2 | MED | `index_items` skipped an item file that failed to parse and said nothing; the symptom would have been a "donor not defined" abort naming the wrong cause. | Tooling: silent skip on a read failure | Same test blind spot: no fixture had a malformed sibling file. | Parse failures are collected into the plan's notes and printed; a test writes a broken sibling and asserts the note. |
| 3 | LOW | `docs/modding/party-templates.md:37` still said TAOM uses `_replaceWhileMerging` nowhere. | Docs: stale claim after a mechanism gained its first use | The doc sweep grepped for "TAOM does not use it" and found the two files phrased that way; this one said "anywhere today". | Fixed; grep for the attribute name itself next time, not for one phrasing. |
| 4 | LOW | The feature doc wrote the price as "x appearance"; the engine applies `(1 + 0.2 x (appearance - 1))` plus a flat bonus above 1. | Docs: formula simplified past correctness | Written from the shape of the formula, not its text. | Fixed with the exact term. |
| 5 | LOW (latent) | Vanilla ships `mercenary` (and battania `kern`) player-start rosters for the six overridden cultures with no TAOM override; unreachable today because TAOM's youth menu never offers those titles, and the coverage test derives its expectation from the youth menu, so a menu change without a re-run of the wiring tool would put that start back in Calradian gear unnoticed. | Data flow: an expectation derived from the same input it guards | Deliberate: the override is driven by what the menu offers, and the test mirrors that. | Recorded in the test's comment and the feature doc residuals; the failure mode is a red coverage test, which is the intended signal. Not fixed further. |

## Root-cause pattern

Finding 1 and 2 share one shape: a tool trusted the state of its inputs at the moment it was
tested, and both the test fixtures and the live proof captured that one moment. The generator was
proven idempotent on un-wired rosters; the wiring step then changed the input the proof relied on.
The rule that would have caught it at authoring time: when a pipeline has two steps and the second
rewrites what the first reads, prove idempotency of the first AFTER the second has run, and make
every writer refuse to shrink what it previously wrote unless told to.

## Why each agent missed the HIGH, and which one found it

- Standards, efficiency and completeness read the tools for convention, cost and coverage; none
  of their rule sets asks what a second run sees.
- Engine compatibility verified the engine claims the design rests on; the defect is entirely in
  tool state, not engine behaviour.
- Data flow traced ids from roster to Armory and back and found them all connected, because the
  live Armory was already populated by the first apply. It never asked how a future run would
  reconstruct that population.
- Tooling correctness ran the tool without `--apply` against the live, already-wired repo state,
  saw 0 clones planned against a 515-line marker block on disk, and reported it. This is the agent
  the deep-review skill adds only when scripts write outside the repo; it is the one that found the
  defect.

## Feedback to codify

One lesson, appended to `docs/reviews/lessons/build-tooling-workflow.md`: a generator whose
input is rewritten by its own downstream step must recover its plan from the rewritten state, and
its writers must refuse to shrink previous output without an explicit flag. No new rule file; the
tooling-correctness agent already exists and caught it, so the preventive action is the two tests
and the guard, not a process change.

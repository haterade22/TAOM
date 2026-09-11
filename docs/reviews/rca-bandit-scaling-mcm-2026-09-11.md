# RCA: bandit scaling MCM (#559), deep-review 2026-09-11

**Summary.** Five review agents on the #559 change set (166 `RequireRestart = false` flips across
four settings classes, the bandit scaling JSON and its config provider deleted, two bandit defaults
moved). Standards, compatibility, efficiency and completeness all passed; the compatibility agent
independently re-decompiled `ModOptionsVM.ExecuteDone` and confirmed the empty Cancel delegate the
whole change rests on. The data-flow agent traced 27 flows and found one gap, MED: the two hint
texts whose defaults moved did not tell an upgrading player that MCM had already saved the old
value. Fixed before commit. Full suite in an isolated worktree: 8406 passed, 1 pre-existing
Armory-data failure unrelated to this change, 2 skipped.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `BanditMaxPartiesPerHideout` (3 to 6) and `BanditInitialHideoutsPerFaction` (14 to 7) changed their compiled defaults, and the CHANGELOG said "the hints and the feature doc say so" about the persisted-value caveat. The doc did; the hints did not. A player reading only the tooltip, which is the surface most players read, had no way to learn their `TAOM.json` still held 14 / 3 and that only a group reset picks up the new values. | Docs and UI: a default change with a per-player persisted copy | The caveat was written into the CHANGELOG and the feature doc first, and the CHANGELOG sentence claiming the hints carried it was written before the hints were re-read. That is the "artifact before evidence" trap in `evidence-over-claims.md` C.1 in miniature: the claim described the intended state, not the file. It is also the second time on this exact group. The 2026-05-29 default change (caps to 100 / 3, initial 7 to 14) put an "Upgrade caveat" paragraph in the feature doc and nothing in the tooltip, and the pattern was not recognised as a pattern. | Rule: when a compiled MCM default changes, the setting's own `HintText` states that an existing `TAOM.json` keeps the old value until the group is reset. The CHANGELOG and feature doc are for us; the tooltip is for the player. Recorded in `docs/reviews/lessons/localization-ui.md` and as one line under "Doc requirement" in `.claude/rules/csharp-architecture.md` "Config Providers MUST Validate". |

## Why each agent missed it, and why one did not

- **Standards (Agent 1)** checks structure, not prose. Out of scope by design.
- **Compatibility (Agent 2)** verified the library claims. Out of scope by design.
- **Efficiency (Agent 3)** costs code paths. Out of scope by design.
- **Completeness (Agent 4)** confirmed the CHANGELOG and feature doc carried the caveat and stopped
  there; its checklist asks whether the artifacts exist and agree with each other, not whether a
  claim inside one of them ("the hints say so") is true of a third file. Same shape as the sweep
  that produced #559 in the first place: verify the branch you can see, infer the rest.
- **Data flow (Agent 5)** caught it because its brief named the three surfaces (CHANGELOG, feature
  doc, hint texts) and asked for each to be checked, so the hint text was read rather than assumed.
  The generalisation: when a claim says "X and Y both state Z", the check is to open X and Y, not to
  confirm Z is true somewhere.

## Root-cause pattern

One finding, but a repeat offender on this exact group (2026-05-29, 2026-09-11), so it counts as a
pattern: a per-player persisted copy of a compiled default makes every default change a
communication problem, and the surface where the communication has to land is the one the player
actually reads. The mod's docs and CHANGELOG are the wrong surface for that message on their own.

## Feedback memories to codify

None beyond the lessons entry. The lessons file is the always-consulted record; the rule-file line
is the enforcement point at authoring time.

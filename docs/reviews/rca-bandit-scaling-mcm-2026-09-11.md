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

## Second pass (post-fix re-run on `2652fe2f`, same day)

Six agents: the five core ones aimed at what the first pass covered thinly, plus a numeric-claims
audit that re-derived every count in the CHANGELOG, RCA, lessons, commit message and issue from the
committed files, and re-ran the suite on a clean checkout of the commit (8406 / 1 / 2 reproduced
exactly; the 1 is the pre-existing `wm_gondor_sword_a04` Armory drift). Standards, compatibility and
efficiency passed; compatibility re-verified every engine claim against the installed 1.4.8 DLLs,
including the two cited line numbers. Six findings, none behavioural, all fixed in the follow-up
commit except the first, which would need a history rewrite across two other sessions' commits.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 2 | LOW | Commit subject of `2652fe2f` is 68 characters against the 50/72 rule. | Commit hygiene | The subject was written for meaning, not measured. Two sessions have since committed on top, so it stays. | Measure the subject before `commit-tree`; the follow-up's subject is 47. |
| 3 | MED | "218 value settings in TaomSettings.cs" in the CHANGELOG, commit message and issue: the true count is 217. The 218 included the one `[SettingPropertyButton]`, which carries no value and which the posture test itself skips. | Evidence: a count with the wrong filter | The number came from a regex that included `Button` in its alternation; the test's filter excludes it. Same artifact, two definitions of "value setting". | Count with the same filter the code uses, then cite. `evidence-over-claims.md` C already says counts are stated only from evidence read this turn; the gap was that the evidence was measured with a different definition than the one being described. |
| 4 | MED | "18 tests" deleted with the config provider: the file held 16 `[TestMethod]`s. The 18 was relayed from the first exploration agent's report and never counted. | Evidence: a subagent's number relayed | `evidence-over-claims.md` A.4 and C.3 both cover this (a subagent's result is a claim to verify), and the number still went into three artifacts. | The numeric-claims audit agent is what caught it. Adding such a pass to `/deep-review` is a candidate; recorded here, not done in this change. |
| 5 | MED | `docs/features/bandit-management.md` "What gets scaled" table still said `default 14` and `default 3 = pinned at vanilla` fifteen lines above the Configuration table the same commit updated to 7 and 6. | Docs: two tables, one updated | The rewrite targeted the Configuration section by name; the doc states the same defaults twice and the second occurrence was never grepped for. | When a value changes, grep the whole doc for the OLD value before declaring it updated. |
| 6 | LOW | `BanditPartySizeCurve`'s hint promised "up to 2.5x" with no mention of the `stack.MaxValue` ceiling that `Patch39` applies per troop template, which its own doc comment names. The Density Curve hint had the equivalent caveat; this one did not. | UI hint vs mechanism | Hints were reconciled for the two knobs whose defaults changed and the master toggle; the four untouched hints were read for restart claims only. | Sentence added to the hint. |
| 7 | LOW (latent) | The posture test's allowlist was keyed on the bare property name across four classes, so a future same-named property on another class would inherit an exemption it never earned. No collision exists today. | Test design | The allowlist had three entries and one class per name, so the collision never presented. | Keyed on `Class.Property` in both the allowlist and its self-check. |

**Disputed, not a finding.** Standards flagged the committed `.cs` blobs as LF rather than CRLF. `git cat-file` shows 0 CR bytes in both the pre- and post-commit blobs: the repository normalises to LF on commit under `core.autocrlf = true`, and the diff is the 162 edited lines, not the file. The agent measured the blob against the worktree's checkout form.

**Root-cause pattern, second pass.** Three of the six (3, 4, 5) are the same shape as the first pass's one finding: a number or a default written into prose from something other than a fresh measurement of the artifact being described, and then repeated into sibling artifacts. The first pass's lesson ("open X and Y, do not confirm Z somewhere") covered the "the hints say so" claim; these show the same reflex with counts and with a second copy of a value in one file. The single rule under all four: before a number or a default goes into prose, measure it from the committed artifact with the definition the prose uses, and grep every artifact that will carry it.

## Codex pass (GPT-6-Astra at ultra, same day, on `2652fe2f` + `37411388`)

Prompt: `docs/reviews/codex-adversarial-bandit-scaling-mcm-2026-09-11.prompt.md`; raw output:
`docs/reviews/raw/codex-adversarial-bandit-scaling-mcm-2026-09-11.md`. Verdict P1 0 / P2 0 / P3 3,
every claim carrying a pasted decompile or a listed grep. It confirmed S1 (the empty Cancel delegate
and the write-through undo stack, with the qualification that the options page's own Cancel undoes
the live edit) and S4 (the 3 / 5 / 6 table is one property, not the clan's party budget, which it
derived as `H x (3 + inside)` per infested hideout), and disputed S2, S3, S5 and S6 with evidence,
including an independent inventory of all 231 value attributes that matches ours (166 flips, 3 kept).
Each P3 was re-verified here against the live map and the committed prose before being accepted.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 8 | P3 | The feature doc said the JSON "was never read" on a real install. The old provider constructor always loaded it, and its one JSON-only field, `MinPartiesToInfest`, was honoured; only the six MCM-backed values were dead. Retiring that override (constant 1 now) was a deliberate decision that the prose did not state. The CHANGELOG had the narrower, true sentence; the doc and the doc's changelog line had the absolute one. | Prose: an absolute where a qualifier was owed | The sentence was written from the MCM-shadowing finding and generalised to the whole file. The exception was in the same class, read that session, and not re-checked when the word "never" went in. | Rule in the lessons: an absolute (never, nothing, all, every) is a claim about every path; name the exception before writing it. |
| 9 | P3 | "112 hideouts on a fresh map against vanilla's 42." Both are formula products, not data. Three TAOM bandit factions own only 10 hideout locations (the doc's own wave-2 section says so), so 14 per faction fills at most 5 x 14 + 3 x 10 = 100; vanilla has five settlement-capable bandit factions (looters have no hideouts), so 5 x 7 = 35. Live map: 159 locations in all. Copied into the hint, the doc and the CHANGELOG. | Data: a per-faction target multiplied without the bound | `InitializeInitialHideouts` calls `FillANewHideoutWithBandits` 14 times and that method no-ops when no candidate remains; the multiplication assumed unlimited candidates. Vanilla's 6 came from counting `is_bandit` rows, not `can_have_settlement`. | Count the data, not the formula: enumerate the hideout locations per culture in the live `settlements.xml` and the settlement-capable factions in `spcultures.xml` before multiplying. Lesson in `data-content-cultures.md`. |
| 10 | P3 | "Turning it off stops NEW scaling only: hideouts already on the map stay until you clear them" (hint) and "switching scaling off changes nothing about a campaign already in progress" (doc). Toggling off also restores vanilla's 2-party minimum for `Hideout.IsInfested`, which is computed live, so a one-party camp stops counting as infested at once. What is true: nothing deletes a party or culls a hideout above the cap. | Prose: an absolute where a qualifier was owed | Same reflex as #8. The first review pass asked whether vanilla removes hideouts and got the right answer (it does not), and the sentence then claimed more than that answer supports. | Same lesson as #8. Hint and doc reworded to say what is restored live and what is not touched. |

Two observations, both fixed with the above: the doc's "applies for the rest of the session" is now
"in-session, until the options page's own Cancel undoes it"; and the 2026-05-29 "Upgrade caveat"
paragraph is labelled historical, since it still recommended the old 14.

**Not done, and why.** `/review-codex` Phase 3h updates `AGENTS.md`'s prior-review lessons. Another
session has that file mid-rewrite in the working tree (620 lines to 40, unstaged), so editing it now
would collide with their work. The entries to add when it settles: Codex did well to enumerate the
live map before accepting a multiplied total and to dispute the "natural attrition" hypothesis the
prompt offered rather than accept it; no false positive this pass.

**Root-cause pattern, Codex pass.** Two of three are absolutes written past their evidence (#8, #10);
the third is a total computed from a per-faction target without the physical bound (#9). All three
sit in prose, none in code, and all three survived two six-agent passes because every agent checked
the sentence against the mechanism the sentence was about and none asked what else the sentence
excluded. Codex found them by starting from the engine and the map and reading the prose last.

# Issue triage: the in-game gate retired, 2026-08-20

HEAD at verification: `4fbd543a` on `bannerlord-1.4.5`, advancing to `ee4215d5` mid-sweep as a
concurrent session committed Mordor balance work. 85 open issues against 378 closed, zero open pull
requests.

This sweep exists because of one decision by the owner: **the in-game smoke test is not going to
happen for most of the tracker, and a regression can get a fresh issue.** The 2026-08-08 sweep left
36 issues open under `triage-needs-ingame`, meaning the code shipped and only an observation was
missing. That label went from "waiting" to "closeable" the moment the gate was retired.

## Outcome

| | Count |
|---|---|
| Closed | **39** |
| Follow-up issues opened | 11 |
| Left open | 46 |

Of the 39: 29 closed outright, 10 closed with a narrow follow-up carrying the work that genuinely
remained. Eleven new issues were filed, #491 to #500 for the ten partials, plus #501 for the branch
divergence the sweep uncovered.

### Decisions that shaped it

| Question | Decision |
|---|---|
| What counts as implemented | Only `bannerlord-1.4.5`, the GitHub default branch. Work living only on `bannerlord-1.5.x` does not close an issue. |
| How a closure is recorded | An evidence comment on every closed issue naming the file and commit, then close. |
| Half-built issues | Close the stale parent, open a follow-up naming only what is left. |

## Method

Same safety model as 2026-08-08, and it earned its keep again. **No agent ever called a `gh` write
command.** Agents produced verdicts; every mutation ran from one paced script driven by a verdict
file that was read first.

1. **Classification.** Three read-only agents over the whole tracker: the 36 `triage-needs-ingame`,
   the 15 unlabelled issues filed since the last sweep, and the 34 held issues re-checked for
   anything that landed in the intervening twelve days.
2. **Adversarial refutation.** Three refuters over the 29 riskiest proposed closures, each given the
   claim but not the first pass's evidence, and told to refute rather than confirm.
3. **Re-scoring.** See below. This step was the difference between a correct sweep and a wrong one.
4. **Execution.** Paced, resumable, logged.

### The re-scoring step, and why it mattered

The refuters returned 13 refusals. **Nine were the same objection: the author's declared close gate
is an in-game observation that never happened.** That is precisely the gate this sweep was convened
to retire, so those nine were overridden by decision rather than by argument.

Four refutations were technical, and they were the point of running the pass:

| # | The refutation | Outcome |
|---|---|---|
| #455 | `MirroredWars` is assigned once at oath (`ServiceDiplomacyService.cs:69`) and never appended. Wars the commander picks up mid-service are never recorded, so `UnwindServiceWars` cannot undo them. The title symptom is live. | Stayed open |
| #392 | The sprite is packed, but only because someone downscaled the PNG by hand. `docs/reviews/lessons/localization-ui.md:203` at HEAD reads "Underlying packer bug remains open as #392." | Stayed open |
| #407 | A second `OnTournamentEnd` null path is marked in its own source comment "not guarded; it IS logged". | Closed, follow-up #491 |
| #332 | The real failure is an NRE thrown inside the parse, fixed only on 1.5.x, so the 1.4.5 guard rests on a stale premise. | **Dissolved.** The trigger, XML comments inside `notable_templates`, counts 0 on `bannerlord-1.4.5` and 3 on `bannerlord-1.5.x`, where the v1.5.0 bump introduced it and the same branch fixed it. Not reachable on the default branch. Closed. |

Two further partials surfaced by the refuters ship a named deliverable switched off: #320's shield
penetration (`shieldPenetration.enabled: false`) and #327's victory detection
(`victoryEnabled: false`). Both closed with follow-ups, #493 and #494.

## Verification traps this sweep hit

Every one of these had already produced a wrong answer in this repository before.

| Trap | How it bit, this time |
|---|---|
| **Confirm a path exists before concluding absence** | The sweep opened by reporting `Main/Features/WarOfTheRing` missing and #327 therefore unshipped. The directory is `Main/Features/WarOfTheRingMomentum/`, fully present and registered. A guessed path, exactly the mistake the last audit recorded twice. |
| **A lead handed to an agent is a claim, not evidence** | #450 was passed down as probably fixed by the polearm commits. Those fix a different defect; the 33 two-handed rosters are untouched and `audit_polearm_shield_parity.py` still reads "once #450 is closed, move TwoHandedWeapon into this set". The agent refuted the lead. |
| **Shipped is not the same as reachable** | #392 looked closeable on a `<SpritePartName>` entry. One level deeper was a manual workaround over an unfixed packer. |
| **A fix can be branch-scoped in both directions** | #332's refutation was true on 1.5.x and irrelevant on 1.4.5. #371's guard is real and absent from the default branch. |
| **File existence is not wiring** | Every closed feature was checked for registration. Four symbols with zero direct references (`CastleNotableMaintainer`, `TownLeavePolicy`, `BattleRenownPolicy`, `ServiceDiplomacyService`) were traced to registered feature IoC modules before their issues closed. |
| **Counts in issue bodies drift** | #341 says "59 troops across 12 cultures"; the real spread is 11 files. #344 names 5 troops; 6 were fixed. Both verified by parsing, not grepping. |

## What stayed open, and why

46 issues:

- **31 not shipped.** Nothing was built. Includes #275 (Music, on an unmerged branch), #421 (CI still
  verifies nothing), #450 (the 33 two-handed rosters), #393, #396, #408, #448.
- **8 need a decision, not code.** #111, #118, #343, #345, #419, #431, #438, #82.
- **2 shipped on 1.5.x only**, held by the branch rule: #371 and #481.
- **5 held by the refutation pass or their own evidence:** #455 and #392 above, plus #349 and #415,
  where the author's own text says the reported symptom is unreproduced and unfixed, and #420, where
  only 3 of 8 rows were corrected and the premise then got worse.

### Mislabelled, not corrected

Four issues carry `triage-needs-ingame` but are not waiting on an observation at all: #393, #396,
#408 and #448 are each unstarted by an explicit deferral in their own body. #480 is the inverse, a
genuine in-game task with no code half to verify. The labels were left alone rather than quietly
rewritten, because relabelling was outside what this sweep was asked to do.

## Findings worth more than a tracker edit

- **#501, filed.** The two release lines have diverged 11 commits each way. `0f1488b4`, the only
  thing that actually blocks a bad Dependencies pairing, exists on `bannerlord-1.5.x` alone, so the
  default branch is still exposed to what #371 describes.
- **#421 is worse than filed.** Run `32421646664` on 2026-08-20 shows `Build & Test -> skipped`. The
  entire v1.5.0 bump merged with no build or test gate.
- **#359 is regressing.** `tools/check_prefab_budget.py` reads 93,830 entities against a global
  engine cap of 131,072, up from 93,407 twelve days ago, and it counts one module of several.
- **The engine pin is unsettled.** The live install reports v1.4.8 for both the client and the world
  editor, which contradicts the 1.5.x branch's claim of a v1.5.0 beta. That decides whether #485 is
  still reproducible.

## Full disposition

**Closed outright (29):** 97, 285, 291, 302, 317, 321, 325, 329, 332, 335, 337, 339, 340, 341, 344,
346, 351, 353, 354, 360, 370, 432, 433, 434, 452, 453, 454, 456, 462.

**Closed with a follow-up (10):** 251 (#496), 278 (#497), 320 (#493), 327 (#494), 357 (#498),
369 (#499), 376 (#492), 380 (#500), 407 (#491), 443 (#495).

**Held open (46):** 12, 62, 82, 89, 111, 117, 118, 120, 275, 296, 318, 319, 343, 345, 347, 349, 359,
371, 385, 392, 393, 396, 398, 408, 415, 419, 420, 421, 422, 431, 438, 439, 446, 448, 450, 451, 455,
457, 460, 477, 478, 479, 480, 481, 482, 485.

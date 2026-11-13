# Deep review: plan 050, messenger arrival classification (2026-11-13)

```
DEEP REVIEW REPORT
===================
Feature: Move the messenger arrival verdict out of MessengerCampaignBehavior into
         MessengerService.ClassifyArrival (improve plan 050, Refs #1132)
Date: 2026-11-13
Range: 664f19c5b..0848c47e2 (branch improve/050-messenger-arrival-classification), then the
       review follow-up commit on top of it

Scope:   C# (Main/Features/Messengers, 7 files), tests (2 files), the entry-point line baseline,
         docs/features/messengers.md. No XML, no hooks, no validators.
Blast radius: callers outside the diff checked by the lenses: the client mixin
         Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs (calls GetOffer, GetReason,
         SendMessenger, all unchanged) and TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs
         (constructs the behavior, returns at the authority gate; not edited). IMessengerService has
         one implementer. No hit for the messenger type names in the bin folders of TAOM.CoopCompat,
         Coop, AWOIAF, Bannerlord.Diplomacy and DualWield (Lens 5).
Waves:   one wave: Agent 2 (Engine compatibility) and Agent 5 (Data flow). Codex not run (out of
         usage); no convergence pass was needed because Step 4 applied no improvement.

STANDARDS:     NOT RUN (no report supplied for this tier)
COMPATIBILITY: PASS: 0 incompatible, 0 unverified (17 API usages verified, 8 engine claims verified)
EFFICIENCY:    NOT RUN (no report supplied for this tier)
COMPLETENESS:  NOT RUN (no report supplied for this tier)
DATA FLOW:     FAIL then fixed: 0 gaps, 1 confirmed inconsistency (MED, fixed), 1 optional LOW
               design note (not applied), 1 pre-existing MED (FOLLOW-UP)
DESIGN:        NOT RUN (no report supplied for this tier)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## DETAILS

### Agent 2: Engine compatibility

No incompatibility and no defect. The lead re-checked the load-bearing claim against the installed
build: `pwsh tools/taom-src.ps1 path Hero` (cache v1.5.7-202611101605) shows `Hero.CharacterStates`
at Hero.cs:28-38 as NotSpawned, Active, Fugitive, Prisoner, Released, Dead, Disabled, Traveling, in
the order of `Main/Features/Messengers/Domain/MessengerHeroState.cs:9-16`. `IsAlive => !IsDead`
(Hero.cs:319), `IsNotSpawned` (:313), `IsWanderer` (:330) and `IsChild` (:490) read as the lens
quoted. The orchestrator's four questions:

1. **Equal verdict for every state.** Yes. Over Hero.cs:303-319 the old lost test reduces to
   `!(Active || (wanderer && NotSpawned))`, which is `reachable` at `MessengerService.cs:81-82`; on the
   path that is not lost the old wait test reduces to `!playerAvailable || IsChild || inMapEvent`
   (`:89`). The order lost, confused, wait, ready is kept (`:83-92`). The 256-cell test
   `ClassifyArrival_EveryInputCombination_MatchesTheRuleTheBehaviorExecuted` holds it.
2. **The reads that now run up front.** Every one is a getter with no write, for a dead, disabled,
   fugitive or partyless hero too; the army calls in `MessengerTargetReader.cs:35-37` run only when the
   target has a party, and `Army.LeaderParty` is set only by the constructor or the loader. The lead
   also read `IsPlayerAvailable` (`MessengerCampaignBehavior.cs:617-623`): getters only.
3. **The by-value cast.** One to one in the installed build. The `RequiresGame` parity test
   `MessengerHeroState_MatchesTheEngineCharacterStates_ByNameAndValue` fails on a rename, reorder,
   added or removed member, or renumbering; only the game-backed suite runs it.
4. **Latch and texts.** `_processingArrivedMessenger = true` is set only at
   `MessengerCampaignBehavior.cs:306`, after the non-Ready return; no added or removed line holds a
   `{=taom_` key.

FOLLOW-UP from this lens: a non-wanderer target that is Released or Traveling on arrival is also Lost,
although the engine moves both states back to Active. This widens #1133, which names only prisoners
and inactive wanderers.

### Agent 5: Data flow

12 flows traced. Flows 1, 2, 4, 5, 6, 7, 9, 10 and 11 CONNECTED.

- **Flow 3 (MED), CONFIRMED and fixed.** The commit body's `Not-tested:` trailer says "a source pin
  holds their text" for the raw party equality, the map event and `IsPlayerAvailable`. The pin
  `TheArrival_AsksTheServiceForItsVerdict_WithTheRawPartyEqualityAndTheEngineState` asserted only the
  service call, the party equality, the absence of `IsInPlayerParty` and the reader cast. The lead
  proved the gap by mutation: with `targetPartyInMapEvent: target.PartyBelongedTo?.MapEvent == null`
  (the battle test inverts: a messenger waits while its target is out of battle and is delivered
  while the target's party is in one) and with `playerAvailable: true`, the old pin would stay green. Two
  assertions were added (`MessengerCampaignBehaviorSourceTests.cs:153-154`); each mutation then failed
  the pin ("Failed! - Failed: 1, Passed: 13, Skipped: 0, Total: 14" both times), and the production
  file was restored by hand edit (`git diff Main` empty afterwards).
- **Flow 8 (LOW, optional design).** CONFIRMED as an observation, not a defect: production reads only
  `Active` and `NotSpawned`, and the snapshot holds the state twice (the booleans and `HeroState`).
  The lens's alternative (one `isNotSpawned` bool from `Hero.IsNotSpawned`, Hero.cs:313) is behaviour
  preserving. NOT APPLIED: see Step 4.
- **Flow 12 (pre-existing MED), FOLLOW-UP.** While the player is a prisoner, `Hero.MainHero.PartyBelongedTo`
  is null, so the raw equality is true for any reachable target with no party, and the paid messenger
  is dropped as Confused instead of waiting. The lead read `TakePrisonerAction.ApplyInternal` (it removes
  the hero from the party roster); that the main hero's `PartyBelongedTo` ends null rests on the lens's
  Hero.cs:2235 and :2241 citations, not re-read. Behaviour is identical at the base; this branch only
  pins it.

## ACTION ITEMS

1. Done: the source pin now asserts the `playerAvailable` and `targetPartyInMapEvent` arguments, so the
   `Not-tested:` trailer of 0848c47e2 is true from the follow-up commit on.
2. For the maintainer: widen #1133 to non-wanderer Released and Traveling targets.
3. For the maintainer: file the prisoner-player Confused case as a bug lead (an in-game repro is owed).

## IMPROVEMENTS (Step 4)

Agents 3 and 6 did not run, so there were no APPLY-scoped or KEEP proposals. The one design note came
from Agent 5.

- APPLIED: none.
- NOT APPLIED: `Main/Features/Messengers/Domain/MessengerHeroState.cs`, `HeroSnapshot.cs:30-31`,
  replace the mirror enum with one `isNotSpawned` bool. Behaviour preserving, but plan 050 (the binding
  contract) names the enum, the by-value cast and the parity test, and the lens rated it optional. The
  enum also gives the 256-cell oracle real engine states. Simplicity: a small win (one file, one
  `RequiresGame` test) against a rewrite of the reviewed test oracle at review time.
- FOLLOW-UP: the #1133 scope (Agent 2), cites #1133. The prisoner-player Confused case (Agent 5 flow
  12): no issue filed, because this run takes no GitHub action; it needs the maintainer's word.

## VERIFICATION

- Before any edit, at 0848c47e2: `Passed!  - Failed:     0, Passed: 39278, Skipped:     9, Total: 39287`.
- After the fix, full suite: `Passed!  - Failed:     0, Passed: 39278, Skipped:     9, Total: 39287`
  (the two assertions sit in an existing test method, so the count is unchanged).
- `python tools/lint_docs.py --fail-on-drift`: exit 0.
- Not run: the RefAsm no-game step (the added assertions read source text, no engine type), the hook
  suite (no hook touched), any in-game check (the game install is held by another session).

VERDICT: READY FOR COMMIT

## CODEX REVIEW

Codex not run (out of usage, 2026-11-12). The two lens reports stand in for it. Phase 3d table over
the lens findings:

| # | Source | Reported severity | Lead severity | Agree? | Reason |
|---|--------|------------------|---------------|--------|--------|
| 1 | Lens 5 flow 3 | MED | MED | Yes | Two mutations of named boundary arguments passed the old pin |
| 2 | Lens 5 flow 8 | LOW (design) | LOW (design) | Yes, not a defect | Behaviour preserving; the plan chose the enum |
| 3 | Lens 5 flow 12 | MED (pre-existing) | MED, PLAUSIBLE | Yes | Unchanged since the base; owed to the maintainer |
| 4 | Lens 2 #1133 scope | FOLLOW-UP | FOLLOW-UP | Yes | Same Lost rule at the base; decided out of scope |

- Confirmed bugs: row 1 (fixed in the follow-up commit).
- False positives: none.
- Design questions: row 2 (not applied, reason above).
- Things the lenses found outside the change: rows 3 and 4.

### Codex lessons (pending)

None: Codex did not run, so there is no false-positive pattern or missed category to record.

## Convergence round 1

Scope: `0848c47e2..3014400f8`. Two LOW findings, both checked against the code and the repo rules.

1. **The follow-up commit's type (LOW): NOT FIXED, left to the maintainer at merge.** CONFIRMED.
   `3014400f8` changes one test file and four `docs/reviews` files, and
   `docs/ai-includes/git-and-commits.md` gives those paths `test:` and `docs:`. As `fix(messengers)`,
   `tools/changelog_from_commits.py` would list it under "Fixes" although its body opens with "No
   change in game". The fix is a message reword of that commit, which rewrites the branch, and this
   pass may not rewrite history. Suggested reword at merge, with the body and `Refs #1132` kept and
   "would make every messenger wait for ever" replaced by the wording of finding 2:
   `test(messengers): v2.0.38 - Pin the arrival's player and battle reads`. The convergence commit
   that adds this section is typed `docs:` for the same reason.
2. **The `MapEvent == null` mutation's effect (LOW): FIXED in the convergence commit that adds this
   section.** CONFIRMED. `MessengerService.cs:83-87` returns Lost and Confused before the wait test at
   :89, so the mutated argument cannot hold those messengers. With the mutation, a target whose party
   is in a battle gets Ready at :92 and the behavior shows the "Messenger Arrived" inquiry
   (`MessengerCampaignBehavior.cs:306-312`), and only a target out of battle, or with no party, waits.
   The lesson (`lessons/testing-qa.md`) and this record (Agent 5 flow 3) now say the battle test
   inverts. The optional part of the finding (no full-suite log for "left all 39,278 tests green")
   is now evidenced, so the lesson keeps that wording: with the old pin restored (the two assertions
   removed), each mutant was run alone over the full suite and each gave
   `Passed!  - Failed:     0, Passed: 39278, Skipped:     9, Total: 39287`. Both files were then
   restored by hand edit and `git status` was clean.

False positives: none.

Verification for this round: the full suite before any edit at `3014400f8` gave
`Passed!  - Failed:     0, Passed: 39278, Skipped:     9, Total: 39287`; the result after the edits is
in the convergence commit body. No gate changed, so no differential sweep ran.

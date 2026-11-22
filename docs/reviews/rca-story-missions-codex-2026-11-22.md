# RCA: Codex review of Story Missions slices 2, 3 and 5 (#1105, 2026-11-22)

## Top-line

The maintainer asked for the Codex pass on `feat/story-missions-fellowship` (`5cf79902c..6c5ff7c5a`, 10 commits). Codex ran at `ultra`
from the worktree and read the installed v1.5.7 DLLs. It reported one HIGH, three MEDIUM and three LOW findings and disputed five of
the nine suspects with evidence. Each finding below was read against the TAOM source and the engine source before any edit. Raw report:
`docs/reviews/raw/codex-adversarial-story-missions-1105-2026-11-22.md` (not tracked); prompt:
`docs/reviews/codex-adversarial-story-missions-1105-2026-11-22.prompt.md`.

## Findings

| # | Codex | Mine | Finding | Verdict |
|---|---|---|---|---|
| F1 | HIGH | HIGH | `ChronicleService.ResolveHero` used the answer of `KillHeroInBattle` for a log line only. After Deferred (his party in a battle or a siege) or Refused (`Hero.CanDie`: an issue or quest veto) the Breaking resolved `Occurred`, paid its effects and told the player that Boromir died, while he lived. With the life and death cycle off, a mark made in a siege has no promised end. | Confirmed against `KillCharacterAction.ApplyInternal` (:34, :51), `Hero.CanDie`, `AgingCampaignBehavior.DailyTickHero` (:104, :110). Fixed. |
| F2 | MEDIUM | Decided: kept | A repulsed assault resolves the Hornburg `Held`, and the existing `ents` event (`requiresOutcome: Held`, two days later) then lowers Isengard's volunteer rate by 15% for 20 days. The maintainer's word for slice 5 was "no other real penalties for Isengard". | Confirmed in `war_chronicle.json`. Not changed: the maintainer took the builder's recommendation on 2026-11-22, and the Ents stay. |
| F3 | MEDIUM | LOW to MEDIUM | `TaomPartyMoraleModel` runs on a BannerlordCoop client, whose effect registry is the join snapshot with no expiry and no updates. | Confirmed by reading `WarChronicleBehavior` and `WarEffectService`. Documented as a known limit; no code change, on the maintainer's word of 2026-11-22. The visible effect in a live session is UNVERIFIED. |
| F4 | MEDIUM | MEDIUM | The scenario judge and three manifest rows took any verdict with any reason, and any `offer.result result=pass` line. A log with `verdict=Won ... reason=HeroFell` and `result=Occurred` for a won defence passed. | Confirmed. Fixed. |
| F5 | LOW | LOW | The judge read `field` and `mustLive` from the checkout after the run, against its own rule that the launch reads the mission once. | Confirmed. Fixed. |
| F6 | LOW | Declined | The validator accepts an `endAlliance` pair with no Permanent row, and a hero id no campaign holds. | True for a malformed file. The shipped file is gated by `ShippedStoryOffersTests` (the pair is a Permanent row; the hero id is pinned), and a wrong pair writes the `alliance-ended` fail line. A runtime guard for a file only the repo ships is a speculative guard (simplicity criterion). |
| F7 | LOW | LOW | Three texts said the Hornburg offers no attack; the feature doc and two comments promised that the engine finishes a death mark; "+5 points" ignored the factors of the morale number. | Confirmed. Fixed. |

Disputed by Codex with evidence, and not changed: a null killer is safe (the engine passes one itself); the order "record the break, then
end the alliance" is right and no shipped path re-forms the pair; the effect registry and the broken-pair set are safe for worker
threads; `1e12` survives the codec, the expiry and the texts; no unarmed session reaches the scenario driver. Field mode has no confirmed
defect, and Codex marked its native side UNVERIFIED.

## Root causes

| # | Bug | Category | Why missed | Preventive action |
|---|-----|----------|-----------|-------------------|
| F1 | A death the engine did not grant was announced and paid | Missing vanilla gate | The builder and two lenses read `ApplyInternal` to the death mark and stopped. Nobody read the two completion paths to their conditions, and the test `AKillTheEngineDefers_StillResolvesTheEvent` wrote the wrong behaviour down as intended. | The event waits (`ChronicleHeroStoryResult.DeathOwed`) and asks again each day; seven tests in `ChronicleHeroStoryTests` and `StoryOfferServiceTests`. Lesson in `lessons/adapters-taleworlds-api.md`. |
| F4 | A judge accepted lines no correct build writes | Logic error | Each pass pattern was built to match every good line; nobody asked which bad lines it also matches. Two of the judge's own fixture logs held a contradiction. | `story_ended_pass` and `offer_result_pass` write the pairs out from `StoryVerdict.Of` and `StoryOfferService.Apply`; `VerdictConsistencyTests`. Lesson in `lessons/testing-qa.md`. |
| F5 | The judge re-read the checkout after the run | Stale state / lifecycle | Slice 2 added two traits beside the side and did not extend the launch snapshot that the side already had. | The launch captures both; a test patches the reader away. |
| F7 | Docs behind the code, and an engine promise the source does not make | Convention inconsistency | Slice 5 changed what the Hornburg offers and three older sentences were not searched for. The death-mark sentence came from a summary of the engine, not from its `return` conditions. | Corrected; the lesson for F1 covers the second half. |

## Open

- F2 and F3 are decided (the maintainer, 2026-11-22: the builder's recommendations stand). The Ents march after a repulsed player
  assault as after every other held Hornburg: it is the book's consequence of the defeat and it ends after 20 days. The co-op client
  limit of the morale effect stays as documented in `docs/features/war-chronicle.md`.
- The fix for F1 ran in the game on the merged build `b8fcea30e` (2026-11-22): the Breaking offer with a lost defence
  (`E:/Temp/scenario-runs/20261122-085433`) and by fate (`-090216`), both `lord_1_75: Killed` and `Occurred`. In such a run Boromir
  is free, so the runs prove the Killed path. The owed path has unit tests only: it needs Boromir in a battle or a siege on the day.

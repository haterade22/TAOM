# RCA: Tournament Rewards, reviewed after it shipped (2026-10-04)

**Scope:** Tournament Rewards ([tournament-rewards.md](../features/tournament-rewards.md): the MCM bet cap, renown
and influence scaled by the field and the winner's culture, the prize and the skill chosen at Join). Its code reached
`bannerlord-1.5.x` inside Mike's mixed commit `9e2a39f4` (2026-10-02, "Add comprehensive localization and mesh
weight transfer tools"), whose message names neither the feature nor its review state. It shipped in the 2026-10-02
testing build, untagged and numbered v2.0.33, and was tagged in v2.0.34, before any review. Seven lenses (Opus 5.5
at max effort) in two waves plus one adversarial checker (workflow `wf_614275cb-eec`); the same run carried the troll-gear
convergence pass, recorded in [rca-troll-gear-tournament-prizes-2026-10-02.md](rca-troll-gear-tournament-prizes-2026-10-02.md).
The checker confirmed 27 of 52 findings (troll-gear pass included), folded 24 as duplicates and left 1 unverified.
No CRITICAL or HIGH in the feature: two MEDIUM, the rest LOW or INFO.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| T1 | MED (TR1-1, TR4-3, TG-5) | `TournamentService.PrizeChoices` read `Game.Current` and `ItemObject.Tier` inside a service and chose the prize band in an inline ternary no test reached | ADR-007, ADR-008, testing | The decision went into a method whose test header already excused its neighbour as game-only; the troll-gear review's finding 2 named this shape in the same file family the same day (repeat) | Pure `TournamentPrizeRules.AdvertisedBand`, pinned over every armour class and null; the Join snapshot carries the tier; the test header names `PrizeChoices` |
| T2 | MED (TR7-1) | Blue Craig, the third goblin kingdom, has no row in `tournament_rewards.json`, so its winners get the neutral 1.0 instead of the orc factors | Culture group | The orc row was listed from memory, and the shipped-config test pinned the same list, so data and test shared one source | Mike: add the row; done (`bluecraig`, its shipped-config DataRow); lesson below |
| T3 | LOW (TR5-2, TR1-3, TR2-8, TR4-5) | MCM values reached the rules unclamped; a hand-edited multiplier past int range cast to `int.MinValue`, which the influence action would have added to a clan | Config validation, float-to-int cast | The provider deferred validation to the rules ("validated where they are used"), which bounded the factor below only; the cast category in `csharp-architecture.md` (finite values past int range) was not walked | `SettingClamp` in the provider, `MaxMultiplier` and `MaxBetSetting` shared by the slider attributes and the rules, a range check before every cast, provider tests |
| T4 | LOW (TR2-1, TR5-3, TR3-1, TR6-4) | `GetRenownReward` has a third engine caller, the new-game leaderboard seeding with a null town, 500 calls, which the override scales | Engine callers | The doc said the model is asked "twice", from the two call sites the author knew; a GameModel override's callers were not listed from the decompile | Mike: vanilla at world generation; done (`RenownReward_WithoutATown_IsVanillasAnswer`); lesson below |
| T5 | LOW (TR2-3, TR1-2, TR4-2) | Patch96's binding pins ran in neither binding gate, failed in isolation without SandBox loaded, and the private `TournamentGame.Prize` setter was not in the reflection-site catalogue | Test categories | The binding class was written from a `RequiresGame`-only template | `BindingVerification` shape, the `ReflectionSiteBindingTests` row and the catalogue row; the binding gate passes 464 |
| T6 | LOW (TR1-4) | Two Patch96 catch-alls swallowed every exception with no trace | Patch hygiene | Repeat of `lessons/harmony-il.md`: a catch in a patch needs a log line; "vanilla's cap stands" was taken as enough | The bet postfix warns once; the hero-count prefix is gone (T13) and its replacement logs |
| T7 | LOW (TR4-1, TR5-5) | No wiring tests, so the one shared `TournamentRewardsService` the patch, the model, the Join service and the award all rely on was unpinned | Testing | Nine of ten feature modules have wiring tests by convention, none by rule | `TournamentRewardsWiringTests` resolves a real container graph |
| T8 | LOW (TR1-6, TR4-4) | Untested rules: the empty-key and null-row rules, two skip guards, the influence gates; two keys normalising to one culture overwrote each other silently | Testing, validation | "One test per validation rule" was not walked rule by rule | Tests; a duplicate key now warns and marks the file rejected |
| T9 | LOW (TR2-4, TR2-2) | Engine claims in the doc and comments were wrong, among them: tournaments do train the skills used, at a third of the battle rate; the prize roll is an abstract override; only the tournament UI calls `GetMaximumBet`; the join menu re-rolls only on a changed hero count; vanilla adds 1 unscaled influence for a player winner | Engine claims in text | Written from the methods the feature touched, not their callers and siblings | Corrected with citations, `arena.md` included |
| T10 | LOW (TR2-5, TR5-4) | "+{XP} experience" prints the raw XP; the engine scales it by the hero's learning rate | Player text | `HeroDeveloper.AddSkillXp`'s scaling was not read | Mike: reworded before #731; done |
| T11 | LOW (TR7-2, TR1-7, TR5-8, TR4-9) | "offers three prizes" while the dialog can show two | Player text | Text written for the common case | Mike: reworded before #731; done |
| T12 | LOW (TR1-5, TG-4) | `PreferCulture`'s summary, which says why the list must never be empty, sat on `ChoiceCount` | Comments | Members inserted between a summary and its method | Moved back |
| T13 | LOW proposal (TR6-1) | The hero count came from a Harmony prefix on a private engine method the feature already observed through a public campaign event | Patch surface | Reuse rung 1 (an engine event) was skipped | The behaviour notes the count from its own listener; the ordering argument is cited line by line and a binding test pins the head-first invoke |
| T14 | LOW proposal (TR6-5) | `Influence` took the engine's int as a float and guarded it with a finiteness check that could never fail | Dead code | Copied from the float-shaped renown path | Takes and returns int |
| T15 | LOW (TR4-7) | No INDEX entry, a stale `gamemodels.md` row, a Tests section that overclaimed | Docs | Mixed commit, no docs pass | Added and corrected |
| T16 | LOW proposal (TR1-8) | Renown and influence take two hops (model, Arena service, rewards service), and Arena now depends on TournamentRewards while TournamentRewards depends on Arena | Design | Built on the existing seam | Pending (it touches `SubModule.cs`) |
| T17 | LOW proposal (TR5-1) | The prize alternatives draw from the whole band, not the engine's hero-count value window, so small tournaments usually offer a dearer pick | Design | The window was not read | Mike: keep it; documented in the feature doc |
| T18 | INFO (TR5-7) | At the default "unlimited", the tournament screen tells the player they can wager 50,000,000 | Player text | The ceiling surfaces through vanilla's text | Documented |
| T19 | INFO (TR4-10, TR7-4) | `CHANGELOG.md` has no entry for the feature | Changelog | The mixed commit's body describes other work | The fix commit's body carries the player paragraph |
| T20 | UNVERIFIED (TR2-7) | Vanilla's Join now runs inside the skill dialog's callback; whether the Join hotkey reaches the menu while a dialog is open is untested | Engine behaviour | Not readable statically | In-game check |
| T21 | INFO (TR5-6) | A parked module would leave the renown and influence formulas running through Arena | Design | Not parked today | Gate both to vanilla in the change that parks it |

## How it shipped unreviewed

The session that built the feature left a memory card saying "built, UNCOMMITTED, OWED: /deep-review". Mike then
committed the working tree as one mixed commit; the card went stale the moment it landed, and nothing in git said a
feature without a review had reached trunk. Before the v2.0.33 cut, this session told Mike the nine `taom_tr_*`
strings were "unused by shipped code", trusting that card instead of grepping the code; a grep at release time
found `FeatureModules.cs:23` registering the module, and Mike chose to ship it and review after. The Mouth of
Sauron's gear took the same road in `799e189e` ([rca-mouth-of-sauron-gear-2026-10-04.md](rca-mouth-of-sauron-gear-2026-10-04.md)).

## Patterns

1. **Engine facts written from the methods touched, not their callers** (T4, T9, T10, T20): a third caller of the
   model, vanilla's own influence and XP paths, the join callback's context.
2. **Tests drawn from the same source as the thing they check** (T2, T5, T8): the culture list, a binding class
   outside the gate, rules nobody counted.
3. **A label or a comment trusted in place of the code** (T1, T3, the release statement above): "game-only",
   "validated where they are used", "UNCOMMITTED".

## Why each lens caught or missed what it did

- **Standards (1)** found T1, T3's validation half, T6, T8 and T12; it does not open the engine.
- **Engine compatibility (2)** found T4, T5, T9, T10 and T20 from the v1.5.3 decompile; every member the code calls
  resolves.
- **Efficiency (3)** found no hot path (every new path runs per click, round or tournament) and joined T4.
- **Completeness (4)** found T5, T7, T8, T15 and T19.
- **Data flow (5)** found T3's overflow, T17, T18 and T21.
- **Design (6)** proposed T13, T14 and T17; the checker kept them under the simplicity criterion.
- **XML (7)** found T2 by reading the cultures the kingdoms carry, and T11.

## Applied, pending, follow-up

- **Applied** (fix agents, then this session's own runs: build 0 errors; `TAOM.Tests` 14,042 passed, 1 failed, the
  #731 translation rows, 5 skipped; the binding gate 464 passed): T1, T3, T5 to T9, T12 to T15, T18.
- **Decided by Mike (2026-10-04) and applied:** T2 (Blue Craig gets the orc factors), T4 (a null town keeps
  vanilla's renown), T10 and T11 (both dialog texts reworded), an elite, lord or named advertised prize draws
  heavy-band alternatives, T17 (kept and documented). T16 stays a follow-up (it touches `SubModule.cs`).
- **Follow-up:** T20 (in-game), T21 (when parked). Issue #732, filed and closed with `triage-needs-ingame`.
- **Convergence pass** (one `deep-reviewer` on the applied fixes): every applied finding resolved, the listener-order
  argument re-traced line by line; two LOW stale comments it found (the model still credited the removed prefix with
  the hero count, and the band rule gave an old reason for a null tier) are fixed. CI's reference-assembly steps,
  run locally: the binding gate 414 of 414; the unit step fails the same 13 tests by name as a clean checkout of
  `18a4e402`, with the 64 new tests passing.

## Feedback to codify

- `lessons/gamemodels-services.md`: before scaling a GameModel answer, list every engine caller from the
  decompile, including world-generation calls with null arguments (T4).
- `lessons/data-content-cultures.md`: a culture group in a config comes from the culture data, never from memory
  (T2).
- `lessons/build-tooling-workflow.md`: a memory card's "UNCOMMITTED" is a claim about the past; before saying what
  ships, grep the code at the commit being released (the release statement).

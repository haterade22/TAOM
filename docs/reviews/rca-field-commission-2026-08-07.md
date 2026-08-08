# RCA — Battlefield Promotions: the offer flow, not the companion (2026-08-07)

**Issue:** [#415](https://github.com/haterade22/TAOM/issues/415) · **Feature:** [#376](https://github.com/haterade22/TAOM/issues/376) Field Commission · **Donor:** `TAOM_Promoted` / RF_Promoted

## Top line

Players reported two things about promoted companions: **no dialogue / cannot talk to them**, and
**crash or freeze when interacting with them**. A 28-agent adversarial investigation over the feature
and the 1.4.7 decompile **could not reproduce either symptom**, and disproved the most attractive
hypotheses one by one. Eleven separate defects survived refutation — none of them a crash, none of
them a mute companion.

The single most likely thing a player would *describe* that way is finding #1: a won battle could
raise dozens of consecutive game-pausing modal prompts with no cap and no way to stop being asked.
That is a plausible read of "I can't deal with my promoted companions" and it is fixed. But it is an
inference, and this document says so rather than dressing it up as the root cause.

**The honest state: the reported symptoms are unexplained.** The feature had never run in a live game
(`docs/features/field-commission.md` said so, and it shipped anyway — the same pattern as #406). The
remedy shipped here is a diagnostic trace behind an MCM switch, so the next report arrives with the
answer in the log instead of costing another round trip.

## What was disproved

Worth recording so these are not re-derived. Each was checked against the installed-DLL decompile:

- **Promoted companions are talkable.** They take the `MainPartyCompanion` branch of
  `DefaultHeroAgentLocationModel.GetLocationForHero`; `AddCompanionAction` sets `CompanionOf`, and
  `Hero.Clan => CompanionOf ?? _clan`, so `Clan`, `MapFaction` and `IsPlayerCompanion` all resolve.
  The recent named-companion null-`MapFaction` bug does **not** apply to them.
- `HeroCreator.CreateSpecialHero` with a null born settlement does not NRE — it falls back to
  `HeroCreationModel.GetBornSettlement`, which picks a random culture-matched town.
- `Hero.Occupation` is `[SaveableProperty(780)]`, so `Occupation.Wanderer` survives save/load and
  `LordConversationsCampaignBehavior.UsesLordConversations` keeps returning true. The priority-0
  `default_conversation_for_wrongly_created_heroes` line ("I am not allowed to talk with you") is
  **not** reachable for these heroes.
- `CharacterObject.GetPersona()` never returns null; `Hero.Template` is non-null for
  `CreateSpecialHero` heroes; `TaomAgeModel` does not move `HeroComesOfAge`, so
  `InitializeHeroDeveloper()` does run.
- Eight further crash hypotheses (action-set derivation, race-monster lookup, a divide-by-zero armed
  by save/load, `HideInquiry` teardown, nested `pauseGameActiveState`, upgrade-target re-derivation,
  wanderer-backstory template keying, two promotions colliding on a shared template key) were each
  refuted with engine evidence.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `EndBattle` queued `min(rosterCount, merit/threshold)` offers for **every** troop type that scored a kill, uncapped, each shown as a separate game-pausing modal one per tick. The donor capped it at one per battle | Donor-parity drop | The donor's cap lived in an `AllowMultiplePromotions` flag the port dropped as YAGNI; nobody asked what the flag's *default* behaviour was | `maxOffersPerBattle` (default 1) + MCM slider + 4 tests. Lesson: when dropping a donor knob, port its DEFAULT as a constant |
| 2 | HIGH | `_pendingOffers` is process-lifetime singleton state, unpersisted and cleared nowhere — an offer queued in one campaign surfaced in the next save loaded in the same session | Lifecycle / singleton scope | `ClearState()` was written against the *persisted* fields; the transient queue was invisible to that framing | Cleared at every session boundary ahead of the load guard; `FieldCommissionSessionReset` names the save-scoped vs process-scoped split |
| 3 | HIGH | `CreateCompanionFromTroop` had no roster precondition; if the last of a type died between offer and answer the hero was created anyway, merit deducted, and `RemoveOneFromRoster`'s `false` discarded | Donor-parity drop + ignored return | The donor's guard was read as redundant with the offer-build gate; the two are separated by an unbounded player-response window | Precondition restored at the adapter; decrement failure logged |
| 4 | HIGH | **Introduced by this changeset.** The decline mark is an absolute merit level, but `TransferMerit` / `_merits.Remove` dropped the merit without dropping the mark — so a type whose bank moved to an upgraded heir could never be offered again | Parallel state not kept in sync | I added a second dictionary keyed on troop id and did not audit the existing removal sites for it | `ForgetTroop()` is now the single removal point for both; regression test |
| 5 | MED | `IsHeroAliveAndValid` used `MBObjectManager.GetObject<Hero>`, which cannot resolve a runtime-created hero — `CampaignObjectManager.AddHero` hand-assigns `hero.Id` and never calls `RegisterObject`. The load prune therefore emptied the promoted-hero list every load | Adapter/engine seam | The lookup reads correct and compiles; every *other* adapter in the repo uses `Hero.AllAliveHeroes`, so this one was the outlier nobody diffed | Switched to `Hero.AllAliveHeroes`; lesson in `lessons/adapters-taleworlds-api.md` |
| 6 | MED | Declining recorded nothing. Merit only grows and the queue condition is `bank >= threshold`, so the same soldier was re-offered after every won battle forever | Behaviour gap created by fixing a donor bug | The donor's queue-time deduction was a real bug (fixed correctly), but it had also been the only thing suppressing the re-ask. Removing a bug removed a side effect nobody had named | Per-troop decline mark, persisted under `_taom_fc_declinedAt`; 5 tests |
| 7 | MED | `EndBattle` computed offers from the raw bank without debiting offers already queued and unanswered, so two won battles inside one uninterrupted encounter could issue two offers backed by one threshold | Accounting | The queue and the bank were treated as independent; `CompleteOffer`'s `Math.Max(0, …)` hid the shortfall | Outstanding offers debited before the count; 2 tests |
| 8 | MED | **Introduced by this changeset.** `diagnostics` was added to `FieldCommissionConfig` but omitted from `Validate()`'s field-by-field rebuild, so the JSON value was silently dropped | Sanitize-by-copy | `Validate()` reconstructs the config by naming each field; adding a field without touching it is a silent no-op with no compiler help | Field added; `GetConfig_ValidJson_ParsesAllFields` now asserts every field including the new ones, and carries a comment naming the trap |
| 9 | MED | Interpolated trace arguments were built before the diagnostics gate could reject them, including once per troop type per battle | Efficiency | The gate was inside the callee; C# evaluates the argument first | `Tracing` checked at each call site; `WriteTrace` no longer gates |
| 10 | LOW | Upgrade-target enumeration had no `IsHero` filter (the donor had one), so merit could be parked under a hero id where `CanPromote` rejects it forever and consolidation never reclaims it | Donor-parity drop | A silent merit sink with no player-visible symptom and no crash — invisible to every test | Filter restored with the reason in a comment |
| 11 | LOW | `AddFocus(…, checkUnspentFocusPoints: false)` does no bounds check, and `InitializeHeroDeveloper` has already spent starting focus, so a maxed skill could exceed `MaxFocusPerSkill` | Engine API precondition | `checkUnspentFocusPoints:false` was read as "skip the points accounting", not "skip the cap" | Clamped against `CharacterDevelopmentModel.MaxFocusPerSkill` |
| 12 | LOW | ~60 TAOM troop templates (all 35 Dale troops among them) declare no civilian equipment, so promoted companions wore vanilla's Calradian `neutral_culture` peasant outfit in every town | Data coverage | The adapter's null-guard skipped its own overwrite silently; the engine had already filled the slot, so nothing looked wrong in code | Falls back to the troop's own battle kit |
| 13 | LOW | The offer pump had no captivity gate; captivity nulls `PlayerEncounter.Current`, so a prompt could pop while the player sat in a cell with no party | Missing lifecycle state | The two existing gates were chosen for "is the player mid-encounter", and captivity is the state where those go quiet | `Hero.MainHero.IsPrisoner` / `MobileParty.MainParty` gate |
| 14 | HIGH | **Introduced by this changeset.** `OnMapEventEnded` never re-read the master toggle. `BeginBattle` latches `_eligible`, so a battle that started enabled and ended disabled — one MCM flip mid-fight — still banked merit and queued an offer, contradicting the "turn it off and nothing accrues" text I had just written | Master-toggle fold | I gated the two handlers that *start* things and never asked which handler *commits* them. The `_trackedMapEvent` reference gate looked like it covered this; it guards against a foreign map event, which is a different question | Toggle folded into `won`, taking the score-nothing path. Found by the deep-review data-flow agent, whose prompt asks this question by name |
| 15 | MED | **Introduced by this changeset.** `Merge` ran the JSON value through the MCM slider clamp, so `ratioThreshold: 0` — explicitly legal in the JSON provider as "never eligible" — came back as `0.1`, re-enabling what the pack author disabled. Same for a `meritThreshold` above the slider's max | Two validation surfaces, one clamp | `SettingClamp.Clamp(value, default, min, max)` clamps `value ?? default`, so passing the JSON value as the *default* silently subjects it to the player-facing range. The signature invites this | Clamp applies only when the MCM value is present; each surface validates its own input. Two tests. Found by Codex |

## Root-cause pattern: dropping a donor knob drops its default

Findings 1, 3 and 10 are the same mistake three times. Each was a donor behaviour removed as YAGNI —
`AllowMultiplePromotions`, the roster precondition, the `IsHero` skip — where the *knob* was
genuinely not worth porting but the behaviour it defaulted to **was** the shipped behaviour. Deleting
the knob silently deleted the default.

Finding 6 is the same shape from the other direction: fixing a donor bug (queue-time merit deduction)
removed an unnamed side effect (natural re-ask suppression) that the fix was not scoped to replace.

**The rule this yields:** when you drop a donor setting, state in the port notes what that setting's
DEFAULT value did, and either keep that behaviour as a constant or record explicitly that you are
changing it. And when you fix a donor bug, enumerate what the buggy behaviour was incidentally
providing before you remove it.

## Second pattern: a second dictionary keyed on the same id

Finding 4 (mine) and finding 8 (mine) are both "added a field, missed the places that already
maintain its siblings". Both were caught by review rather than by a compiler or a test, because C#
gives no help when a parallel structure falls out of sync or a copy-constructor omits a field.

**Prevent:** when adding a field to a POCO that is rebuilt field-by-field somewhere, or a dictionary
keyed the same way as an existing one, grep for the existing key's removal/copy sites in the same
edit — and add the assertion that would have failed.

## Third pattern: four of the fifteen findings were mine

Findings 4, 8, 14 and 15 were introduced by the very changeset that fixed the other eleven. Three of
them (4, 8, 15) are the same failure at different scales: **I added a thing without auditing the
places that already maintain its peers.** Finding 14 is different and worse — I wrote the master
toggle's promise into the MCM hint text and the XML doc, then failed to implement it at one of the
two ends of the window I had just documented.

**Prevent:** when a change adds a promise to user-facing text, enumerate every code path that could
violate it before the text is written, not after. The deep-review data-flow prompt already asks for a
"master-toggle fold check" by name and it is what caught 14 — that prompt earned its place.

The general lesson worth carrying: a fix pass is a change like any other, and it needs the same review
as the thing it fixes. Running the reviews only against the ORIGINAL defect list would have shipped
all four.

## Why each review agent missed what it missed

- **Standards (Agent 1)** — caught the ADR-002 breach the fixes introduced (the behaviour grew to 189
  lines). It has no visibility into behaviour, so findings 1–13 were out of scope by construction.
- **API compatibility (Agent 2)** — signature-level; finding 5 is a *semantic* API misuse where the
  signature is perfectly valid. Its prompt now asks explicitly whether a lookup can resolve a
  runtime-created object, not just whether it compiles.
- **Efficiency (Agent 3)** — found finding 9, which is squarely its remit. Correctly declined to rate
  two other costs it had not measured.
- **Completeness (Agent 4)** — found the stale doc test count and the missing CHANGELOG. It counts
  tests; it does not reason about what the tests fail to say.
- **Data flow (Agent 5)** — the intended catcher for findings 4, 7 and 8. Its prompt was written to
  chase exactly the decline-mark/`TransferMerit` interaction.
- **The 28-agent investigation** — found 1, 2, 3, 5, 6, 7, 10, 11, 12, 13. This is what a parallel
  adversarial pass buys over a linear review: eleven of its own hypotheses were refuted, and saying so
  is as valuable as the findings, because it stops the next session re-deriving them.

## Lessons to codify

- `lessons/adapters-taleworlds-api.md` — `MBObjectManager.GetObject<Hero>` cannot resolve heroes built
  by `HeroCreator` at runtime; use `Hero.AllAliveHeroes`.
- `lessons/misc.md` — dropping a donor setting drops the behaviour of its default value.
- `lessons/state-lifecycle-save.md` — un-persisted state on a `Reuse.Singleton` service crosses
  campaign boundaries; clear it at the session boundary, ahead of any load guard.
- `lessons/testing-qa.md` — a config POCO rebuilt field-by-field in a validator needs a test that
  asserts EVERY field survives a full-file parse.

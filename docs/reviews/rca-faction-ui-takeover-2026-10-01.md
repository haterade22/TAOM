# RCA: faction-screen takeover review (#704 follow-up, 2026-10-01)

## Top line

Mike, after #704 went out in `0bd6abf3`: "When I pick an existing character like Thranduil from the UI,
it should skip the character creation all of the way until the career picker, user picks the career and
it starts the game." The follow-up drives Player Switcher (#514) from the faction screen: a living clan
hero Player Switcher would hand over becomes its selection, the 1060 handler takes the face generator
and the four stages after the career choice out of the stage list when the culture stage completes,
Patch78 walks the backstory to the career menu and the 1100 handover makes the player that hero.

A six-lens `/deep-review` (standards, engine, efficiency and data flow in wave 1; completeness and
design in wave 2) found no CRITICAL or HIGH: three MEDIUM, a LOW tail, three design proposals and one
decision owed to Mike. One defect that would have broken startup was caught earlier, by a test written
before the review: the new service's constructor graph reached a registration `IoC.cs` makes after the
point where FactionMap resolves it.

Every finding below is fixed, not applied with a reason, or owed to Mike. A convergence pass on the
applied improvements passed with one LOW, also fixed. Lens reports: the session's agent transcripts (not
committed).

## Root-cause classes

**A. Engine-list surgery written as adapter code.** The stage plan (which five stages a takeover drops,
the vanilla-list guard, the restore order) lived in the adapter because it called engine list methods,
so the tests faked the whole plan and the one table the binding test pinned was not the table the
restore read. The same plan as a service over engine primitives (count, index, has, remove, append by
kind) is testable against a fake list that removes and appends as the engine does.

**B. One flow, two features' state.** The takeover writes Player Switcher's session store and keeps its
own record. Written in the wrong order and cleared only when its own record said so, the two could
drift: a half-made pick could leave a selection the handover would still act on.

**C. Claims written into the review brief before the doc.** Two limitations were described as
"accepted and documented" in the wave-2 brief while no doc carried them, and the doc stated the options
consequence from vanilla's provider alone. Both were caught by lenses that read the doc against the
claim (Completeness, Engine).

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| T0 | (startup) | The pick service's eager constructor graph reached `IUncapturableRegistry` (IoC.cs:213) from FactionMap's registration-time resolve (IoC.cs:111, FactionMapIoC.cs:26): DryIoc would throw inside `IoC.Configure` | IoC order | The feature's wiring test registered substitutes for every cross-feature dependency up front, which cannot see an order hazard | `Lazy<IHeroPickerService>` (the `CampService` precedent); `TheLauncher_ResolvesWhileFactionMapRegisters_BeforeTheUncapturableRegistryExists` registers in IoC.cs order with nothing from later; lesson in `build-tooling-workflow.md` |
| T1 | MED | The takeover's stage plan lived in the adapter, untested; the binding test pinned a list the restore never read (Standards) | ADR-007 thickness | Calls to engine list methods read as adapter work | Plan moved into `FactionPickService` with a fake-list suite; one per-kind table in the adapter, read by `TypeOf`; lesson in `adapters-taleworlds-api.md` |
| T2 | MED | Two limitations (CHARACTER button, "Next" label) called documented, documented nowhere (Completeness) | Evidence | The brief was written from memory of the decision, not from the doc | Both added to Known limitations and checklist step 6; the existing rule (`evidence-over-claims.md` C.1) already covers it: a repeat, see below |
| T3 | MED | #704 still records the first decision; the Haldir and Bolg decision was nowhere as owed (Completeness) | Process | The follow-up was scoped as code | Owed bullets in the feature doc; a #704 comment drafted for Mike's word |
| T4 | LOW | The doc said only Iron Man locks after creation; Birth and Aging Options locks the life and death cycle too (Engine) | Engine claim | Read vanilla's options provider only, not the official optional module's | Corrected from the module's DLL (`DisabledLater`, then `Disabled`); lesson in `campaign-mechanics.md` |
| T5 | LOW | No guard that the culture stage is first; `NextStage`'s handlers-before-activate order unpinned (Engine) | Engine ordering | Count and membership looked sufficient | `CurrentIndex == 1` guard with a test; `NextStage_RunsTheHandlersBeforeItReadsTheListAndOpensTheNextStage` reads the IL; lesson in `adapters-taleworlds-api.md` |
| T6 | LOW | A click ran the campaign-wide takeover check with no exception guard; Gauntlet's command dispatch rethrows (Data flow) | Entry-point robustness | Player Switcher's equivalent runs inside Patch77's try; the faction screen's command was not seen as an entry point | `Pick` and `TakeoverRow` guarded, falling back to a copy; lesson in `localization-ui.md` |
| T7 | LOW | Takeover state in two places could drift (write order; conditional clear) (Data flow) | Class B | Clearing "only what I set" looked careful | `_takeover` before the session; `Clear` and the reset always clear the selection; lesson in `state-lifecycle-save.md` |
| T8 | LOW | Doc said "back returns to the faction screen"; Previous walks six auto-answered backstory menus first, showing the pre-pick figures (Data flow, Completeness) | Doc accuracy | Written from the design, not the menu chain (`CareerMenuService` input menu, `FaceGenUpdated` only on face-generator completion) | Doc and Known limitations corrected |
| T9 | LOW | `FindTakeover` computed a group nothing reads (Standards) | Simplicity | Filled the row "meaningfully" | Constant group |
| T10 | LOW | Comments still named the old writers of Player Switcher's selection, and the face-generator patch still said the faction screen turned Player Switcher off (Standards, Engine) | Stale contract comments | A new writer was added without reading the contract's own comments | Four comments updated; lesson in `state-lifecycle-save.md` (with T7) |
| T11 | LOW | The 1060 catch logged `ex.Message` (Standards) | Logging | Copied the registration catch's shape | Logs `{ex}` |
| T12 | LOW | Changelog dated 2026-10-02 (UTC) (Standards) | Process | Took the date from a UTC timestamp | 2026-10-01 |
| T13 | LOW | The stage-composition check, the restore warning and Patch77's check-before-clear order were not pinned (Completeness) | Test meaningfulness | Each test refused earlier, or pinned presence only | Three tests, each mutation-checked |
| T14 | LOW | The faction-screen hint promised a takeover for every lord, and the Player Switcher hint said a copy took only the look (Data flow, Completeness) | Player-facing text | Written before the culture rule's effect on the named cards was checked | Hints reworded (English only: MCM hints here carry no loc key) |

## Design proposals (wave 2)

- **Applied, P1:** the "a takeover copies nothing" rule moved from `FactionPresetService` (its flag and
  `SelectTakeover` removed) into `FactionPickService.OnCharacterCreationFinalize`, so one class owns the
  takeover fact; the 1060 behaviour takes only the pick service.
- **Applied, P2:** "this creation's list is short" comes from the per-creation adapter (`HoldsRemoved`)
  instead of a flag in the singleton.
- **Not applied, P3:** deleting the pick-time `IsSwitchable` check as unreachable. It is unreachable
  only because Player Switcher's adapter reads `Hero.AllAliveHeroes`, which no test pins, and the
  Efficiency lens judged the same line as deliberate parity with the handover's three refusals. Kept.

## Not applied, with reasons

- **The CHARACTER button and the career button's "Next" label** during a takeover: cosmetic, the first
  is a no-op and the second still leads into the game. Documented; relabelling needs a hook on the
  vanilla narrative view model for a word.
- **A handover that throws at 1100 after the skip** leaves the created character with the hero's look,
  the generated clan name and the default banner. Every refusal the handover can predict is asked at
  the pick; a fallback would need a hook after 1100 for an exception path. Documented.
- **The gold** (1,000 plus the culture's starting gold, not the lord's treasury): vanilla's
  `FinalizeCharacterCreationState` assigns 1,000 after every handler, the same for Player Switcher's own
  takeovers. Pre-existing; documented, and offered to Mike as a separate fix.

## Owed to Mike

- Haldir and Bolg: keep the copy, point the cards at lords of their factions, or allow a takeover across
  kingdoms.
- The #704 comment recording the takeover (public, on his word).
- The rest of checklist steps 6 to 8. His first takeover in game (Boromir, 2026-10-02) went from the
  faction screen to the career page and into the game.

## Convergence pass

One reviewer on the applied improvements (P1, P2, the one-table adapter, the new tests): PASS. Its three
must-hold claims were checked against the installed v1.5.3: `Resolve` gets the same arguments as
before, the 1060 finalize never clears the selection the 1100 handover reads, and a throw in `Pick`
leaves no pick and no selection. One LOW, fixed: when restoring the look from before an earlier pick
threw, the catch's own `Clear` threw it again out of the click. `FactionPresetService.Clear` now forgets
the look before restoring it, and `Pick_WhenPuttingBackTheEarlierLookThrows_StillDoesNotThrow` was red
first. Two comment nits (the reset's callers, why `Clear` may drop the selection when the screen
declines) and a spacing nit in the tests were fixed. Both IL binding tests were confirmed passed, not
inconclusive.

## Repeat offenders

- **T2 is a repeat of `evidence-over-claims.md` C.1** ("writing the summary before its evidence
  exists"), which the #704 RCA's own miscounts hit the same day. The rule stands; what failed is that a
  review brief was not treated as produced prose. The brief now gets the same check as a commit body.
- **T0 extends `build-tooling-workflow.md`** "A feature is not verified until a real container has
  resolved it": the container test existed and passed, because it registered every dependency first.

## Why each lens caught what it did

- **Standards** read the adapter against ADR-007 and the binding test against the code it claimed to
  pin (T1), and the contract comments against the new writer (T10).
- **Engine** decompiled every module that registers an options provider, which is how Birth and Aging
  surfaced (T4), and checked the ordering the design leans on (T5).
- **Efficiency** found no hot path; its only judgement call (keeping `IsSwitchable`) decided P3.
- **Data flow** walked every exit of the pick (T6, T7, T8) and resolved every named card through the
  XSLT to find the two copies (owed to Mike).
- **Completeness** read the brief's "documented" claims against the docs (T2) and the issue against the
  change (T3), and mutation-reasoned each test (T13).
- **Design** found the duplicated takeover fact and the per-creation flag in a singleton (P1, P2).

## Lessons appended

- `build-tooling-workflow.md`: a container that resolves while it registers fixes the order for its
  whole graph (T0).
- `adapters-taleworlds-api.md`: an adapter over an engine collection exposes primitives, the plan is a
  service (T1); pin an engine method's internal order from its IL when a design leans on it (T5).
- `campaign-mechanics.md`: a claim about campaign options enumerates every loaded options provider (T4).
- `localization-ui.md`: a Gauntlet command handler is an entry point and is guarded like one (T6).
- `state-lifecycle-save.md`: a flow that writes another feature's state clears it on every exit, and
  updates that state's "who writes this" comments (T7, T10).

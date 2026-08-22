# RCA: Rebindable Time Acceleration keys (2026-08-22)

Feature: three hardcoded campaign-map time keys (Space, E, Ctrl+Space) became native rebindable
`GameKey`s in Options > Keybindings > Campaign Map. Motivation: E is also vanilla `MapRotateRight`
(GameKey 59), so pressing E accelerated time and rotated the camera with no way to change either.

Review passes: 5-agent `/deep-review` plus an independent Codex adversarial pass. Six findings
survived verification. Two were HIGH, and **both were about behaviour that only breaks once a key is
actually rebound**, which is exactly the state no existing test and no manual check exercised.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | Key names placed in a GameText-registered XML never reach the Options screen, which reads `Module.CurrentModule.GlobalTextManager`. Every TAOM row would render `ERROR: Text with id str_key_name doesn't exist!` | Wrong engine subsystem | Traced the string id SHAPE character-by-character and stopped there. Never asked *which manager performs the lookup*. Both the data-flow agent and I verified the id format and declared the flow CONNECTED | New lessons entry: for any engine-rendered string, verify the MANAGER and its loader, not just the id |
| 2 | HIGH | A fast-forward key rebound off Space sets `SpeedUpMultiplier` but never enters a fast-forward mode, and `TickMapTime` ignores the multiplier in Play/Stop. The rebound key does nothing | Hidden coupling exposed by the change | The branch was unchanged by this work, so every reviewer (me included) classified it as "no behavioural delta". Its correctness depended on an undocumented coupling to vanilla's Space handler that the change silently removed | New lessons entry: making a value configurable changes the correctness conditions of code that reads it, even when that code is untouched |
| 3 | MED | Turbo opener not idempotent: a second observed press re-saves the already-boosted state, so the restore leaves the engine turboed with the latch closed | Latch/toggle | Pre-existing. The rename drew attention to the latch but I reviewed it for "did the rename break anything" rather than "is this correct" | Covered by the existing `harmony-patches.md` "Latches & Toggle Gates" rule; add the opener-idempotency case to it |
| 4 | LOW | Test asserted that clearing a binding in Options nulls `KeyboardKey`. Options actually calls `Key.ChangeKey(Invalid)` and keeps the object | Wrong test premise | I wrote a test from the ctor's behaviour and described it as the player-facing scenario without checking the player-facing code path | Test both shapes; the guard needs to handle null AND non-null/Invalid |
| 5 | MED | `EnsureResolved` latched only on success, so a failed registration re-scanned every category on every property read, forever | Hot-path fallback | Found by the efficiency agent. I had reasoned about the success path only | Fixed by latching unconditionally |
| 6 | LOW | Turbo latch could not close while Ctrl was held if the turbo key was unbound mid-turbo | Latch closer coverage | Found by the data-flow agent | Adapter reports an unbound key as released |

## Root-cause pattern: I verified the shape of things, not the path they travel

Findings 1 and 2 are the same mistake pointed at different subsystems.

For #1 I confirmed that `str_key_name.TaomTimeControlHotKeyCategory_500` was the exact id the engine
would compute, cross-referenced it against the XML, confirmed all 12 translations, ran an XML parse
test, and wrote an invariant test. Every one of those checks passed, and the feature would still have
shipped with six error labels, because none of them asked which of the engine's *two* text managers
the Options screen reads. Codex answered that in one step by opening `GameKeyOptionVM.RefreshValues`.

For #2 I diffed old against new, confirmed the if/else chain was byte-for-byte equivalent modulo
renames, and concluded there was no behavioural delta. That was true of the code and false of the
system: the fast-forward branch had a silent dependency on its key being Space, and the entire point
of the change was to let that stop being true.

The unifying error is treating "the artifact is correct" as "the behaviour is correct". Both times the
missing question was about the *consumer*, one subsystem out from what I had changed.

There is also a self-correction worth recording: my first fix for #1 was wrong. I moved the strings to
a dedicated file and removed its `<IncludedGameTypes>` gate, reasoning that the Campaign gate was what
kept them out of a main-menu screen. The gate was real and the reasoning was plausible, but ungating a
`GameText` node does not promote it to the global manager. I had verified the gate mattered without
verifying that removing it was sufficient, which is the same defect one level down.

## Why each agent missed these

| Agent | Why its rule set did not fire |
|---|---|
| Standards | Correctly scoped to ADRs and structure. Neither finding is a structural violation |
| API compatibility | Verified every signature TAOM *calls*. Both HIGHs live in engine code TAOM does not call: a text manager it never touches, and a mode transition it deliberately never made |
| Efficiency | Found #5 on its own. Out of scope for the rest |
| Completeness | Correctly flagged the stale feature doc, missing CHANGELOG, and missing tests. Cannot see a wrong engine assumption |
| Data flow | Got closest and still missed #1. It traced the id shape end-to-end and asserted the `<XmlNode>` registration was "the same mechanism as vanilla's own module_strings", which is true of the declaration and false of the consumer. It also asserted vanilla's `module_strings` node is `IncludedGameTypes`-scoped like TAOM's; it is not, and I caught that by reading Native's `SubModule.xml` directly |

The data-flow agent's near-miss is the instructive one. Its claim was specific, confident, and
supported by a real file reference, and it was wrong about the one thing that mattered. Spot-checking
its load-bearing claim against the source is what caught it, per `evidence-over-claims.md` §A.4.

## Lessons to codify

Both belong in `docs/reviews/lessons/`:

1. **Localization & UI.** When a string is rendered by an engine screen, verify which manager
   performs the lookup and how that manager is populated, before verifying the id. Bannerlord has two:
   `Module.CurrentModule.GlobalTextManager` (filled by `LoadDefaultTexts()` scanning every module for
   the literal `ModuleData/global_strings.xml`) and the per-`Game` `GameTextManager` (filled from
   `SubModule.xml` `<XmlNode id="GameText">`). Options, and anything else reachable from the main
   menu, reads the former. A correct id in the wrong manager renders an ERROR string.

2. **Campaign Mechanics / GameModels & Services.** Making a hardcoded value configurable changes the
   correctness conditions of every consumer of that value, including consumers the diff does not
   touch. Before shipping, re-derive each consumer's behaviour under a value that has NEVER been used
   before, not just under the shipped default. A "no behavioural delta" verdict from a diff is
   evidence about the code, not about the system.

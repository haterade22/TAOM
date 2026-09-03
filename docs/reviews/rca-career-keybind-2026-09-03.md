# RCA: rebindable career ability key (#533), 2026-09-03

The change itself was small and copied a shipped precedent (`TaomTimeControlHotKeyCategory` +
`MapInputAdapter`), so the category-registration half came out clean: Standards found zero
violations and Data Flow confirmed 7 of 9 traces connected against decompiled v1.4.8 engine source.

Every finding that survived verification came from the ONE place the precedent could not be copied
from. The time-control adapter exposes booleans only. The career adapter has to expose a
**display string**, and all three bugs live in that string: how often it is computed, what the
engine returns when the lookup fails, and what the UI does when it comes back empty. The precedent
was load-bearing for the parts it covered and silent on the part it did not.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | `ActivationKeyName` allocated a `TextObject` and three strings on every frame. `MissionAgentStatusCareerMixin` is a `[ViewModelMixin("Tick")]`, and `GameTextManager.TryGetText` ends in `CopyTextObject()` + `AddIDToValue`. | Performance | The property was moved into `OnRefresh` deliberately, to make a mid-mission rebind update the chip. The cost of the read was never asked about, because the pre-change code read it once in the constructor where cost was irrelevant. Moving a read to a hotter call site is a performance change, and it was reasoned about purely as a correctness change. | Lesson added: when relocating a read to a hotter call site, cost the callee before moving it. |
| 2 | MED | `GameTextManager.FindText` never returns null or empty for a missing entry: it returns a `TextObject` rendering as `ERROR: Text with id ... doesn't exist!`. The `string.IsNullOrEmpty` fallback was therefore unreachable, and the chip would have printed that sentence. | API misread | The guard was written from the method NAME plus an assumption about how a "find" behaves on a miss. `FindText`'s body was never read. Vanilla's own `GameKeyOptionVM` calls it unguarded, so nothing in the surrounding code contradicted the assumption. | Switched to `TryGetText`, which has an honest success flag. Lesson added below. |
| 3 | MED | Clearing the binding empties `ActivationKeyText`, but the key chip is a fixed 30x22 sprite with no `IsVisible` gate, so the player is left looking at an empty dark box. Its sibling glyph medallion three lines above IS gated, on `HasCareerGlyph`. | UI data flow | Known and written down as smoke step 9 of the plan ("check the empty chip does not leave a stray box, if it does add an `IsVisible` binding"), then deferred to in-game observation instead of being answered by reading the prefab that was already in the repo. | Fixed with `HasActivationKey`, mirroring `HasCareerGlyph`. Lesson added below. |

## Root-cause pattern

**All three are the same shape: a claim about a callee that was never read.**

1. How expensive is `GetHotKeyGameTextFromKeyID` per call? Not read, so the per-frame move looked free.
2. What does `FindText` return on a miss? Not read, so a guard was written against an invented contract.
3. What does the prefab do with an empty string? Not read, so a question the repo could answer was
   filed as an in-game observation.

Finding 3 is the sharpest of the three, because the uncertainty was correctly identified and written
into the plan, then routed to the slowest possible resolution. A question that a file in the repo
can answer in one read must not be deferred to a smoke test. Deferring it does not just delay the
answer, it converts a certainty into a maybe and puts a defect on the ship path.

Finding 2 also carries a second lesson worth separating from the first. Vanilla having the identical
hole is not evidence the hole is safe. Native gets away with `FindText` unguarded only because it
ships `str_game_key_text` for the whole standard keyboard, which is a data-coverage accident, not a
contract. `InputKey.Extended` has no entry.

## Why each agent missed these, and what it means

This is not the usual "all agents missed it" story, so the useful detail is which lens caught what.

- **Standards (Agent 1):** correctly PASS. None of the three is a standards violation; the code
  follows the precedent faithfully. This agent's scope was right and its answer was right.
- **API compatibility (Agent 2):** caught finding 2, by reading `FindText`'s body rather than
  trusting its name. It also settled the `protected internal RegisterGameKey` cross-assembly
  question a previous review had left open, and confirmed `ActionCategory` is yielded before the
  `isMultiplayer` branch. The value came entirely from decompiling rather than pattern-matching.
- **Efficiency (Agent 3):** caught finding 1, and did so only because its prompt explicitly required
  decompiling before asserting a cost. An efficiency pass that reasoned from method names would have
  called a text lookup cheap.
- **Completeness (Agent 4):** caught the missing GitHub issue and one absent test
  (`GameKeyConstructedUnbound_LeavesKeyboardKeyNull`, which exercises the `?.KeyboardKey?` branch of
  `BoundKey`). It did not look at runtime behaviour, correctly, since that is not its lens.
- **Data flow (Agent 5):** caught finding 3, by opening the prefab and asking what it renders for an
  empty string. This is the agent's whole purpose and it worked as intended.

Two of its reports carried factual errors that had to be caught on verification: Standards claimed
the changeset ADDED an `IoC.Resolve` line to the mixin constructor when the change actually removed a
line there, and Completeness claimed `ActivationKeyName` was newly added to `IAbilityInputAdapter`
when it predates this work. Neither changed a verdict, but both are the reason a returned agent
report is a hypothesis. Every finding acted on here was re-verified against the source first, and
finding 2's verification (reading `FindText` lines 81-92) is what produced the fix that was actually
shipped, which differs from the fix the agent proposed: it suggested guarding on the literal
`"ERROR:"` prefix, and reading the body showed `TryGetText` was public and made the prefix hack
unnecessary.

## Lessons to append to `docs/reviews/lessons/`

Three, all in the same direction. Full text appended to the category files:

- **`adapters-taleworlds-api.md`**: a `FindXxx` engine method may return a formatted error object
  rather than null. Read the body before writing a null-or-empty fallback against it, and prefer the
  `TryGetXxx` sibling where one exists. Vanilla calling it unguarded is not evidence of safety.
- **`gamemodels-services.md`**: moving a property read to a hotter call site is a performance
  change. Cost the callee before the move, especially for anything that resolves localized text:
  `GameTextManager.TryGetText` copies a `TextObject` on every successful lookup.
- **`localization-ui.md`**: a fixed-size widget wrapping a bound string needs its own `IsVisible`
  gate for the empty case. Check the sibling widgets in the same prefab: if one is gated and yours is
  not, that is the bug. And when a plan defers a question to an in-game smoke, first check whether a
  file already in the repo answers it.

## Verification after fixes

Full suite 7904 passed, 0 failed, 2 skipped (both pre-existing warg tests), 0 build errors.
`validate_moduledata.py` 0 errors. `lint_docs.py` dash check clean. In-game smokes listed on #533
remain owed, and the 12-language translator run is still owed on a missing `ANTHROPIC_API_KEY`.

## Signed off without change

Data flow trace 8: if `TaomCareerHotKeyCategory.Register()` throws, the warning is logged under
`[CareerSystem]` and the adapter latches to a permanently unbound key, so the player sees an ability
that never fires and no on-screen explanation. This is the same fail-open shape the shipped
TimeAcceleration registration uses, and it is deliberate: a startup exception must not take the mod
down. Recorded here as a conscious sign-off rather than an oversight.

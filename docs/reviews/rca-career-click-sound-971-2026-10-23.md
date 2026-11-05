# RCA: career button click sound (#971), 2026-10-23

**Summary.** #971 said the career button's click sound (#378) never plays, because the mixin method that held
it has no `[DataSourceMethod]` and UIExtenderEx never adds it to the view model. The first fix moved the sound
into the core Patch27 postfix that handles the click. Risk tier 1 by file (a Harmony patch), reviewed as the
handoff set it: Codex (gpt-6-astra, xhigh) plus the Data flow lens. The Data flow lens proved the premise
wrong: the postfix already calls `CharacterDeveloperVM.ExecuteDone`, which runs vanilla's
`GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen`, and that plays the same
`event:/ui/default` (v1.5.5 `GauntletCharacterDeveloperScreen.cs:82`, `:127-131`). The click was never
silent, and the first fix played the sound twice. The shipped change keeps only the deletion of the dead mixin
method, names vanilla's close callback as the sound source, and pins the real flow in IL tests.

Sources: Codex output `docs/reviews/raw/codex-prereview-career-click-sound-971-2026-10-23.md` (local, ignored),
prompt `docs/reviews/codex-prereview-career-click-sound-971-2026-10-23.prompt.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The added `PlayUISound` doubled vanilla's close sound; the issue's "never plays" was false | Wrong premise | The issue traced the dead mixin call and the brush, not what the live handler's own calls already do | Added call removed; `Patch27Postfix_PlaysNoSoundOfItsOwn` and `VanillaDeveloperClose_PlaysTheDefaultUiClick` pin both sides |
| 2 | LOW | The event-string test scanned raw bytes, so a string elsewhere in the method passed it | Test cannot fail | A byte scan, not decoded instructions | Tests use `IlCallScanner.Fingerprint`; the string must be the call's own argument |
| 3 | LOW | The call-order test passed only when an earlier class had loaded the game assemblies; alone it failed | Order-dependent test | `ExtractCalledMethods` skips tokens that do not resolve, and `TaleWorlds.MountAndBlade.View` is not in the test bin | `GameAssemblies.EnsureLoaded` in `[ClassInitialize]`, Inconclusive without the game, `RequiresGame` category |

No new order pin existed for "close the developer before the career screen opens", which the feature doc calls
critical; `Patch27Postfix_ClosesTheDeveloperBeforeItOpensTheCareerScreen` adds it. Mutations seen RED: the sound
call added back, `ExecuteDone` and `OpenCareerScreen` swapped, and the mixin method restored.

## Why the reviewers found these

- Data flow found #1 by following `ExecuteDone` into the vanilla screen's close callback, and #3 by checking
  which assemblies the test bin holds.
- Codex found #2 by building a method whose `ldc.i8` operand held the `ldstr` byte pattern.

## Lesson

Before adding behaviour an issue says is missing, trace what the live path already does, vanilla calls
included (`docs/reviews/lessons/adapters-taleworlds-api.md`).

## 1.4.x port (plan 129)

Ported to the 1.4.x line by plan 129. Here the mixin lives in `Main/` (this line has no `Client/`
project), and the UIExtenderEx build it compiles against (2.13.2) adds a mixin method only when it
carries `[DataSourceMethod]`, as the finding above says of the 1.5.x build. On v1.4.8 vanilla's
close callback is `GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen` (:139-143).

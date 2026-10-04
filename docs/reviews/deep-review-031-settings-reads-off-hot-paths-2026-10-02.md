# Deep review: plan 031, MCM settings reads off hot paths (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: MCM settings reads off hot paths: cached settings references, gate order, hotkey parse on
         change, localization id lookup without Substring (plan 031)
Date: 2026-10-02
Branch: perf/031-settings-reads-off-hot-paths, reviewed range 0d1e91f0..bf1898c3

Scope:   C# (17 Main files: nine settings providers, four combat services, Patch35, the localization
         prefix, CachedEnumParse and its caller), 17 test files, 5 docs. No XML, no harness, no gate.
Blast radius: no graphify run in this review. Callers checked by grep: CachedEnumParse has one
         production caller (MixedFormationsMissionBehavior.cs:32); the slice key is used only by the
         localization prefix; all nine providers stay Reuse.Singleton in their *IoC.cs and none is
         built with `new` in Main.
Waves:   one wave of six lenses (Standards, Engine compatibility, Efficiency, Completeness, Data flow,
         Design); review lead verification, fixes and Step 4 afterwards.

STANDARDS:     FAIL, 4 LOW violations (ADR-002 algorithm in a patch class, test-only public
               constructor, gate test outside Infrastructure, self-oracle assertions) plus 2 doc nits
COMPATIBILITY: PASS, 0 incompatible, 3 unverified (other MCM versions, a load order with two
               MCMSubModule declarations, the native body of String.CompareOrdinal)
EFFICIENCY:    PASS, 0 issues
COMPLETENESS:  INCOMPLETE: GitHub issue drafted, not filed (waits on Mike); 3 LOW test and comment
               gaps (L1 to L3)
DATA FLOW:     PASS, 0 gaps, 2 LOW inconsistencies (warning wording, file-catalogue scope)
DESIGN:        4 KEEP proposals (2 apply, 2 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Orchestrator focus, verified

**MCM settings identity.** Five lenses decompiled the installed `MCMv5.dll` (5.12.3, the version
`Main/TAOM.csproj:94` compiles against) and `Bannerlord.MBOptionScreen.v1.5.1.dll`; the review lead
re-read the load-bearing lines in one of those decompiles. `BaseSettingsProvider.Instance` is assigned
once per process in `MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot`, behind a was-called flag.
`BaseSettingsContainer.RegisterSettings` returns early for an id it already holds, `GetSettings` returns
that stored object, and `OverrideSettings` and `ResetSettings` copy values into `LoadedSettings[id]`.
The MCM menu's preset switch, reset to defaults, Done and Cancel all act on that same object. Only the
per-save and per-campaign containers react to game start, load and end; `TaomSettings` and
`BlowDiagnosticsSettings` are `AttributeGlobalSettings`. There is no file watcher and no profile
feature, and no TAOM code creates, registers or overrides a settings object. Result: the cached
reference is MCM's only registered object for the process, and every getter of the nine providers
reads `Settings?.X` at call time, so presets, resets, menu edits and Cancel reach them live. This is
the contract of trunk commits 7feca96b and 02157b18. The race on `??=` is benign: every racing thread
resolves the same object, and a null before MCM is up is never kept. A new test now fails if either
settings class stops being an MCM global setting (Step 4).

**Reordered gates.** Every reorder changes the order of side-effect-free operands inside one `||` or
`&&` chain, or swaps two early returns with the same value (`CreatureCombatService.cs:67,82,90`,
`CrushThroughService.cs:88-91,100-102`, `ChargeKnockdownService.cs:40-43`,
`RaceCombatModifiersResolver.cs:35-45`). NaN momentum still returns null or false through
`float.IsNaN` and `!(x > 0f)`; a null monster or race id still short-circuits before `Contains`,
`TryGetValue` or `IsValidRaceId`, which run under exactly the conditions they ran under before. In
Patch35, `Formation.Team` is a readonly field and `Mission.PlayerTeam` returns `Teams.Player` (v1.5.3),
so moving the team filter ahead of the settings read changes no outcome.

**Localization slice against `Substring`.** The old key was a string under the default (ordinal,
case-sensitive) comparer. The slice compares lengths first, then `string.CompareOrdinal` over the same
UTF-16 code units, and hashes FNV-1a over the slice's own characters. Both slices always lie inside
their strings (the probe is `(text, 2, end - 2)` with `end < text.Length`; a registered id is
`(id, 0, id.Length)`), so an id that is a prefix of another never matches, an empty `{=}` matches a
registered empty id, and a case difference falls through. A null id still throws
`ArgumentNullException` (parameter name now `id`). The native body of `CompareOrdinal` was not read;
the tests that pin these cases run on the real CLR and pass.

## Details

**Agent 1, Standards.** Four LOW: (1) the slice key and an FNV-1a comparer were nested in the Harmony
patch class, an algorithm in an entry point (ADR-002), with a placement comment working around the
co-op veto source scan; (2) `CachedEnumParse`'s public parameterless constructor had no production
caller and built a parser that never logs, against its own summary, and its `Parser` delegate was
public for an internal constructor; (3) `HotPathSettingsProvidersTests` sat at the `TAOM.Tests/Features`
root, mirroring no feature (`tests.md` "Test Organization"), where cross-feature gates live in
`TAOM.Tests/Infrastructure/`; (4) two crush-through tests computed `expected` with the reordered code
itself. Doc nits: `file-catalogue.md:268` limited "MCM wins" to the `*Enabled` getters, and
`localization-override.md:98` listed only the old test categories under the new count. Checks passed:
ADR-002 sizes, ADR-003/004/005, ADR-007, IoC lifetimes, naming, service locator use.

**Agent 2, Engine compatibility.** 18 usages verified against the installed v1.5.3 and MCM, 0
incompatible, 3 unverified (above). Follow-ups: `CavalryChargeServiceTests.cs` ended with a note that
SmartCavalryAISettingsProvider "has no direct unit tests" because MCMv5 is unavailable in the test
host, which this change made false; and an optional test pinning both settings classes as MCM global
settings.

**Agent 3, Efficiency.** No issues. Every changed hot path got cheaper; the slice probe allocates
nothing; the hotkey costs one ordinal compare per frame. The reorders gain little now that a toggle
read is two field loads, but add no complexity.

**Agent 4, Completeness.** Tests, docs, IoC and gates complete; the GitHub issue is drafted in the
run's issue drafts and unfiled by design. L1: the auto ratio's live-neutral floor was not pinned
through the cached settings. L2: `CachedEnumParse` returning `default` on a failure after a success was
not pinned. L3: the `CombatMechanicsSettingsProviderTests` summary still said `Instance` "cannot be set
in a test" although the class now tests the merged path.

**Agent 5, Data flow.** 15 flows traced, 0 gaps. Two LOW inconsistencies: `CachedEnumParse` logs each
time the setting changes to a value that does not parse (re-entering a bad value after a good one logs
again, which its own test asserts with `Received(2)`), while its summary, parameter doc, test name, the
mixed-formations doc and the `bf1898c3` body say once per distinct value; and the file-catalogue scope
(as Agent 1).

**Agent 6, Design.** P1 (APPLY, preserving): one `IEquatable<IdSlice>` struct instead of a struct plus
a comparer class. P2 (APPLY, preserving): the "once per distinct value" wording. P3 (FOLLOW-UP,
changing): a native rebindable GameKey for the cycle hotkey. P4 (FOLLOW-UP, changing): the language
check first in the prefix, for #706. The lazy read-through accessor is the right shape given MCM's
in-place editing.

## Findings and dispositions

| # | Sev | Finding | Lens | Verdict | Disposition |
|---|---|---|---|---|---|
| C1 | LOW | Hash and equality algorithm nested in the Harmony patch class, with a placement workaround for the co-op scan | 1, 6 (P1) | CONFIRMED | `IdSlice` is now an `internal readonly struct : IEquatable<IdSlice>` in `Main/Features/LocalizationOverride/IdSlice.cs`; the comparer class and the placement comment are gone; `IdSliceTests` (5) added red first |
| C2 | LOW | Test-only public constructor on `CachedEnumParse`; public `Parser` delegate | 1 | CONFIRMED | Constructor deleted, `Parser` internal; tests use the constructor production uses |
| C3 | LOW | Cross-feature gate test outside `TAOM.Tests/Infrastructure` | 1 | CONFIRMED | Moved to `TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs` |
| C4 | LOW | Self-oracle `expected` in two crush-through tests | 1, 3 | CONFIRMED | Assert the concrete `true` |
| C5 | LOW | `file-catalogue.md:268` limited MCM's precedence to the toggles | 1, 5 | CONFIRMED | Names every getter: the toggles and the five sliders; `RaceCombatModifiersEnabled` MCM-only |
| C6 | LOW | `localization-override.md:98` described only the old tests | 1 | CONFIRMED | Lists the slice cases and the Substring scan; links `IdSliceTests` |
| C7 | LOW | "Once per distinct value" wording against once-per-change behaviour | 5, 6 (P2), 2 | CONFIRMED | Summary, parameter doc, test summary and comment, test renamed `TryGet_UnusableValue_LogsOnceEachTimeTheSettingChangesToIt`, doc bullet; the `bf1898c3` body is corrected in the follow-up commit body |
| C8 | LOW | Stale "no direct unit tests" note in `CavalryChargeServiceTests.cs`, made false by this change | 2 | CONFIRMED | Deleted |
| C9 | LOW | Live-neutral floor not pinned through the cached settings | 4 (L1) | CONFIRMED | `AutoRatioFloor_FollowsALiveNeutralEdit_ThroughTheCachedSettings` |
| C10 | LOW | `default` after a failure following a success not pinned | 4 (L2) | CONFIRMED | `TryGet_FailureAfterASuccess_ReturnsDefault` |
| C11 | LOW | Stale test-class summary in `CombatMechanicsSettingsProviderTests` | 4 (L3) | CONFIRMED | Rewritten for the two constructors |
| N1 | n/a | GitHub issue for plan 031 not filed | 4 | NEEDS MIKE | Drafted in the run's issue drafts |

No finding was a false positive. No HIGH or MEDIUM finding was raised.

Every new or tightened test was run against a deliberate mutation of the code it guards and failed:
removing `_lastValue = default` (C10), flooring on the JSON neutral ratio (C9), inverting the roll
comparison (C4), a `Substring` call inside `IdSlice.GetHashCode` (the widened
`Prefix_NeverCallsSubstring`), and a non-MCM type row (the global-settings pin). `IdSliceTests` failed
to compile before the type existed. The files were restored byte for byte afterwards.

## Action items

1. Convergence pass owed (Step 4.6): one `deep-reviewer` on the follow-up commit's diff, standards and
   behaviour parity only. The review lead cannot spawn agents.
2. File the plan 031 issue from its draft on Mike's word.
3. Mike: decide P3, P4 and the hotkey `Enum.IsDefined` question (below).

```
─────────────────────────
IMPROVEMENTS (Step 4)
─────────────────────────
APPLIED:
- Main/Features/LocalizationOverride/IdSlice.cs (new) and MBTextManager_GetLocalizedText_Patch.cs:27:
  Agent 6 P1 merged with C1. One IEquatable struct, the dictionary uses its default comparer, no
  nested types, no placement rule. Proof: the 18 MBTextManager_GetLocalizedText_PatchTests and
  CoopVetoClassificationTests green before (baseline run) and after, plus IdSliceTests (5) and the
  widened Prefix_NeverCallsSubstring.
- Main/Features/MixedFormations/CachedEnumParse.cs:6-29, CachedEnumParseTests.cs, mixed-formations.md:150:
  Agent 6 P2 (C7). Wording only; the existing Received(2) assertion pins the behaviour.
- TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs: Agent 2's optional pin,
  CachedSettingsClasses_AreMcmGlobalSettings (2 rows). Test only; it guards the assumption the whole
  change rests on.
NOT APPLIED:
- Agent 1 item 1, second half: moving the _overrides table itself out of the patch behind a TryGet.
  The table predates this change and is storage, not an algorithm; the algorithm moved (C1).
  Simplicity criterion: one more class for no behavioural or test gain.
- Agent 6 P3 (native rebindable GameKey for the cycle hotkey): behaviour-CHANGING (a custom MCM
  string reverts to L once) and a SubModule.cs registration line. Needs Mike.
- Agent 6 P4 (language check first in the localization prefix, #706): behaviour-CHANGING (non-English
  players then see their translations). Pre-existing; tracked by open issue #706. Needs Mike.
FOLLOW-UP (pre-existing code, none filed: issue filing waits on Mike in this run):
- Hotkey strings such as "3" or "L, K" parse into undefined InputKey values passed to native every
  frame; native handling UNVERIFIED (Agent 1). Enum.IsDefined would change which strings are accepted.
- Patch35_Formation_SetMovementOrder.cs:62-65: the catch logs "CancelStanceOnMove disabled for the rest
  of this session", but only the log is one-shot; the postfix keeps running (Agents 1 and 5; re-read).
- CultureDoctrineSettingsProvider: the Morale and Aggression `?? false` fallbacks cannot change a
  result, since both require IsEnabled, which is false whenever Settings is null (Agent 5; re-read).
  The plan's open question can close as no runtime effect.
- ChargeKnockdownService.cs:84-85: a NaN VictimMaxHealth gives an owned false, not null; whether
  Agent.HealthLimit can be NaN is UNVERIFIED (Agent 5).
- NameplateFadeSettingsProvider.cs:18-23 reads TaomSettings.Instance in its constructor, the pattern
  02157b18 removed from BattleBalance; safe today by resolve timing (Agent 4; re-read). Migrate it and
  add it, BattleBalance and NameplateRelation as rows of HotPathSettingsProvidersTests.
- docs/modding/module-dependencies.md "Update a library": after an MCM bump, re-check that
  OverrideSettings, ResetSettings and the menu's preset path still write into the registered object;
  the same doc says 5.12.1 where the csproj and DLL are 5.12.3 (Agents 4 and 5).
- docs/features/elephant.md:246 still describes the Howdah provider tests as "the toggle default" only
  (Agent 4).
- UNVERIFIED: MCM versions other than 5.11.4 and 5.12.3, and a load order with two modules declaring
  MCM.MCMSubModule (Agent 2).

VERDICT: READY FOR COMMIT
```

The verdict holds with the convergence pass owed (action item 1). Final full suite, run in the
worktree after every fix: `Failed! - Failed: 1, Passed: 12423, Skipped: 2, Total: 12426`. The one
failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey` (nine tournament keys without
language rows), present at the start of the review with `Failed: 1, Passed: 12414, Skipped: 2,
Total: 12417`. The nine added results are `IdSliceTests` (5), the two new provider and parser tests,
and the two global-settings rows. `python tools/lint_docs.py` exits 0 with no new dash.

Gate sweep: none. The diff changes no hook, validator or CI step.

## Codex review

Codex not run for this item (no paid dispatch was asked for). Phase 3d assessment table:

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | n/a | n/a | n/a | Codex not run |

Confirmed bugs from Codex: none. False positives: none. Design questions: none from Codex; the
review's own are P3, P4 and the hotkey `Enum.IsDefined` question. Things Codex missed: not applicable.

### AGENTS.md lessons (pending)

For the orchestrator's wrap-up into the Codex lessons (`.ai/review-reference.md` "Look harder here"):

- When a change makes a class testable, look for the notes elsewhere that said it was not (C8, C11).
- A test whose expected value is computed by the code under test proves determinism only (C4).
- A log-once helper's contract is what it remembers: one last value is not a set of seen values (C7).

RCA: [rca-settings-reads-off-hot-paths-2026-10-02.md](rca-settings-reads-off-hot-paths-2026-10-02.md).

## Convergence round 1

A convergence `deep-reviewer` read the fix diff `bf1898c3..3dc3739c` and the orchestrator's focus
again, and found no defect. What it re-checked:

- **Cached settings references.** Against the installed MCMv5 (5.12.3) and MBOptionScreen: the settings
  provider's `Instance` is set once, the providers and global containers are registered once and never
  removed, and a preset, a reset or an edit in the UI copies values into the same object. A cached
  reference therefore keeps reading live values, and all nine providers read the setting per call.
- **Reordered gates.** Each reorder returns the same result as before, for NaN and null inputs too.
- **The localization id slice.** It compares exactly as `Substring` did: ordinal, lengths first, every
  slice in bounds.
- The fix diff's own tests, oracles, doc counts and links.

Outside the diff, not a finding: line 24 of the `3dc3739c` commit body is 84 characters, over the
72-column wrap; history is not rewritten, so it is left for the merge.

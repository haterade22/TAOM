# RCA: Stale Character Repair + battle-load scene-wait heartbeat (2026-09-06)

**Scope:** the deep review of one session's work: a new save-load crash guard
(`Patch83_StaleCharacterRepair`) and an addition to `BattleLoadDiagnostics` (the
`WaitingForSceneLoad` heartbeat plus stall-marker logging). Six agents: standards, API
compatibility, efficiency, completeness, cross-system data flow, and an adversarial pass.

**Top line.** The root-cause diagnosis survived an adversarial attack that eliminated five rival
hypotheses with quoted engine source, two of them using evidence inside the crash bundle itself. The
*fix* did not survive. It repaired one of six null fields on the broken object, which would have
moved the crash from a deterministic load failure to a later party-screen NRE with a stack naming
nothing about save staleness. Separately, a diagnostic shipped in the same session printed an
inverted diagnosis to whoever would triage the next bundle.

Nothing here was caught by the build or by 8,373 passing tests. Every finding came from an agent
reading engine source or executing the tool.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Repair covered `DefaultCharacterSkills` only. `UpgradeTargets` (null, an auto-property *initializer* the save system skips) crashes the party screen at `PartyCharacterVM:1113`; `BodyPropertyRange` crashes agent spawn | Scope | Fixed the frame in the stack trace instead of the object's state. Never asked "what else is null on this object?" | New lesson, below. Widened to four fields |
| 2 | HIGH | `triage_battle_load.py` printed "the main thread BLOCKED inside one frame (the #352 shape)" for heartbeat-sourced `polls=1`, which means the opposite | Cross-language contract | Reused `FinishMissionLoadingBegin`'s token pair for parser convenience and inherited its *interpretation* without re-deriving it for the new emit site | New lesson, below. Branch on `wait_incomplete`; two tests pin both readings |
| 3 | HIGH | The heartbeat is driven by the loop it measures, so it cannot observe a single-frame native spin: the exact failure its own comment claimed to close | Diagnostic design | Wrote the justification from the symptom ("dies and hangs look identical") without tracing what drives the marker | Documented as the *second reading* (silence on a long load IS the spin signal) in the enum, both triage hints and the feature doc |
| 4 | MED | Three false claims in shipped docs: "also fires on a new game and on the initial data load" (it fires on neither), "SubModule's guarded loop logs it and carries on" (there was no guard), "by then vanilla has fixed what it can" (`CharacterObject` overrides neither hook) | Evidence | Wrote *rationale* prose containing empirical claims and verified none of them. Reasoning felt like knowledge | New lesson, below |
| 5 | MED | `_harmony.PatchCategory` + `IoC.Resolve` unguarded, so a renamed binding would throw out of `OnSubModuleLoad` and abort ~250 lines of module init | Robustness | Copied Patch58's bare shape without asking what this patch's failure mode costs. Made worse by finding 4: the docs described a guard that did not exist | Wrapped in the Patch61/62 `try`/`catch` + `LogError` shape |
| 6 | MED | `bool` return collapsed five outcomes. "Already repaired" (safe) and "engine binding lost" (catastrophic, logged nowhere) both reported as "could NOT repair, can still crash the campaign" | API design | Designed the return type around the happy path and let every other case fall into `false` | `StubRepairOutcome` enum; five outcomes, each with its own report |
| 7 | MED | No player-visible notice. Stale ids are re-serialised into every later save while the repair is not, so the player overwrites their last recoverable file unwarned | UX / data safety | Treated a crash guard as a code concern and never asked what the player needs to *do* | Deferred `OnSessionLaunched` in-game message |
| 8 | MED | `dominant:` named bucket1 on exactly the logs the feature exists for, because bucket2's span needs a marker a stalled load never writes | Tooling | Added the marker without re-reading what the *report* does with a half-open bucket | bucket2 renders `>=Nms`; dominant names it |
| 9 | LOW | Nothing pinned `SCENE_LOAD_WAIT_INTERVAL_MS` against the C# constant, though it is interpolated into player-facing prose | Twin literal | The sibling `Format*Detail` literals *are* pinned; I added a third and did not follow the precedent | Test reads the C# source; verified it fails on divergence |
| 10 | LOW | `SCENE_TERMINALS` had no `⊆ _SCENE_HINTS` invariant, a latent `KeyError` for the next terminal added | Tooling | Same blind spot as 9: added a member to a set whose consumers I did not re-read | Invariant test |
| 11 | LOW | `nowMs < lastEmitMs` arm untested while the identical arm on the sibling seam was | Test parity | Wrote tests for the cases I was thinking about, not against the sibling's existing coverage | Test added |
| 12 | LOW | `_currentSceneName` set *after* the origin was armed, so correctness rested on the 3s threshold rather than on structure | Ordering | Relied on a threshold value for a property that ordering could guarantee outright | Reordered; unreachability is now structural |

## Root-cause patterns

### Pattern A: I fixed the frame in the stack trace, not the object's state (findings 1, 6)

The crash report named `GetSkillValue`, so I repaired the field `GetSkillValue` reads. That is
fixing the *symptom's location*. The actual defect was "this object never went through
`Deserialize`", and its blast radius is every field `Deserialize` would have set. `CharacterObject`
persists exactly two fields, which was readable in thirty seconds and would have reframed the whole
fix.

The existing rule closest to this is `.claude/rules/csharp-architecture.md` "Entity State Matrix",
which mandates enumerating entity states before mutating on load. It did not fire because it is
scoped to `CampaignBehaviorBase` OnGameLoaded handlers, and this is a Harmony postfix. **Third
consecutive instance of a rule missing by one category** (the NaN-gate class did this four times
before its scope was widened to name four categories explicitly).

### Pattern B: reusing a format inherits its interpretation (finding 2)

I reused `polls=`/`waitMs=` so one regex would parse both lines. The tokens transferred; the meaning
did not, because the two lines are emitted from different points in the loop. The reuse was correct
and the failure was in not re-deriving what each value *means* at the new site. This is the
diagnostic-tooling analogue of the "provenance change" NaN category: a gate (here, a reading rule)
was correct for its original inputs and silently wrong for a new source.

### Pattern C: rationale prose is where unverified claims hide (findings 3, 4)

All four false statements were in *explanations of why the design is right*, not in descriptions of
what the code does. Comments like "it also fires on a new game", "by then vanilla has fixed what it
can", "the reading rule is identical" read as design reasoning, so they bypassed the reflex that
would have fired on a bare factual claim. Every one was falsifiable in one grep, and three agents
independently falsified the same one.

## Why each agent missed what it missed

- **Standards** caught findings 4 and 5 and passed all ten formal checks. It could not catch 1
  because ADR compliance says nothing about *which* fields a repair covers.
- **API compatibility** verified all ten engine claims and independently refuted the call-site
  claim. It did not catch 1 because it was asked "do these bindings exist?", not "is this the right
  set of bindings?"
- **Efficiency** correctly found nothing; it decompiled `Stopwatch`, `Interlocked` and
  `GetObjectTypeList` rather than estimating, and refused to recommend a log-level change without
  reading `FileLogger` first.
- **Completeness** found the missing issues, the missing INDEX row and the test-parity gap (11). It
  cannot see semantic scope.
- **Data flow** found 2, 3, 6, 8, 9, 10, 12: the largest haul, and the two HIGHs on the diagnostics
  side. Its structural remit (trace the contract end to end, enumerate the state machine) is what
  surfaced them.
- **Adversarial** found 1 and 7, and only because it was told to assume the fix was harmful and
  prove it. A confirm-oriented pass would have read the same files and agreed with them. **This is
  the argument for keeping an explicitly adversarial agent in the set**: the other five all
  validated a fix that would have moved the crash.

## Lessons to codify

Appended to `docs/reviews/lessons/state-lifecycle-save.md` (Pattern A) and
`docs/reviews/lessons/harmony-il.md` (Pattern C). Pattern B is recorded in
`docs/reviews/lessons/testing-qa.md` as a diagnostics-contract lesson.

## Not fixed, deliberately

- **`_culture` and `Level` on a stale character.** Culture cannot be invented without a silent
  gameplay lie; `Level` lived only in the deleted XML. The character is inert, not correct, and the
  feature doc says so.
- **The underlying data defect.** The repair keeps the save loadable; the ids it names are the real
  fix, which is why they are logged and shown in game.
- **Crash A (`battle_terrain_biome_094` CTD).** Root cause unproven; it needs the player's
  `rgl_log`. Only instrumentation shipped.

## Environment note for anyone rebuilding this

The crash bundle's save was written by TAOM v2.0.18 on Bannerlord v1.4.7 and loaded by v2.0.27 on
v1.4.8, and its `[BuildStamp]` reports a TAOM / TAOM.Dependencies mismatch (issue #371). Neither
causes the NRE, but both belong in any analysis built on this bundle. Separately, this laptop's
`LOTRLOME_Armory/ModuleData` is empty, so `BannerBearerReplacementWeaponDataTests` fails here for
environmental reasons unrelated to any of the above.

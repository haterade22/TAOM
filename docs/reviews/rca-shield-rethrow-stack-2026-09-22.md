# RCA: shield rethrows erased the throw site from every crash report (2026-09-22)

**Trigger:** player crash bundle `2d446100` (TAOM v2.0.28 build `8dabf4a6`, Bannerlord 1.4.8). The
campaign map tick threw four times in one session, and every report ended at the same five frames:

```
System.ArgumentNullException: Value cannot be null. Parameter name: source
  at TaleWorlds.CampaignSystem.GameState.MapState.OnTick_Patch2(MapState this, Single dt)
  at TaleWorlds.Core.GameStateManager.OnTick(Single dt)
  at TaleWorlds.Core.Game.OnTick(Single dt)
  at TaleWorlds.Core.GameManagerBase.OnTick(Single dt)
  at TaleWorlds.MountAndBlade.Module.OnApplicationTick_Patch3(Module this, Single dt)
```

`MapState.OnTick` threw nothing itself. Correlating the engine log showed each throw landing 0.1 to
0.6 s after `creating hero from template with id: lord_AB8_1` or `lord_6_23_8`, which the v1.4.8
decompile resolves to `HeroCreator.DeliverOffSpring` in the daily pregnancy tick, several calls below
`MapState.OnTick`. The report could not say where. (The childbirth crash itself has an issue drafted
but not yet filed: creating issues was denied by the harness in this session. It is waiting on the
player's save or a full stack.)

## Root cause

PatchShield attaches a finalizer to every Harmony-patched method in the process (1,476 in that
session). For any exception outside the missing-API trinity it returns the original exception so it
keeps propagating. When any finalizer on a method returns a value, Harmony 2.4.2's generated wrapper
stores it and ends with the `throw` opcode (`MethodCreator.CreateReplacement`; only an all-void
finalizer set gets `rethrow`), and throwing an existing exception object replaces its stack trace with
the frames from that point outward. Every frame between the throw site and the shielded method was
lost, once per shielded method the exception crossed.

The bundle's own Harmony correlation lists `PatchShield.ShieldFinalizerVoid` on
`MapState.OnTick_Patch2`, so that frame's reset is attributable. `DeliverOffSpring` was most likely
reset too. TAOM applies `Patch13_RaceAge` (its transpiler) and `Patch43` (the `MapState.OnTick`
postfix) in its own `OnGameInitializationFinished`, which the engine calls after TAOM.Dependencies'
(module order), so PatchShield's pass 2 in Dependencies misses both on a process's first game. Pass 2
has no once-guard, though, and reruns on every game init. The player loaded a save at 20:45:50 into a
process whose campaign had been running since at least 20:37:58, so every crash happened in the
second game, after both methods were shielded. That fits `_Patch2` (Patch43, then the shield).

`RethrowStackPreserverTests.HarmonyRethrow_FinalizerReturnsException_LosesTheThrowSiteFrame` proves
the premise with a real Harmony patch in the test host, against the same `0Harmony.dll` the game
loads: after the rethrow, the throw-site method is absent from `StackTrace`.

## Second consequence: distinct crashes shared a signature

`CrashSignatureCalculator` hashes the exception identity, the origin, and the top five frames from
`new StackTrace(ex)`. That constructor reads only the segment after the last throw. Every exception of
one type passing through one shielded method therefore got the same signature, and
`CrashBundleThrottle` suppressed all but the first. In this session occurrences #2 and #3 of
`40de8e64` were suppressed, with no way to tell whether they were the same bug.

## Why it survived this long

The reset was known and written down twice:

- `PatchShield.cs` (the #354 fix, 2026-07-21) stopped unwrapping `TargetInvocationException` before
  returning it, because that destroyed the inner exception's stack. That rescued the reflection-invoked
  case and left a plain exception's own frames exactly as exposed as before.
- `SaveShield.cs` recorded that "each outer pass would walk a truncated stack" and answered it with an
  attribute-once rule, which fixes the culprit column and nothing else.

Both times the fix covered the case in hand, and the comment describing the wider limitation read as
if the limitation were handled.

## Fix

| File | Change |
|---|---|
| `Dependencies/Foundation/RethrowStackPreserver.cs` | New. On a finalizer's rethrow path, moves the current trace into `_remoteStackTraceString` (what `Exception.InternalPreserveStackTrace` does) with a marker line naming the rethrowing method, clears the live trace the same way, and records the first five frames of the original throw under `Exception.Data["TAOM.ThrowSite"]`. Returns the same instance, so a finalizer writes one `return`. Idempotent per rethrow (returns early when the live trace is already null), fails open (a runtime without these fields keeps today's truncated trace). |
| `Dependencies/Foundation/PatchShield.cs` | Both finalizers return through it and count the rethrow; `WriteSessionSummary` prints "rethrew N with the stack preserved", apart from the swallow counts. The no-exception path is still a single null check, since Harmony runs finalizers on every call. |
| `Dependencies/Foundation/SaveShield.cs` | All three rethrow paths return through it. The attribute-once rule stays, because `AttributeCulprit` walks frames, not trace text. |
| `Main/Features/CrashReport/Collectors/CrashSignatureCalculator.cs` | `DescribeIdentity` appends `|site=<frames>` after each level of the inner chain that has one. An exception that never crossed a shield hashes exactly as before; one that did gets a new signature once, on purpose. |
| `Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs` | When `TAOM.ThrowSite` is present, the `Stack Frames` and `Patches on throwing call stack` sections say they show the last segment only. |

A preserved report now reads, innermost first (illustrative, shaped like the nested-shield test's
real output; frame names here are the player's case, not a captured trace):

```
   at <real throw site>
   at <...>
   at TaleWorlds...HeroCreator.DeliverOffSpring_Patch1(...)
   --- End of stack trace from previous location (rethrown by a Harmony finalizer on TaleWorlds.CampaignSystem.HeroCreator.DeliverOffSpring) ---
   at TaleWorlds...HeroCreator.DeliverOffSpring_Patch1(...)
   at <...>
   at TaleWorlds...MapState.OnTick_Patch2(...)
   --- End of stack trace from previous location (rethrown by a Harmony finalizer on TaleWorlds.CampaignSystem.GameState.MapState.OnTick) ---
   at TaleWorlds...MapState.OnTick_Patch2(...)
   ...
```

and the report's `Data` block carries `TAOM.ThrowSite = <frame> <- <frame> ...`. The `Stack Frames`
and `Patches on throwing call stack` sections still cover only the last segment, because they come
from `new StackTrace(ex)`; the trace text and `TAOM.ThrowSite` are where the inner frames live.

## Rejected alternatives

- **Return a wrapping exception with the original as `InnerException`.** Keeps the stack but changes
  the exception TYPE every caller sees, so any vanilla or third-party `catch (SpecificException)`
  above a shielded method would stop matching. PatchShield exists to be invisible on this path.
- **Call `ExceptionDispatchInfo.Capture(ex).Throw()` from the finalizer.** Cannot work: Harmony wraps
  each finalizer call on the exceptional path in its own try/catch that pops what it throws, then
  throws the shared slot anyway (`MethodCreator.AddFinalizers`, verified by the deep review's engine and
  design lenses).
- **Record the trace in `Exception.Data` only, and teach the crash reporter to print it.** Only TAOM's
  reporter would see it; the engine log, BUTR's report and any debugger would still get the truncated
  trace.

## Scope left open (follow-up)

Twenty exception-returning finalizer methods across twelve files outside the two shields still return
the exception without preserving it: ten feature files (`Patch50` drop-flagged-item guard, `Patch49`
army gathering guard, `Patch69`, `Patch62` movie-release guard, `Patch20` youth-menu guard, `Patch77`,
`Patch65`, `Patch88` x2, `Patch56` scene-notification guard), plus the crash reporter's own Patch37 and
Native2Managed finalizers, which return the exception only on their fallback paths. The ten
SaveLoadDiagnostics finalizers are `void`, so their methods keep Harmony's `rethrow` and lose nothing.

PatchShield covers one of these only when the target was already patched when pass 2 ran, and TAOM's
own `OnGameInitializationFinished` categories (`Patch65`, `Patch88`, `Patch69`, `Patch77`, `Patch56`,
`Patch20`, `Patch50`) are applied after it. So on the FIRST game of every process those methods rethrow
with the trace reset, PatchShield installed or not, and on every game with a co-op module or
`patchshield-disabled.flag`. Gauntlet-namespace targets (`Patch62`) are never shielded. The fix is one
line per rethrow path (`return RethrowStackPreserver.PreserveForRethrow(__exception, null)`, passing
`null` so no hot finalizer gains an `__originalMethod` binding and its per-call cost), or making an
observe-only finalizer `void`. It touches code this change did not modify, so the deep review ruled it
out of this change. The first-game shielding gap itself predates this change.

## Deep review (2026-09-22, six lenses, two waves)

No CRITICAL or HIGH. Every finding below was re-verified against the source or reproduced by a RED
test before it was fixed.

| # | Sev | Finding | Lens | Why missed | Fixed by |
|---|---|---|---|---|---|
| 1 | MED | A throw site recorded on an INNER exception never reached the signature: a `TargetInvocationException`'s own site is the reflection path and its inner's `TargetSite` the shielded wrapper, both constants, so two UI crashes still collided. Reproduced: both hashed `9c9f3752`. | Data flow | The signature change was designed from the player's plain-exception shape. The shape the calculator was built for (#552, TIE at `ScreenManager.Update`) sat in the same test file and was not re-walked. | Site appended per chain level; `Compute_TieWrappingInnersWithDifferentThrowSites_ReturnsDifferentSignatures`. |
| 2 | MED | The docs said feature finalizers matter "only with PatchShield uninstalled", called the void SaveLoadDiagnostics finalizers resetters, and stated "Harmony rethrows with `throw`" universally. | Engine compat, Data flow | PatchShield's coverage was reasoned from what it enumerates ("every patched method"), not from WHEN it enumerates relative to TAOM's own patching. The Harmony rule was generalised from the one finalizer shape the tests used (value-returning). | Corrected here, in the lesson, `dr3-maintenance.md`, `submodule-lifecycle-and-harmony.md` and the CHANGELOG; new lesson on enumeration timing. |
| 3 | MED | Nothing measured how often exceptions cross a shield in normal play, where the preserver's cost lands. | Efficiency | Costing stopped at "exception path only". The existing lesson that a blanket mechanism's population is chosen by the modlist applies to crossings as much as to calls. | `PatchShield.RethrownCount` in the session summary; `ShieldFinalizer_NonTrinityRethrow_IsCountedApartFromSwallows`. |
| 4 | LOW | The lesson-count edit stripped `LESSONS-LEARNED.md`'s UTF-8 BOM. | Standards | `[IO.File]::ReadAllText` strips the BOM from the string it returns, so the "keep a BOM if present" check on that string was always false. | BOM restored byte-level; lesson in `build-tooling-workflow.md`. |
| 5 | LOW | `ThrowSiteFrameCount` (5) and the ` <- ` join are hashed into every shielded signature and pinned by nothing. | Completeness | The sibling cap `InnerChainDepth` has a pin; the new cap was added without asking the same question. | `PreserveForRethrow_DeepStack_RecordsExactlyFiveFramesInnermostFirst`. |
| 6 | LOW | The report's `Stack Frames` and `Patches on throwing call stack` sections silently cover only the last segment. | Data flow | The docs said so; the report a triager actually reads did not. | Renderer note; two renderer tests. |
| 7 | LOW | The crash reporter reads frames correctly only because its finalizer (800) outranks PatchShield's (400) on shared methods; unpinned. | Data flow | The ordering was implicit in two unrelated priority choices. | `CrashReporterFinalizer_SharesAMethodWithPatchShield_SeesLiveFramesBeforeThePreserve`, `CrashReporterFinalizers_OutrankPatchShield`, and a sentence in the class summary. |
| 8 | LOW | `feature-map.md` still called the layer "11-class"; this change moved the real count to 18. | Completeness | The map row duplicates a count the maintenance doc owns. | Row updated. |
| 9 | LOW | `.claude/rules/harmony-patches.md`, the rule that loads under `Main/**/Hooks/**`, had no Finalizer entry, so the preserve-before-rethrow rule lived only where a patch author does not look. | Standards, Completeness | The lesson was placed in the category file and the engine doc, not in the path-scoped rule. | One bullet under "Patch Types". |

Improvements applied (design lens, behaviour-preserving): `PreserveForRethrow` returns its argument,
which deleted SaveShield's private `Rethrow` wrapper and made every rethrow path one `return`
(`PreserveForRethrow_ReturnsTheSameInstance`). Not applied, with reasons: collapsing the innermost
pass to one stack walk (efficiency; it would bypass a custom exception's virtual `StackTrace`), a
remote-string length cap (no trigger exists), merging PatchShield's two identical finalizers (the
rename ripples into other docs and patches; follow-up), and an experiment with
`Exception.PrepareForForeignExceptionRaise` that could let `new StackTrace(ex)` see the real frames
(a private InternalCall whose per-thread semantics could not be read from source; follow-up).

**Convergence pass** (one reviewer on the fix-pass diff, no new design round): no CRITICAL or HIGH;
parity confirmed (a chain with no recorded site hashes, and a report without one renders, byte for
byte as at HEAD). Fixed: one MED, `dr3-maintenance.md` still naming the deleted `SaveShield.Rethrow`
(a doc sentence written before a wrapper was deleted and not re-read after); three LOW wording fixes
(the class summary now says the crash reporter's Native2Managed bridge finalizers run before
PatchShield's only by insertion order at equal priority, and that a cached `TargetSite` survives the
preserve; the "twenty" finalizer count now separates ten feature finalizers from the reporter's ten
fallback paths); one duplicated test bullet. Follow-up, not applied (file outside this change): pin
the Native2Managed bridge at `priority = 800` like Patch37, so its ordering stops depending on
insertion order.

**Why each lens caught what it did.** Nothing was missed by the gate. Engine compatibility and Data
flow both decompiled Harmony and the module dispatch order independently and converged on finding 2;
Data flow alone walked the TIE shape (finding 1); Efficiency alone asked how often the exception path
runs (finding 3). The author-side misses share one root, below.

## The pattern

Findings 1 and 2 are the same mistake at two scales: reasoning from the single shape in hand (a plain
exception, a value-returning finalizer, PatchShield as "wraps everything") instead of enumerating the
shapes the touched code already serves. The calculator's own tests named the TIE shape; the Harmony
decompile names the void-finalizer branch; `Dependencies/SubModule.cs` names when pass 2 runs. Each
was one read away.

## Lessons

- `docs/reviews/lessons/harmony-il.md`: "A value-returning finalizer that hands back its exception
  erases the throw site" (corrected by this review) and "A pass that enumerates everything patched
  covers only what was patched before it ran".
- `docs/reviews/lessons/build-tooling-workflow.md`: "`ReadAllText` strips the BOM, so a
  preserve-the-BOM check on its result can never fire".

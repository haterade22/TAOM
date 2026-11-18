# RCA: #1429 port of Silas He's crash and hang fixes (2026-11-17)

**Scope:** Patch159 (help defenders against an attached camp leader), Patch160 (saved-game load retry stop),
Patch161 (scene widget without a scene), and the messenger arrival for a target in the army party the player is
attached to. Branch `fix/silas-port` at base 55806e3df, reviewed uncommitted. Tier 1: Lens 2 Engine compatibility,
Lens 5 Data flow, Lens 7 XML. Codex skipped (out of usage since 2026-11-15); its pass is owed before merge.

**Top line:** one MED and four LOW findings, all in Patch160 and Patch161, and all from one gap. The port re-derived
each fix from the decompile of the engine method it patched, but not from the TAOM wrappers around that method:
PatchShield, which attaches a finalizer to every newly patched engine method, and Patch37, whose swallow is the only
reason the "vanilla" load retry exists. Lens 7 found nothing. The messenger change and Patch159 had no finding.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Patching `SandBoxGameManager.DoLoadingForGameManager` made PatchShield shield it. A swallowed missing-API exception leaves the override's pre-set `nextStep = None`, which `GameManagerBase` reads from step 5 as "loading finished" (v1.5.7 GameManagerBase.cs:227-233), so the map opens on a half-loaded campaign; the unpatch after the swallow strips Patch160's prefix. Fixed: the method is on `PatchShieldPolicy.ExcludedTargetMethods`, pinned by `PatchShield_DoesNotShieldTheTarget`. | Harmony, finalizer set | The builder listed the target's callers but not its whole finalizer set, which lessons/harmony-il.md already requires for every new engine patch (2026-10-02 entry). The build brief did not name that lesson. | Repeat of a recorded lesson: the lesson gains a line on out-parameter sentinels (below). |
| 2 | LOW | Patch160's finalizer latch was one `static bool` per process, so any earlier throw hid a later load's stack, while the prompt says "The error is in the TAOM log". Fixed: keyed on the manager (`_reportedFor`), tested by `Finalizer_ALaterLoadInTheSameProcess_LogsItsOwnFailure`. | State lifetime | Fifth instance of the process-scoped latch lesson. The builder thought of "once" as "once per failure" without naming the lifetime. | Existing lesson covers it; the registry and doc now state each static's lifetime. |
| 3 | LOW | Patch161's texts said "once per session" for a process-scoped `_warned`. Fixed: comment states the lifetime, test renamed `WarnsOncePerProcess`, docs say "per game process". | State lifetime | Same as 2. | Same as 2. |
| 4 | LOW | Patch160's binding test did not pin the parameter names Harmony binds (`gameManagerLoadingStep`, `nextStep`). Fixed: `SandBoxGameManager_StepParameters_HaveTheNamesThePatchBinds`. | Harmony binding | The Patch159 test pinned names; the Patch160 test was written from the method-resolution template, which checks types only. | Existing lesson (harmony-il.md, "pin the names"). |
| 5 | LOW | Five engine claims in text were wrong: the load retry is not vanilla (Patch37 swallows the exception, Patch37_CrashReport.cs:47-54); `SceneView.SetScene` throws a managed NRE, not a native fault (SceneView.cs:20-23); `SetScene` never makes the texture of the guarded tableau (`SceneTextureProvider` builds a new tableau per scene); `NativeObject ==` handles null; "TAOM's game managers derive from CustomGameManager" omitted the autorun host, which builds a `SandBoxGameManager` for new campaigns only. The Patch159 caller list missed three callers and called main-party defenders attackers (fix re-review). The Patch160 co-op row said no campaign exists yet; `SetLoadingParameters` has already set `Campaign.Current` (Campaign.cs:1372) when the prefix acts (fix re-review). Fixed in the code comments, tests, registry and feature doc. | Engine claims | The builder stated the mechanism of the engine method it read and inferred the rest of the chain (who catches, who builds the next tableau) without reading it. | Existing rule (AGENTS.md "Research first": quote the line that produces the effect). |

Not fixed, recorded:

- `FOLLOW-UP` (#1429): Patch160's rule misses a throw before `_loadedGameResult = null` (SandBoxGameManager.cs:79-80). The retry then runs `LoadSaveGame` again with the same result and the prefix never matches. Whether that loops is unverified and no producer is named. A rule such as "the second entry into step 3 for the same manager" covers it but adds state, so it is Mike's decision (simplicity criterion).
- `FOLLOW-UP` (#1429): the quit inquiry is shown after `LoadingWindow.DisableGlobalLoadingWindow()`, the call vanilla's cancel path makes. Whether it renders, takes a click and quits cleanly with a half-loaded `Game.Current` is an owed in-game check.
- Accepted: the messenger verdict is computed on the hourly tick, not when the player clicks the inquiry (Lens 5 #4). Nothing found changes army membership while the pausing inquiry is open.

## Root-cause pattern

Findings 1 and 5 share one cause: the builder treated the engine method as the whole system. In TAOM a patched
engine method also gains PatchShield's finalizer, and every exception that escapes the tick meets Patch37. A port
of someone else's guard is the riskiest case, because the original author's environment had neither wrapper.

## Why each agent missed these

- The builder (this session) ran `/verify` green; no gate checks a new patch target against PatchShield's semantics.
  `HarmonyPatchBindingTests` and the field-injection gates check bindings, not finalizer sets.
- Lens 7 (XML) had no C# logic in scope.
- Lens 2 and Lens 5 found every item; the briefs asked the finalizer-set question directly.

## Lessons

- lessons/harmony-il.md, "A new patch on an engine method also gets PatchShield's finalizer": a line added for a
  target that pre-sets an out parameter or return sentinel before its work (finding 1).
- No new feedback memory: the existing lessons cover findings 2 to 5.

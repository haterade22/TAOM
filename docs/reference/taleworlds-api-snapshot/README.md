# TaleWorlds API Snapshot + Binding-Verification Gate

A committed, in-repo snapshot of the exact TaleWorlds engine surface (the pinned game version) that TAOM patches, overrides, and reflects into, plus the offline test gate that keeps it honest. The goal: **answer "what's the signature of X / is this member still here in the pinned version?" without the external decompile dump** (`E:\Decompiled_Bannerlord\`), and **turn a silent in-game binding failure into a red `dotnet test`.**

## Why this exists

The v1.3.15 → v1.4.5 migration completed its code stages but the formal runtime validation gate (S6 smoke test) was never run as a discrete step — see [`docs/migration/TRACKING.md`](../../migration/TRACKING.md). The compiler proves a lot (an `override` whose base signature drifted is CS0115; `AccessTools` typos are flagged by the BUTR analyzer below), but three failure modes slip through a green build and surface only at game load — silently:

1. **Harmony resolution ambiguity** — a name-only `[HarmonyPatch]` on a method that is overloaded *anywhere in the type hierarchy* throws `AmbiguousMatchException` at `PatchAll`, so the patch never applies. (The compiler and the BUTR analyzer both pass it — the member exists.)
2. **Auxiliary reflection drift** — a private engine field/method reached by string name returns null after a rename; the reflecting code logs-and-survives, so the feature degrades with no crash.
3. **Unregistered GameModel** — a `Taom*Model` that compiles but is never `AddModel`'d; the engine silently uses the vanilla `Default`.

## Files

| File | What it is |
|------|------------|
| [`reflection-sites.md`](./reflection-sites.md) | Authoritative catalogue of every reflection touchpoint, grouped by gated / runtime-dynamic / internal. Data source for `ReflectionSiteBindingTests`. |
| [`gamemodel-bases.md`](./gamemodel-bases.md) | Auto-generated: each `Taom*Model`, its `Default*Model` base, and the exact signature of every overridden method, as of the pinned version. |
| [`patch-targets.md`](./patch-targets.md) | Auto-generated: each `[HarmonyPatch]` / `TargetMethod` class and the engine method it patches, resolved as Harmony resolves it. The file's `Patches:` header line holds the current count. A `TargetMethods()` class is recorded by its first target only (#1038). |
| [`../../../tools/snapshot_api_surface.ps1`](../../../tools/snapshot_api_surface.ps1) | Regenerates the two auto-generated files from the installed DLLs. Auto-derives the type list from `TAOM.dll` and `TAOM.Client.dll`. |

> **The generator is PowerShell 7 only.** Windows PowerShell 5.1 stops at its `#requires` line.
>
> `/verify` Step 2d runs `-Check` and fails on drift; regenerate and commit with the change.

## The gate (`TAOM.Tests/Migration/`)

Runs under `dotnet test`, no game launch. TAOM.Tests references the installed `TaleWorlds.*.dll` (the pinned game version); `GameAssemblies.cs` pre-loads the SandBox/CustomBattle/StoryMode module DLLs from the install so every engine type resolves.

| Test class | Covers | Granularity |
|------------|--------|-------------|
| `HarmonyPatchBindingTests` | Every `[HarmonyPatch]` / `TargetMethod` target, resolved as Harmony does at `PatchAll` (count: `patch-targets.md` header). | one assertion accumulating all failures |
| `GameModelOverrideBindingTests` | Every GameModel: registered in `SubModule.cs`, override ≥1 base virtual, no shadow-without-`override` (count: `gamemodel-bases.md` header). | 2 tests |
| `ReflectionSiteBindingTests` | The auxiliary static-engine reflection members from `reflection-sites.md` Category B. | one `[DataRow]` per site |
| `ViewModelMixinBindingTests` | Every UIExtenderEx `[ViewModelMixin("Method")]` names one non-abstract method on its target view model. | one assertion accumulating all failures |

> **First run (2026-05-28) caught a real defect.** `HeroViewModel_FillFrom_Patch` used a name-only `[HarmonyPatch(typeof(HeroViewModel), "FillFrom")]`. `HeroViewModel` inherits two more `FillFrom` overloads from `CharacterViewModel`, so Harmony's `AccessTools.Method` resolution found 3 candidates and threw `AmbiguousMatchException` — the postfix silently never applied in v1.4.5 (hero-portrait clan colors broken). Fixed by pinning argument types. This is exactly the gap the compiler and the BUTR analyzer miss.

## Running the gate

```bash
dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"
```

The classes are in `TAOM.Tests/Migration/`: `HarmonyPatchBindingTests`, `GameModelOverrideBindingTests`, `ReflectionSiteBindingTests` and `ViewModelMixinBindingTests`.

**Skipped is never green.** `BANNERLORD_GAME_DIR` (or `BANNERLORD_OVERRIDE_DIR`) points the gate at the install; with neither set, the gate uses the folder the build recorded. A no-game RefAsm build records its stub `refasm-game` folder and loads only that, whatever the variables say ([verification](../../../.ai/verification.md)). With no usable install, the tests that need the module DLLs call `Assert.Inconclusive`, and `binding-gate.runsettings` turns each one into a failure. Without that file MSTest reports them as Skipped and exits 0: never quote such a run, or any run with a non-zero `Skipped:` count, as a green gate. A missing install is an environment fact to report, not to fix.

**Reading a failure (do not just rerun):**

- `Game assemblies not loaded`, `unavailable` or `Game dir unresolved` means no install resolved. Report it and change no code.
- `Main/SubModule.cs not found`: the test ran outside a `TAOM.sln` tree. Rerun from the repo.
- `No test matches the given testcase filter` with a non-zero exit on the command as written is a finding, not a command to edit. An MSTest discovery warning above it means `/investigate`; with none, the gate tests have most likely lost their `BindingVerification` category. Never edit the settings.

| Failure | Meaning | Fix |
|---|---|---|
| `Only N [HarmonyPatch] types discovered` or `Only N GameModel subclasses discovered` | TAOM types failed to load: a stale build, or a test-time variable naming a different install | Rebuild without `--no-build`, check the variables, then `/investigate` |
| `AmbiguousMatchException` on a patch | A name-only `[HarmonyPatch]` on an overloaded method | Pin the argument types |
| A target or member `did not resolve` or `not found` | The engine renamed, moved or removed it | Look up the new signature (`pwsh tools/taom-src.ps1 path <Type>`) and update the patch, adapter or GameModel; for a reflection site update both `reflection-sites.md` and the `[DataRow]` |
| A GameModel `never AddModel'd` | The model compiles but is not registered | Add the `AddModel` call in `Main/SubModule.cs` |
| A `[ViewModelMixin]` refresh method `is not declared` | UIExtenderEx throws a NullReferenceException at `UIExtender.Register` for a name that no longer resolves | Look up the renamed method (`pwsh tools/taom-src.ps1 path <Type>`) and update the attribute |

**Refresh:** `pwsh tools/snapshot_api_surface.ps1`, then `pwsh tools/snapshot_api_surface.ps1 -Check` (exit 0 means the committed files reproduce). A new reflection site needs a row in `reflection-sites.md` Category B and a matching `[DataRow]` in `ReflectionSiteBindingTests.cs`; runtime-dynamic sites go in Category C. The gate is offline only: the in-game residue is in `docs/migration/s6-runtime-punchlist.md`.

## Relationship to BUTR.Harmony.Analyzer (compile-time)

TAOM already references [`BUTR.Harmony.Analyzer`](https://github.com/BUTR/BUTR.Harmony.Analyzer) (`Main/TAOM.csproj`). It is a Roslyn analyzer that validates `AccessTools.*` member references at **compile time** against the referenced assemblies (via `System.Reflection.Metadata`, so it can see non-public members) and flags typos like `_privateFld` for `_privateField`. It is excellent and catches a large class of drift early.

This snapshot gate is the **complement**, not a replacement, because the analyzer:

- emits **warnings**, which a warning-tolerant build ignores — the gate is a hard test failure;
- checks member **existence**, not Harmony's **resolution semantics** — it passed the `HeroViewModel` ambiguity above (the member exists; the *resolution* is ambiguous);
- covers `AccessTools.*` but not raw `typeof(X).GetField(...)` reflection (used in several catalogue sites) or `TargetMethod()` computed targets;
- says nothing about GameModel **registration** (`AddModel`), which is not a reflection concern.

In short: the analyzer is compile-time existence checking against referenced metadata; this gate is a runtime resolution check against the **installed** engine, plus registration coverage.

## Maintenance

`/verify` Step 2d runs `-Check` and fails on drift; regenerate and commit with the change.

## External references

- HarmonyLib — patching overloaded methods requires an argument-type array: `[HarmonyPatch(typeof(String), "IndexOf", new Type[] { typeof(char), typeof(int) })]`. See <https://harmony.pardeike.net/articles/annotations.html> (`/pardeike/harmony` docs).
- HarmonyLib `TargetMethod()` / `TargetMethods()` — return `MethodBase` / `IEnumerable<MethodBase>` for computed targets. See <https://harmony.pardeike.net/articles/patching-auxiliary.html>.
- BUTR.Harmony.Analyzer — compile-time `AccessTools` member validation. See <https://github.com/BUTR/BUTR.Harmony.Analyzer>.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/creature-mount-authoring.md](../../ai-includes/creature-mount-authoring.md)
- [docs/features/lotr-issues.md](../../features/lotr-issues.md)
- [docs/reference/engine/submodule-lifecycle-and-harmony.md](../engine/submodule-lifecycle-and-harmony.md)
- [docs/reference/taleworlds-api-snapshot/reflection-sites.md](./reflection-sites.md)

<!-- backlinks-end -->

# TaleWorlds API Snapshot + Binding-Verification Gate

A committed, in-repo snapshot of the exact TaleWorlds v1.4.8 engine surface that TAOM patches, overrides, and reflects into — plus the offline test gate that keeps it honest. The goal: **answer "what's the signature of X / is this member still here in v1.4.8?" without the external decompile dump** (`E:\Decompiled_Bannerlord\`), and **turn a silent in-game binding failure into a red `dotnet test`.**

## Why this exists

The v1.3.15 → v1.4.5 migration completed its code stages but the formal runtime validation gate (S6 smoke test) was never run as a discrete step — see [`docs/migration/TRACKING.md`](../../migration/TRACKING.md). The compiler proves a lot (an `override` whose base signature drifted is CS0115; `AccessTools` typos are flagged by the BUTR analyzer below), but three failure modes slip through a green build and surface only at game load — silently:

1. **Harmony resolution ambiguity** — a name-only `[HarmonyPatch]` on a method that is overloaded *anywhere in the type hierarchy* throws `AmbiguousMatchException` at `PatchAll`, so the patch never applies. (The compiler and the BUTR analyzer both pass it — the member exists.)
2. **Auxiliary reflection drift** — a private engine field/method reached by string name returns null after a rename; the reflecting code logs-and-survives, so the feature degrades with no crash.
3. **Unregistered GameModel** — a `Taom*Model` that compiles but is never `AddModel`'d; the engine silently uses the vanilla `Default`.

## Files

| File | What it is |
|------|------------|
| [`reflection-sites.md`](./reflection-sites.md) | Authoritative catalogue of every reflection touchpoint, grouped by gated / runtime-dynamic / internal. Data source for `ReflectionSiteBindingTests`. |
| [`gamemodel-bases.md`](./gamemodel-bases.md) | Auto-generated: each `Taom*Model`, its `Default*Model` base, and the exact v1.4.8 signature of every overridden method. |
| [`patch-targets.md`](./patch-targets.md) | Auto-generated: each of the 220 `[HarmonyPatch]` / `TargetMethod` classes and the engine method it patches, resolved as Harmony resolves it. |
| [`../../../tools/snapshot_api_surface.ps1`](../../../tools/snapshot_api_surface.ps1) | Regenerates the two auto-generated files from the installed DLLs. Auto-derives the type list from `TAOM.dll`. |

> **The generator is PowerShell 7 only, and now says so.** It uses the null-coalescing `??` operator
> and an em dash inside a double-quoted `throw`, both of which Windows PowerShell 5.1 rejects at PARSE
> time. Without a version declaration it failed with ten cascading parser errors naming neither the
> version nor the cause (observed 2026-09-06 on a machine with no `pwsh`). A `#requires -Version 7.0`
> line was added that day, so 5.1 now reports one clear message instead. The shebang alone does not
> cover this: on Windows the script runs under whichever host invoked it, and 5.1 is still the default.
>
> **Regenerating is the only way to keep these files honest.** On 2026-09-06 `patch-targets.md` was
> found **21 rows stale** (194 committed against 220 real) because patches had been added across
> several features without a regeneration. Hand-editing a row works and is format-checkable, but it
> cannot find rows nobody knew were missing. Run the generator after any patch or GameModel change,
> not only after an engine bump.

## The gate (`TAOM.Tests/Migration/`)

Runs under `dotnet test` — no game launch. TAOM.Tests references the installed `TaleWorlds.*.dll` (v1.4.8); `GameAssemblies.cs` pre-loads the SandBox/CustomBattle/StoryMode module DLLs from the install so every engine type resolves.

| Test class | Covers | Granularity |
|------------|--------|-------------|
| `HarmonyPatchBindingTests` | All 220 `[HarmonyPatch]` / `TargetMethod` targets, resolved as Harmony does at `PatchAll`. | one assertion accumulating all failures |
| `GameModelOverrideBindingTests` | All 46 GameModels: registered in `SubModule.cs`, override ≥1 base virtual, no shadow-without-`override`. | 2 tests |
| `ReflectionSiteBindingTests` | The 32 auxiliary static-engine reflection members from `reflection-sites.md` Category B. | one `[DataRow]` per site |

```bash
dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"
```

> **First run (2026-05-28) caught a real defect.** `HeroViewModel_FillFrom_Patch` used a name-only `[HarmonyPatch(typeof(HeroViewModel), "FillFrom")]`. `HeroViewModel` inherits two more `FillFrom` overloads from `CharacterViewModel`, so Harmony's `AccessTools.Method` resolution found 3 candidates and threw `AmbiguousMatchException` — the postfix silently never applied in v1.4.5 (hero-portrait clan colors broken). Fixed by pinning argument types. This is exactly the gap the compiler and the BUTR analyzer miss.

## Relationship to BUTR.Harmony.Analyzer (compile-time)

TAOM already references [`BUTR.Harmony.Analyzer`](https://github.com/BUTR/BUTR.Harmony.Analyzer) (`Main/TAOM.csproj`). It is a Roslyn analyzer that validates `AccessTools.*` member references at **compile time** against the referenced assemblies (via `System.Reflection.Metadata`, so it can see non-public members) and flags typos like `_privateFld` for `_privateField`. It is excellent and catches a large class of drift early.

This snapshot gate is the **complement**, not a replacement, because the analyzer:

- emits **warnings**, which a warning-tolerant build ignores — the gate is a hard test failure;
- checks member **existence**, not Harmony's **resolution semantics** — it passed the `HeroViewModel` ambiguity above (the member exists; the *resolution* is ambiguous);
- covers `AccessTools.*` but not raw `typeof(X).GetField(...)` reflection (used in several catalogue sites) or `TargetMethod()` computed targets;
- says nothing about GameModel **registration** (`AddModel`), which is not a reflection concern.

In short: the analyzer is compile-time existence checking against referenced metadata; this gate is a runtime resolution check against the **installed** engine, plus registration coverage.

## Maintenance

- After a Bannerlord version bump (or any patch/GameModel change): `pwsh tools/snapshot_api_surface.ps1` to refresh the two generated files, then `dotnet test --filter "TestCategory=BindingVerification"`.
- When you add a reflection site against an engine member: add a row to `reflection-sites.md` Category B **and** a `[DataRow]` to `ReflectionSiteBindingTests`.
- CI/local reproducibility check: `pwsh tools/snapshot_api_surface.ps1 -Check` (exits non-zero if the committed files don't reproduce).

## External references

- HarmonyLib — patching overloaded methods requires an argument-type array: `[HarmonyPatch(typeof(String), "IndexOf", new Type[] { typeof(char), typeof(int) })]`. See <https://harmony.pardeike.net/articles/annotations.html> (`/pardeike/harmony` docs).
- HarmonyLib `TargetMethod()` / `TargetMethods()` — return `MethodBase` / `IEnumerable<MethodBase>` for computed targets. See <https://harmony.pardeike.net/articles/patching-auxiliary.html>.
- BUTR.Harmony.Analyzer — compile-time `AccessTools` member validation. See <https://github.com/BUTR/BUTR.Harmony.Analyzer>.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/taleworlds-api-snapshot/reflection-sites.md](./reflection-sites.md)

<!-- backlinks-end -->

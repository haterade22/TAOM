# ADR-013: Server-Safe Core, Client UI Submodule

**Status**: Accepted (2026-10-22, the maintainer)

**Date**: 2026-10-21

**Priority**: Standard

## Context

TAOM's core assembly, `TAOM.dll`, cannot be loaded by the Bannerlord v1.5.5 dedicated server that the
co-op appliance runs. The engine calls `Assembly.GetTypes()` on every submodule DLL, then runs its
constructor, before any `OnSubModuleLoad`. A failure in either step ends the process with
`MBDebug.AbortGame` (exit 5). Found on 2026-10-21 by bisection on a v1.5.5 server:

- 40 of `TAOM.dll`'s types derive from or implement a type in an assembly the server cannot resolve
  when TAOM loads (Gauntlet widgets, `MapView`, mission views, view-model mixins, CustomBattle,
  WinForms). What fails `GetTypes` is the base type and the interfaces (generic arguments included), a
  value-typed field, a generic constraint, or an override of another assembly's virtual whose signature
  names such a type. Reference-typed fields, arrays and method bodies do not.
- `TAOM.Dependencies` failed in its static constructor, which loaded `TaleWorlds.Engine.GauntletUI`, and two
  UI submodules in its manifest carried neither server tag. Both were fixed in `5fcf8390c` (batch B2) and
  proven on a staged server.

The engine already has a convention for this. Vanilla tags its UI submodules `DedicatedServerType=none`
and `IsNoRenderModeElement=false`, and the server skips them. TAOM tags its single core submodule that
way today, so the appliance's staging has to strip the tags to load TAOM at all, and then the engine
aborts. The tables of types, problem points and batches are in
[taom-headless-server](../research/taom-headless-server.md).

## Decision

Adopt option (b): a minimal client assembly, with core made server-safe.

1. A new `TAOM.Client.dll` with a `TAOM.Client.ClientSubModule`, tagged `DedicatedServerType=none` and
   `IsNoRenderModeElement=false` like vanilla's UI submodules. It holds only what cannot load headless:
   the 40 types, the 15 UIExtenderEx prefab classes, and the callers that exist only to instantiate them.
2. Core never references `TAOM.Client`. Core defines a slot, `TAOM.Composition.ClientComposition`, holding
   an `IClientComposition` with `RegisterServices(IRegistrator)`. The `ClientSubModule` constructor sets
   it; `IoC.Configure` calls it after core registrations with replace semantics. On a server the slot stays
   empty and core's null adapters stay registered (ADR-007).
3. Core keeps what loads but must not run headless, behind the engine's own build constant
   `GameNetwork.IsDedicatedServer`, read through `IDedicatedServerProvider`, or behind a null adapter.
   `TAOM.Dependencies` reads the same constant directly, and its Gauntlet and UIExtenderEx steps run only
   off-server, in NoInlining helpers a server never compiles.
4. Gates in `TAOM.Tests/Infrastructure/` keep it true: a load gate whose allowlist of failing types only
   shrinks until it is empty (run over `TAOM.dll` and `TAOM.Dependencies.dll`, with a fixture per failing
   shape), a cross-check of the client-only list against a staged server, and an IL gate on
   `TAOM.Dependencies` that pins the server guard before each UI helper call. A UI patch-class check joins
   them with the type moves.
5. When the core submodule is server-safe, its manifest tags are removed and the appliance stages by the
   generic rule "drop client-only tagged submodules, keep the rest as written", with no TAOM names in the
   server repo.

The path then grows toward a full split (a) only feature by feature, where a feature pays for it.

## Consequences

### Positive
- TAOM can boot on the dedicated server, and a gate fails the build when a core type regains a UI base.
- Single-player development stays almost unchanged: one rule, enforced by a test, says a new UI type goes
  into `TAOM.Client`.
- The module author's manifest becomes the contract; the server repo carries one mod-agnostic rule.

### Negative
- One more project, submodule and build step, about 55 type moves and 26 core methods inverted behind
  adapters, in a dozen batches.
- Two submodules to keep in step in the manifest and in every deploy path: `Main/TAOM.csproj`'s server
  mirror excludes `TAOM.Client` and its editor mirror deletes a stale copy, and `Client/TAOM.Client.csproj`
  deploys it only beside the core it was built against (Game Pass is not supported, so there is no third
  client bin).
- Booting is only the first milestone: guards for startup hooks, patch categories and campaign-time UI
  paths follow (batches B6 and B7), and some may only show in a long server run.
- Unproven until batch B4a: that Gauntlet finds widget types in a second assembly. Every move batch ends
  with a client boot.

### Neutral
- `Main/SubModule.cs` and `Main/IoC.cs` are single-owner files and take hunks from several batches, so
  those batches run serially.
- A server reads TAOM settings from a TAOM server settings file (decision 2), validated like every TAOM
  config, with the server defaults (compiled, except `EnableHitchProbe` off) for anything it leaves out;
  built in B8, see [server-settings](../features/server-settings.md).
- A player's client keeps the same features; only where the UI types live changes.

## Alternatives Considered

### Alternative (a): full split
- **Pros**: the compiler proves core never touches UI.
- **Cons**: about 190 types with UI bodies, 98 UI patch classes and every core-to-UI call move in one
  effort, before any boot proves the approach, and every later UI-touching feature pays for an inverted call.
- **Why rejected as one change**: kept as the direction, reached through (b) feature by feature.

### Alternative (c): GetTypes shim installed early
- **Pros**: smallest code change; the loadable types load.
- **Cons**: swallows `ReflectionTypeLoadException` process-wide, leaves 40 types absent with latent
  `TypeLoadException`s, and does not fix the Dependencies constructor.
- **Why rejected**: it hides the fault instead of removing it. Fine as a throwaway local spike.

### Alternative (d): a `TAOM.Server` submodule
- **Pros**: a server entry point with no UI.
- **Cons**: repeats gameplay wiring and drifts from it, or still needs (b)'s separation underneath.
- **Why rejected**: more complexity, no gain over (b).

### Alternative (e): make the view layer resolvable early
- **Pros**: only 5 types would still fail.
- **Cons**: depends on Coop's private bin layout and load order, and leaves TAOM UI code half-alive headless.
- **Why rejected**: wrong owner for the dependency, and a fragile one.

## Decisions

All four were decided by the maintainer on 2026-10-22. Batches B1 (the gate tests) and B2
(`TAOM.Dependencies` headless) did not depend on them and landed in `5fcf8390c`.

1. **Option (b) over a full split (a): (b).** TAOM is a single-player mod first and co-op is a second
   goal. (b) keeps single-player development almost unchanged, where (a) would put every UI-touching
   feature behind an inverted call for good.
2. **Server settings: a TAOM server settings file, done right the first time.** Batch B8 builds it behind
   one settings-source seam, validated like every TAOM config, with compiled defaults as the fallback.
   Built 2026-10-23 as a static `Instance` hide on each settings class (`ServerSettingsSource`).
3. **Whether the dedicated server ever runs missions: prove it first, then decide.** A code trace on
   2026-10-22 found no co-op path that opens a mission on the server (battles run on the players'
   clients, and the server simulates or finalizes them), and [Likely] no vanilla path, because the server
   removes its main party's menu and encounter. Not yet proven at runtime: during a field battle, a siege,
   a hideout, a tournament and an AI encounter with a client, the server log must never show
   `[Coop] Game State is changing to "MissionState"`. Batch B4c is sized after that check; until then TAOM
   plans for "no missions on the server", with a loud log and no TAOM mission behaviors if one ever opens.
4. **`CollectAssemblyTypesShim` on the server: keep.** It matches a co-op client. B2 already made it live
   there.

## Migration Strategy

The batches are B0 to B10 in the research note: ADR and epic; gates; `TAOM.Dependencies` headless; client
skeleton and slot; type moves by group; UIExtenderEx to the client (milestone M1, the allowlist reaches
zero); lifecycle guards; campaign-time UI paths; settings; manifest flip with the generic staging rule;
docs. Each batch is test-first, deep-reviewed before its commit and booted on a staged server where it says
so; each move batch also ends with a client boot.

## References

- [taom-headless-server](../research/taom-headless-server.md): evidence, the 40 types, the problem points, the batches.
- [coop-interop](../features/coop-interop.md): the co-op feature page.
- [server-settings](../features/server-settings.md): the B8 server settings file (decision 2).
- [ADR-007](./007-adapter-pattern.md): null adapters for the server side of each inverted call.
- Issue #967 (Batch C of the co-op epic #964).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/014-separate-coop-compat-module.md](./014-separate-coop-compat-module.md)
- [docs/adrs/README.md](./README.md)
- [docs/features/coop-compat.md](../features/coop-compat.md)
- [docs/features/coop-dedicated-server.md](../features/coop-dedicated-server.md)
- [docs/features/coop-interop.md](../features/coop-interop.md)
- [docs/features/server-settings.md](../features/server-settings.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/module-taom.md](../modding/module-taom.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/research/taom-headless-server.md](../research/taom-headless-server.md)
- [docs/reviews/lessons/build-tooling-workflow.md](../reviews/lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/testing-qa.md](../reviews/lessons/testing-qa.md)

<!-- backlinks-end -->

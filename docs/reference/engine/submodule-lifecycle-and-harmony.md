# Bannerlord module integration — MBSubModuleBase lifecycle + Harmony patching (Phase 18)

> **One process, traced from TAOM's own entry point + the engine base** (v1.4.5): the meta-layer — *how* TAOM's code
> hooks into everything in Phases 1-17. `MBSubModuleBase` gives the lifecycle callbacks; Harmony patches the engine's
> managed methods; `AddModel`/`AddBehavior` register GameModels (Phase 7) + CampaignBehaviors (Phase 9). This is the
> "how does any of our code run" layer that every TAOM feature sits on. Capstone of the phased engine study.

## WHAT it is

A Bannerlord module ships a **`MBSubModuleBase`** subclass — the engine instantiates it and calls lifecycle methods at
fixed points (load, pre-menu, game start, mission init, tick, unload). Inside those, TAOM does three things:
**(1) Harmony-patch** engine methods (Prefix/Postfix/Transpiler/Finalizer), **(2) register GameModel overrides**
(`AddModel`, Phase 7), **(3) register CampaignBehaviors** (`AddBehavior`, Phase 9). TAOM's is `SubModule : MBSubModuleBase`
(Main/SubModule.cs:100).

## HOW it works — the lifecycle (TAOM's SubModule.cs)

| Override | When | What TAOM does |
|---|---|---|
| **`OnSubModuleLoad()`** (:114) | Earliest: module DLL loaded, before menu | `_harmony = new Harmony("com.taom.mod")` (:226); apply the module-load patch categories (22 literal `TryPatchCategory("...")` call sites on 2026-11-03, plus the feature modules' `ApplyPhase.ProcessLoad` categories at :629; the larger batch is in `OnGameInitializationFinished`) via **`TryPatchCategory("PatchNN_X")`** (:240-629), the guarded helper over `PatchCategoryIndex.Apply(_harmony, category)`: a failure logs `[PatchApply]`, stops that category at its failing class (classes applied before it stay patched), and every other category still applies (a class whose attribute names a type the engine no longer has is skipped and reported on its own, see Categories below); wire static patch fields via `.Initialize(service, …)`; IoC bootstrap. Nothing receives `InformationManager.DisplayMessage` yet, so this phase's failures are shown later, in the startup inquiry of `OnBeforeInitialModuleScreenSetAsRoot`. |
| **`OnBeforeInitialModuleScreenSetAsRoot()`** (:638) | After all modules load, before main menu | Pre-menu setup; applies `Patch55_BasicTableauRaceGuard`, then shows every startup patch failure (this phase's and `OnSubModuleLoad`'s) in one persistent inquiry, since Native's `GauntletUISubModule` has built the inquiry manager by now. **A throw here repeats every tick (v1.5.5):** `Module.OnApplicationTick` calls `SetInitialModuleScreenAsRootScreen` on every tick once loading has finished, while the global state manager's `ActiveState` is null (`Module.cs:538-549`), and that method calls every module's callback with no `try` (`:796-799`). A throw from any module's callback therefore leaves `ActiveState` null and runs every module's callback again on the next tick, until a state becomes active (TAOM-verified 2026-10-15, managed decompile, code read). TAOM's guard (#807): the main-menu customization runs through `MainMenuStep.Run` (`Main/Composition/MainMenuStep.cs`), which never rethrows, logs one `[MainMenuStep]` WARNING per step name per process with the full exception, and runs the step again on the next call, so the step must be safe to repeat. The other calls in the hook need no wrapper: `ShaderCompileNoticeEntry.Stop()` only sets a flag, the Patch55 block is latched to run once and its category applies through `TryPatchCategory`, which catches, and each diagnostic sampler has its own `try`. `TAOM.Tests/Composition/SubModuleMainMenuHookTests.cs` pins that `CustomizeMenu` is called once, inside `MainMenuStep.Run`. Another module's throw is not contained by this. |
| **`OnGameStart(Game, IGameStarter)`** (:904) | A game (campaign) is starting | `if (gameStarterObject is CampaignGameStarter campaignStarter)` → the `Register*` groups (`RegisterProgressionAndIdentity`, `RegisterCulturalFeatModels`, `RegisterCampaignLifeBehaviors`, ...), which call **`AddBehavior(new XxxBehavior(...))`** (every CampaignBehavior, Phase 9) + **`AddModel(new TaomXxxModel(...))`** (every GameModel, Phase 7/15/16); then `FeatureModuleHooks.AddGameStartContent(gameStarterObject)` adds the feature modules' behaviors and models last. |
| **`OnGameInitializationFinished(Game)`** (:1529) | Campaign fully initialized | Per-game-init work (monster sizes, armour gate, load-time stamps), then, once per process (`_gameInitPatchesApplied`), the larger batch of `TryPatchCategory` patch categories (`ApplyPhase.GameInit`, :1990), the manual patches, then a PatchShield pass of TAOM's own, so that batch is shielded from the first game start. |
| **`OnMissionBehaviorInitialize(Mission)`** (:2068) | Each mission is being built (Phase 17 step 6) | **Deferred patches** whose target's cctor reads `Mission.Current`/`Campaign.Current`: the `Patch_MissionTime_SetMovementOrder` category (Phase 13/17), applied from `AddTaomMissionBehaviors` (:2115), one-shot-guarded by `_missionTimePatchesApplied`. Per-mission MissionBehavior wiring. |
| **`OnApplicationTick(float dt)`** (:2289) | Every frame | Per-frame polling (sparingly). |
| **`OnSubModuleUnloaded()`** (:2311) | Module unload / shutdown | Cleanup. |

## HOW it works — Harmony mechanics
- **`new Harmony(id)`**: the patch *owner* (`"com.taom.mod"`); TAOM's patch categories belong to this owner, and a few patches create their own (`TAOM.Realms.*`, `taom.factionui.campaign-time`). PatchShield's protected-owner list does not match `com.taom.mod` (decision D13 plans a culprit-only strip, then protection; see `PatchShieldPolicy`, and [harmony-il lessons](../../reviews/lessons/harmony-il.md), "Derive a Harmony owner allowlist...").
- **Categories:** a patch class carries `[HarmonyPatch(typeof(Target), "Method")]` + `[HarmonyPatchCategory("PatchNN_X")]`; **`TryPatchCategory("PatchNN_X")`** (in `SubModule`, through `PatchCategoryApplier`) applies all patches in that group. Harmony stops a category at its first class whose target does not resolve and throws, with no rollback of the classes it already applied; the helper contains that to the one category. Harmony's own `PatchCategory` builds its category index once per assembly by reading every type's attributes, so one attribute naming a type the engine no longer has would fail every category; TAOM builds that index class by class in `PatchCategoryIndex` (Harmony's public API only), skips the unreadable class, logs it as `[PatchApply] ... SKIPPED` and names it in the startup inquiry (#653). The index reads TAOM.dll, then TAOM.Client.dll when it loaded; on a dedicated server, where TAOM.Client never loads, `PatchCategoryIndex.Build(isDedicatedServer, ...)` also leaves out every `[ClientOnly]` class before reading its attributes and logs them in one Info line, so a category applies its server-safe classes there (ADR-013, B6a). TAOM applies categories *selectively* (some conditionally; `Patch37_CrashReport` has been applied unconditionally since 2026-09-24, because no MCM setting is readable in `OnSubModuleLoad`), so a category can be skipped without disabling everything.
- **Patch kinds:**
  - **Prefix:** runs before the original; **`return false` skips the original** (and you set `__result`). Used to fully replace behavior (QuickActions Sell-All).
  - **Postfix** — runs after; reads/modifies `__result` + args. The default (SmartCavalry/CompanionTactics `SetMovementOrder`, banner-color).
  - **Transpiler**: rewrites the target's IL (CastleRecruitment's `IsCastle`-gate swap). Pin to an ordinal + anchor, fail-safe to vanilla ([harmony-il lessons](../../reviews/lessons/harmony-il.md), "Pin a single-occurrence transpiler swap...").
  - **Finalizer** — catches exceptions thrown by the original/other patches (PatchShield wraps every patch in one). Returning `null` swallows. When ANY finalizer on the method returns a value, the wrapper ends with the `throw` opcode, which REPLACES the exception's stack trace with the frames from the patched method outward (proven with a real patch in `RethrowStackPreserverTests`); only an all-`void` finalizer set gets `rethrow`, which keeps it (Harmony 2.4.2 `MethodCreator`). So an observe-only finalizer should be `void`, and one that hands the exception back writes `return RethrowStackPreserver.PreserveForRethrow(__exception, site);`, or the throw site is lost. `site` names the rethrowing method in the marker line: `__originalMethod` on a cold target (SaveShield), `null` on a hot one (the marker then says "an unknown method"), or the method found only after the throw (PatchShield). A finalizer that takes `__originalMethod` makes the wrapper call `MethodBase.GetMethodFromHandle` on every call: about 63 ns per call more than one that takes only `__exception` (1,145 ns with 8 threads contending, 241 bytes per call), measured on .NET Framework 4.8.1 with Harmony 2.4.2 on an AMD Ryzen 9 9950X3D (plan 034, 2026-10). PatchShield's finalizers therefore take only `__exception` and find the shielded method after a throw with `PatchShield.ResolveShieldedOriginal`.
- **Manual patches:** `_harmony.Patch(AccessTools.Method(typeof(T), "M"), prefix: new HarmonyMethod(...))` for explicit overload/private resolution (Patch23/Patch28 manual entries; the June 2026 spider spawner, since deleted, reflected `CreateAgent`/`BuildAgent`).
- **`AccessTools`**: reflection helpers (`Method`/`Field`/`Constructor`/`PropertyGetter`) for private members; cache the result + bind an **open delegate** for hot paths ([harmony-il lessons](../../reviews/lessons/harmony-il.md), "Call private engine methods from hot-path patches...").
- **A patch method never lives on a closed generic type, and is never a `DynamicMethod`.** Harmony 2.4.2 records
  each patch method as module plus metadata token and re-resolves it whenever another patch lands on the same
  target. The token carries no type arguments, so it resolves to the open generic type's method, and every later
  patch on that target throws `InvalidProgramException` in `PatchFunctions.UpdateWrapper`. In game it
  disabled three patches for the session. A test with only one patch per target cannot see it; test "our patch,
  then a foreign patch" and the reverse. (yotthani, MithrilForge `docs/engine/perf.md`, PerfProbe section of
  2026-10-04, in game on v1.5.3 with Harmony 2.4.2.)
- **Deferred application**: a category whose target type's **static cctor reads `Mission.Current`/`Campaign.Current`** must be applied in `OnMissionBehaviorInitialize`, NOT `OnSubModuleLoad` (where they're null → JIT-prep NRE); see [harmony-il lessons](../../reviews/lessons/harmony-il.md), "Defer patches whose parameter types have Mission/Campaign-dependent cctors". Guard with a one-shot static flag (it fires per mission).

## WHY it's shaped this way

`MBSubModuleBase` is the only entry the engine calls — everything TAOM does hangs off its lifecycle. The split matters:
patches that touch *type metadata only* go early (`OnSubModuleLoad`); registrations that need a `CampaignGameStarter` go
in `OnGameStart`; patches that need a live `Mission` go in `OnMissionBehaviorInitialize`. Harmony lets TAOM modify
*sealed/private* engine behavior without forking the engine (the foundation of the whole `[Patch]→Service→IAdapter`
architecture), while `AddModel`/`AddBehavior` are the *sanctioned* extension points (no patch needed) — preferred when
they exist (Phase 7/9).

## TAOM relevance + gotchas
- **The architecture one-liner is wired HERE:** `[HarmonyPatch/GameModel/CampaignBehavior] → Service → IAdapter`
  (an `IOnXxx` hook in between only when the patch needs a narrow seam or a test fake, AGENTS.md "Architecture")
  — the patch/model/behavior is the thin entry point (<150 lines, ADR-002) registered in this file; it delegates to a
  service. `Main/SubModule.cs` + `Main/IoC.cs` are **single-owner** (recommend edits, don't make them from subagents).
- **Three registration mechanisms, choose the right one:** an engine method to intercept → **Harmony patch**; a vanilla
  GameModel calc to override → **`AddModel`** (Phase 7); campaign-event logic → **`CampaignBehavior` + `AddBehavior`**
  (Phase 9). Prefer `AddModel`/`AddBehavior` (sanctioned, forward-compatible) over a patch when they fit.
- **Native methods can't be Harmony-patched:** Harmony rewrites *managed* IL. `TaleWorlds.Native.dll` behaviour is out of
  TAOM's reach: since NativeSkinFixes was removed (2026-10-05) TAOM carries no native detour, and the
  `Native2ManagedPatcher` (CrashReport, `SubModule.cs:243`) only wraps the managed callback shims native code
  calls into, for crash capture. This is why the native-creature-render
  problems (the spider AV, Phase 1) can't be fixed with a Harmony patch — they're past the managed boundary.
- **PatchShield** (Dependencies/Foundation, DR3) wraps every patch, except targets in the hot layers `PatchShieldPolicy.ExcludedTargetNamespacePrefixes` and `ExcludedTargetMethods` name, in a Finalizer that catches the
  MissingMethod/MissingField/TypeLoad trinity and **auto-unpatches the offending owner** — so an engine-bump signature
  drift degrades gracefully instead of crashing. Its owner-filter must enumerate every `new Harmony("X")` in vendored
  DLLs we ship, not namespace prefixes (same harmony-il lesson as above).
- **Patch signature verification** — before writing/maintaining a patch, verify the target signature with `ilspycmd` on
  the *installed* DLLs (AGENTS.md "Research first"; [Running the gate](../taleworlds-api-snapshot/README.md) refreshes the committed API snapshot after an
  engine bump). A wrong target = silent no-op (Postfix) or TypeLoad at startup (Prefix/Transpiler).

## The native boundary
`MBSubModuleBase` + Harmony operate **entirely in managed space** — Harmony patches the engine's **managed** C# methods
(TaleWorlds.*.dll). The native engine (`TaleWorlds.Native.dll`, Phases 1/3/14/15 render+physics) is **not** Harmony-reachable;
TAOM does not cross that boundary: it declares no `[EngineMethod]`, its only P/Invokes are kernel32/psapi memory probes,
and the `Native2ManagedPatcher` (CrashReport) only observes managed callback shims. So *managed behavior* =
Harmony/AddModel/AddBehavior (this phase); *native behavior* is the engine's alone (the engine-and-toolchain doc).

## Evidence (file:line)
- `Main/SubModule.cs`:100 (`: MBSubModuleBase`), :114 (`OnSubModuleLoad`), :226 (`new Harmony("com.taom.mod")`), :240-629 (`TryPatchCategory("PatchNN_X")` ×N; CrashReport unconditional since 2026-09-24), :125-2064 (`.Initialize` static wiring), :638 (`OnBeforeInitialModuleScreenSetAsRoot`), :904 (`OnGameStart` → the `Register*` groups, `AddBehavior`/`AddModel`), :1529 (`OnGameInitializationFinished`), :2068 (`OnMissionBehaviorInitialize`: deferred MovementOrder category), :2289 (`OnApplicationTick`), :2311 (`OnSubModuleUnloaded`).
- TAOM patch catalogue + categories: `docs/reference/harmony-patch-registry.md` (full rationale/history); model overrides: `docs/reference/gamemodel-registry.md`. Defensive infra: `Dependencies/Foundation/PatchShield.cs`. Gotcha lessons: [harmony-il.md](../../reviews/lessons/harmony-il.md).
- Linked: gamemodel-system.md (Phase 7, AddModel), campaignevents-and-campaignbehavior.md (Phase 9, AddBehavior), formations-and-team-ai.md / campaign-to-mission-bridge.md (Phases 13/17, deferred patch), bannerlord-engine-and-toolchain.md (the native boundary).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lotr-issues.md](../../features/lotr-issues.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/doc-lookup.md](../doc-lookup.md)
- [docs/reference/engine/issue-and-quest-system.md](./issue-and-quest-system.md)
- [docs/reference/engine/mission-and-missionbehavior-lifecycle.md](./mission-and-missionbehavior-lifecycle.md)
- [docs/reviews/lessons/harmony-il.md](../../reviews/lessons/harmony-il.md)

<!-- backlinks-end -->

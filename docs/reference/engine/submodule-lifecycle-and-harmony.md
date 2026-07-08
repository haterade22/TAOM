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
(Main/SubModule.cs:82).

## HOW it works — the lifecycle (TAOM's SubModule.cs)

| Override | When | What TAOM does |
|---|---|---|
| **`OnSubModuleLoad()`** (:91) | Earliest — module DLL loaded, before menu | `_harmony = new Harmony("com.taom.mod")` (:104); apply most patch categories via **`_harmony.PatchCategory("PatchNN_X")`** (:133-242); wire static patch fields via `.Initialize(service, …)` (:207-235); IoC bootstrap. |
| **`OnBeforeInitialModuleScreenSetAsRoot()`** (:247) | After all modules load, before main menu | Pre-menu setup; NativeSkinFixes **install** (the native MinHook layer — managed Harmony can't touch native; **parked 2026-07-08** — the install call is commented out, so it does no MinHook work until re-enabled). |
| **`OnGameStart(Game, IGameStarter)`** (:294) | A game (campaign) is starting | `if (gameStarter is CampaignGameStarter cs)` → **`cs.AddBehavior(new XxxBehavior(...))`** (every CampaignBehavior, Phase 9) + **`cs.AddModel(new TaomXxxModel(...))`** (every GameModel, Phase 7/15/16). |
| **`OnGameInitializationFinished(Game)`** (:512) | Campaign fully initialized | Post-init; defensive-infra success marker (DR3 `OnGameInitializationFinished`). |
| **`OnMissionBehaviorInitialize(Mission)`** (:640) | Each mission is being built (Phase 17 step 6) | **Deferred patches** whose target's cctor reads `Mission.Current`/`Campaign.Current` — the `Formation.SetMovementOrder` category (Phase 13/17), one-shot-guarded. Per-mission MissionBehavior wiring. |
| **`OnApplicationTick(float dt)`** (:704) | Every frame | Per-frame polling (sparingly). |
| **`OnSubModuleUnloaded()`** (:726) | Module unload / shutdown | NativeSkinFixes **uninstall**; cleanup. |

## HOW it works — Harmony mechanics
- **`new Harmony(id)`** — the patch *owner* (`"com.taom.mod"`); all TAOM patches belong to this owner (used by PatchShield's allowlist — `feedback_harmony_owner_allowlist_from_vendored_dll_enumeration`).
- **Categories:** a patch class carries `[HarmonyPatch(typeof(Target), "Method")]` + `[HarmonyPatchCategory("PatchNN_X")]`; **`_harmony.PatchCategory("PatchNN_X")`** applies all patches in that group. TAOM applies categories *selectively* (some conditionally, e.g. `Patch37_CrashReport` :109), so a category can be skipped without disabling everything.
- **Patch kinds:**
  - **Prefix** — runs before the original; **`return false` skips the original** (and you set `__result`). Used to fully replace behavior (the spider spawn patch, QuickActions Sell-All).
  - **Postfix** — runs after; reads/modifies `__result` + args. The default (SmartCavalry/CompanionTactics `SetMovementOrder`, banner-color).
  - **Transpiler** — rewrites the target's IL (CastleRecruitment's `IsCastle`-gate swap). Pin to an ordinal + anchor, fail-safe to vanilla (`feedback_transpiler_ordinal_plus_anchor_failsafe`).
  - **Finalizer** — catches exceptions thrown by the original/other patches (PatchShield wraps every patch in one).
- **Manual patches** — `_harmony.Patch(AccessTools.Method(typeof(T), "M"), prefix: new HarmonyMethod(...))` for explicit overload/private resolution (Patch23/Patch28 manual entries; the spider spawner's reflected `CreateAgent`/`BuildAgent`).
- **`AccessTools`** — reflection helpers (`Method`/`Field`/`Constructor`/`PropertyGetter`) for private members; cache the result + bind an **open delegate** for hot paths (`feedback_hotpath_private_method_open_delegate`).
- **Deferred application** — a category whose target type's **static cctor reads `Mission.Current`/`Campaign.Current`** must be applied in `OnMissionBehaviorInitialize`, NOT `OnSubModuleLoad` (where they're null → JIT-prep NRE) — `feedback_movementorder_cctor_mission_current`. Guard with a one-shot static flag (it fires per mission).

## WHY it's shaped this way

`MBSubModuleBase` is the only entry the engine calls — everything TAOM does hangs off its lifecycle. The split matters:
patches that touch *type metadata only* go early (`OnSubModuleLoad`); registrations that need a `CampaignGameStarter` go
in `OnGameStart`; patches that need a live `Mission` go in `OnMissionBehaviorInitialize`. Harmony lets TAOM modify
*sealed/private* engine behavior without forking the engine (the foundation of the whole `[Patch]→IHook→Service→IAdapter`
architecture), while `AddModel`/`AddBehavior` are the *sanctioned* extension points (no patch needed) — preferred when
they exist (Phase 7/9).

## TAOM relevance + gotchas
- **The architecture one-liner is wired HERE:** `[HarmonyPatch/GameModel/CampaignBehavior] → IHook → Service → IAdapter`
  — the patch/model/behavior is the thin entry point (<150 lines, ADR-002) registered in this file; it delegates to a
  service. `Main/SubModule.cs` + `Main/IoC.cs` are **single-owner** (recommend edits, don't make them from subagents).
- **Three registration mechanisms, choose the right one:** an engine method to intercept → **Harmony patch**; a vanilla
  GameModel calc to override → **`AddModel`** (Phase 7); campaign-event logic → **`CampaignBehavior` + `AddBehavior`**
  (Phase 9). Prefer `AddModel`/`AddBehavior` (sanctioned, forward-compatible) over a patch when they fit.
- **Native methods can't be Harmony-patched** — Harmony rewrites *managed* IL. `TaleWorlds.Native.dll` methods need
  **MinHook** (NativeSkinFixes) or the `Native2ManagedPatcher` (CrashReport, :113). This is why the native-creature-render
  problems (the spider AV, Phase 1) can't be fixed with a Harmony patch — they're past the managed boundary.
- **PatchShield** (Dependencies/Foundation, DR3) wraps every patch in a Finalizer that catches the
  MissingMethod/MissingField/TypeLoad trinity and **auto-unpatches the offending owner** — so an engine-bump signature
  drift degrades gracefully instead of crashing. Its owner-filter must enumerate every `new Harmony("X")` in vendored
  DLLs we ship, not namespace prefixes (`feedback_harmony_owner_allowlist_from_vendored_dll_enumeration`).
- **Patch signature verification** — before writing/maintaining a patch, verify the target signature with `ilspycmd` on
  the *installed* DLLs (CLAUDE.md "Research First"; `/verify-bindings` refreshes the committed API snapshot after an
  engine bump). A wrong target = silent no-op (Postfix) or TypeLoad at startup (Prefix/Transpiler).

## The native boundary
`MBSubModuleBase` + Harmony operate **entirely in managed space** — Harmony patches the engine's **managed** C# methods
(TaleWorlds.*.dll). The native engine (`TaleWorlds.Native.dll`, Phases 1/3/14/15 render+physics) is **not** Harmony-reachable;
TAOM crosses that boundary only via **MinHook** (NativeSkinFixes byte-pattern hooks) and `[EngineMethod]`/P-Invoke. So:
*managed behavior* = Harmony/AddModel/AddBehavior (this phase); *native behavior* = MinHook (the engine-and-toolchain doc).

## Evidence (file:line)
- `Main/SubModule.cs`:82 (`: MBSubModuleBase`), :91 (`OnSubModuleLoad`), :104 (`new Harmony("com.taom.mod")`), :133-242 (`PatchCategory("PatchNN_X")` ×N + conditional :109 CrashReport), :207-235 (`.Initialize` static wiring), :247 (`OnBeforeInitialModuleScreenSetAsRoot`), :294 (`OnGameStart` → `AddBehavior`/`AddModel` :310-391+), :512 (`OnGameInitializationFinished`), :640 (`OnMissionBehaviorInitialize` — deferred MovementOrder category), :704 (`OnApplicationTick`), :726 (`OnSubModuleUnloaded`).
- TAOM patch catalogue + categories: `CLAUDE.md` "Harmony Patch Categories" + "GameModel Overrides". Defensive infra: `Dependencies/Foundation/PatchShield`. Gotcha memories: `feedback_movementorder_cctor_mission_current`, `feedback_transpiler_ordinal_plus_anchor_failsafe`, `feedback_hotpath_private_method_open_delegate`, `feedback_harmony_owner_allowlist_from_vendored_dll_enumeration`.
- Linked: gamemodel-system.md (Phase 7, AddModel), campaignevents-and-campaignbehavior.md (Phase 9, AddBehavior), formations-and-team-ai.md / campaign-to-mission-bridge.md (Phases 13/17, deferred patch), bannerlord-engine-and-toolchain.md (the native/MinHook boundary).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/lotr-issues.md](../../features/lotr-issues.md)
- [docs/INDEX.md](../../INDEX.md)
- [docs/reference/engine/issue-and-quest-system.md](./issue-and-quest-system.md)

<!-- backlinks-end -->

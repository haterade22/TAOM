# Orientation

Where things are in TAOM, and the trap index. [AGENTS.md](../../AGENTS.md) links here and
CLAUDE.md imports it ([ADR-011](../adrs/011-knowledge-delivery-tiers.md)).

## Where things are

- **Code:** `Main/` (features `Main/Features/<Name>/`, adapters `Main/Adapters/`, core `Main/Core/`),
  tests `TAOM.Tests/`, data `Main/_Module/ModuleData/`, tools `tools/`
  ([tools/README.md](../../tools/README.md)).
- **Maps:** [docs/INDEX.md](../INDEX.md) by topic, [feature-map.md](../reference/feature-map.md)
  from feature to code, [doc-lookup.md](../reference/doc-lookup.md) from task to doc.
- **Registries:** for crash triage, grep the failing type in
  [harmony-patch-registry.md](../reference/harmony-patch-registry.md); model overrides are in
  [gamemodel-registry.md](../reference/gamemodel-registry.md).
- **Three modules:** the data spans this repo plus the live, unversioned `TAOM_Map` and
  `LOTRLOME_Armory` installs ([module coverage](../features/moduledata-validation.md)).
- **Two machines:** every `E:\` path is the desktop. The laptop holds a partial install, so
  reference failures there are environment gaps, never repo defects
  ([development-machines.md](../reference/development-machines.md)).
- **Localization:** new player-facing text is `{=KEY}default`, then registered, translated and
  validated. 12 languages, all AI first drafts; `tools/translation_overrides/<lang>.json` wins
  ([TRANSLATOR_GUIDE.md](../localization/TRANSLATOR_GUIDE.md)).
- **Lessons:** the evidence archive per subsystem is `docs/reviews/lessons/<category>.md`
  ([index](../reviews/LESSONS-LEARNED.md)); append after every review or RCA.

## Trap index

One line per trap: its trigger and what never to do. The linked doc holds the mechanism, the history
and the gate.

| Trap | Rule | Doc |
|---|---|---|
| TAOM_Map settlements | Edit the live `TAOM_Map/ModuleData/settlements.xml`; the repo's copy is a stale shadow | [naming](../reference/taom-map-settlement-naming.md) |
| Prefab entity cap | The 131,072 queue is global across modules; `check_prefab_budget.py` counts TAOM_Map only | [module map](../modding/module-map.md) |
| Unversioned modules | A fix in the live Armory or TAOM_Map reverts on reinstall; land an in-repo gate with it | [coverage](../features/moduledata-validation.md) |
| Parked features | NavalTravel and NativeSkinFixes are disabled at the `SubModule.cs` wiring | [naval](../features/naval-travel.md), [skin](../features/native-skin-fixes.md) |
| Persisted MCM defaults | json2 keeps the old value, so rename a setting to change its default, never flip it | [shaders](../features/shader-precompilation.md) |
| Moving platforms | Agents need a navmesh riding the entity, plus physics; teleporting fails. Crew stand inside the deck | [mumakil](../features/mumakil.md) |
| Vendored DLLs | `Main/_Module/bin/Win64_Shipping_Client/` ships only `MinHook.x64.dll` and `TAOM.NativeSkinFixes.dll`; never MCMv5 | [deps](../modding/module-dependencies.md) |
| Mission logic base | `BehaviorTreeMissionLogic` derives from `MissionLogic`, never `MissionBehavior` | [RCA](../reviews/rca-looter-battle-nre-2026-05-24.md) |
| Armory dependency | It is `LOTRLOME_Armory`. A root-level `<action>` kills a dedicated server: `audit_action_set_parity.py` | [armory](../reference/armory-guide.md) |
| Armory item ids | Grep every `LOTRLOME_items/*/` for the id prefix first; a second folder silently shadows one | [armory](../reference/armory-guide.md) |
| Shield body name | `bo_capwm_isengard_shield_a02_clean` ships misspelled; the "fixed" name resolves to nothing | [shields](../reference/armory-shield-audit.md) |
| Shield plus polearm | A shield troop never draws a polearm absent from `OneHandedPolearm`: `audit_polearm_shield_parity.py` | [pipeline](../features/weapon-xml-pipeline.md) |
| Co-op gating | Co-op loaded, peer authority and dedicated server are three different questions | [co-op](../features/coop-interop.md) |
| Console commands | Route through `TaomConsole`; a wrongly shaped command throws in unguarded startup discovery | [console](../features/dev-console.md) |
| Landless cultures | A culture owning no settlement CTDs the daily clan tick: `Patch65`, `LANDLESS_CULTURE` | [spawn guard](../features/lord-spawn-guard.md) |
| Culture party templates | An XSLT culture block inherits vanilla for every attribute it omits; caravan lists union | [wiring](../features/culture-playability-wiring.md) |
| NPCCharacter, no `<face>` | Renders as a toddler with no error: `CharacterFaceCoverageTests` | [body properties](../modding/body-properties.md) |
| Enlisted service | Only `DischargeService` ends it; parked, or visible in the commander's settlement, is legitimate | [enlistment](../features/enlistment.md) |
| Leaving a map event | Clearing `MainParty.AttachedTo` mid-event nulls the upgrade tracker; vanilla CTDs later | [guard](../features/map-event-guard.md) |
| Menus stop time | Land on the map after anything timed; a false WAIT-menu condition hides every option | [field camp](../features/field-camp.md) |
| "Frozen" game | Can be a stuck kingdom vote waiting on an `IsKingsDecisionOver` edge: `Patch80` | [diplomacy](../features/diplomacy.md) |
| Hero capture | Patch `Hero.CanBecomePrisoner`; denying capture grants escape; both seams share one `DeathMark` guard | [capture](../features/uncapturable-heroes.md) |
| `Town.LastCapturedBy` | A leader-clan stamp, not participation; score sieges from `IFiefSiegeParticipationService` | [fiefs](../features/fief-granting.md) |
| MarriageModel | One slot: `AddModel` does not compose, so every marriage rule goes in `TaomMarriageModel` | [marriage](../features/marriage-alignment.md) |
| Player Switcher | Register at priority 1100; reassign the player clan before removing the created hero | [switcher](../features/player-switcher.md) |
| Settlement menus | Need an encounter; only `IEncounterAdapter.EnsureSettlementEncounter` places the player | [enlistment](../features/enlistment.md) |
| Armory art drops | A mesh rename strands XML refs and hangs preload; repoint refs, never restore a tpac | [ref audit](../features/armory-ref-audit.md) |
| Borrowed `bo_` body | A weapon's body is its own mesh's `bo_` twin; a borrow dies on the next art drop | [validation](../features/moduledata-validation.md) |
| Unsaved tpac | A package without its `RuntimeDataCache` `.rdc` is skipped, silently: `check_rdc_entries.py` | [pipeline](../reference/ue-to-bannerlord-asset-pipeline.md) |
| Armory asset tree | Loose `Assets/**` loads and wins; the inventory is generated, never counted by hand | [armory](../reference/armory-guide.md) |
| Horse-skeleton reskins | Share the engine's action vocabulary; the rig's only attack is `act_horse_kick` | [war ram](../features/war-ram.md) |
| HorseHarness | Required beside every Horse slot; exemptions only in `_HARNESSLESS_BY_DESIGN` | [war ram](../features/war-ram.md) |
| Mount size | A Monster with `taom_body_length` overrides every item's `body_length`: resize on the Monster | [monster size](../features/monster-size.md) |
| Animation | Author on the engine skeleton: engine frames, rest pose at frame 0, list-order hierarchy | [skeleton](../reference/bannerlord-skeleton-authoring.md) |
| Kit clip rename | Corrupts the clip: keep the name, close the Kit, run `rename_anim_clip_tpac.py` | [tools](../../tools/README.md) |
| Own-skeleton humanoid | A clip stores parent-relative rotations: re-framing fixes axes only; retarget the clips too | [skeleton](../reference/bannerlord-skeleton-authoring.md) |
| UE clip export | The root node carries pelvis height above bind; keep root height, drop only travel and yaw | [pipeline](../reference/ue-to-bannerlord-asset-pipeline.md) |
| Kit FBX materials | Bound by name module-wide, reset on every re-import; a same-named older material wins silently | [troll](../features/troll-race.md) |
| Player start kits | Defaults are `starter_<donor>` twins, careers take troop gear; override the roster, never a set | [start kits](../features/starting-equipment-tuning.md) |
| Troop bows | Generated `ladder_*` items in the unversioned Armory: `generate_ranged_ladder_items.py --verify` | [ladders](../features/ranged-ladders.md) |
| Stop order | `StandGround` never forms a line; shape a formation with a Move | [cavalry](../features/smart-cavalry-ai.md) |
| Agent slots, threads | Indices recycle and callbacks run off-thread: never key on `Agent.Index`, write via `RunOrDefer` | [rule](../../.claude/rules/csharp-architecture.md) |

# Hero Race

## Overview
HeroRace ensures that non-human heroes (elves, dwarves, orcs, goblins, etc.) render with the correct skeletal monster, animations, and camera framing in character tableaux, inventory, and spawned scenes. It also persists each hero's race integer across save/load cycles to counteract Bannerlord resetting races on campaign reload.

## Why This Exists
- **Vanilla behavior:** `CharacterTableau`, `CharacterSpawner`, and `FaceGen` all assume race 0 (human). When a non-human race is set, the tableau refreshes using the human monster base, producing T-pose or wrong proportions. No race data is serialized in the vanilla save path for heroes.
- **TAOM requirement:** TAOM has 10+ custom races (dwarf, orc, goblin, elf, etc.) defined in `monsters.xml`. Each race needs its own `Monster` base when building `AgentVisuals`, and its own camera position offset so that dwarves and orcs are framed correctly in the inventory and character creation screens.
- **Without this feature:** Non-human heroes appear with human proportions in all UI views. Dwarf eye-height is at human level, making the camera clip through foreheads. Hero races revert to 0 on every campaign load, destroying any race-specific gameplay downstream.

## Architecture
### Design Challenge
`CharacterTableau` and `CharacterSpawner` are sealed TaleWorlds classes. Their `InitializeAgentVisuals` and `InitWithCharacter` methods are private, and their internal fields (`_agentVisuals`, `_agentEntity`, `_race`, etc.) are private. The methods must be fully reimplemented when a non-human race is involved.

`Monster` is also sealed, and its `StandingEyeHeight`/`CrouchEyeHeight` are auto-properties with private setters, requiring reflection to adjust.

Race is an `int` in `CharacterObject` and `Hero`, not a strong type. The mapping from `int` to race name (e.g., `2 -> "dwarf"`) lives in the game's `monsters.xml` via `IRaceManager`.

Save compatibility: Bannerlord's `SyncData` serializes `Hero` fields but not the `Race` property directly in all paths. Races must be captured before saving and restored after loading.

### Solution Approach
Five Harmony patches intercept the key rendering and race-assignment paths:

- `Patch3_SetRace` (`CharacterTableau_SetRace_Patch`) — Postfix on `CharacterTableau.SetRace`. After the race field is written, resets agent visuals and calls `InitializeAgentVisuals` so that the tableau rebuilds with the new monster base.
- `Patch4_CharacterSpawner` (`CharacterSpawner_InitWithCharacter_Patch`) — Prefix on `CharacterSpawner.InitWithCharacter`. When `characterCode.Race > 0`, fully replaces the method with `CharacterSpawnerService.InitWithCharacter`, which replicates the vanilla logic but calls `_faceGenAdapter.GetBaseMonsterFromRace(race)` and applies per-race position offsets from `RacePositionConfig`.
- `Patch5_FaceGen` (`FaceGen_GetBaseMonsterFromRace_Patch`) — Postfix on `FaceGen.GetBaseMonsterFromRace`. Delegates to `EyeHeightAdjustmentHook`, which lowers `StandingEyeHeight` and `CrouchEyeHeight` by 0.2 for the `dwarf` race via reflection.
- `CharacterTableau_RefreshCharacterTableau_Patch` — Postfix on `CharacterTableau.RefreshCharacterTableau`. Delegates to `CharacterTableauService.RefreshCharacterTableau`, which rebuilds agent visuals with race-aware position offsets read from `CharacterAvatarPatch.json`.
- `BasicCharacterTableau_RefreshCharacterTableau_Patch` (`Patch55_BasicTableauRaceGuard`, applied in `OnBeforeInitialModuleScreenSetAsRoot`) — Prefix on the private `BasicCharacterTableau.RefreshCharacterTableau` (the Save/Load hero preview, a **different** class from `CharacterTableau`). Coerces the private `_race` to the human base via `IBasicTableauRaceGuard.ResolveSafeRace` so the agentless static-morph build can't AV on a custom-race head — see "Save/Load Hero Preview CTD Guard" below.

`RacePersistenceBehavior` (CampaignBehaviorBase) captures all hero races to a `Dictionary<string, int>` before save (`OnBeforeSaveEvent`) and restores them after session launch (`OnSessionLaunchedEvent`). The dictionary is serialized through `SyncData` under the key `_taom_heroRaceMap`.

Position offsets per race are stored in two JSON config files: `CharacterAvatarPatch.json` (inventory/avatar view) and `CharacterImagePatch.json` (character spawner). Each entry has `race`, `horizontal`, `vertical`, and `zoom` float offsets.

### Component Diagram
```
CharacterTableau.SetRace() [Postfix Patch3_SetRace]
    |-> resets _agentVisuals, calls InitializeAgentVisuals()

CharacterTableau.RefreshCharacterTableau() [Postfix]
    |-> CharacterTableauService.RefreshCharacterTableau(tableau)
            |-> IRaceManager.GetRaceNameFromId(_race)
            |-> RacePositionConfig["CharacterAvatarPatch"].Items[raceName]
            |-> applies position offset to charframe / mountframe
            |-> rebuilds AgentVisualsData with Race(race) and adjusted frame

CharacterSpawner.InitWithCharacter() [Prefix Patch4_CharacterSpawner, returns false]
    |-> CharacterSpawnerService.InitWithCharacter(spawner, characterCode)
            |-> IFaceGenAdapter.GetBaseMonsterFromRace(race)
            |-> RacePositionConfig["CharacterImagePatch"].Items[raceName]
            |-> builds AgentVisuals with correct monster base + position offset

FaceGen.GetBaseMonsterFromRace() [Postfix Patch5_FaceGen]
    |-> EyeHeightAdjustmentHook.OnGetBaseMonsterFromRace(ref result, race)
            |-> if raceName == "dwarf": adjusts StandingEyeHeight and CrouchEyeHeight via reflection

RacePersistenceBehavior (CampaignBehaviorBase)
    |-> OnBeforeSave -> RacePersistenceService.CaptureHeroRaces()
    |-> OnSessionLaunched -> RacePersistenceService.RestoreHeroRaces()
    |-> SyncData -> RacePersistenceService.SyncRaceData(dataStore)
                       serializes _taom_heroRaceMap
```

## Configuration
Two JSON files under the mod's config path (resolved via `IPathService.ConfigPath`):

| File | Purpose |
|------|---------|
| `CharacterAvatarPatch.json` | Per-race position offsets for inventory/avatar tableau (`CharacterTableauService`) |
| `CharacterImagePatch.json` | Per-race position offsets for character spawner scenes (`CharacterSpawnerService`) |

Each file contains a JSON array of objects with fields: `Race` (string, lowercase race name), `Horizontal` (float), `Vertical` (float), `Zoom` (float).

Special case: entries prefixed with `mount_` (e.g., `mount_dwarf`) are used to offset the mount position in mounted tableau views.

## Key Files
| File | Purpose |
|------|---------|
| `Main/Features/HeroRace/HeroRaceIoC.cs` | DryIoc registrations; also initializes `FaceGen_GetBaseMonsterFromRace_Patch` with the resolved hook |
| `Main/Features/HeroRace/CharacterTableauService.cs` | Rebuilds tableau agent visuals with race-aware camera offsets |
| `Main/Features/HeroRace/IBasicTableauRaceGuard.cs` / `BasicTableauRaceGuard.cs` | Allow-list deciding which races are safe in the agentless `BasicCharacterTableau` build (Save/Load preview); coerces others to the human base |
| `Main/Features/HeroRace/CharacterSpawnerService.cs` | Full reimplementation of `CharacterSpawner.InitWithCharacter` with race-aware monster base |
| `Main/Features/HeroRace/EyeHeightAdjustmentHook.cs` | Lowers dwarf eye height by 0.2 via reflection on the `Monster` struct |
| `Main/Features/HeroRace/RacePersistenceService.cs` | Captures and restores hero race integers across save/load |
| `Main/Features/HeroRace/RacePersistenceBehavior.cs` | CampaignBehaviorBase wiring for save/load events and SyncData |
| `Main/Features/HeroRace/RacePositionConfigurationService.cs` | Reads both config files and exposes race and mount position items by race name |
| `Main/Features/HeroRace/Configuration/RacePositionConfig.cs` | JSON config POCO + `LoadConfig`/`WriteConfig` helpers |
| `Main/Features/HeroRace/Hooks/CharacterTableau_SetRace_Patch.cs` | Patch3_SetRace postfix |
| `Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_Patch.cs` | RefreshCharacterTableau postfix |
| `Main/Features/HeroRace/Hooks/CharacterSpawner_InitWithCharacter_Patch.cs` | Patch4_CharacterSpawner prefix |
| `Main/Features/HeroRace/Hooks/FaceGen_GetBaseMonsterFromRace_Patch.cs` | Patch5_FaceGen postfix delegating to EyeHeightAdjustmentHook |
| `Main/Features/HeroRace/Hooks/BasicCharacterTableau_RefreshCharacterTableau_Patch.cs` | `Patch55_BasicTableauRaceGuard` prefix (applied pre-menu in `OnBeforeInitialModuleScreenSetAsRoot`); coerces the Save/Load preview `_race` to a tableau-safe race (CTD guard) |
| `Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs` | `Patch56_SceneNotificationVisualGuard` — three classes: a Finalizer on `GauntletSceneNotification.OpenScene` that aborts a become-king/sibling cinematic when a character's human `AgentVisuals` NREs (CTD guard); a deferred-close Postfix on `.OnTick` (tears the notification down after OnTick re-touches input, avoiding a soft-lock); a diagnostic Prefix on `PopupSceneSpawnPoint.InitializeWithAgentVisuals` that probes the engine's deref chain and logs the failing member |
| `Main/Features/HeroRace/Hooks/ActionSetCode_GenerateActionSetNameWithSuffix_Patch.cs` | Action set suffix handling for non-human races |
| `TAOM.Tests/Features/HeroRace/EyeHeightAdjustmentHookTests.cs` | Dwarf eye-height adjustment logic |
| `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs` | Capture/restore race dictionary |
| `TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs` | Event registration |
| `TAOM.Tests/Features/HeroRace/Configuration/RacePositionConfigTests.cs` | Config POCO loading |
| `TAOM.Tests/Features/HeroRace/BasicTableauRaceGuardTests.cs` | `ResolveSafeRace` allow-list: human preserved, custom/invalid coerced to human |

## Dependencies
- `IRaceManager` — maps race int to race name string (from `TAOM.Core.Domain`)
- `IFaceGenAdapter` — wraps `FaceGen.GetBaseMonsterFromRace` (sealed type adapter)
- `IHeroRosterAdapter` — iterates all alive heroes and sets race values
- `IModLogger` — diagnostic logging

## Tests
- `EyeHeightAdjustmentHookTests.cs` — verifies that `OnGetBaseMonsterFromRace` modifies `StandingEyeHeight` and `CrouchEyeHeight` for race id mapping to "dwarf", and is a no-op for other races or race 0.
- `RacePersistenceServiceTests.cs` — verifies that `CaptureHeroRaces` skips race 0, that `RestoreHeroRaces` only calls `SetHeroRace` for heroes whose stored race differs from the current, and that `SyncRaceData` calls `dataStore.SyncData` with the correct key.
- `RacePersistenceBehaviorTests.cs` — verifies event registration bindings.
- `RacePositionConfigTests.cs` — verifies deserialization of config items and fallback to an empty config when the file is absent.

## How to Add a New Race's Position Offset
1. Determine the race name as registered in `monsters.xml` (must match `IRaceManager.GetRaceNameFromId` output, lowercase).
2. Edit `CharacterAvatarPatch.json` to add an entry: `{ "Race": "yourrace", "Horizontal": 0.0, "Vertical": 0.0, "Zoom": 0.0 }`.
3. If the race can be mounted, add a `mount_yourrace` entry for the mount offset.
4. Edit `CharacterImagePatch.json` similarly for spawner scenes.
5. Tune values in-game by equipping a hero of the race in inventory and adjusting until framing looks correct.
6. If the race requires eye-height adjustment (e.g., very short), extend `EyeHeightAdjustmentHook.OnGetBaseMonsterFromRace` with a new branch for the race name, write the test first.

## Wanderer Race Fix (2026-04-08)

Bannerlord's `BasicCharacterObject.Deserialize()` natively supports a `race=` XML attribute (lines 323-328), calling `FaceGen.GetRaceOrDefault(value)`. This means wanderer templates can declare their race directly in XML — no C# code needed.

**Changes applied to `taom_wanderers.xml`:**
- 30 elven wanderers (Rivendell 10, Mirkwood 10, Lothlorien 10): added `race="elf"`
- 10 Dol Guldur wanderers: fixed `race="orc"` to `race="dg_uruk"`, fixed `BodyProperty.fighter_empire` to `BodyProperty.fighter_dolguldur`
- 57 wanderers already had correct race attributes (Mordor, Gundabad, Isengard, Erebor)
- 83 human-culture wanderers correctly default to race 0 by omission

The existing `RacePersistenceService` automatically handles wanderer race persistence — when a wanderer is spawned from a template with `race="elf"`, the Hero inherits the race via `CharacterObject.CreateFrom()` / `FillFrom()`, and `CaptureHeroRaces()` captures it on save.

**Save compatibility:** Pre-existing wanderer heroes keep race=0 until they die and are replaced by new wanderers from updated templates. Natural wanderer turnover handles migration.

## Wanderer Equipment Must Fit the Custom Skeleton (2026-06-01)

Setting `race="dwarf"` (above) gives a wanderer the correct monster skeleton — but it does **not** change their equipment. The two are independent: an NPC can have the right skeleton and still wear human-rigged cloth, which clips/floats on the custom skeleton.

The 12 Erebor wanderers (`spc_wanderer_erebor_0` … `_11`) share one equipment roster, `npc_companion_equipment_template_erebor`, defined in `Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml` (the wanderer XML only *references* it via `<EquipmentSet id="…" />`). That roster was still populated with **vanilla Bannerlord items** (`tunic_with_shoulder_pads`, `leather_cap`, `vlandia_sword_1_t2`, `scarf`, `strapped_shoes`, …) — so the Encyclopedia showed dwarf wanderers in a vanilla green tunic sitting wrong on the dwarf body.

**Rule:** any NPC with `race="dwarf"` must be equipped only with LOTRLOME_Armory items authored for the dwarf skeleton — `sk_dwarf_erebor_*` / `sk_dwarf_iron_*` armour and `sm_dwarf_erebor_*` weapons/shields. The same principle applies to every custom-skeleton race (elf, orc, goblin, …): the `race=` attribute and the equipment meshes must match, or the mesh renders wrong.

**Fix (issue [#261](https://github.com/haterade22/TAOM/issues/261)):** swapped `npc_companion_equipment_template_erebor` (6 battle + 3 civilian sets) to low-end dwarf gear, mirroring the lowest Erebor troop (`erebor_militia_spearman`). The roster's *structure* was already vanilla-correct (verified against vanilla `npc_companion_equipment_template_khuzait` in `SandBoxCore/ModuleData/sandboxcore_equipment_sets.xml` — right `equipmentType="Civilian"` tagging, slot vocabulary, no `Horse` for an infantry culture), so this was a pure item-ID swap. The roster id is referenced only by `taom_wanderers.xml`, so the edit is scoped to the 12 wanderers.

**Already correct (checked, not changed):** dwarf town notables (`characters/npcs_erebor.xml`), dwarf named companions (Gimli et al., `named_companions/named_companions.xml`), and dwarf troops (`troops/troops_erebor.xml`) were already on `sk_dwarf_*` / `sm_dwarf_*` gear (audited with `grep 'id="Item\.(?!sk_dwarf_|sm_dwarf_)'` → zero matches). Dwarf lords carry no inline equipment (template-driven path). The generic wanderers were the only gap.

**Audit recipe for the next dwarf NPC:** `python tools/validate_moduledata.py` confirms refs resolve and civilian sets stay tagged; the negative-lookahead grep above flags any non-dwarf item that slipped into a dwarf NPC's roster.

## Save/Load Hero Preview CTD Guard (2026-06-24)

Loading a save from the main menu hard-crashed (`AccessViolationException`) when the save's character was a custom (non-human) race. The crash is **vanilla code fed bad data** — no TAOM patch was on the stack.

**Root cause.** The Load Game screen renders each save's hero into a `BasicCharacterTableau` (a class **distinct** from the `CharacterTableau` used in inventory/CC). `BasicCharacterTableau.RefreshCharacterTableau` parses the save's character code → sets a private `_race`, then builds the body via the **agentless** native `MBAgentVisuals.FillEntityWithBodyMeshesWithoutAgentVisuals(entity, SkinGenerationParams{_race}, _bodyProperties, glovesMesh)` on the hardcoded human skeleton. For a custom-race head the native static-morph build dereferences a null morph-data pointer (custom LOTRLOME heads lack the per-face-component morph data vanilla heads carry — the same gap behind the Erebor-arena crash, issue #295). Because a native AV is a corrupted-state exception, a `try/catch` in managed code can't catch it; the fix must prevent the bad native call.

**Why `CharacterTableau` doesn't crash but `BasicCharacterTableau` does.** `CharacterTableau` (inventory, character-creation) builds through `AgentVisuals.Refresh` with `.UseMorphAnims(true).Race(race)` — the morph-tolerant path. `BasicCharacterTableau` (Save/Load preview only) uses the agentless static-morph path with no such handling. The two are separate classes; the existing four HeroRace patches target `CharacterTableau`, leaving the Save/Load path unguarded.

**Fix.** `BasicCharacterTableau_RefreshCharacterTableau_Patch` (Prefix) coerces the private `_race` to a render-safe race via `IBasicTableauRaceGuard.ResolveSafeRace` before the native build. `BasicTableauRaceGuard` keeps an allow-list of races whose heads carry the morph data (`TableauSafeRaces`, today only the human base `0`); any race not on it is coerced to human. Coercing `_race` selects vanilla human head+body meshes (which have the morph data), so the AV can't fire — the other `SkinGenerationParams` fields (`_bodyMeshType`/`_bodyDeformType`/`_skinMeshesMask`) are sub-selectors within the chosen race's mesh set and don't drive the head morph path independently.

**Scope / tradeoff.** Decompile confirmed `BasicCharacterTableau` is instantiated **only** by `SaveLoadHeroTableauTextureProvider`, so the guard touches nothing but the Save/Load preview. An affected custom-race save shows a human-headed thumbnail (equipment still correct) until the head morph data is authored asset-side (issue #295) — at which point that race's id joins `TableauSafeRaces` and its true preview returns.

**Timing (Codex C1, issue #299) — the easy-to-miss part.** The guard has its own `Patch55_BasicTableauRaceGuard` category applied from `SubModule.OnBeforeInitialModuleScreenSetAsRoot`, with a process-static one-shot flag. It does **not** ride the sibling CharacterTableau patches' `Patch2_RefreshTableau`, which `SubModule` applies in `OnGameInitializationFinished` (campaign init). The other HeroRace tableau patches protect in-game/CC screens that only appear after a game starts, so applying them at campaign-init is both safe and in time. But the Save/Load preview is the one View tableau that renders on the **cold main menu, before any game-init callback** — so its guard must attach earlier, before the initial module screen is pushed (the guard object is already set by `IoC.Configure` in `OnSubModuleLoad`). The original fix reused `Patch2_RefreshTableau` and would have left the reported crash unguarded; the deep-review's lifecycle trace conflated "after module load" with "after game-init." RCA: `docs/reviews/rca-savetableau-2026-06-24.md`.

## Scene-Notification Visual CTD Guard (2026-06-25)

Becoming the **ruler of a kingdom** crashed to desktop. Two crash logs (2026-06-24/25, both `MainHero='Isildur' (human), kingdom='empire_w'`) show a managed `NullReferenceException` (HResult 0x80004003) in `PopupSceneSpawnPoint.InitializeWithAgentVisuals`, reached via `GauntletSceneNotification.OnTick → OpenScene`. No TAOM patch is on the stack — unguarded engine code, the **fourth** raw custom-race/visual render path (after the Save/Load preview above, the in-game `CharacterTableau`, and mission spawns).

**Mechanism.** Becoming king fires the engine's `BecomeKingSceneNotificationItem` cinematic (`scn_become_king_notification`, from `DefaultCutscenesCampaignBehavior.OnKingdomDecisionConcluded`). `GetSceneNotificationCharacters()` enqueues ~20 culture characters (the new ruler + the kingdom culture's townsfolk + bodyguards + the ruler's own clan companions). For each, `OpenScene` builds a human `AgentVisuals` and passes it to `PopupSceneSpawnPoint.InitializeWithAgentVisuals`, which derefs the human visual **without a null guard** (`PopupSceneSpawnPoint.cs:91/92` and the unconditional else `:108/109` — `_humanAgentVisuals.GetEquipment().Clone(false)`). The **mount** visual is fully null-guarded (foot characters legitimately have none); the engine assumes the **human** is always non-null — that asymmetry is the bug. One character's null/unbuildable visual NREs the whole cinematic.

**Fix.** `Patch56_SceneNotificationVisualGuard` — a **Finalizer** on the private `GauntletSceneNotification.OpenScene` swallows ONLY `NullReferenceException` (returning to `OnTick` lets `:135 _isPendingSceneLoad=false` run → no per-frame re-crash loop). Net: a cinematic that CAN render still plays; one that would crash aborts. Cause-agnostic and generic — also covers the behavior's sibling notifications (`KingdomCreated`/`JoinKingdom`/`Marriage`/death cutscenes), which share the same render path. Registered in `SubModule.OnGameInitializationFinished` (the cinematic only fires in an active campaign — unlike Patch55's cold-menu site). Unlike the Save/Load guard (`Patch55`, a preventive race-coercion Prefix), this is a Finalizer because the crash is a **catchable managed NRE**, not a native AV, and the exact failing character isn't statically pinnable.

**Deferred close (the easy-to-miss part — deep-review MED, 2026-06-25).** The finalizer must NOT call `MBInformationManager.HideSceneNotification()` synchronously. That `→ CloseNotification()` releases focus/input (`GauntletSceneNotification.cs:469-472`), but control then returns to `OnTick`, which **unconditionally** re-acquires `IsFocusLayer=true` + `SetInputRestrictions(true,7)` at `:127-129` one line after the swallowed `OpenScene` returns — leaving the campaign map focus/input-locked with no normal path to release it (a soft-lock as bad as the CTD). So the finalizer instead raises a static `CloseRequested` flag and a sibling **Postfix on `GauntletSceneNotification.OnTick`** runs `HideSceneNotification()` AFTER the OnTick body — so the input/focus release is the final word. The reference guards (Patch49/Patch50) sit on leaf methods with no caller-side continuation, so they never hit this class of problem; this finalizer's caller (`OnTick`) keeps mutating the same shared layer/focus/input state the teardown releases, which is why the close must be deferred a frame's-end later.

**Why a Finalizer, not race-coercion.** Static analysis (three decompile passes + direct data trace) could **not** name the exact null member: every `empire_w`/`gondor` become-king character resolves to a valid race-0 human with valid equipment (king = human; `empire_w` culture remapped to gondor via `spkingdoms.xslt:44-55`; bodyguard = `fighter_sturgia` = vanilla human; companions = the king's own gondor clan via `GetMilitaryAudienceForHero`'s same-clan filter). The precise null is most likely a native facegen/skin build edge case or a companion's null notification-equipment — only a live mixed-mode capture can name it. A guard at the engine chokepoint is therefore the correct, cause-agnostic fix. A companion **diagnostic Prefix** on `InitializeWithAgentVisuals` (pure logging, in the same Patch56) replicates the engine's own first derefs — `GetCopyAgentVisualsData()` then `GetEquipment()` — and logs which one fails, so the next occurrence self-identifies the culprit (the first cut probed only `GetEquipment()==null`, which the deep-review showed never fires on the real path since the engine's first deref is `GetCopyAgentVisualsData()` and the equipment is always non-null). The "missing culture filler-troop" hypothesis was **ruled out** — the gondor refs all resolve.

**Scope / residual.** `Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs` (three patch classes: the OpenScene Finalizer, the OnTick deferred-close Postfix, the InitializeWithAgentVisuals diagnostic Prefix). The one remaining live-only check is the deferred-close path — verify in-game that after an aborted become-king cinematic the campaign map still accepts input and the next scene-notification still displays. Known minor residual (engine-side, not fixable from a patch): the `PopupSceneSpawnPoint` that crashed throws before it's added to `_sceneCharacterScripts`, so its half-built `AgentVisuals` isn't `Reset()` during teardown — a bounded one-per-abort managed-reference leak reclaimed at scene `ClearAll()`. Fallback if the deferred close ever proves insufficient: suppress the offending notification up front via a `MBInformationManager.ShowSceneNotification` Prefix (deterministic, but loses those cinematics). Adversarially reviewed by 5 agents (0 HIGH; the deferred-close gap above was the one real finding, now fixed).

## Changelog
- 2026-06-25 — Scene-notification visual CTD guard: `Patch56_SceneNotificationVisualGuard` Finalizer on `GauntletSceneNotification.OpenScene` aborts a become-king/sibling cinematic cleanly when a character's human `AgentVisuals` is null (the engine derefs it unguarded in `PopupSceneSpawnPoint.InitializeWithAgentVisuals`). Fourth raw render path; cause-agnostic; registered in `OnGameInitializationFinished`. Teardown is **deferred** to an `OnTick` Postfix so `OnTick:127-129` can't re-lock input after the close (deep-review MED soft-lock fix). Companion diagnostic Prefix probes the engine's deref chain (`GetCopyAgentVisualsData`/`GetEquipment`) and logs the failing member. 5-agent review: 0 HIGH. Exact culprit pending a live capture.
- 2026-06-24 — Save/Load hero preview CTD guard (#299): `BasicCharacterTableau_RefreshCharacterTableau_Patch` + `BasicTableauRaceGuard` coerce a custom `_race` to the human base so the agentless static-morph build can't AV on a morph-less custom head (issue #295 class). Save/Load-preview-only; in-game `CharacterTableau` untouched. Own `Patch55_BasicTableauRaceGuard` category applied pre-menu in `OnBeforeInitialModuleScreenSetAsRoot` (Codex C1: `Patch2_RefreshTableau` applies at campaign-init, too late for the cold-menu save list).
- 2026-05-14 — Hardened RacePersistence (#130): added `ResetForNewCampaign()` on new-game so stale race maps don't carry across campaigns, dropped the `race>0` filter so human resets are captured, and null-guarded the hero-roster adapter.
- 2026-05-14 — Added `RacePersistenceBehavior` wiring tests (#183) pinning the OnBeforeSave (capture) and OnSessionLaunched (restore) subscriptions and IoC registration.
- 2026-04-08 — HeroRace fixes: ActionSetCode BaseMonster/StringId preference and EyeHeight init retry.
- 2026-01-29 — Initial HeroRace feature: race-aware character spawning/tableau rendering, eye-height adjustment, race persistence across save/load, and the four Harmony patches.

## GitHub Issue
- **Issue:** Unknown
- **Status:** Unknown

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/arena.md](./arena.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->

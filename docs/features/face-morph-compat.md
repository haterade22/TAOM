# Face Morph Compatibility (custom-race static-morph crash guard)

## Overview

Forces the engine's **GPU face-morph** path for custom-LOTR-race agents at the
`Agent.AddSkinMeshes` chokepoint, so batched/deferred crowd spawns (arena & town
spectators) never take the **CPU/static-morph** path that access-violates on a custom head
whose face component lacks morph data. Fixes a dwarf-in-Erebor-arena crash-to-desktop and the
"naked spectator crowd" that ships with it.

## Why This Exists

A dwarf entering the Erebor arena produced a native access violation
(`0xC0000005` reading `0x24C`, entirely inside `TaleWorlds.Native.dll`, on a worker thread)
**and** every dwarf spectator in the stands rendered naked (bare base mesh, no clothes).

Root cause, established by evidence (not inference):

1. **Native triage** (`tools/native_crash_triage.py` over the 7 fault frames, module base
   `0x191A0000`) put the crash in the function carrying the string
   **`"No morph data found for face mesh. Can not do static morph."`** The faulting
   instruction `movzx edx,[r8+r9*2]` indexes ~element 294 off a **null** morph-data pointer
   (`r8 = [r13+0] = null`, a face mesh that exists but whose morph-data pointer is null) →
   fault at `0x24C`. The calling frame references `face_base_mesh / face_eye_mesh /
   face_eyelash_mesh / face_mouth_mesh` → the FaceGen face-mesh builder.
2. **The path is a managed bool.** `Agent.cs:4560` builds skin meshes with
   `AddSkinMeshes(!neededBatchedItems || prepareImmediately, …)`. That first bool flows
   unchanged into native `add_skin_meshes_to_agent_entity(…, useGPUMorph, …)`
   (`Agent.AddSkinMeshes` → `AgentVisuals.AddSkinMeshes`, 3rd arg `useGPUMorph`). When it is
   `false` the engine runs the **CPU/static-morph** branch — the crashing one. Batched,
   deferred location-character spawns (arena/town spectators, via
   `MissionAgentHandler.SpawnLocationCharacters → SpawnWanderingAgent`) are the only spawns
   that arrive `false`. Main/battle agents arrive `true` → GPU path → no crash. That is why
   dwarves render fine in battles, town, and character-creation but crash in the arena stands.
3. **The asset gap.** The custom LOTR head meshes lack the per-face-component morph data the
   static path indexes. Verified two ways:
   - **Blender** (importing `LOTRLOME_Armory/AssetSources/Race Test/dwarf/sk_dwarf_bm_f1.fbx`):
     `…head` = 102 shape keys, `…head.mouth` = 102, **`…head.eye` = 0**, and there is **no
     eyelash mesh** at all.
   - **Compiled tpac** (`grep` over `AssetPackages/*.tpac`): no LOTRLOME head has a
     `face_eyelash_mesh` component, while vanilla `head_female_a` (Native `core_game.tpac`)
     does. The same gap holds for **uruk, orc, nazghul** (and every other custom head in
     `skins.xml`).

The naked rendering is the same failure: the aborted AgentVisuals build never attaches the
equipment meshes, so the body shows as bare base mesh.

Ruled out during diagnosis:
- **`NativeSkinFixes` is NOT involved** — its 7 signatures ship as `<PATTERN_TBD>` stubs
  (`IsAuthored()` false), so the hooks never install. This is a **pure vanilla** crash.
- **Not the equipment "underwear bug"** — `tools/validate_moduledata.py` PASS and
  `tools/validate_all_troop_refs.py` reports `erebor … missing=0`. Equipment refs all resolve.

## Architecture

### Design challenge

The static-morph AV is in vanilla native code; the morph data the custom heads lack is an
asset-pipeline gap. The cheapest, most robust managed fix is to never let a custom-race agent
take the static path — route it onto the GPU path the same head already renders correctly
with in every other context.

### Solution approach

A Harmony **Prefix** on the private `Agent.AddSkinMeshes(bool prepareImmediately, bool, int)`
flips the first argument (the native `useGPUMorph` flag) `false → true` when the agent's race
is a custom-mesh race. The decision is a pure service keyed on race name. The gate is
**fail-safe**: it forces GPU morph for every race except the vanilla `"human"` race (and for
an unrecognized race id), so it can never miss a custom-head race; over-forcing is harmless
(GPU morph is the path main/battle agents already use).

```
Agent.AddSkinMeshes(prepareImmediately=false, …)            ← batched crowd spawn
        │  [HarmonyPrefix]
        ▼
Patch52_CustomRaceFaceMorph.Prefix(Agent __instance, ref bool __0)
        │  __0 already true? → return (main/battle agents)
        │  __instance.Character.Race ──► IFaceMorphCompatService.ShouldForceGpuMorph(raceId)
        │                                   IsValidRaceId? no → true (fail-safe)
        │                                   name == "human"? → false ; else → true
        ▼  if true: __0 = true   (→ GPU morph; static-morph branch never taken)
```

This is a thin boundary (ADR-002/007): the patch holds the sealed `Agent`, the service takes
an `int` and returns a `bool`.

## Configuration

None. The safe-race set is a code constant `VanillaMeshRaces = { "human" }` in
`FaceMorphCompatService`. Extend **that set** (never the inverse) if a future custom race ever
ships complete static-morph data and you want to keep its crowds on the cheaper static path.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/FaceMorphCompat/IFaceMorphCompatService.cs` | Decision contract (`ShouldForceGpuMorph(int raceId)`) |
| `Main/Features/FaceMorphCompat/FaceMorphCompatService.cs` | Pure decision: non-`"human"` (or unknown) race → force GPU morph; validate-before-lookup |
| `Main/Features/FaceMorphCompat/FaceMorphCompatIoC.cs` | `Reuse.Singleton` registration |
| `Main/Features/FaceMorphCompat/Hooks/Patch52_CustomRaceFaceMorph.cs` | Harmony Prefix on private `Agent.AddSkinMeshes(bool,bool,int)`, flips `__0` |
| `Main/IoC.cs` | `RegisterFaceMorphCompatFeature(container)` |
| `Main/SubModule.cs` | `_harmony.PatchCategory("Patch52_CustomRaceFaceMorph")` (idempotent `OnGameInitializationFinished`) |
| `TAOM.Tests/Features/FaceMorphCompat/FaceMorphCompatServiceTests.cs` | 16 tests over the gate |

## Dependencies

- `IRaceManager` (`Main/Core/Domain/`) — race id ↔ name resolution (already loaded from FaceGen
  game data). The service applies the validate-before-lookup rule against its `"human"` fallback.
- Verified TaleWorlds bindings (v1.4.5/1.4.6 identical): `Agent.AddSkinMeshes(bool,bool,int)`
  (private, unique), `Agent.Character` (stored field → safe null-guard), `BasicCharacterObject.Race`
  (`int`), `AgentVisuals.AddSkinMeshes(…, useGPUMorph, …)`. `FromHorseObj` agents never call
  `AddSkinMeshes`, so the patch is mount-safe.

## Tests

`TAOM.Tests/Features/FaceMorphCompat/FaceMorphCompatServiceTests.cs` — 16 tests:
`human → false` (incl. case-insensitive); 12 concrete custom races → `true`; invalid race id →
`true` (fail-safe); invalid id never consults the `"human"` fallback name (pins the
validate-before-lookup order). The Harmony prefix itself is not unit-tested (ADR-008 — engine
entry point; verified in-game).

## How-To

**Verify the fix in-game (the real gate):** close Bannerlord → `./build.ps1` → launch → enter
the Erebor arena as a dwarf. Expect no AV and clothed spectators. Repeat for an
uruk/orc/nazghul-culture arena (same latent bug).

**Add a newly-authored custom race:** nothing needed — any non-`"human"` race is auto-forced.

## The complementary asset-level fix (optional, proper structural correction)

The code fix routes *around* the asset gap. The gap itself is real and worth closing for
vanilla-structure parity: the custom heads' **eye component carries 0 morph targets** (vs 102
on base/mouth) and there is **no `face_eyelash_mesh`** component. Authoring the eye morph data
and adding an eyelash component to each custom head (`dwarf`, `uruk`, `orc`, `nazghul`, `elf`,
`goblin`, trolls, `saruman`) and recompiling the tpacs would let the static path work natively.
That is a Kit/Blender task across the LOTRLOME_Armory assets and is not required to stop the
crash; this feature is the immediate, all-races guard.

## Arena-spectator naked-dwarf (separate fix — `CharacterSpawnerService` action set)

The same Erebor-arena report carried a *second*, independent symptom: every dwarf spectator in the
stands rendered naked (in town they are clothed). This is NOT the morph crash above and NOT a
missing item/roster — it is a **skeleton mismatch** in how the arena crowd is spawned.

**Trace:** the stand crowd is 519 scene-baked `crowd` `<game_entity>` (`CharacterSpawner`) entities in
`arena_sturgia_a/scene.xscene`. Each spawns through `CharacterSpawner.InitWithCharacter` →
TAOM `Patch4_CharacterSpawner` → `HeroRace/CharacterSpawnerService.InitWithCharacter`. That method
built the action set via the engine's `MBGlobals.GetActionSetWithSuffix(baseMonster, …)`, which
resolves a custom-race base monster to the **human** action set (`as_human_warrior`, confirmed in the
`taom_debug` log) → the human skeleton. The dwarf body skin still applies (so you see dwarf bodies),
but the dwarf-skeleton-rigged clothing meshes can't bind to a human skeleton → invisible → naked.

The **town** walk path is unaffected: it builds the dwarf `_settlement` monster directly
(`GetMonsterWithSuffix(race, "_settlement")` → `as_dwarf_villager`). **CC + encyclopedia** dwarves are
correct because they use `CharacterTableau_RefreshCharacterTableau_Patch`, which builds the name from
the race monster's `StringId` (`as_dwarf_*`). Only the scene-`CharacterSpawner` crowd path used the
human-resolving `GetActionSetWithSuffix`.

**Fix:** `CharacterSpawnerService.ResolveRaceActionSet` builds the action set by **race name** for
custom races — `as_<race>_<spawnerSuffix>` (e.g. `as_dwarf_warrior`), falling back to
`as_<race>_warrior` when the suffix-specific set isn't authored — mirroring the proven CC/encyclopedia
patch. Human / unknown races keep the original engine resolution (no change for vanilla-mesh races).
The pure name builder `BuildRaceActionSetNames` is unit-tested; per-spawn `[HeroRace][CrowdSpawn]`
debug logging records the race + resolved action set so the fix is verifiable in-game (strip the
verification logging after sign-off).

## Performance

The Prefix runs once per agent skin-mesh build (per spawn, not per frame). Its first statement
`if (__0) return;` exits immediately for main/battle agents (the bulk; they already pass
`true`), so they pay one bool check. Custom-race crowd agents pay one lock-free
`IRaceManager` cache lookup + one `HashSet.Contains`. The service is lazy-cached
(`??=` singleton); no per-call allocation. `/deep-review` efficiency agent: no issues.

## GitHub Issue

TODO — open via `/issue` (Erebor-arena dwarf face static-morph CTD + naked spectators). Distinct
from #277 (tournament cavalry dwarf-in-horse clipping).

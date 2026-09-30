# Adopting knowledge from yotthani's `bannerlord` repository: DualWield and FaceLearner (2026-09-30)

Written for: TAOM maintainers deciding what to take from yotthani's private monorepo.
Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)). Mike's scope for
this pass: take the knowledge from DualWield and FaceLearner, leave the rest for now, and implement nothing from the
repository in TAOM yet.

## Source, and what was read

`github.com/yotthani/bannerlord`, shared with Mike by its author, cloned read-only into the session scratchpad at
commit `8e040ab`. It holds the `HoN/` solution (DualWield and its add-ons, Armory, FieldCamp, Refuge, SupplyLines,
RingSystem, TAOM_UI, TAOM_RacePortraits and about 30 more projects), `bn faces/` (FaceLearner, FaceLearner.ML,
FaceLearner.HeadExtract, FaceLearner.AtlasTool), `LOTRAOM_FactionMap`, `TAOM_POI`, `KoMModpack`, `CustomLobbyMod`,
`BannerlordThemeSwitcher` and `docs/`.

| Part | Read |
|---|---|
| `HoN/DualWield` | README, `docs/plan-offhand-root-set.md`, `tools/README.md`, `AnimMirror/README.md`; every file in `Core/` and `Patches/`; the comment blocks of `DualWieldMissionBehavior.cs` (2,420 lines); every tool's docstring |
| `bn faces` | `CLAUDE.md`; `docs/phenotype-races/` (recipe, Blender pipeline, Mode 2 plan, 2026-07-03 status); `FaceLearner/Core` `GroomPoCPatch.cs`, `GroomConform.cs`, `GroomRuntimeBuild.cs`, `MeshLockWriter.cs`, `HeadVariantResolver.cs`, `HeadVariants.cs`, `RaceCodePatch.cs`; the 644 lines of notes in `FaceLearner.HeadExtract/Program.cs` |
| The rest | Surveyed on 2026-09-30 by four read-only agents (Armory; gameplay modules; UI and map; animation, faces and lore) and parked at Mike's word |

## Security

- **Safe to learn from:** yes. Nothing was built or run.
- **What was checked:** `DataSharing` (uploads to a gist, Discord or ix.io) has no callers; `FaceLearner.ML` downloads
  ONNX models (Hugging Face, archive.org, GitHub) only when run, and the models' own trust needs a separate pass if it
  is ever run; the Claude config auditor's HIGH and MEDIUM hits over the clone were false positives; the
  `.claude/settings.local.json` files in it hold allow-lists only.
- **Licensing:** no licence file anywhere in the repository, so its terms are `UNKNOWN`. Nothing was copied: facts
  only, restated in TAOM's words. Register row: "Yotthani `bannerlord` repository (DualWield, FaceLearner)",
  `comparison-only`, `uncleared` until yotthani states terms.

## Where the knowledge went

| Destination | What |
|---|---|
| [scripted-melee-strikes.md](../reference/scripted-melee-strikes.md) (new) | Handing a made-up collision to `Mission.MeleeHitCallback` for vanilla damage; the inputs that collapse damage when wrong; limb rays, flail reach, scene contact and the collision window; impact sounds, blocked reactions; playing your own strike against vanilla's channels and the AI; off-hand items, usage-set roots, the AI's shield rule and banners; cost at scale; DualWield's measuring kit; what it would mean for `CustomAttacksUtils` |
| [head-mesh-and-groom-authoring.md](../reference/head-mesh-and-groom-authoring.md) (new) | The 63 deform keys and the 101 channels; setting a face from code; FaceLearner's head package without the Kit and its traps; the neck-seam causes; the head material and texture rules; eye sockets; runtime groom on a baked head; head variants |
| [bannerlord-animation-system-map.md](../reference/bannerlord-animation-system-map.md) section 7 | Correction: an unknown action set id returns set 0, not an invalid set (below) |
| [race-face-and-hand-morphs.md](../reference/race-face-and-hand-morphs.md) | The key to channel map, now verified; channels 64 to 100 as yotthani's facial animation finding; the open question narrowed |
| [lessons/animation-skeleton.md](lessons/animation-skeleton.md) | Three lessons: the managed validity check, measuring the consumed value, a cut listing is not absence |
| [provenance-register.md](../reference/provenance-register.md) | The new row and its detail section |

## Checked against TAOM and the v1.5.3 engine

Every API name the two new pages cite was found in the v1.5.3 decompile cache or `TaleWorlds.Engine.dll` on
2026-09-30 (`ManagedMeshEditOperations` is in the DLL but missing from the cache). What the check changed:

| # | Claim | Result |
|---|---|---|
| 1 | Native `skins.xml` has 64 deform keys; engine keys 59, 61, 62, 63 (FaceLearner) | **Corrected.** 1.5.3 has 63 keys on time points 1 to 63; the engine keys are 59 `eyebump` (pinned 1) and 60 to 63 `weight`, `build`, `height`, `age`. 10 or 11 inverted keys per skin |
| 2 | `MBGlobals.GetActionSet` throws on a missing set, `MBActionSet.GetActionSet` returns an invalid one (DualWield, MithrilForge, and TAOM's own docs) | **Refuted by the decompile.** Both go through native `get_index_with_id` (0x6DF010) and the `base_set` search (0x58FAC0), which returns 0 on a miss after logging `could not be found, using default action set!`. Index 0 is valid. DualWield's own in-game observation (a missing `as_elf_warrior_dw` answered with `as_human_warrior`) agrees with the decompile, not with its comment |
| 3 | The elf plays `as_human_warrior` | **Confirmed**, `LOTRLOME_Armory/ModuleData/monsters.xml` |
| 4 | `ActionIndexCache.Create` does no caching | **Confirmed**: each call asks `MBAnimation.GetActionCodeWithName` |
| 5 | `AIParryOnAttackAbility` holds the AI level | **Confirmed**: `SetAiRelatedProperties` stores the clamped level there; DualWield's skill and difficulty factors are not re-derived |
| 6 | Friendly-fire stun 0.4 s; flail windows 0.60 to 0.88 against `1h_up` 0.38 to 0.50 | **Confirmed**, `managed_core_parameters.xml`, `combat_parameters.xml`, `Mission.cs:5382` |
| 7 | `EnableScriptDrivenPostIntegrateCallback` is one-way | **Confirmed** for managed code: `Skeleton` has no disable |
| 8 | Everything tagged [yotthani] on the two pages | Not reproduced by TAOM; measured by yotthani in game or on packages |

## Findings for Mike (not acted on)

1. **Eleven TAOM files carry the refuted "throws on a miss" claim** (claim 2). Live:
   `docs/reference/lotrlome-animalia-changes.md:71` and the comment in
   `docs/reference/lotrlome-armory-snapshot/action_sets.xml:66393` (the same comment is in the live Armory's
   `action_sets.xml`). History, to stay as written: `docs/reviews/rca-war-ram-headbutt-2026-09-18.md:15`,
   `docs/reviews/rca-troll-bandits-2026-09-28.md:19`, `docs/reviews/rca-prone-character-tableau-2026-07-31.md:157`
   (Codex finding F5), `docs/reviews/REVIEW-LOG.md:1576`, the handwritten changelog archive and four raw review logs.
   The `_map` sets stay required either way: a missing one would hand the map set 0, a human set on a creature
   skeleton, whose effect nobody has tested.
2. **Four creature behaviours detect a missing action set with `!set.IsValid`**, which never fires:
   `AnimaliaMissionBehavior.cs:66`, `ElkMissionBehavior.cs:64`, `WarRamMissionBehavior.cs:73`,
   `TrollBruteForceMissionBehavior.cs:61`. A missing set falls through to the next check and logs "has no clip",
   the wrong diagnosis rather than silence. Comparing `set.GetName()` with the id is the fix; it wants an issue.
3. **One in-game check would close claim 2 for good**: look up a set id that does not exist from the dev console and
   print the name that comes back.
4. **Still parked from the survey**: the `LOTRAOM_FactionMap` provenance gap; the register's "external developer
   drop" (`Features_fixed`) row, whose seven feature names all appear under `HoN/` and so are most likely yotthani's
   work; `TAOM_UI` overriding TAOM's nameplate prefabs; `Armory_TAOM` patching six TAOM types by name; and the
   missing licence file, which one line from yotthani would settle.
5. **Tooling**: `pwsh tools/taom-src.ps1` finds nothing for `ManagedMeshEditOperations` or `Mission`, although both
   are in the installed DLLs; its namespace probe misses and the fallback scan errors.

## Recommendation

Nothing to build now. When creature combat is next in scope, the scripted-melee page's section 9 is the starting
point: routing `CustomAttacksUtils` through `MeleeHitCallback` is the large win (armour, shields, skill and swing
speed would count) and wants its own issue, `/research` on the internal method, and a `/verify-bindings` entry. The
face pages are useful as they stand for any custom head whose neck shows a line or whose tools touch face channels.

# Modding Kit editor assert: `rglConcurrentQueue` overflow — the 131,072-entity prefab load budget

**Date:** 2026-07-24 · **Status:** root-caused, split applied, in-editor verification pending
**Symptom:** editor startup assert dialog `c:\buildagent\work\mb3\source\engine\rgl\rglConcurrentQueue.h:882 — Expression: chunk_index < max_chunk_count` (companion form `:969 — global_index + 1 <= chunk_size * max_chunk_count`), reproducible every launch; clicking **Ignore** leads to a permanent loading-screen hang; clicking **Abort** to a crash.

> The prefab XMLs live in the LIVE module folder `E:\Steam\...\Modules\TAOM_Map\Prefabs\` — **not in git** — so this doc is the durable record of both the mechanism and the remediation.

## Root cause

At editor startup every `<game_entity>` in every prefab XML across all loaded modules' `Prefabs\` folders is enqueued for **parallel deserialization** into a native chunked MPMC queue with a **hard-compiled capacity of 131,072 items** (`chunk_size 4096 × max_chunk_count 32`). `TAOM_Map\Prefabs\` had grown to **132,378 entities** (80.5 MB of XML) after four imported packs (~40.7K entities) were batch-added on 7/24 — the backlog crossed the cap.

Evidence chain (wEditor `TaleWorlds.Native.dll`, build v1.4.7.117377, disassembled with `tools/native_crash_triage.py --dll <wEditor dll>`):

| RVA | Function | Identification |
|-----|----------|----------------|
| `0x7708F0` (+0x36) | queue push/reserve | references BOTH assert strings + `rglConcurrentQueue.h`; capacity check `cmp eax, 0x20000` (131,072); chunk check `shr ebx,12; cmp ebx,0x20` (4096 × 32) |
| `0x764B60` | prefab entity deserializer | sole caller of the above; strings `entity_element`, `prefab_init_data`, `rglEntity_reader_writer.cpp` |
| `0x7677A0` | recursive `<children>` reader | strings `children`, `game_entity`, `new_child->get_scene() == entity_->get_scene()` |
| `0x9D920` | parallel-for worker | strings `semaphore_`, `Tasks/rglParallel_for_task.h` |
| `0xF5E7B5` | post-abort secondary crash site | null-global deref, no assert strings — this is what the 7/15 Event-Log APPCRASHes (bucket 1784240035966946980) recorded, NOT the root site |

The assert can fire **before engine logging initializes** (observed: 0-byte `watchdog_log`, no `rgl_log` at all for the 6:30 AM session) — which is why no historical log contained the assert text. The first successful capture is archived at `E:\Bannerlord_Backups\rgl-assert-2026-07\` (`rgl_log_61564.txt` holds the full assert stacks; the errors log additionally shows a `CONTENT WARNING: Unable to find material for mesh cube.001` storm from Blender-default meshes in the imported packs, which plausibly accelerates overflow by stalling queue consumers).

## Remediation applied (2026-07-24)

**User-directed design: classify every top-level prefab by real scene usage, split Used/Unused, keep only Used live.**

- **Classification:** union of `references.txt` `prefab` records + `scene.xscene` `prefab="…"` attributes across all scenes in TAOM_Map (38), LOTRLOME_Armory (10), A Dance of Dragons - Map (2), plus transitive prefab-in-prefab closure. Result: **1,020 used top-level occurrences (40,319 entities) / 524 unused (92,059 entities)**.
- **Guard checks:** (1) all 518 unique unused names grepped against `Main/` + `TAOM_Map\ModuleData\` — zero code/data spawns (the one hit, `hobbiton`, is an unrelated `LandmarkDef` id); (2) name collisions vs Native/SandBox/SandBoxCore/CustomBattle/StoryMode/Multiplayer/LOTRLOME_Armory prefabs — exactly one: `icon_camera` in `Soisson_Prefabs_2.xml` shadows `Native\editor_helpers_&_tests.xml` (it had classified "used" spuriously via the vanilla name; forced to Unused, flagged rename-before-re-enable).
- **Split mechanics:** 118 fully-used files untouched · 60 fully-unused files moved wholesale · 12 mixed files node-surgically split (XmlDocument, `PreserveWhitespace`). `Dale_kiosque_a` in-file duplicate renamed `__dup2`. Verified: zero parse failures, used/unused name sets exactly partition, live entity recount matches prediction.
- **User review pass (same day):** `*kitbash*` files are working palettes — **standing decision: kitbash libraries stay live even when scene-unused**; their parked nodes (erebor 79, gondor 93, mordor 1) were merged back. Final state: live `Prefabs\` = 130 files / **41,355 entities (~32% of cap)**; parked `Prefabs_Unused\` = 69 files / **347 prefabs, 91,023 entities** + **`_INVENTORY.md`** (per-file checkbox review table).
- **Backups:** full pre-split folder + SHA256 manifest at `E:\Bannerlord_Backups\TAOM_Map_Prefabs_2026-07-24\`; stale `.bak`/`.bak2` clutter moved to its `_bak_clutter\`.
- **Budget gate:** `python tools/check_prefab_budget.py` — warns > 120,000 entities, errors ≥ 131,072 (constant EXACT from disassembly).

## Re-enable workflow (for the Unused review)

Move a file (or individual top-level `<game_entity>` nodes) from `Prefabs_Unused\` back into `Prefabs\`, keeping the budget check green. Before re-enabling the four imported packs: fix the `cube.001`-family missing materials and rename `icon_camera`.

## Open items

- [ ] In-editor verification: two consecutive clean launches + Lossarnach town scene opens (`X_Lossarnach` is scene-used and stayed live).
- [ ] User review of `Prefabs_Unused\_INVENTORY.md` keeps.
- [ ] Consider version control for `TAOM_Map\Prefabs\` — the absence of history cost this investigation its cheapest evidence.

## Lessons

Distilled to `docs/reviews/lessons/data-content-cultures.md` ("prefab folders have a hard entity budget") and the assert-dialog addendum in `.claude/skills/native-crash-triage/SKILL.md` Phase 1.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/modding/module-map.md](../modding/module-map.md)

<!-- backlinks-end -->

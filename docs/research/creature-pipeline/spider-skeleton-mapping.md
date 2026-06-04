# Spider Skeleton Mapping — new rig (59) ↔ in-game (62)  *(loop iter 6)*

## The two skeletons
| | Resource name | Bones | Case | Source |
|---|---|---|---|---|
| **In-game (compiled)** | `spider_skeleton` | **62** | lowercase (`root_m`, `joint40_r`) | `LOTRLOME_Armory/Assets/creature/spider/meshes/sk_spider_forest_c_geo.tpac` — Usage=`horse`, 62 bodies + 61 D6 constraints (ragdoll already populated) |
| **New `.blend`** | `sp_skeleton` | **59** | mixed-case (`Root_M`, `joint40_R`) | `E:\LOTRAOMAssets\ErkamSpider (1).blend` |

Engine lowercases bone names on import, so case differences resolve (`Root_M` → `root_m`).

## Mapping result (evidence: `tpac_skeleton_dump` + set-diff, single python invocation)
- The new **59 bones are a STRICT SUBSET** (case-insensitive) of the in-game 62.
- **NEW-RIG-ONLY:** none.
- **IN-GAME-ONLY (3 bones absent from the new rig):** `joint16_m`, `joint21_l`, `joint21_r` — minor tip bones (stinger tip + two leg/pedipalp tips).
- common: 59.

## Integration implication — favors **Strategy B (retarget onto the existing skeleton)**
Because the new rig is a clean subset, retargeting the new animations onto the in-game 62-bone `spider_skeleton` is **1:1 by lowercased name**; the 3 extra bones (`joint16_m`, `joint21_l/r`) simply stay at rest pose. **No skeleton/mesh re-import, no `monsters.xml` 30+ bone-ref rewrite, no fang-index re-derivation.** This de-risks integration substantially vs Strategy A (adopt-new-rig-wholesale). Recommend Strategy B unless in-game testing shows the new mesh/skeleton is materially better.

## ⚠️ FINDING — skeleton NAME mismatch (verify in-game → MORNING QUEUE)
`action_sets_spider.xml` (line ~13) binds `skeleton="erkamspider_skeleton"`, but the **compiled skeleton resource is named `spider_skeleton`** (confirmed by `tpac_skeleton_scan`). If the engine matches the action_set's `skeleton` attribute against the loaded resource name, this mismatch would prevent the `as_spider` animations from binding — a **plausible root cause for the spider animations not playing in-game** (the feature was disabled before in-game validation).
- **NEEDS (human):** confirm whether any tpac provides `erkamspider_skeleton`; if not, `action_sets_spider.xml` line ~13 likely should read `skeleton="spider_skeleton"`. Verify in a Custom Battle after the fix.
- **NOTE:** this is a *live Armory data* file — per loop guardrails the autonomous loop does NOT edit it; it is queued for human action.

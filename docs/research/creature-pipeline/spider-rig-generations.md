# Spider rig generations — which skeleton/mesh is which (R2.3, 2026-06-03)

There are **three** spider rig generations in the assets. Know which is which before integrating the new animations.

| Generation | Skeleton | Bones | Mesh | Where | Status |
|---|---|---|---|---|---|
| **In-game compiled** | `spider_skeleton` | **62** | `sk_spider_forest_c` | `LOTRLOME_Armory/…/meshes/sk_spider_forest_c_geo.tpac` + the compiled `an_dg_spi_*` anim tpacs | **LIVE**; action_set now binds `spider_skeleton`; all 20 clips valid (R2.2) |
| **New `.blend`** (today's delivery) | `sp_skeleton` | **59** (strict subset of the 62, lowercased names match) | `sk_spider_forest_bm_a1` (6 parts) | `E:\LOTRAOMAssets\ErkamSpider (1).blend` | source of the IMPROVED anims (in-place walks, genuine walk_right); NOT compiled |
| **`a_01` FBX** | (unnamed) | **46** | `sk_spider_forest_bm_a1` (6 parts) | `E:\LOTRAOMAssets\Creatures\Giant Spiders\Meshes Final\sk_spider_forest_a_01.fbx` | a third/older generation; 46 bones; NOT the in-game target |

## Consequence for the Strategy-B retarget (task 5)
To bake the new 59-bone clips onto the in-game 62-bone `spider_skeleton`, that 62-bone skeleton must be in Blender. **No 62-bone source FBX is on disk** (only the 46-bone `a_01.fbx` + the 59-bone `.blend`); the 62-bone target lives only inside `sk_spider_forest_c_geo.tpac`. Options:
- **(a)** Reconstruct the 62-bone target from `tools/tpac_skeleton_dump.py` (bone names + `parent_idx` + `rest_frame` 4×4). Feasible but **fiddly** — rest-frame space (parent-relative vs world) and roll conventions need care.
- **(b)** Re-export the 62-bone skeleton from the Modding Kit if its source project exists (human).
- **(c) RECOMMENDED (lowest effort):** since the 59 new bones are a lowercased subset of the 62, re-import the NEW rig into the Modding Kit and **name its skeleton `spider_skeleton`** — then the new clips compile directly against the existing action_set bindings, unchanged. The 3 missing bones (`joint16_m`, `joint21_l/r`) simply won't be skinned (they're tips — acceptable).

**Recommendation: (c).** It gets the new rig's improved clips in-game with the least work and keeps the (now-correct) action_set bindings intact. The full retarget (a) is only needed if the exact 62-bone in-game skeleton must be preserved verbatim.

## Two compiled SKELETON RESOURCES in the shipped module (found 2026-06-03)

Distinct from the three *rig generations* above, the `LOTRLOME_Armory` module ships **two separate spider skeleton resources** — easy to confuse because `erkamspider_skeleton` *contains* the substring `spider_skeleton`:

| Resource | Asset source | Location | Clip state |
|---|---|---|---|
| **`spider_skeleton`** | `sk_spider_forest_c.fbx` | `Assets/creature/spider/` (singular) | **full** — ~20 compiled `an_dg_spi_*` clips |
| **`erkamspider_skeleton`** | `AssetSources/creatures/spider/erkamspider.fbx` → `erkamspider_geo.tpac` | `Assets/creatures/spider/` (plural) | early — `attack` skeleton-anim + `spiderclip` Animation Clip |

⚠️ **Substring-match trap:** any scan that greps for `spider_skeleton` will also match inside `erkamspider_skeleton`. When verifying the skeleton name, extract the **full token** before `|`, never a substring (cf. `feedback_substring_keyword_matches_external_data`).

**DECISION (user, 2026-06-03): bind to `spider_skeleton`** — it already has the full compiled clip set, so the spider works without re-authoring. The round-2 `action_sets_spider.xml` edit (`skeleton="spider_skeleton"`) is correct; **do not** revert it to `erkamspider_skeleton`. New improved clips from `ErkamSpider (1).blend` are delivered as `spider_an_dg_spi_clips.fbx` (bare `an_dg_spi_*` takes) to import under Owner Skeleton `spider_skeleton`.

## Skeleton Animation vs Animation Clip (Modding-Kit resource model)
See `clip-binding-map.md` "Resource model" + `LESSONS-LEARNED.md` L11. Short version: **Skeleton Animation** = raw motion + Owner Skeleton (`<skel>|<bare-take>`); **Animation Clip** = gameplay resource pointing at a skeleton anim with duration/blend/hand-pose/sound/flags. The action_set binds the bare clip name; the `<skel>|` is the Owner-Skeleton prefix, not part of the FBX take.

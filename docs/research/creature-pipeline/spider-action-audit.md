# Spider Action Audit — `ErkamSpider (1).blend` (autonomous loop, iteration 1)

Source: `E:\LOTRAOMAssets\ErkamSpider (1).blend` (Blender 5.1.2), armature `sp_skeleton` (59 bones), 34 actions. Generated live via Blender MCP (read-only). Metrics per action:
- **kf** = total keyframes (413 fcurves × frames). **root_mag/root_z** = Root_M translation baked over the clip (engine-driven locomotion wants this ≈0). **loc_seam / rot_seam** = sum of |value(last)−value(first)| over location / rotation channels (0 = loops cleanly). **chk** = naive keyframe-coord checksum (for duplicate detection — equal chk + equal kf ⇒ near-identical motion).

## Key findings (evidence-based)

### 1. Root-motion vs in-place inconsistency (Track A — highest value)
`walk_left` (root_z **+2.362**) and `walk_right` (root_z **−2.362**) carry baked Root_M translation; every other locomotion clip (`walk_1/2`, `run_001.001`, `run_2`, idles, jump) is **in-place (root=0)**. Bannerlord drives creature translation from the engine → the two root-motion walks will most likely **foot-slide / over-travel** in-game. Fix: strip Root_M translation from `walk_left/right` to match the in-place convention, re-measure. (Confirm engine expectation against the bundled manual first.)
- Minor baked motion also in: `run_001` (0.13), `attack_top_001/002` (0.38), `attack_bottom`/`bottom_attack` (0.27), `turn_left/right` (0.095), `death_1` (1.51), `death_2` (1.58). Deaths sliding may be intentional; verify.

### 2. Heavy duplication — 34 actions collapse to ~12–15 canonical clips (Track A)
Near-identical clusters (equal kf + checksum within noise):
- **Forward walk:** `sp_walk_1_001` (206193) ≈ `sp_walk_2_001` (206174) — Δ19, kf 12390.
- **Short "charge/bottom" (kf 14455, fr 1-35):** `sp_attack_charge` (262254.6) ≈ `sp_attack_bottom` (262249.4) ≈ `sp_bottom_attack_001` (262255.8) ≈ `sp_charge_attack_001` (262229.3) — **four** near-identical.
- **Attack mid (kf 16520, fr 1-40):** `sp_attack_left_001` (340962.1) ≈ `sp_attack_top_001` (340957.1) ≈ `sp_attack_top_002` (340985.2) — **three** near-identical. ⚠️ a *"left"* clip matching *"top"* clips ⇒ names are unreliable.
- **Long charge (kf 53690, fr 2-131):** `sp_charge_001` (3577909) ≈ `sp_charge_002` (3577918) — Δ9.
- **Possible L/R mirror pair (kf 19824):** `sp_attack_left_002` (488465) vs `sp_attack_right_001` (487622).

### 3. Name ≠ content — verify before export (Track A, blocking)
`sp_attack_right_002` (chk 356853.8, kf 16520, fr 2-41) collides with `sp_walk_right_001` (chk 356863, kf 16520, fr 2-41). Same as the old compiled `walk_right = turn_right.001` mislabel. **Action names cannot be trusted**; each clip's actual motion must be verified (render/scrub) before it is exported or bound. This is a process rule for every creature, not just the spider.

### 4. Loop-seam outliers (Track A)
`sp_walk_right_001` rot_seam **13.35** vs `sp_walk_left_001` 2.46 — mirror artifact, fix and re-measure. `sp_hit_right_001` rot_seam 6.68 (vs hit_left/front 0); `sp_attack_right_001/002` ~10 — but hits/attacks don't loop, lower priority. `sp_turn_right` 32.3 vs `turn_left` 16.6 (turns are transitions, not loops — asymmetry noted).

## Full table (34 actions)
| action | frames | kf | root_mag | root_z | loc_seam | rot_seam | chk |
|---|---|---|---|---|---|---|---|
| sp_attack_bottom | 1-35 | 14455 | 0.271 | 0 | 0.29 | 2.79 | 262249.4 |
| sp_attack_charge | 1-35 | 14455 | 0 | 0 | 0 | 0.83 | 262254.6 |
| sp_attack_front | 2-47 | 18998 | 0 | 0 | 0 | 0 | 468151.6 |
| sp_attack_front_001 | 1-46 | 18998 | 0 | 0 | 0 | 0 | 449153.6 |
| sp_attack_left_001 | 1-40 | 16520 | 0 | 0 | 0 | 0 | 340962.1 |
| sp_attack_left_002 | 1-48 | 19824 | 0 | 0 | 0 | 0 | 488464.8 |
| sp_attack_right_001 | 1-48 | 19824 | 0 | 0 | 0 | 10.26 | 487622.2 |
| sp_attack_right_002 | 2-41 | 16520 | 0 | 0 | 0 | 9.99 | 356853.8 |
| sp_attack_right_02_002 | 2-49 | 19824 | 0 | 0 | 0 | 0 | 507669.9 |
| sp_attack_top_001 | 1-40 | 16520 | 0.382 | 0 | 0.53 | 3.09 | 340957.1 |
| sp_attack_top_002 | 1-40 | 16520 | 0.382 | 0 | 0.53 | 3.09 | 340985.2 |
| sp_attack_top_003 | 2-52 | 21063 | 0 | 0 | 0 | 0 | 571668.1 |
| sp_bottom_attack_001 | 1-35 | 14455 | 0.271 | 0 | 0.29 | 2.77 | 262255.8 |
| sp_charge_001 | 2-131 | 53690 | 0 | 0 | 0 | 0.54 | 3577909.1 |
| sp_charge_002 | 2-131 | 53690 | 0 | 0 | 0 | 0 | 3577917.9 |
| sp_charge_attack_001 | 1-35 | 14455 | 0 | 0 | 0 | 0.18 | 262229.3 |
| sp_death_1_001 | 2-61 | 24780 | 1.513 | 1.225 | 2.14 | 10.38 | 784144.8 |
| sp_death_2_001 | 2-126 | 51625 | 1.584 | 0 | 2.24 | 29.39 | 3311026.1 |
| sp_hit_back_001 | 2-38 | 15281 | 0 | 0 | 0 | 1.96 | 307782.2 |
| sp_hit_front_001 | 2-36 | 14455 | 0 | 0 | 0 | 0 | 276655.0 |
| sp_hit_left_001 | 2-34 | 13629 | 0 | 0 | 0 | 0 | 247251.6 |
| sp_hit_right_001 | 2-34 | 13629 | 0 | 0 | 0 | 6.68 | 246731.9 |
| sp_idle_1_001 | 2-44 | 17759 | 0 | 0 | 0 | 0 | 411007.3 |
| sp_idle_2_001 | 2-51 | 20650 | 0 | 0 | 0 | 0 | 550171.6 |
| sp_jump_1_001 | 2-46 | 18585 | 0 | 0 | 0 | 0.03 | 448677.7 |
| sp_run_001 | 1-22 | 9086 | 0.126 | 0 | 0.16 | 2.47 | 105766.6 |
| sp_run_001.001 | 2-23 | 9086 | 0 | 0 | 0 | 0 | 114852.6 |
| sp_run_2_001 | 2-13 | 4956 | 0 | 0 | 0 | 0 | 37870.7 |
| sp_turn_left_001 | 2-46 | 18585 | 0.095 | -0.002 | 0.1 | 16.56 | 448597.3 |
| sp_turn_right_001 | 2-46 | 18585 | 0.095 | 0.002 | 1.1 | 32.26 | 447885.5 |
| sp_walk_1_001 | 2-31 | 12390 | 0 | 0 | 0 | 0 | 206193.0 |
| sp_walk_2_001 | 2-31 | 12390 | 0 | 0 | 0 | 0 | 206174.0 |
| sp_walk_left_001 | 2-41 | 16520 | 2.362 | 2.362 | 2.37 | 2.46 | 357594.0 |
| sp_walk_right_001 | 2-41 | 16520 | 2.362 | -2.362 | 2.85 | 13.35 | 356863.0 |

## Derived Track-A backlog (ordered)
1. **Verify name↔content** for every action (render/scrub each canonical candidate) — names are proven unreliable. Build the verified canonical clip list.
2. Confirm engine in-place vs root-motion expectation (bundled manual), then strip Root_M from `walk_left/right`; measure foot-slide delta.
3. Resolve the dup clusters → one canonical per intended action; document which take won and why.
4. Fix `walk_right` rotation loop-seam.
5. Curate the final set → export-prep with the Bannerlord FBX preset → morning compile queue.

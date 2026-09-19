# Adopting Yotthani's animation handoff and MithrilForge (2026-09-18)

Written for: TAOM maintainers deciding what to take from Yotthani's DualWield and MithrilForge work.
Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)).

## Sources, and what was actually read

| Source | What it is | Read |
|---|---|---|
| `Bannerlord_Animation_Handoff_EN.md` (Downloads; first read as the `.crdownload`) | Three months of DualWield mod work on Bannerlord 1.4.6: mirroring attack clips for the left hand without the Modding Kit, the clip's compressed segment format, left-hand combat, verification tooling, the MithrilForge tool chain, dead ends, working practices | In full, all 441 lines (28 KB). The finished download that replaced the `.crdownload` has the same size, line count and ending, so the review covered the whole document |
| `MithrilForge-main` | C# (net8.0) command-line tool: a GLB file in, a static prop `.tpac` out, no Modding Kit. MIT, (c) yotthani. Its README lists TAOM_POI as a consumer | README, `docs/donor-findings.md`, `docs/delivery.md` in full; the plan's structure; build files and code for the security pass |

**The repo snapshot is not the one the handoff describes.** The handoff's animation tool chain
(`src/MithrilForge.Anim`, `tools/anim/`, `docs/anim-findings.md`, `examples/dualwield-recipe.json`) is not in it,
and `vendor/TpacTool` (the patched TpacTool fork every writer depends on) is an empty submodule pointing at a
private repository. Every animation-tooling claim below is therefore the handoff's, not something read in code.

## Security

- **Safe to learn from:** yes.
- **Safe to run:** not needed, and not buildable here without the private fork.
- **What was checked:** no process launch, network, registry, dynamic assembly load, install script or package
  lifecycle hook. The MSBuild files set one configuration property. The code reads one environment variable (the
  game path). It deletes one file: its own output, when its verifier rejects the package it just wrote.
- **Licensing:** MIT. ImageSharp is pinned to 2.1.11 on purpose, the last Apache-2.0 line.

## Checked against TAOM

Each claim was compared with TAOM's docs, tools and the installed 1.5.3 engine before it went anywhere.

| # | Handoff / MithrilForge claim | TAOM status | Evidence (2026-09-18) |
|---|---|---|---|
| 1 | An `AnimationClip` can carry its own motion segment (type `6c1e136f`); clip field `UnknownUInt2` 0 plays the named `SkeletalAnimation`, 2 plays the clip's own segment | **New to TAOM** (no doc or tool mentioned it) | TpacTool.Lib 0.4.0 reads the field. Census of TAOM's 243 creature clips: 235 at 0 with no data segment; the 8 elephant attack clips (4 elephant, 4 rider, ADOD_Beasts-derived) at 2 with no segment |
| 2 | Animations cannot be overridden by re-shipping a GUID | New | Handoff, citing TaleWorlds |
| 3 | A new `SkeletalAnimation` in a mod package does not register (index -1) | **Contradicted as a general rule** | TAOM's Kit-imported masters register and play (troll, ram, warg, elephant). Holds for TpacTool-written masters only |
| 4 | Native hit timing is `collision_check_starting/ending_percent` in `Native/ModuleData/combat_parameters.xml`, per the clip's `CombatParameterId` | New | 165 entries in the 1.5.3 file, 130 with an explicit window |
| 5 | An action naming a missing clip crashes at agent spawn | **Partly gated** | `verify_mount_assets.py` "PHANTOM BINDING" covers spider, elephant, mumakil; `gen_troll_anim_clips.ps1 -Verify` covers the troll; ram, warg and chariot are not covered |
| 6 | `Mission.RayCastForClosestAgentsLimbs` queries the engine's limb capsules | New | Public on the installed 1.5.3 DLL, signature as quoted |
| 7 | Scripted strikes can reach vanilla damage (armour, blow, reactions) through `Mission.MeleeHitCallback` by reflection | New; TAOM's creature attacks bypass armour | Reflection into an internal method; not verified here |
| 8 | Never read a direction off gameplay footage; check orthographically in Blender | Partly practised, never written down | |
| 9 | Measurement traps: medians swallow spikes; a verified file is not a played file; a reference from your own pipeline is not independent; metrics reject, never confirm | Two of four were already TAOM lessons (independent TpacTool read-back; "prove the running process loaded the change") | |
| 10 | A per-frame pose recorder in the mod is the ground truth for "does the engine play our clip" | **New** | TAOM's lesson "a landed blow never proves its animation played" has no tool behind it |
| 11 | Upstream TpacTool corrupts meshes on save (+56-byte channel prefix, 32-bit index criterion) and cannot write Material metadata | New as a warning | TAOM writes meshes with `tpac_clone_metamesh.py`; TpacTool `Save` only touches clip and master packages |
| 12 | Hand-written packages in a module's `AssetPackages/` load with no Kit save (props and heads, 1.4.6, in game) | **Untested by TAOM** | TAOM's #616 rule is about the loose `Assets/` tree needing a Kit-cooked `.rdc`; `AssetPackages/` is a different load path |
| 13 | A zero checksum is fine | Agrees | The #616 addendum census |
| 14 | Kit FBX import: primary bone axis X, secondary Y | **Conflicts** | TAOM's engine-frame rigs are measured working with its own settings (`bannerlord-skeleton-authoring.md`); the difference is rig convention. Not adopted |
| 15 | The Kit is stable only with native modules loaded, no Harmony mod | Not re-checked | |
| 16 | Clip segment codec, mirroring math, left-hand combat, dual wield | Out of TAOM's scope today | TAOM authors clips in the Kit and has no dual wield |
| 17 | Donor clone, enumerate the donor's channels, round-trip every write, a verifier that deletes bad output | Already TAOM practice | `tpac_clone_metamesh.py`, the tools README byte round-trip rule, `skeleton_hit_capsules.py` refusing and reading back |
| 18 | Working practices: one variable per test, bundle changes, the user's eye is the oracle, restate after two failures | Duplicative | TAOM lessons, `/investigate`, `/build-fix` retry budget, the live-session memory |

## Recommendation

**Tier 1, adopted now as knowledge (docs only, each attributed to this report):**
- Claims 1, 2, 3 (scoped) and 4 → `docs/reference/bannerlord-animation-clip-flags.md` "A clip can carry its own
  motion".
- Claim 6 → `docs/reference/bannerlord-skeleton-authoring.md` "Hit capsules", testing them in a mission.
- Claim 11 → `tools/README.md`, creature tpac surgery.
- Claim 12 → `docs/reference/ue-to-bannerlord-asset-pipeline.md` as a lead with a test, never as a rule.
- Claims 8 and 9 → `docs/ai-includes/creature-animation-blender-mcp-workflow.md` lessons.
- The 8 elephant attack clips → `docs/features/elephant.md` Open items: watch whether they animate; if not, try the field
  at 0 as an experiment with a backup (the rule is the handoff's, unmeasured by TAOM).

**Tier 1, proposals that need code (each wants an issue and TDD):**
1. A pose recorder dev-console command: sample an agent's bone frames every tick while an action plays, write
   CSV. It answers "did the ram lower its head" from a log instead of Mike's eye in a crowded battle.
2. The `AssetPackages/` load test (claim 12): one new, uniquely named package, no Kit save, Custom Battle. If it
   loads on 1.5.3, hand-built clones stop needing a Kit save.

**Tier 2:**
- Limb raycasts for scripted creature strikes (warg, spider, elephant, ram), replacing distance to bone origins.
- Vanilla damage for scripted strikes through `MeleeHitCallback` (armour would count). Reflection into an internal
  method breaks on engine bumps; `/research` first.
- Register the ram, warg and chariot in `verify_mount_assets.py` so a missing clip is caught for them too (claim 5).
- MithrilForge for TAOM static props (POIs, camp props). It already targets TAOM_POI; it needs the private fork.

**Skip:** the segment codec and mirroring pipeline (not in the snapshot; TAOM authors clips in the Kit), the
dual-wield combat code, the Kit axis settings (claim 14), the working practices (claim 18).

## What the census changed about the war ram

The ram's head-butt clip is at 0 with no segment of its own, so re-pointing it at the new hold master with
TpacTool did reach the game. That rules out one explanation for "I don't see the ram lower its head"; the clip
flags and the Kit re-cook remain the live ones (`docs/features/war-ram.md`).

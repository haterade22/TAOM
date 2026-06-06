# War Elephant (Harad rideable mount)

> **Status: C# BUILT + WIRED (2026-06-05); data + animations are human seams.** The trample/mount-lock C# is a
> 1-for-1 behavioral port of ADOD's elephant, adapted to **v1.4.5**, fully wired (IoC + SubModule) and green
> (3041 tests). The Monster/Item data + the 1.4.5 action-set + the animation clips remain a Modding-Kit/Blender
> seam (see "Implementation" + "Remaining seams"). Supersedes the paused [Giant Spider](spider.md).
>
> **Scope — this is a *standard* war elephant, NOT the giant mumakil / Oliphaunt.** A normal-scale ridden mount
> (one Harad crewman rides it). TAOM already represents the mumakil separately (the existing `mumak_rider` troop
> archetype + "Mumakil War Tower" framing in the Harad culture); this feature does not touch those.
>
> **⚠️ ADOD_Beasts is built for Bannerlord ~1.2.12, NOT 1.4.5.** So ADOD is a *behavioral reference only* — its
> runtime DLL + its `action_sets.xml`/`monster_usage_sets.xml` (1.2.12 schema) must **not** be depended on at
> runtime. The C# was verified call-by-call against v1.4.5 (the one drift — `ActionIndexCache.Name` → `GetName()`
> — was caught by the compiler and fixed); the data is authored fresh for 1.4.5.

## Implementation — C# built + wired (2026-06-05)

The trample + mount-lock are **done, wired, and green** (build + 3041 tests). This is a 1-for-1 behavioral port of
ADOD's `ADODBeastsElephantAgentComponent.OnTickAsAI` + `ADODAgentStatCalculateModel`, re-implemented in TAOM-clean
architecture on the v1.4.5 API.

| File | Role |
|------|------|
| [`Main/Features/Elephant/ElephantConfig.cs`](../../Main/Features/Elephant/ElephantConfig.cs) | Constants — `ElephantMonsterId="taom_war_elephant"`, `MountDifficulty=999`, trample gates (range 3, dot 0.25, chance 0.001/tick, radius 2) + base damage 10 — all 1-for-1 ADOD. |
| [`Main/Features/Elephant/IElephantAttackService.cs`](../../Main/Features/Elephant/IElephantAttackService.cs) + [`ElephantAttackService.cs`](../../Main/Features/Elephant/ElephantAttackService.cs) | **Pure** logic (no TaleWorlds deps): `IsElephantMonster`, `ShouldAiTrample(dist,dot,roll,attacking)`, `ComputeInflictedDamage(blocking)` = `round(10*(blocking?0.25:1))*2`. Unit-tested. |
| [`Main/Features/Elephant/ElephantMissionBehavior.cs`](../../Main/Features/Elephant/ElephantMissionBehavior.cs) | Boundary `MissionLogic`: tracks elephant agents (OnAgentBuild + first-tick scan, fail-soft), and per tick, for each **AI-ridden** elephant whose rider has a target in range while facing it, plays an attack action + deals a **radial knockdown** (`CustomAttacksUtils.TakeDamage`, the TAOM-clean equivalent of ADOD's `RegisterBlow`) to enemies within 2 m of the target. |
| [`…/CareerSystem/Models/TaomAgentStatCalculateModel.cs`](../../Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs) | EDITED — the shared `AgentStatCalculateModel` slot now also carries the **mount-lock**: `CanAgentRideMount`→false for the elephant + `MountDifficulty=999` (both via the injected `IElephantAttackService`, applied with ternaries per gamemodels.md rule 4). |
| `ElephantIoC.cs`, `IoC.cs`, `SubModule.cs` | Service registered (Singleton); `ElephantMissionBehavior` added to the mission list; the stat-model ctor takes the elephant service. |
| [`TAOM.Tests/Features/Elephant/ElephantAttackServiceTests.cs`](../../TAOM.Tests/Features/Elephant/ElephantAttackServiceTests.cs) | 11 tests (IsElephantMonster ×3, ShouldAiTrample gate-exhaustion ×6, ComputeInflictedDamage ×2). |

**1.4.5 adaptations vs ADOD's 1.2.12 decompile:** `ActionIndexCache.GetName()` (not `.Name`); the 2-arg
`SetActionChannel(0, anim)` (ADOD's long arg list is exactly the engine defaults); `CustomAttacksUtils.TakeDamage`
(TAOM's clean damage primitive, with a `knockDown` flag that maps to ADOD's knockdown BlowFlag) instead of a raw
`Blow`/`RegisterBlow`. Behaviorally identical; no native code (ADOD's `NativeHook.dll` is dead anyway).

**Graceful degradation (important):** the attack action codes are `act_war_elephant_attack_1..3`, defined in the
(not-yet-authored) 1.4.5 action-set. If they're absent they resolve to `act_none` — the attack **animation** is
skipped but the **trample damage + knockdown still land**. So the gameplay works before the animations exist.

### Remaining seams (need the Modding Kit / Blender / you)

1. **Monster + Item data** — **ready-to-drop files authored**: [`elephant/lotr_monster_elephant.xml`](elephant/lotr_monster_elephant.xml)
   (copy to `LOTRLOME_Armory/ModuleData/Monsters/LOTR/` + register in its SubModule.xml) and
   [`elephant/horse_item_taom_war_elephant.xml`](elephant/horse_item_taom_war_elephant.xml) (paste the `<Item>` into
   `LOTRAOM_horses.xml`). Both validated well-formed; the Monster id stays `taom_war_elephant` to match `ElephantConfig`.
   (Corrected one ADOD 1.2.12-ism on the way: `bones_to_modify_on_sloping_ground_0` was `Spine2` — a bone our rig
   doesn't have — fixed to ` Spine_04`.)
2. **1.4.5 action-set + clips** — author `as_war_elephant` against the **vanilla 1.4.5** quadruped-mount schema
   (NOT ADOD's 1.2.12 `as_elephant`), binding TAOM-compiled clips. This is the gating dependency for animation.
3. **Animations** — **idle + walk authored** (2026-06-06, in Blender via MCP — see below); remaining clips
   (run/turn/rear/death + the 3 attack clips) + the Modding-Kit compile of all clips are the next step.
4. **Harad rider troop + recruitment**, and an in-game smoke test.

### Authored animations — first pass (2026-06-06, Blender via MCP)

Authored two in-place, looping elephant clips directly on our `elephant_skeleton` rig via the Blender MCP
(procedural keyframing, no Cascadeur/visual-iteration loop — these are a **rough first pass to refine**, NOT
production-grade; validated via side-view renders that the rig deforms correctly + the gait alternates):

| Clip | Frames | Motion |
|------|--------|--------|
| `an_war_elephant_idle` | 1–49 (loop) | breathing (spine pitch) + head sway + a phase-offset wave down the 29-segment trunk + ear flick + tail sway; legs static |
| `an_war_elephant_walk` | 1–25 (loop) | 4-leg lateral-sequence gait (LH→LF→RH→RF, ±11° hip swing + slight knee lift) + subtle body bob + trunk/tail sway; **zero root translation** (engine supplies forward travel — `feedback_movement_anims_in_place_engine_driven`) |

- **Exported** (Modding-Kit-ready, verified: 1 Null + 60 LimbNodes + animation take, 0 meshes):
  `E:\LOTRAOMAssets\Elephant\clips\an_war_elephant_idle.fbx` + `an_war_elephant_walk.fbx`. Recipe:
  `object_types={ARMATURE}`, **`primary_bone_axis='Y'`** (lesson L13), `add_leaf_bones=False`,
  `bake_anim_use_nla_strips=True` (bare take names, lesson L11) — same axis as the body FBX so they bind.
- **Workspace saved** for refinement: `E:\LOTRAOMAssets\Elephant\elephant_anim_workspace.blend` (the 2 actions live on the rig).
- **Bone-axis reference for further authoring** (from the rig diff): forward=+Y, up=+Z; **local-Z rotation = fore/aft
  pitch** (leg swing, head nod, trunk/tail fore-aft) for nearly every bone (their local-Z ≈ world +X);
  **local-X = lateral sway**. +local-Z swings a leg forward. Legs: front = ` R/L UpperArm`→`Forearm`→`Hand`;
  back = ` R/L Thigh`→`Calf`→`Foot` (point −Z). The 29-seg trunk = ` Queue de cheval *`, the 7-seg tail = ` Tail*`.
- **Next:** author run/turn/rear/death + the 3 `act_war_elephant_attack_*` clips the same way (or refine these in
  Cascadeur), import all into the Modding Kit → `*_anm.tpac`, and bind them in the 1.4.5 `as_war_elephant` action-set.

### The real ADOD animation source — found, but unsliced (2026-06-06)

The production ADOD elephant clips exist as **source** at
`…/Modules/ADOD_Beasts/AssetSources/elephants/elephant_anims_all_{left,right,turn_left,turn_right}.fbx` (on
`elephant_skeleton` — all 52 non-leaf bones match our rig exactly). **But each is one ~1341-frame concatenated take
with no embedded clip boundaries** — no named sub-takes, no pose markers (ADOD defined the per-clip frame ranges
inside the Modding Kit project, which isn't shipped). A leg-motion-profile heuristic couldn't separate them cleanly
(a 672-frame low-activity block then dozens of 8–12 frame fragments). So the source **can't be auto-sliced** — it
must be sliced by scrubbing in the Modding Kit (how ADOD built it).

**The already-sliced real clips = the 31 compiled `elephant_*_anm.tpac`** in `…/ADOD_Beasts/Assets/elephants/animations/`
— skeleton-agnostic clip data that binds to `elephant_skeleton` (our rig). **Fastest path to the full 1-for-1 set:**
copy those tpacs into `LOTRLOME_Armory/Assets/creature/elephant/animations/` and reference them in `as_war_elephant`
(rename to `an_war_elephant_*` to avoid an ADOD id collision). Caveat: 1.2.12-era, but clip tpacs are skeleton-relative
keyframe data (no embedded skeleton — confirmed by the deep-dive), so they should bind on 1.4.5; verify in-game.

**Three animation paths — your call:** (a) **compiled tpacs** — real, full 31-clip set, fastest, slight 1.2.12 risk;
(b) **Kit-slice the source FBX** — fully re-authored/owned, but manual boundary definition; (c) **the hand-authored
idle+walk above** — fully owned + 1.4.5-clean, but only 2 rough clips. (a) is quickest to a working animated elephant;
(c) is the safest fully-owned starting point. The trample C# works regardless (codes degrade to `act_none`).

## Overview

A rideable **Harad war elephant** — a `Mountable=true` mount (ridden by a Harad crewman) that also **auto-attacks**
(tramples / gores with its tusks) on its own, modelled on TAOM's working **warg** mount. The asset we import is **our
own FBX** (`E:\LOTRAOMAssets\Elephant\Meshes BL\elephant_harad_armor_01.fbx`); its **rig + mesh + textures** match
ADOD's elephant (the asset was purchased from Artem, the ADOD author, for use in TAOM), while the **animations are
TAOM-authored on that FBX** — *not* reused from ADOD (project-owner decision, 2026-06-05). The data is re-authored
under TAOM ids and themed for Harad, held to TAOM standards (adapter pattern, tests, ADR compliance).

## Why this exists — and why it is tractable where the spider was not

The [Giant Spider](spider.md) was paused after an exhaustive 2026-06-05 investigation: spawned as a **non-mountable,
detached `FromHorseObj` agent** (`Mountable="false"`), it hard-crashes in native `Agent.PreloadForRendering` on its
62-bone skeleton, and the crash is **specific to the detached/non-mountable mount-render path** (every data-level fix
— mesh split, ≤4 influences, physics, skeleton integrity, shader cache, material binding — was refuted; see
[rca-spider-troop-2026-06-04.md](../reviews/rca-spider-troop-2026-06-04.md) §2026-06-05).

The elephant **sidesteps that wall entirely** because it is a **ridden mount** (`Mountable=true`), which uses the
fully-proven horse/warg machinery — no detached-creature hacks, no `FromHorseObj`-mismatch, no native-wield guards.
This is confirmed, not hoped: **ADOD ships the exact same 60-bone elephant rig as a working `Mountable=true` mount.**

### The asset facts (verified 2026-06-05)

| | TAOM's elephant FBX (`E:\LOTRAOMAssets\Elephant\...\elephant_harad_armor_01.fbx`) | ADOD's shipped elephant (`ADOD_Beasts/Assets/elephants/adod_elephant_geo.tpac`) |
|---|---|---|
| Skeleton | `elephant_skeleton` (renamed from `…_unused` + re-exported, 2026-06-05; verified 60 bones / 0 leaf) | `elephant_skeleton`, **60 bones**, `Usage='horse'`, 59 D6 ragdoll joints |
| Body mesh | `SK_Elephant_Armor_A` (+ .base/.legs/.nose/.tusk/.head/.platform/.pillow/.cloth/.belt/.feather parts) | `sk_elephant_armor_a` + `elephant_mesh` |
| Rider bone | `" Spine1_05"` (leading space) | `rider_sit_bone=" Spine1_05"` |
| Animations | none shipped (mesh+rig only) | `elephant_anims_all` (bundled on the skeleton) |
| Textures | `t_creature_elephant_a1/a2` + 4 armor sets (d/n/s) | (same family) |

**Conclusion: it is the same rig.** So the bone count (60) is a **non-issue** — it works as a ridden mount. We adopt
ADOD's **rig + mesh + textures** (which are our FBX) and the proven `Mountable=true` mount recipe, but **TAOM authors
its own elephant animations on our FBX** (the creature-animation pipeline) — *not* a reuse of ADOD's `elephant_anims_all`
(project-owner decision, 2026-06-05). So it is a **rig/mesh adopt + custom-animation build**, with the proven mount recipe.

#### Verified bone roster (`tpac_skeleton_dump.py` on `adod_elephant_geo.tpac`, 2026-06-05)

`elephant_skeleton` — **60 bones, `Usage='horse'`, 59 D6 ragdoll joints**. The body chain (leading-space + `_NN`
Blender-export names): ` Pelvis_03` (root, parent −1) → ` Spine_04` → ` Spine1_05` (the rider-sit bone) → ` Neck_06`
→ ` Neck1_07` → ` Head_08`. Hanging off ` Head_08`: the **ear chains** (`ear_L_1_023`/`ear_L_2_024`,
`ear_R_1_025`/`ear_R_2_026`) and a long **trunk chain** rigged as ` Queue de cheval *` (French "ponytail" — ~19
segments, the deformable trunk). The body bodies carry TaleWorlds `body_type`s (`abdomen`/`chest`/`neck`) + masses
(pelvis 13.4, spine ~32.7). **This is the roster TAOM's imported `elephant_skeleton` must match bone-for-bone**
(open item #2) before the action-set copy / clip authoring rely on it. Note: the trunk + ears *do* have dedicated
bones (so trunk/ear motion is keyable), they're just not surfaced as `<Monster>` attributes — the Monster only names
spine/neck/head for rider-sit + look-direction.

## How this differs from the paused spider (the core lesson)

The engine supports exactly **two agent shapes**: a **humanoid combatant** (skin + weapons + formation, the normal troop
path) and a **ridden mount** (a rider sits on it; the horse/warg/camel path). There is no third "non-humanoid riderless
combatant" shape — and that, not the bone count, is what the spider session ran into.

| | **ADOD elephant** (works) | **TAOM spider** (paused, crashed) |
|---|---|---|
| Agent shape | **Ridden mount** — a supported lane | Non-humanoid **riderless combatant** — a shape the engine doesn't have |
| `Mountable` | `true` (a rider mounts it) | `false` (detached, no rider) |
| Spawn | Normal vanilla troop spawn (rider + mount, like cavalry) | Custom **detached `FromHorseObj`** spawn via a Harmony patch (`SpiderDetachedAgentSpawner`) |
| Render | Standard ridden-mount render — proven by every horse/warg | The **detached/non-mountable mount-render sub-path** → AVs in native `Agent.PreloadForRendering` |
| Native wield state | Rider carries the weapons; the mount has none → no uninitialised native-wield garbage | Riderless → garbage native-wield pointers (`0xee0`/`0xee4`) → needed a 3-method wield guard |
| AI / movement | The rider's normal cavalry AI drives the mount | Hand-driven via `SetScriptedPositionAndDirection` (a riderless-AI hack) |
| Auto-attack | The mount tramples/gores while ridden (the **warg** pattern) | A bespoke detached BT + move-node + bite service |
| Bone count | 60 — fine (ridden) | 62 — crashed, **but because of the detached path, NOT the count** — ADOD's 60-bone elephant proves a 60-bone skeleton renders fine as a ridden mount |

**Takeaway:** ADOD did the *supported* thing — a creature as a **ridden mount** — and never touched any of the five
crash layers we fought. We tried the *unsupported* thing — a creature as a **riderless autonomous combatant** — by
hacking a mount (`FromHorseObj`) into a riderless fighter. The elephant is trivial by comparison precisely because it is
*naturally a ridden mount that auto-attacks* — exactly the warg pattern. (If the spider is ever revived, the same
insight applies: make it a **ridden mount**, not a detached agent.)

## The recipe (from ADOD's working elephant)

Extracted by the ADOD deep-dive (workflow `w21npmp7s`, 4 agents, 2026-06-05) — decompiled `ADOD_Beasts.dll`
+ read `adod_beasts.xml`, `adod_beasts_items.xml`, `elephant_troop_tree.xml`, `action_sets.xml`,
`monster_usage_sets.xml`. The headline: **ADOD's elephant is a STANDARD Bannerlord mount — the exact warg
pattern — not a detached agent.** The recipe has four data parts + one C# part:

**1. Monster** (`adod_beasts.xml` `<Monster id="elephant">`, lines 58–114):
- `Mountable="true"`, `CanRear="true"`, `CanCharge="true"`; **no rein bones**
- `action_set="as_elephant"`, `monster_usage="elephant"`, `family_type="10"`
- `sound_and_collision_info_class="bovine"` (a vanilla class)
- Bone block (leading-space + `_NN` Blender-export names — **copy verbatim**): `rider_sit_bone=" Spine1_05"`,
  `pelvis_bone=" Pelvis_03"`, `head_look_direction_bone=" Head_08"`, plus the full ragdoll roster
  (` Spine_04`, ` Neck_06`, ` Neck1_07`, ` L/R Clavicle`, ` L/R Thigh`, ` L/R UpperArm`).

**2. Item** (`adod_beasts_items.xml`, lines 79–182): a `Type="Horse"` item with
`<Horse monster="Monster.elephant" is_mountable="true" charge_damage="350" …>`; 4 `HorseHarness` items
(`family_type="10"`). The mount mesh hangs off this Horse item (and the howdah/armor off the HorseHarness),
exactly like a cavalry horse + barding.

**3. Rider troop** (`elephant_troop_tree.xml`): a **normal `NPCCharacter`** whose equipment fills the
`Horse` + `HorseHarness` slots with the elephant item — **there is NO non-humanoid "creature troop."** A
5-tier rider tree. This is the single most important structural fact: the elephant is ridden by an ordinary
humanoid, so the whole spider problem (a riderless non-humanoid body) never arises.

**4. Action set** (`action_sets.xml` `as_elephant`, lines 353–463): `skeleton="elephant_skeleton"`,
`movement_system="quadrupedal"`; binds the 31 elephant clips; defines custom `act_elephant_attack_1..4`;
**requires** the `as_elephant_town_and_village` + `as_elephant_map` child derivations. (Porting note: ADOD's
`as_elephant_town_and_village` has a copy-paste bug — it sets `act_elephant_stand_1` four times instead of
`stand_1/2/3/4`; fix on port.) Plus a full `monster_usage_set` named `elephant` (`monster_usage_sets.xml`).

**5. Auto-attack C#** (decompiled `ADODBeastsElephantAgentComponent` + `ADODBeastsMissionLogic`): the trample
is **pure managed C#**, the same mechanic as TAOM's `WargAttackService` — `OnTickAsAI` (when a rider's target
is < 3 m, look-dot > 0.25, roll < 0.001) plays `act_elephant_attack_N` and builds a `Blow` + `RegisterBlow`;
the player triggers it on Space (Input 57). The heavy hit is the engine's native charge (`charge_damage=350`).
The component is attached in `OnAgentBuild` off the HorseHarness, mirroring `WargMissionBehavior`. A
mount-lockout (`CanAgentRideMount=false` + `MountDifficulty=999f` for non-rider agents) stops the AI stealing
the elephant. **`NativeHook.dll` / `EasyHook.dll` are DEAD in ADOD** — a leftover `using`, zero call sites in
the 5029-line decompile, no `DependedModule`. **There is no native code to port.**

### Deep-dive decision: structure from the warg, data from ADOD

The deep-dive's verdict (which I'm adopting): **clone the warg's architecture for the C#, re-author ADOD's
data under TAOM/Harad ids, port no native code.** Concretely — `Main/Features/Elephant/` as a sibling of
`Main/Features/Warg/`; an adapter-pure `IElephantAttackService` (ADR-002/007) that uses the warg's
`CustomAttack` + `CustomAttacksUtils.TakeDamage` path (a clean bone-collision hit) **instead of** ADOD's
radial `Blow` loop; `ElephantAttackServiceTests` (ADOD ships none); `FiniteFloatValidator` on any config;
fold the `CanAgentRideMount` + `MountDifficulty 999` mount-lock into the existing TAOM `AgentStatCalculateModel`;
defer the howdah (YAGNI); and — per the faction-map update rule (memory `feedback_faction_map_update_with_cultural_feats`)
— update `factions.json` if the war elephant becomes a Harad identity element. Use a plain war-elephant id
(e.g. `taom_war_elephant`), `culture=Culture.harad` — **not** a mumakil/Oliphaunt id (those are a separate, giant
creature TAOM already frames via `mumak_rider`).

## Plan (port → re-theme → standards)

Ordered, following the deep-dive's step list (structure from warg, data from ADOD, no native port):

1. **Verify the rig matches — DONE (2026-06-05).** Diffed our FBX armature against ADOD's `elephant_skeleton`:
   **identical, bone-for-bone, 60 bones, same order** (` Pelvis_03` → ` Spine_04` → ` Spine1_05` → ` Neck_06`
   → ` Neck1_07` → ` Head_08`, then ears + trunk + 4 legs + tail). The rig is confirmed.
   **Skeleton renamed + FBX re-exported — DONE (2026-06-05):** the armature (object + data) was
   `elephant_skeleton_unused`; renamed to **`elephant_skeleton`** (the spider's `_notused` → `_` fix), the body mesh
   `elephant_mesh_unused` → **`elephant_mesh`** (matches ADOD), and `elephant_harad_armor_01.fbx` re-exported with the
   proven recipe (`object_types={ARMATURE,MESH}`, `primary_bone_axis='Y'`, `secondary_bone_axis='X'`,
   `axis_forward='-Y'`, `axis_up='Z'`, `add_leaf_bones=False`, `bake_anim=False`). **Verified** via
   `tools/extract_fbx_bones.js`: root Null `elephant_skeleton` + **60 LimbNodes (0 leaf bones)** + 11 meshes
   (`elephant_mesh` + 10 `SK_Elephant_Armor_A.*`). Original backed up to `elephant_harad_armor_01.orig.fbx`.
   **The FBX is ready to import into the Modding Kit.** The `SK_Elephant_Armor_A.*` part names are fine as-is (the
   Monster/Item XML references whatever we author).
2. **Assets** — copy ADOD's elephant rig/mesh/textures into TAOM (or `LOTRLOME_Armory`); keep the mount
   `Usage='horse'` on the skeleton; run `tools/tpac_skeleton_transplant.py <tpac> elephant_skeleton --usage horse`
   if physics needs re-applying after a re-import. (Animations: TAOM-authored on our FBX — step 4b, NOT ADOD's clips.)
3. **Data — Monster + Item + usage + action_set** (re-authored under TAOM/Harad ids — a plain war-elephant name,
   e.g. `taom_war_elephant`, **not** a mumakil id; final id is your call):
   - **Monster** `taom_war_elephant`: copy ADOD's `<Monster>` bone block verbatim, keep `Mountable/CanRear/CanCharge=true`,
     `action_set=as_war_elephant`, `monster_usage=…`, `sound_and_collision="bovine"`.
   - **Item** (Horse) `taom_war_elephant`: `Type="Horse"`, `culture=Culture.harad` (not empire), tuned `charge_damage`,
     body mesh + `<AdditionalMeshes>` for armor/howdah + `<Materials>` → the elephant textures,
     `<Horse monster="Monster.taom_war_elephant" is_mountable="true">`; + a `HorseHarness` (`family_type="10"`).
   - **action_set** `as_war_elephant` + the **required** `_town_and_village` + `_map` children (fix ADOD's
     `stand_1`×4 copy-paste bug); the `monster_usage_set`. Rename `act_elephant_*` → `act_war_elephant_*` so we
     carry no runtime dependency on ADOD being installed. Register all in `SubModule.xml`.
4. **Validate data** — `python tools/validate_moduledata.py` before any C# (the external LOTRLOME XML is
   out of its scope, but the Main-module refs are not).
4b. **Animations (TAOM-authored, on our FBX)** — author the elephant clips on our FBX (walk / run / idle /
   turn / charge / trample / tusk-gore / death + trunk + ear motion) via the creature-animation pipeline
   (Blender/Cascadeur → Modding-Kit compile), bound in the action set. The rig is a standard quadruped, so
   body+leg gaits can retarget from horse/warg; the **trunk + ears are the bespoke part** (ADOD bakes trunk/ear
   motion into each clip — no separate bones — so TAOM must key them into our clips). Reference ADOD's action
   *coverage* only (it reuses `elephant_walk` for trot + strafe — TAOM can author real trot/strafe for polish).
   See "Declined: reusing ADOD's clips" below for why we author rather than reuse.
5. **C# — clone the warg.** `Main/Features/Elephant/` as a sibling of `Main/Features/Warg/`: an adapter-pure
   `IElephantAttackService` (ADR-002/007) using the warg's `CustomAttack` + `CustomAttacksUtils.TakeDamage`
   bone-collision path (**not** ADOD's radial `Blow` loop); an `ElephantMissionBehavior` that attaches the
   attack component in `OnAgentBuild` (mirroring `WargMissionBehavior`); `FiniteFloatValidator` on config.
6. **Mount-lock** — fold `CanAgentRideMount=false` + `MountDifficulty=999f` (non-rider agents) into the existing
   TAOM `AgentStatCalculateModel` so the AI can't steal the mount.
7. **Recruitment** — author the Harad rider tier(s) + pool via `VolunteerRecruitmentService`; the rider is a
   **normal humanoid `NPCCharacter`** with the elephant in its `Horse`+`HorseHarness` slots (no creature troop).
8. **Tests** — service 100%, mission behavior 80%+ (ADR-008).
9. **Standards / ship** — `/verify` → `/deep-review` → `/review-codex` → issue + `docs/features/elephant.md` +
   CHANGELOG before commit.
10. **In-game smoke** — mount, charge, confirm trample + knockdown, confirm AI cannot steal the mount.

### Declined: reusing ADOD's clips (recorded, not chosen)

The deep-dive found ADOD ships **31 elephant-skeleton clips + 32 human-rider clips**, all *clip-only*
(skeleton-agnostic, TaleWorlds anim-clip type `506509c8-…`, no embedded skeleton), so they would technically
drop straight onto our `elephant_skeleton` if the bones match — a complete, production-grade set covering
walk/trot/canter/gallop(+turns)/idle/rear/dash/hit/death + 4 trample attacks. **The project owner chose to
author TAOM's own clips on our FBX instead (2026-06-05), so ADOD's clip set is a declined fallback / coverage
reference only — not the shipping animations.** (Licensing is moot either way — the asset was purchased from
Artem; see below.)

## Key files

The **C# is built + wired + green** — see the "Implementation" section above for the file table. The remaining
files are data/assets:

| Component | Path | Status |
|-----------|------|--------|
| Mesh + skeleton tpac | `LOTRLOME_Armory/Assets/creature/elephant/mesh/elephant_harad_armor_01_geo.tpac` (+ textures) | **imported** (2026-06-05) |
| Monster XML | `…/LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_elephant.xml` (id `taom_war_elephant`) | TODO — recipe below |
| Item XML | `…/LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` (Horse item `taom_war_elephant`, mesh `elephant_mesh`) | TODO — recipe below |
| Action set (1.4.5) | `…/LOTRLOME_Armory/ModuleData/…/action_sets…` (`as_war_elephant`, vanilla-1.4.5 schema, NOT ADOD's) | TODO — gating seam |
| Animation clips | `LOTRLOME_Armory/Assets/creature/elephant/animations/` (TAOM-authored, Blender → Kit) | TODO — gating seam |
| Recruitment | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` (Harad pools) | TODO |

## Reference: ADOD_Beasts

A shipped community beasts mod (wolves, wights, elephant) with `ADOD_Beasts.dll` + a `NativeHook.dll` +
`EasyHook.dll`. It is the working reference for the elephant. **Deep-dive verdict (`w21npmp7s`, 2026-06-05):**
the elephant is a standard Bannerlord mount driven entirely by data + managed C# — `OnTickAsAI` builds a
`Blow`/`RegisterBlow` (same mechanic as `WargAttackService`), a `CanAgentRideMount=false`/`MountDifficulty=999f`
GameModel locks out non-rider AI, and the attack component is attached in `OnAgentBuild`. **`NativeHook.dll` /
`EasyHook.dll` are dead** — a leftover `using`, zero call sites across the 5029-line decompile, no
`DependedModule` declaration. **TAOM ports no native code and ships neither DLL.** Structure is cloned from the
warg; data is re-authored from ADOD; meshes/textures are our own FBX (purchased — see License below).

## License / provenance

The elephant asset was **purchased from Artem (the ADOD author) for use in TAOM** — no clean-room re-derivation or
attribution gating required. (Confirmed by the project owner, 2026-06-05.)

## Open items

- [x] Fold in the exact recipe + the ADOD code/NativeHook verdict from the deep-dive workflow. *(done 2026-06-05)*
- [x] Capture ADOD's `elephant_skeleton` bone roster as the match reference (60 bones — see "Verified bone roster" above). *(done 2026-06-05)*
- [x] **Verify our FBX rig matches ADOD's `elephant_skeleton` bone-for-bone** — DONE (2026-06-05, Blender diff:
      identical 60 bones, same order).
- [x] **Rename the armature → `elephant_skeleton` + body mesh → `elephant_mesh`, re-export `elephant_harad_armor_01.fbx`**
      — DONE (2026-06-05; verified 60 LimbNodes / 0 leaf bones / 11 meshes via `extract_fbx_bones.js`; original backed up).
      **Ready to import into the Modding Kit.** Remaining: re-confirm the rig in the Kit after the tpac compile.
- [x] Scaffold the `Main/Features/Elephant/` service core (clone of `Main/Features/Warg/`) — `IElephantAttackService`
      via the warg `CustomAttack` path + 19 unit tests. *(done 2026-06-05, build green)*
- [x] **C# trample + mission behavior + IoC/SubModule wiring** — DONE (2026-06-05, 1-for-1 ADOD on v1.4.5, build green; 11 service tests).
- [x] **Mount-lock** (`CanAgentRideMount=false` + `MountDifficulty=999`) folded into `TaomAgentStatCalculateModel` — DONE (2026-06-05).
- [x] **Mesh + skeleton imported** to `LOTRLOME_Armory/Assets/creature/elephant/` — DONE (2026-06-05, by the project owner).
- [ ] Author the **Monster + Horse Item** (`taom_war_elephant`) into LOTRLOME_Armory — recipe below; Monster id must stay `taom_war_elephant`.
- [ ] Author a **1.4.5 action-set** `as_war_elephant` (vanilla-1.4.5 quadruped-mount schema, **NOT** ADOD's 1.2.12 `as_elephant`) — gating seam.
- [ ] **Build the elephant animations on our FBX** (Blender → Modding-Kit compile) — TAOM-owned, NOT ADOD's 1.2.12 clips. Gating seam for animation.
- [ ] Author the Harad rider tier(s) + recruitment pool; update `factions.json` if the war elephant is a Harad identity element.
- [ ] In-game smoke: mount, charge, confirm trample + knockdown (works even pre-animation), confirm AI cannot steal the mount.
- [ ] Tune damage/gates after in-game testing (current values are ADOD's 1-for-1 baseline — the "improve" step).

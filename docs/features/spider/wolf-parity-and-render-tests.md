# Spider revival — wolf-parity reference + ready-to-apply render tests

> **Status (2026-06-06):** spider feature PAUSED (`SpiderConfig.Enabled=false`) on a native render
> AccessViolation in `Agent.PreloadForRendering`. This doc holds two **separate, ready-to-apply**
> workstreams so the bisection stays clean:
> - **A — render bisection (THE blocker).** Mesh experiments. *This is what unblocks the spider.*
> - **B — wolf XML alignment (OPTIONAL robustness, downstream of A).** Does **not** fix the render.
>
> Apply and test **one at a time** — never A2 and B together, or you can't tell which moved the needle.
> Full analysis + ranked experiments: [`docs/reviews/rca-spider-troop-2026-06-04.md`](../../reviews/rca-spider-troop-2026-06-04.md)
> "Update 2026-06-06".

## What the ADOD deep-dive settled (why A is the blocker, not B)

Decompiled `ADOD_Beasts.dll` + `NativeHook.dll` (the wolf "has a lot of code"):

- The wolf's code — `ADODBeastsWolfAgentComponent` (~600 lines), `ADODBeastsMissionLogic` (~460), and
  `NativeHook.dll`'s 3 EasyHook hooks (`Agent_AiTick`, `Agent_Tick`,
  `AgentMovementAndDynamicsSystem_UpdateFlags`) — is **all movement/AI. ZERO render hooks.**
- So the wolf **renders through the stock engine path with no render code** — purely because its mesh
  fits the native per-mesh bone-palette cap (single-mesh `Type="Animal"`, 57-bone skeleton).
- Therefore **no code (managed or native) fixes the spider AV. It's the mesh.** → workstream A.
- ADOD's creatures aren't even roster troops (wolf = scripted companion via public
  `Mission.SpawnMonster(mountItem, default rider, …)`; elephant = ridden mount + howdah). The spider's
  "riderless recruitable troop in a formation" is TAOM's own, harder design.

---

## Workstream A — render bisection (apply in order; stop at the first that renders)

The active mesh asset lives in `Assets/creature/spider/animations/spider_correct_geo.tpac` (carries the
**split** meshes `sk_spider_forest_c` + `sk_spider_forest_c_2` + the 62-bone `spider_skeleton`).
The **original single mesh** is backed up at `Assets/creature/spider/meshes/sk_spider_forest_c_geo.tpac.backup`
(7,755,375 B vs the split's 7,991,676 B — only **+3%**, which is *consistent with a real L/R partition +
seam duplication OR a non-functional "split"* — inconclusive without a palette dump, so test empirically).

### A1 — Confirm the current (split) spider still AVs  *(free, ~5 min, no asset work)*

1. `Main/Features/Spider/SpiderConfig.cs` → `Enabled = true`.
2. Build (`./build.ps1`), recruit a spider at a Dol Guldur fief, take it to one battle.
3. **AVs** → the split is exhausted; go to A2. **Renders** → the earlier AV was environmental
   (shader cache / driver); skip to "ship it" + re-enable.

### A2 — Un-split single mesh  *(cheap, no Blender; THE genuinely-untried direction)*

**Asset step (Kit/file — yours):** make the **original single mesh** the active `sk_spider_forest_c`
asset (restore `meshes/sk_spider_forest_c_geo.tpac.backup`, or re-export a single-mesh tpac that carries
`spider_skeleton`). The goal: `mesh="sk_spider_forest_c"` resolves to the *un-split whole body*, not the
split's left half.

**Item edit (ready-to-apply)** — `LOTRLOME_Armory/ModuleData/LOTRLOME_items/LOTRAOM_horses.xml`, replace
the `spider_mount_a` block. The ONLY change vs current is **dropping `<AdditionalMeshes>`** (back up the
file first):

```xml
<Item
    id="spider_mount_a"
    name="{=spider_mount_a}Giant Spider"
    mesh="sk_spider_forest_c"
    subtype="horse"
    item_category="war_horse"
    weight="250"
    is_merchandise="false"
    Type="Horse">
    <ItemComponent>
        <Horse
            monster="Monster.spider"
            maneuver="60"
            speed="40"
            charge_damage="10"
            body_length="100"
            is_mountable="false" />
    </ItemComponent>
</Item>
```

**Decision:** renders → the split *was* the corruption; ship single-mesh. Still AVs → the mesh's own skin
data overflows the cap regardless of split → A4 (re-author the FBX skin).

### A3 — Wolf-mesh control  *(cheap; isolates pipeline-vs-mesh if A2 still AVs)*

Point the item at a **known-good single creature mesh** and keep `monster="Monster.spider"`:
`mesh="wolf_4"` (ADOD's wolf body) in the `spider_mount_a` block above.

- **Renders** (even wolf-shaped) → the riderless pipeline + spider monster/skeleton binding are fine; the
  defect is **100% the spider mesh** → A4.
- **AVs** → the defect is in the skeleton↔mesh binding / spider skeleton render data, not the mesh alone.
- *Caveat:* `wolf_4` is skinned to the 57-bone wolf skeleton; with `monster=Monster.spider` (62-bone
  skeleton) a bone-index mismatch is possible — this is a coarse control, read it as AV-vs-no-AV only.

### A4 — Re-author `spider_correct.fbx` skin  *(Blender/Kit, expensive; the real fix if A2/A3 say "mesh")*

Clamp ≤4 influences/vertex, drop zero-weight influences, genuinely minimal per-mesh palette (≤~40),
export skeleton+mesh in **one** FBX, regenerate the tpac, re-run
`python tools/tpac_skeleton_transplant.py` to restore the IK ragdoll.

### A5 — Fallback: ship as a ridden mount  *(proven; only if A2–A4 fail)*

`Mountable=true` via the warg/elephant machinery (both render). The wolf proves riderless *can* work, so
this is the fallback, not the first move.

---

## Workstream B — wolf XML alignment  *(OPTIONAL robustness — NOT the render fix, downstream of A)*

> **Read before applying.** B switches the spider off its custom `monster_usage="spider"` vocabulary onto
> the vanilla horse pipeline (what the wolf uses). It is **optional** and has **two caveats**:
> 1. It does **NOT** fix the 13 `TEMP-ANM-UNBLOCK` DivideByZero risk — that `/0` is a **missing
>    `_anm.tpac`** (clip-compile) problem, independent of `monster_usage`. Only re-exporting those clips
>    in the Kit fixes it.
> 2. It risks **degrading the gait** — the custom `act_spider_*` set drives proper spider clips; the
>    wolf's horse pipeline makes the wolf move horse-ish.
>
> Do this only if you specifically want the spider on the engine's tested horse-movement pipeline. Back up
> every live file first.

### B1 — Monster: `monster_usage="spider"` → `"horse"`

`LOTRLOME_Armory/ModuleData/Monsters/LOTR/lotr_monster_spider.xml` — set `monster_usage="horse"` (use the
vanilla usage, like ADOD's wolf, instead of the custom `lotr_monster_usage_spider.xml`). Keep
`IsHumanoid="false"`, `Mountable="false"`, `CanRide="false"`.

### B2 — Item: `Type="Horse"` → `"Animal"`, `item_category` → `"animal"`

In the `spider_mount_a` block, mirror ADOD's `adod_wolf_*` items: `Type="Animal"`,
`item_category="animal"`, drop `subtype="horse"`. The `<Horse monster="Monster.spider" …>` component
**stays** (ADOD's `Type="Animal"` wolves still use the `<Horse>` component — that's normal).

### B3 — Action-set: rebuild `as_spider` on vanilla `act_horse_*` types

`LOTRLOME_Armory/ModuleData/Animations/action_sets_spider.xml` — the current `as_spider` defines a custom
`act_spider_*` vocabulary. With `monster_usage="horse"` the engine drives `act_horse_*` movement actions,
so the action-set must bind **those** types to the `an_spi_*` clips. Mapping (bind `act_horse_*` → spider
clip, mirroring how ADOD's `as_adod_wolf` reuses a few gaits):

| vanilla action family | bind to spider clip | note |
|---|---|---|
| `act_horse_stand*` (idles) | `an_spi_walk_2` *(temp)* / `an_spi_idle_2` | use `walk_2` until `an_spi_idle_2._anm` is re-exported |
| `act_horse_forward_walk` | `an_spi_walk_2` | the forward gait |
| `act_horse_walk_side_left/right` | `an_spi_walk_left` / `an_spi_walk_right` | |
| `act_horse_forward_trot*` / `canter*` / `gallop*` | `an_spi_run` | never `an_spi_run_2` |
| `act_horse_turn_left/right` | `an_spi_walk_left` / `an_spi_walk_right` | turn clips lack `_anm` |
| `act_horse_backward_walk*` | `an_spi_walk_2` | engine reverses |
| death / fall / rear | `an_spi_hit_back` *(temp)* | death clips lack `_anm` |

**Author it in the Kit** (not blind hand-edit): pull the **full** vanilla horse action-set's `act_horse_*`
type list from `SandBox`/`Native` `action_sets.xml`, copy every movement type, bind per the table, and
validate. A hand-authored list that misnames even one `act_horse_*` type silently fails to bind →
idle-shuffle. (This is why B is "do it in the Kit," not a drop-in XML block here.) Keep the existing
`act_horse_*`/`act_inventory_*`/MP-mount-idle **fallback** block at the bottom of `as_spider`.

---

## The real remaining gaps (both are Kit asset work, neither A nor B fixes them in XML)

1. **Render mesh** — the AV. Resolved by A2–A4 (un-split / re-author skin), confirmable only in-game.
2. **13 missing `_anm.tpac` clips** — the `TEMP-ANM-UNBLOCK` substitutions in `action_sets_spider.xml`
   (`an_spi_idle_2`, `an_spi_turn_left/right`, `an_spi_attack_left/right/top`, `an_spi_hit_front/right`,
   `an_spi_jump`, `an_spi_death_1/2`, …). Each ships a `_geo.tpac` but no `_anm.tpac` → DivideByZero at
   spawn. Re-export the `_anm.tpac` for each in the Modding Kit, then restore the real bindings.

## Backups

Every live-file edit above is reversible: back up before editing (`.bak` alongside, as the prior session
did for `action_sets_spider.xml`). The mesh asset's original single mesh is already preserved at
`meshes/sk_spider_forest_c_geo.tpac.backup`.

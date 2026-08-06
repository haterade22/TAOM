# Investigation: Isengard Uruk-Hai render as black silhouettes in the encyclopedia (#389)

**Status: OPEN — root cause not confirmed.** This records an investigation still in progress, not a
fix. It is written now because the session produced several durable results, two instrument defects
worth never repeating, and a framing correction that invalidates most of the first day's hypotheses.

**Date:** 2026-08-06 · **Issue:** [#389](https://github.com/haterade22/TAOM/issues/389) ·
**Engine:** v1.4.7 · **Related:** [#390](https://github.com/haterade22/TAOM/issues/390) (separate
missing meta-mesh defect found en route)

---

## Symptom

The encyclopedia unit page draws affected Isengard troops as a **pure black silhouette** — geometry
correct (helmet outline, shield shape, body contour all legible), every pixel black, **including the
armour**. `[Isengard] Orc Ravager` renders perfectly on the same page. All other custom races
(`pale_uruk`, `dg_uruk`, `uruk`, `goblin`, `dwarf`, `elf`) render correctly.

## The framing correction that matters most

The investigation spent a full day scoped to the **race**, because the reported set (`uruk_hai`,
`berserker`) maps cleanly onto two `skins.xml` race entries. Every hypothesis inherited that scope.

**It was wrong.** The reporter then observed that `urukhai_recruit` renders correctly. Checked against
the roster:

| troop | race | body property | Head item | body | renders |
|---|---|---|---|---|---|
| `urukhai_recruit` | `uruk_hai` | `fighter_uruk_hai` | **NONE** | `sk_uruk_hai_tunic_a1` | **FINE** |
| `urukhai_fighter` | `uruk_hai` | `fighter_uruk_hai` | `sk_uruk_hai_helmet_sword_light_a2` | `sk_uruk_hai_chainmail_a1` | black |
| `urukhai_warrior` | `uruk_hai` | `fighter_uruk_hai` | `sk_uruk_hai_helmet_sword_light_a4` | `sk_uruk_hai_plate_light_b1` | black |
| `urukhai_spearman` | `uruk_hai` | `fighter_uruk_hai` | `sk_uruk_hai_helmet_spear_light_a3` | `sk_uruk_hai_plate_med_b2` | black |
| `urukhai_berserker` | `berserker` | `fighter_uruk_berserker` | `sk_uruk_hai_helmet_bers_a2` | (none) | black |
| `isengard_orc_ravager` | `orc` | `fighter_orc_mordor` | `sk_gn_orc_mrd_helmet_light_a` | `sk_md_orc_arc_chest_med_a` | **FINE** |

`urukhai_recruit` is the same race, the same body-property template, and wears an `sk_uruk_hai_*` body
mesh — and renders correctly. The **only** thing it lacks is a helmet. That one observation exonerates
the race, the body-property template, the race base-body meshes, and the entire `sk_uruk_hai_*`
body-armour family simultaneously.

**Lesson: the troop roster was already a controlled experiment and nobody read it as one.** A 21-agent
sweep, five parallel evidence tracks and adversarial verification all stayed inside the race framing
because the *reported set* was described in race terms. One user observation about a troop nobody had
thought to open beat all of it.

## The current lead

Every affected helmet bundles extra sub-meshes. From
`LOTRLOME_Armory/Shaders/D3D11/shader_compile_report.log` (mesh → material → shader → variant count):

```
sk_uruk_hai_helmet_sword_light_a2 -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a1(120)
sk_uruk_hai_helmet_spear_light_a3 -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a1(120)
sk_uruk_hai_helmet_bers_a2        -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a2(120)
sk_uruk_hai_helmet_skir_a2        -> m_uruk_hai_gloves_a1(120), m_uruk_hai_hands_a1(888), m_uruk_hai_helmet_a1(120)
sk_uruk_hai_helmet_sword_light_a4 -> m_uruk_hai_gloves_a1(120),                           m_uruk_hai_helmet_a1(120)

sk_gn_orc_mrd_helmet_light_a      -> m_md_orc_helmets_b(120)     <-- working control: ONE material
```

A helmet MetaMesh should not carry glove and hand geometry. `m_uruk_hai_hands_a1` compiles **888
shader variants** against everyone else's 120 — the signature of a *skin* material (skin/morph
permutations), not an armour material. A skin material reaching the entity through
`AgentVisuals.AddArmorMultiMeshesToAgentEntity` rather than `AddSkinMeshesToEntity` has no generated
skin texture bound to it, which is a plausible route to a black draw.

**Not proven.** Two honest caveats:
1. `..._a4` (the warrior's helmet) carries **no** hands material yet the warrior is still black. So
   "carries the 888-variant skin material" does not fit; "bundles glove/hand geometry" fits all five.
2. It does not obviously explain the *whole figure* going black rather than just the helmet's
   sub-meshes. That is exactly what the render census must settle.

All four materials and their `_d`/`_n`/`_s` textures exist on disk. This is a structural/authoring
anomaly, not a missing asset.

## Eliminated, with evidence — do not re-investigate

| Candidate | Why it is dead |
|---|---|
| The **race** (`uruk_hai` / `berserker`) | `urukhai_recruit` is `uruk_hai` and renders fine. |
| The **body-property template** | `urukhai_recruit` uses `fighter_uruk_hai` and renders fine. |
| The **race base-body meshes** (`sk_uruk_hai_bm_a_*`) | Same — the recruit draws them correctly. |
| The **`sk_uruk_hai_*` body-armour family** | The recruit's `sk_uruk_hai_tunic_a1` renders fine. |
| `skinning_precise` missing from base materials | `PRECISE_SKINNING` occurs at exactly 2 sites in `Shaders/Sources/gpuskinning.rs` (L95/L135) and gates **only vertex-position load width**; a wrong value mangles geometry, never albedo. Also 485 of 509 `pack3` materials lack it — it is the pack default. Also elf/goblin lack it and render fine. |
| `uses_stitching="false"` | `pale_uruk`, `dg_uruk`, `goblin`, `elf` share it; all render fine. |
| Any `skins.xml` attribute or child element | Exhaustive per-attribute + per-child census over 14 races × 10 skins found **zero** properties true for exactly {`uruk_hai`, `berserker`}. |
| Missing / dangling asset reference | Every mesh→material and material→texture GUID resolves across all 10 packs; LOD chains complete; loose `Assets/` tree complete. |
| The engine failing to bind a material | `rgl_log_errors_30700.txt` for a session where the bug was on screen is **180 bytes, header only** — zero content warnings of any kind. |
| **Resource residency** (H-A, the original leading theory) | Refuted twice. Structurally: `RefreshCharacterTableau` hides the refreshed buffer and `OnTick` shows a visual only when **both** loading counters clear, so a character whose resources never load renders **blank, not black**. Empirically: the census measured `agentLoading=0 mountLoading=0 agentVisible=True oldVisible=False` on the black troop. |
| Culture cloth tint / `UseTeamColor` | Measured at runtime: `clothColor1=0xFF2B2B2B clothColor2=0xFF8C8C8C` are **byte-identical** between the fine orc and the black uruk. Note the *arrow is inverted* from the first write-up — the orc armour is 100% `UseTeamColor="true"` (103 items) and renders fine; `sk_uruk_hai_*` is 0 true / 87 false / 50 absent. |
| Action sets / monsters / skeletons | Live log: `race=5 uruk_hai` and `race=6 berserker` both resolve `as_*_warrior`, a valid skeleton, idle clip and pose exactly as the working orc. `ActionIndexCacheRepair` reports 214/215 statics healthy. |
| Stale deployed build | Deployed `TAOM.dll` byte-size and mtime identical to the repo build output. |
| A second module overriding the races | Only `LOTRLOME_Armory` declares the 14 custom races; `Native`/`NavalDLC` declare `human` only; `TAOM_Map` is a stub. |
| `BasicTableauRaceGuard` allow-list | `BasicCharacterTableau` and `CharacterTableau` are independent classes with different texture providers; the guard never runs on this path. |

## Instrument defects — two aiming errors, both caught outside the test suite

The diagnostic (`Patch67_TableauResidencyDiag`) was built, then wrong twice. Both are recorded because
the *class* of error is the transferable lesson.

**1. It measured a state the symptom makes impossible.** v1 reported "loading counter never cleared ⇒
black silhouette". Vanilla only makes a visual visible once both counters clear, so a character with
correct geometry on screen has by definition finished loading — the instrument could only ever have
logged "fine". A negative result from a test that cannot produce a positive carries no information.
Caught by adversarial review before it shipped. *Ask of any diagnostic: is the state I am measuring
reachable given the symptom I already observe?*

**2. It read the wrong mesh container.** v2 reported `metaMeshCount=0` for **every** character
including ones that render correctly — `AgentVisuals` attaches skin and armour through
`GetSkeleton()`, so `GameEntity.MultiMeshComponentCount` is 0 on a character tableau. Caught only by
reading the live log. *A census that returns zero for a known-good control is measuring the wrong
thing, and the control is what reveals it.*

**3. A third defect never reached the log at all:** the patch used three underscores where Harmony
needs four (`____field`), so the whole category threw at apply time and was swallowed by TAOM's
isolated batch. It passed its own binding tests and all 5588 suite tests. See
[`lessons/harmony-il.md`](lessons/harmony-il.md); gate is now
`TAOM.Tests/Migration/HarmonyFieldInjectionNamingTests.cs`.

## What the instrument does now

Dumps one **render census** per troop on the frame its visual becomes ready: mesh names, material
names, shader names, shader flags, per-mesh `Mesh.Color`/`Color2` and bound diffuse texture, grouped
by material so two troops diff on one screen. Plus context: race, cloth colours, body-property
default-ness, both loading counters, `isEnabled`, and both buffers' visibility.

## Open questions

1. **Which meshes are actually black at runtime** — needs the census from a relaunch with
   `urukhai_fighter` vs `urukhai_recruit` (the decisive pair now: one wears the suspect helmet, one
   does not).
2. Whether the helmet's bundled glove/hand sub-meshes are the black draw, or whether equipping the
   helmet blackens the *whole* skeleton.
3. Why `..._a4` (no hands material) is black if the 888-variant skin material is the mechanism.
4. Whether the fix is a re-export of `SK_Uruk_Hai_Helmets_A/B_geo.tpac` or a material rebind.

## If the fix lands in LOTRLOME_Armory

It is a **dependency module outside this repo**. Three traps:
- Binary containers, no text representation — needs a Modding Kit re-export.
- **Dual tree:** this machine loads the loose `Assets/` tree while a player install loads
  `AssetPackages/pack*.tpac`. A fix must land in **both** or it works for the dev and not for players.
- Untracked by TAOM git and destroyed by the next Armory sync — record it in
  [`docs/reference/lotrlome-armory-snapshot/README.md`](../reference/lotrlome-armory-snapshot/README.md).

## Technique worth reusing

`LOTRLOME_Armory/Shaders/D3D11/shader_compile_report.log` is a **plain-text mesh → material → shader →
variant-count map** for every compiled asset. It is the cheapest way to answer "what material does this
mesh actually use" without a tpac parser, and it is what exposed the helmet bundling. Grep the mesh
name; do not use `-m1`, since one mesh legitimately has several material rows.

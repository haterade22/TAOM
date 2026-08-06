# Investigation: Isengard Uruk-Hai render as black silhouettes in the encyclopedia (#389)

**Status: ROOT CAUSE IDENTIFIED (2026-08-06, from runtime census). Fix not yet applied.**

## Root cause

`m_uruk_hai_hands_a1` is authored to require the shader flag that **team-colouring** adds. Items that
are not team-coloured never get it, and the material renders black.

`AgentVisuals.AddTeamColorToMesh` (v1.4.7) is the only thing that adds it:

```csharp
meshAtIndex.Color = color1;  meshAtIndex.Color2 = color2;
Material val = meshAtIndex.GetMaterial().CreateCopy();          // <-- hence the "(copy)" suffix
val.AddMaterialShaderFlag("use_double_colormap_with_mask_texture", false);
meshAtIndex.SetMaterial(val);
```

and it is called only when the **item** carries `UseTeamColor="true"`. Measured shader flags from the
live census:

| meshes | material as bound at runtime | flags | renders |
|---|---|---|---|
| `sk_uruk_hai_bracer_*` / `pauldron_*` / `helmet_*` (Isengard, `UseTeamColor="false"`) | `m_uruk_hai_hands_a1` | **`0x480090`** | **BLACK** |
| `sk_is_orc_*_helmet_*` (Isengard, `UseTeamColor="true"`) | `m_uruk_hai_hands_a1(copy)` | `0x4C0090` | fine |

The delta is exactly **`0x40000`** — `use_double_colormap_with_mask_texture`. Same material, same
shader, same texture; the only difference is whether team-colouring copied it and set that bit.

**Perfect correlation across all 11 censused troops:** every troop carrying the raw `0x480090`
variant is black (`urukhai_fighter`, `_warrior`, `_swordman`, `_infantry`); every troop that either
lacks the material (`urukhai_recruit`, `main_hero`, `isengard_orc_ravager`, `_berserker`) or gets the
team-coloured copy (`isengard_orc_warrior`, `_butcher`, `_marauder`) renders correctly.

This also explains the two facts that had looked contradictory: `urukhai_recruit` renders because its
gear (tunic, gloves, shoes) never pulls in the hands sub-mesh; and the near-black Isengard culture
colour `0xFF2B2B2B` is harmless — **the absence of team colour is the bug, not its presence.** The
earlier elimination of "cloth/team colour" had the arrow exactly backwards.

### The fix

92 items bind a mesh carrying `m_uruk_hai_hands_a1`. 13 (`isengard/head_armors.xml`) are already
`UseTeamColor="true"` and render fine. The remaining **79 are `false` or absent** and are the black
set:

| count | file (under `LOTRLOME_Armory/ModuleData/LOTRLOME_items/isengard/`) |
|---|---|
| 43 | `head_armors.xml` |
| 12 | `arm_armors.xml` |
| 10 | `body_armors.xml` |
| 8 | `shoulder_armors.xml` |
| 6 | `leg_armors.xml` |

Setting `UseTeamColor="true"` on those 79 makes the engine copy the material and add the mask flag,
exactly as it already does for the 13 that work. Side effect: those meshes also receive
`Color = 0xFF2B2B2B` / `Color2 = 0xFF8C8C8C` (Isengard culture colours) — which is what the 13 working
items and every orc item already do, so it is the established look, not a new one.

**Alternative** (cleaner but needs the Modding Kit): re-author `m_uruk_hai_hands_a1` so it does not
depend on the mask flag — or fix the upstream defect that the hand/glove sub-meshes are bundled into
helmet, bracer, greave and pauldron MetaMeshes at all.

Either way this is **data in `LOTRLOME_Armory`, a dependency module outside this repo** — see the
dual-tree trap at the end of this document.

---

Everything below is the investigation record that led here, retained because the process lessons are
the transferable part.

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

**Shape after the deep review** (Review 82b — the pre-review shape had a per-frame allocation, a slot
leak and a hash-identity collision; see the findings table below):

| File | Role |
|---|---|
| `Hooks/CharacterTableau_OnTick_ResidencyCensus_Patch.cs` | the census postfix — thin, holds no state |
| `Hooks/CharacterTableau_ResidencyReset_Patches.cs` | `SetRace` + `OnFinalize` resets |
| `Diagnostics/TableauCensusSession.cs` | per-tableau identity keys + the shared tracker |
| `Diagnostics/TableauResidencyTracker.cs` | the report trigger (pure, 17 tests) |
| `Diagnostics/TableauRenderCensus.cs` | reads `Skeleton.GetAllMeshes()`, groups by material |

`TableauCensusSession` keys on `object` rather than `CharacterTableau`, which keeps it free of an
engine dependency and makes it unit-testable (8 tests, including reference-equality assertions that
prove the no-per-frame-allocation guarantee — `AreEqual` would pass on a rebuilt duplicate). Keys live
in a `ConditionalWeakTable` so a finalised tableau's entry is collected rather than pinning an engine
object, and each tableau gets a monotonic serial rather than `RuntimeHelpers.GetHashCode`, which is
reused after GC.

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

---

# Deep review (Review 82b) — findings and Phase 3e RCA

Six agents (Standards, API Compatibility, Efficiency, Completeness, Data Flow, Tooling Correctness)
over the Patch67 instrumentation, its tests, and the two scripts. Completeness and Compatibility
returned clean (47/48 engine members verified, 0 incompatible). All findings below are FIXED.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | **HIGH** | `BuildKey` rebuilt a key string on EVERY rendered frame per live tableau, before the early-out | Hot-path allocation | The earlier adversarial pass explicitly checked for closure/lambda allocation on the steady-state path and correctly found none — but never asked what ran *before* the early return. The allocation was in the first statement. | New lesson (testing-qa): in a per-frame hook, the cheapest possible early-out must be the FIRST statement; audit what precedes it, not just what follows |
| 2 | **GAP** (confirmed) | `Forget` fired only from `SetRace`; nothing hooked `OnFinalize`, so closed tableaus held tracked slots for the session — after 64 characters the instrument goes quiet | Lifecycle completeness | The lifecycle matrix was applied to the *value* (the loading counters, where the sentinel collision was correctly caught) but never to the *container* (the tracker's own entries) | Added `CharacterTableau_OnFinalize_ResidencyReset_Patch`; binding test now pins `OnFinalize` |
| 3 | MED | `RuntimeHelpers.GetHashCode` keys can be reused after GC, so a recycled hash + repeat troop could silently suppress a new tableau's census | Identity collision | "Instance identity" was assumed to mean "hash", which is identity-ish but not unique over time | Monotonic serial per tableau via `ConditionalWeakTable`; serials are never reissued |
| 4 | MED | Two Harmony patch classes in one file, against the `{TargetClass}{TargetMethod}Patch.cs` convention | Standards | Convention not re-read while iterating fast on scaffolding | Split into `CharacterTableau_OnTick_ResidencyCensus_Patch.cs` + `CharacterTableau_ResidencyReset_Patches.cs` |
| 5 | MED | `fix_uruk_hai_hands_teamcolor.py` used plain `utf-8` text read + text write — the forbidden mixed shape | Tooling / XML I/O | **THIRD instance.** The convention lives in `tools/README.md`, which nothing auto-loads, and `moduledata-validation.md` was paths-scoped to *repo* ModuleData only — so authoring a script that edits the game install loaded no rule at all | **Rule scope extended to `tools/**/*.py` + `tools/**/*.ps1`** with the two sanctioned idioms inline. A blocking lint was evaluated and rejected (92/124 existing scripts trip the heuristic) |
| 6 | MED | The `added-flags` branch spliced bare `\n` into CRLF files (dormant — fired on 0 items) | Tooling / line endings | Only the branch that actually ran was reasoned about | Branch now takes the file's detected `eol` |
| 7 | LOW/MED | Target-set derivation used substring containment (`TARGET_MATERIAL in line`), which a future `_a10`/`_a1b` material would false-match | Tooling / matching | "It returns the right 92 today" was treated as correctness | Exact token compare on the Material column |
| 8 | LOW | A comment attributed AV-catchability to `legacyCorruptedStateExceptionsPolicy`, which is set next to the *launcher* exe, not the gameplay process | Comment accuracy | Copied the reasoning from a sibling file's comment without re-verifying | `[HandleProcessCorruptedStateExceptions]` is self-sufficient; noted for the next touch of either file |

## Root-cause pattern

Findings 1, 5, 6 and 7 share one shape: **the code was verified against the path that actually ran,
and the paths that did not run were never examined.** The steady-state early-out was checked for what
follows it but not what precedes it; the tooling was checked on files that happen to have no BOM, on
the branch that happened to fire, and against a material set that happens not to collide today. Each
is correct on today's data and wrong on tomorrow's.

The counter-discipline is to ask, for every check that passes: *what input would make this fail, and
is that input reachable?* That is the same question that killed the original residency hypothesis
earlier in this investigation — and it is now recorded as a testing-qa lesson in its own right.

## Why each agent missed what it missed

- **Standards** caught #4 and correctly exonerated the static tracker and the lazy `IoC.Resolve` for a
  diagnostics boundary class. It has no rule about per-frame cost, so #1 was out of scope.
- **Compatibility** verified all 48 members and caught #8. It does not reason about allocation or
  lifecycle, so #1 and #2 were out of scope.
- **Efficiency** found #1 — the highest-value finding of the pass — but proposed a fix
  (`Dictionary<CharacterTableau, string>`) that would have pinned engine objects for the process
  lifetime, trading a bounded allocation for an unbounded leak. **An agent's diagnosis and its
  prescription need separate verification.**
- **Completeness** returned COMPLETE and was right to; every owed artifact existed.
- **Data Flow** found #2 and #3 by enumerating destruction paths from the decompiled `OnFinalize` —
  exactly the cross-file reasoning the other five structurally cannot do.
- **Tooling Correctness** (Step 2c, launched because the changeset writes outside the repo) found
  #5–#7. None of the five core agents review scripts; without it these ship silently.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/lotrlome-armory-snapshot/README.md](../reference/lotrlome-armory-snapshot/README.md)

<!-- backlinks-end -->

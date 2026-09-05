# Shield `item_usage` audit — TAOM armory vs vanilla 1.4.7

> Audited 2026-08-03 against `LOTRLOME_Armory\ModuleData\LOTRLOME_items\LOTRAOM_shields.xml` and
> vanilla `SandBoxCore\ModuleData\items\shields.xml` (game v1.4.7). Answers "is our
> `hand_shield` / `shield` split wrong?" — short version: the split is mechanically sound, the
> divergence is in **which shield shapes get which grip**. Authoring rules: [armory-guide.md](armory-guide.md).

## What `item_usage` actually selects

`item_usage` is an id in `Native\ModuleData\item_usage_sets.xml`. For shields there are exactly two:
`shield` (strapped to the forearm — kite, heater, oval) and `hand_shield` (centre-grip, held in the
fist — boss-gripped round shields). It picks the **wielder's** grip, stance and block animations, not
the shield's stats.

| | `shield` | `hand_shield` |
|---|---|---|
| Movement set | `shield` | `1h_with_hand_shield` |
| Block actions | `act_defend_shield_{up,down,left,right}_0h_*` | `act_defend_hand_shield_*` |
| Bash action | `act_shield_bash` | `act_hand_shield_bash` |
| `offhand_begin_arm_rotation` | magnitude 0.3 | magnitude 0.4 (arm held further out) |
| Cross-body block arc (`native_parameters.xml`) | `±0.8` on foot, `±0.6` horseback | **`±0.55`, foot and horseback** |
| Tuning block (`combat_parameters.xml`) | `shield_defence` | `hand_shield_defence` |

The arc row is the only one with a gameplay consequence rather than a cosmetic one: a `hand_shield`
blocks a **narrower** cone across the body (`left_horizontal_arc_limit_when_defending_right_side_*` =
`0.8`/`0.6` vs `0.55`/`0.55`), so the same shield reclassified from `shield` to `hand_shield` covers
less.

No managed code branches on the string. `WeaponComponentData.cs:352` reads it raw off the attribute;
`MBItem.GetItemUsageIndex` resolves it natively, and the index is passed as `leftHandUsageSetIndex`
when the **right-hand weapon's** action is resolved. An unregistered usage name therefore fails inside
native animation resolution, not as a managed exception.

## The invariant — enforce this when authoring

**`item_usage="hand_shield"` ⇒ `ForceAttachOffHandPrimaryItemBone="true"`.
`item_usage="shield"` ⇒ `ForceAttachOffHandSecondaryItemBone="true"`.** Never both, never neither.

Zero exceptions in either codebase:

> **Counts re-measured 2026-09-01 and they have moved.** The table below was taken before two item deletions (`easterling_shield`, `rhun_tournament_sparring_shield`, both centre-grip) and the KEYforce cleanup. Parsed today: **224** `Type="Shield"` items, **115** `item_usage="shield"`, **109** `hand_shield`, **54** kite-holstered centre-grip, still **0** rule violations. The numbers below are left as written so the earlier reconciliation still reads, but re-derive before relying on them. The two DO-NOT-FIX entries are unaffected; only one now survives, see the note above.

| Source | `shield` | `hand_shield` | invariant violations |
|---|---|---|---|
| `LOTRAOM_shields.xml` (TAOM) | 115 | 111 | **0 / 226** |
| `SandBoxCore\items\shields.xml` | 64 | 11 | 0 / 75 |
| `SandBoxCore\items\tournament_weapons.xml` | 6 | 1 | 0 / 7 |
| `Native\mpitems.xml` (multiplayer) | 44 | 9 | 0 / 53 |

Secondary conventions, also clean in TAOM: `weapon_class="LargeShield"` on all 226 (vanilla ships no
`SmallShield` at all, so that attribute carries no information); `body_name` is the `bo_cap_*`
collision **capsule** while `shield_body_name` is the full `bo_*` body, paired as
`bo_cap_<stem>` / `bo_<stem>` on 224 of 226 (the two exceptions are documented below and are **not**
to be fixed).

Animation coverage is not a concern either. Vanilla defines `hand_shield` actions in only
`as_human_warrior` and `as_human_female_warrior`, but every TAOM race reaches them: eleven race sets
declare `base_set="as_human_warrior"`, `elf` and `sauron` use it directly, and the one standalone set
(`as_dwarf_warrior`) carries the full surface. `python tools/audit_action_set_parity.py` reports
*"every humanoid set has the full Native as_human_warrior surface (0 gaps)"* across 1304 humanoid sets.

## The real divergence — shape vs grip

| usage × primary holster | TAOM | Vanilla (SP) |
|---|---|---|
| `hand_shield` + `shield_kite` | **56** | 3 |
| `hand_shield` + `shield_round` | 55 | 9 |
| `shield` + `shield` | 103 | 25 |
| `shield` + `shield_kite` | 12 | 38 |
| `shield` + `shield_oval` | 0 | 4 |
| `shield` + `shield_round` | 0 | 3 |

TAOM holds 56 kite-holstered shields centre-grip. Vanilla does that for three items only
(`battania_large_shield_a/b/c`) — so the combination is **legitimate**, just rare: it is the "large
flat shield gripped centrally" pattern, not an error in itself. Whether each of TAOM's 56 belongs
there is a visual call on the mesh, which is why nothing below was changed.

### The 56, grouped by mesh family

Rotation clusters cleanly by family, which is the quickest tell for copy-paste lineage.

| Family | ids | mesh | rotation | length | Note |
|---|---|---|---|---|---|
| Dale | `wm_dale_shield_a01`–`a09`, `b01`–`b06` | `wm_dale_shield_a0*` / `b0*` | `280.00,-62.10,8.70` | 140 | Largest single block. Long (140) + kite holster — strongest reclassification candidate |
| Rhûn Loke-Rim | `sm_rh_loke_shield_{med_a,med_b,heavy_a..d,cav_med_a,cav_med_b,cav_heavy_a}` | `sm_rh_loke_shield_*` | `280.00,-62.10,8.70` | 87–140 | Tower siblings use `shield` — see the anomaly below |
| Rhûn Dragon-Wrath | `sm_rh_drag_shield_*` (same 9) | `sm_rh_drag_shield_*` | `280.00,-62.10,8.70` | 87–140 | |
| Dol Guldur / Khamûl | `sm_dg_khml_shield_*` (same 9) | `sm_dg_khml_shield_*` | `280.00,-62.10,8.70` | 87–140 | |
| Easterling | `easterling_shield`, `rhun_tournament_sparring_shield` | `easterling_shield` | `280.00,-62.10,8.70` | 118 | |
| Rivendell / Noldor | `wm_elven_shield_a`, `wm_elven_shield_b`, `noldor_tournament_sparring_shield`, `wm_gf_knight_shield`, `wm_gf_knight_shield_cloth`, `wm_rivendell_shield_a02`, `_a02_black`, `_a02_silver` | `wm_elven_shield_*`, `wm_gf_knight_shield*`, `wm_rivendell_shield_a02*` | `-80,-82,-0.08` | 146–148 | Longest in the set (148); elven kite/leaf shields |
| Rohan (CTS) | `cts_rohan_shield`, `cts_rohan_shield1`, `cts_rohan_shield2` | `cts_rohan_shield*` | `254.50,-62.10,8.70` | 118 | Only family on this rotation |
| Théoden | `wm_theoden_shield` | `wm_theoden_shield` | `-80.0,-90.0,-10.0` | 88 | Shares the rotation of the 55 round Rohirrim `hand_shield`s — short (88), so the kite holster is probably the odd attribute, not the grip |

To decide per family, spawn the troops and look at the block pose. Reclassifying means changing three
things together — `item_usage`, the offhand-bone flag, and the rotation — never `item_usage` alone.

## DO NOT FIX

Two entries look like obvious typos and are not safe to correct. Both were checked against the
packaged PhysicsShape table of contents (8262 entries across 160 `.tpac` files, 0 unparsed).

**`wm_isengard_shield_a04` (line 434) — `body_name="bo_capwm_isengard_shield_a02_clean"`**

Missing underscore. The asset is **packaged under that exact misspelled name**; the corrected spelling
does not exist anywhere:

```
bo_capwm_isengard_shield_a02_clean    present? True
bo_cap_wm_isengard_shield_a02_clean   present? False
```

"Fixing" the XML converts a resolving reference into a missing collision body — the defect class
behind the #352 infinite mission-load hang. Leave it misspelled.

> **STALE as of 2026-09-01.** `gond_shld4` no longer exists: KEYforce's `5cd6115a` deleted the item definition. Nothing references it. The `wm_isengard_shield_a04` entry below is UNAFFECTED and still load-bearing: its `bo_capwm_isengard_shield_a02_clean` really does ship under that misspelling, and correcting it would break the shield.

**`gond_shld4` (lines 740-741) — `body_name` == `shield_body_name` == `bo_wm_gondor_shield_a`**

The only item using its full body as its own capsule. There is no correct value to substitute:
`bo_cap_wm_gondor_shield_a` does not exist. The only Gondor capsules in the TOC —
`bo_cap_wm_gondor_shield_a02`, `_aa`, `_a_cair_andros`, `_a_minas_ithil` — all belong to different
meshes, so borrowing one would recreate the Boromir defect below. Leave it.

## Open — anomalies documented, not changed

**12 Rhûn / Dol Guldur tower shields keep a `hand_shield` rotation.**
`sm_rh_loke_shield_tower_{med_a,med_b,heavy_a,heavy_b}`, `sm_rh_drag_shield_tower_*`,
`sm_dg_khml_shield_tower_*` are `item_usage="shield"` with the Secondary bone, but carry
`rotation="280.00,-62.10,8.70"` — the value their 44 `hand_shield` siblings use. The same rotation on a
different bone yields a different world pose, so this reads as a copy-paste that changed usage and
flags without updating rotation. No replacement is derivable from the data: sibling `shield` items use
six different rotations (`0.0,10.0,40.00` ×31, `0.0,20.0,40.00` ×27, `0.0,20.0,43.00` ×19,
`-0.10,-20.0,197.00` ×16, `-10.0,-05.0,-90.00` ×8, `-12.0,10.0,-02.00` ×2). Needs an in-game look.

**A commented-out `rohan_horse_shield` sits at lines 3062+.** It is inside a `<!--<Item` block. This is
why a raw grep reports 227 `Type="Shield"` and 112 `item_usage="hand_shield"` while an XML parse sees
226 and 111. Grep-based counts of this file are off by one; parse it.

## Fixed

**`wm_boromir_shield` (line 1161)** — `body_name` was `bo_cap_wm_rohan_shield_a01`, the capsule of a
*round* Rohirrim shield used by 14 `hand_shield` items, on an item that is `item_usage="shield"` with
`weapon_length="90"`. It was the only shield in the file whose capsule stem did not match its own
`shield_body_name`. Its own capsule `bo_cap_wm_boromir_shield` is packaged and was simply unwired;
`body_name` now points at it.

> `LOTRAOM_shields.xml` lives in the game's Modules directory and is in no git repository. This edit
> is unversioned — an Armory dependency refresh reverts it silently, and this section is the only
> record of what to re-apply.

## Reproducing this audit

```bash
# collision bodies + meshes all resolve (Tier C is the body check)
python tools/validate_mesh_refs.py --scan-bodies --code MISSING_BODY

# every humanoid race has the full hand_shield/shield action surface
python tools/audit_action_set_parity.py
```

Neither tool checks shield *attributes*: `validate_mesh_refs.py` owns `body_name` /
`shield_body_name` existence only, and `validate_all_troop_refs.py` skips shields by design (its
`ARMOR_PREFIX_RE` matches `sk_*`/`ar_*` only, never `wm_*`/`sm_*`). The usage↔offhand-bone invariant
above has no automated check — it was verified by ad-hoc parse for this audit.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/black-numenorean.md](../features/black-numenorean.md)
- [docs/features/mesh-ref-validation.md](../features/mesh-ref-validation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/items-shields.md](../modding/items-shields.md)
- [docs/modding/items-weapons-and-crafting.md](../modding/items-weapons-and-crafting.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/reference/armory-guide.md](./armory-guide.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)

<!-- backlinks-end -->

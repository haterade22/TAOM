# `item_usage_features` / `excluded_item_usage_features` reference

How a smithed weapon gets its animation set, which tokens exist, and the authoring rule that keeps a
crafting piece from advertising an attack it cannot perform. Verified against installed v1.4.7 data
and the matching decompile (2026-07-26).

Read this before adding a `<CraftingPiece>` blade head. The one-line summary: **a swing-only head
whose weapon description carries a `thrust` token must exclude it, or the crafted weapon gets a
thrust attack with no thrust damage.**

## Mechanism

| Step | Where |
|---|---|
| `excluded_item_usage_features` parsed verbatim into `CraftingPiece.ItemUsageFeaturesToExclude` (raw string, `:`-separated) | `TaleWorlds.Core/CraftingPiece.cs:204-205` |
| `WeaponDescription.ItemUsageFeatures` (`item_usage_features` in `weapon_descriptions.xml`) split on `:`; every token excluded by **any** used piece removed; survivors joined with `_` | `Crafting.cs:423-447` (`GetItemUsage()`) |
| Result becomes the crafted weapon's `item_usage` | `Crafting.cs:216` (`weapon.Init(…)`) |
| Name resolved against the 58 sets in `Native/ModuleData/item_usage_sets.xml` (native side) | — |

Four consequences that drive the authoring rules:

- **Tokens are name fragments, not capability flags.** The join must land on an id that exists in
  `item_usage_sets.xml`. `polearm:block:long:shield:swing:thrust` minus `swing` is
  `polearm_block_long_shield_thrust` — a real set. Minus `thrust` it would be
  `polearm_block_long_shield_swing`, which **does not exist**.
- **Nothing validates the result.** `WeaponComponentData` just stores the string
  (`WeaponComponentData.cs:33,178`); no assert fires on an unknown name. Unknown *tokens* are also
  silently ignored — vanilla's own `spear_blade_14` ships
  `excluded_item_usage_features="swing:TwoHandedPolearm_Bracing"`, where the second token is a
  weapon-description id and does nothing.
- **Exclusions are unioned across every piece in the weapon**, not applied per piece. Auditing pieces
  one at a time is insufficient; see "Verifying" below.
- **It affects smithed weapons only** — including pre-authored `<CraftedItem>` entries, which are
  recomposed from their piece list on every load (`ItemObject.cs:469` →
  `Crafting.CreatePreCraftedWeaponOnDeserialize`, `Crafting.cs:1066`). Plain `<Item>` weapons set
  `item_usage="…"` directly (`WeaponComponentData.cs:352`) and are unaffected.

## Token vocabulary

Every excludable token is one that appears in `item_usage_features` across the 22 native
`WeaponDescription`s. Neither TAOM nor LOTRLOME adds a description — LOTRLOME's
`weapon_descriptions.xslt` only extends their `<AvailablePieces>` lists, and TAOM ships no
`weapon_descriptions` override of its own. A new piece registration therefore belongs in the
Armory's XSLT, beside the other 19 cultures' (`tools/register_one_handed_polearms.py` is the
worked example, and the reason that external edit needs a replay script).

| Token | Kind | What removing it does |
|---|---|---|
| `onehanded`, `onehanded_polearm`, `polearm`, `twohanded`, `throwing` | root | Base identity — never excluded |
| `swing` | attack | Drops the swing `attack_up/left/right` usages → thrust-only (spear heads) |
| `thrust` | attack | Drops the thrust `attack_down/up` usages → swing-only (mace / cleaver heads) |
| `block` | defence | Weapon `defend_*` usages (parry with the weapon). Never excluded in shipped data |
| `shield` | left hand | Lands on a set flagged `requires_no_shield` — shield no longer usable |
| `rshield` | left hand | The `requires_shield` sets (bastard weapons' 1H-with-shield mode) |
| `long` | mounted carry | Loses the mounted spear/lance carry idles that don't need a free left hand (`act_rider_idle_spear_*`, `…_lance_*`). No effect on attacks or blocking |
| `widegrip` | grip | `twohanded_widegrip_axe` (staff animations, 4-way `act_guard_*_staff`) → `twohanded_axe` (2H-sword animations, up/down `act_guard_*_2h`). Both allow mounted swings — this is a feel choice, not a capability |
| `tipdraw` | animation | The mace draw/attack family |
| `couch` / `bracing` / `pike` / `thrown` | mode | `polearm_couch` (`requires_mount` + `passive_usage`), `polearm_bracing` (no mount, no shield, passive), `polearm_pike` (up/down thrust only), `polearm_thrown` (javelin-derived) |
| `axe`, `dagger`, `knife`, `javelin` | leaf class | Name their own sets; excluding one only works if the shortened name exists |

Only five distinct values appear in all shipped data (SP + MP + LOTRLOME): `swing`, `thrust`, `long`,
`widegrip`, `shield:thrust`.

## The authoring rule (vanilla's own convention)

Vanilla is exact and unanimous about swing-only heads — those declaring `<Swing>` with no `<Thrust>`:

| Family | Count | Excludes `thrust`? |
|---|---|---|
| `mace_*` | 30 | **Yes, all** |
| `cleaver_*`, `battania_*`, `sickle_*` | 6 | Yes |
| swing-only `spear_*` | 7 | Yes |
| `blacksmith_hammer_tip_1` | 1 | Yes |
| `axe_*` | 41 | **No — correctly** |

**The rule:** exclude `thrust` when the head has no `<Thrust>` **and** its weapon description carries
a `thrust` token. Axe heads are the exception that proves it — `OneHandedAxe` is
`onehanded:shield:axe` and `TwoHandedAxe` is `twohanded:widegrip:axe`, neither of which has a `thrust`
token, so there is nothing to remove. The mirror rule applies to `swing`: a thrust-only head
(`<Thrust>`, no `<Swing>`) in a description carrying `swing` must exclude it.

**The trap this creates.** The swing-only axe-head example in
[weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md) correctly omits the
attribute. Copying that shape for a **mace** head is wrong, because `Mace` is
`onehanded:block:shield:tipdraw:swing:thrust`. That is exactly how TAOM shipped 20 mace heads with a
0-damage thrust attack (`BladeData` defaults `ThrustDamageType` to `DamageTypes.Invalid` and the
factor to 0 — `BladeData.cs:39`), fixed 2026-07-26. Naming does not decide this — several of those
heads are named "Orc Axe" but are authored into the `Mace` description, and the description is what
counts.

A head declaring damage it then excludes is also wrong in the other direction: vanilla ships **zero**
blades that declare `<Thrust>` while excluding `thrust`. If the exclusion is intended, delete the
damage element; the item card otherwise advertises a stat the animation set cannot deliver.

## Verifying

Because exclusions are unioned across all pieces in a weapon, a correct audit enumerates
*combinations*, not pieces:

1. Build the effective piece list per `WeaponDescription`, honouring that
   `weapon_descriptions.xslt` **appends to** `<AvailablePieces>` for the descriptions it targets —
   every one of its 15 override templates ends with `<xsl:apply-templates select="@*|node()"/>`,
   which copies the vanilla entries it matched straight through. Measured 2026-08-10 by running the
   real chain (Native XML → the Armory's XSLT) under lxml: `OneHandedPolearm` emerges with 364
   entries — 130 from the Armory, 234 from vanilla — and the merged document holds 5,067
   `AvailablePiece` entries in total. This paragraph previously said the transform *replaced* the
   lists wholesale and "re-lists zero vanilla piece ids"; that was wrong, and it matters because an
   audit built on it would miss every vanilla piece a mod weapon can legally use.
2. Group each description's pieces by slot (`Blade` / `Guard` / `Handle` / `Pommel` — a weapon uses at
   most one per slot, so only cross-slot unions are reachable).
3. For every cross-slot combination, union the exclusion sets, remove them from the description's
   `item_usage_features`, join with `_`, and confirm the result is one of the 58 set ids.

A description only composes when every used piece belongs to its own `AvailablePieces`
(`Crafting.GenerateCraftedItem`, `Crafting.cs:566-610`), which is what makes that per-description
grouping the correct gate.

**State as of 2026-07-26:** 47 reachable combinations → 27 distinct names → **0 missing**. Vanilla
baseline: 66 → 35 → 0. Of TAOM's 41 pieces carrying the attribute, 51 (piece × description) pairs are
effective and 10 pieces are inert everywhere they appear.

**Inert is not a defect.** Vanilla ships 17 fully-inert exclusions of its own 93 —
`mace_head_31`–`mace_head_39` exclude `thrust` while appearing only in `TwoHandedMace`
(`twohanded:axe`, no `thrust` token), and five `spear_handle_*` exclude `long` while appearing only in
Javelin descriptions. Tagging a head by its own nature rather than by its description's token list is
deliberate practice; do not "clean up" inert exclusions.

## Where pieces are authored

`Modules/LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml` — a **live game-folder file, not
git-tracked**, like the rest of the Armory item defs. Back it up before scripted edits and follow the
XML I/O convention in [`tools/README.md`](../../tools/README.md).

Through the generator path, the attribute passes through from the manifest: put
`excluded_item_usage_features="thrust"` on the `<Blade>` element of a `<CraftedWeapon>` and
`render_pieces.py` emits it on the `<CraftingPiece>` open tag — see
[weapon-xml-pipeline.md](../features/weapon-xml-pipeline.md).

## Related

- [weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md) — the manual authoring walkthrough
- [weapon-xml-pipeline.md](../features/weapon-xml-pipeline.md) — the generator
- [rca-crafting-usage-features-2026-07-26.md](../reviews/rca-crafting-usage-features-2026-07-26.md) — how the 20 mace heads shipped unexcluded

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md)
- [docs/features/weapon-xml-pipeline.md](../features/weapon-xml-pipeline.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](./doc-lookup.md)
- [docs/reviews/lessons/xslt-moduledata.md](../reviews/lessons/xslt-moduledata.md)
- [docs/reviews/rca-crafting-usage-features-2026-07-26.md](../reviews/rca-crafting-usage-features-2026-07-26.md)

<!-- backlinks-end -->

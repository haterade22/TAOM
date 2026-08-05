# RCA — crafting-piece `excluded_item_usage_features`: 20 mace heads shipped a 0-damage thrust

**Date:** 2026-07-26 · **Issue:** not yet filed (precedent for Armory data fixes: #213) · **Scope:** `LOTRLOME_Armory/ModuleData/LOTRLOME_crafting_pieces.xml` (live, not git-tracked)
**Reference written from this incident:** [item-usage-features.md](../reference/item-usage-features.md)

## Top-line

Every one of the 20 swing-only blade pieces in TAOM's `Mace` weapon description shipped without
`excluded_item_usage_features="thrust"`. Vanilla tags **30/30** of its own mace heads. The composed
animation set was therefore `onehanded_block_shield_tipdraw_swing_thrust` — a set that includes thrust
attacks — while `BladeData` left `ThrustDamageType = DamageTypes.Invalid` and the factor at `0`
(`BladeData.cs:39`, consumed `Crafting.cs:135`/`216`). Result: a thrust attack that deals nothing, on
19 shipped `<CraftedItem>`s carried by ~60 troop entries across Mordor, Isengard, Gundabad, Goblin,
Misty Mountain Orcs, Dol Guldur and Rhûn, plus anything a player smiths from those heads.

**No gate could have caught it.** It was surfaced by a user question about what the attribute does —
not by a crash, a log line, a test, or a validator. There is no crash, no assert, no warning: the
engine accepts any composed name, and `WeaponComponentData` merely stores the string
(`WeaponComponentData.cs:33,178`).

Fixed the same session: the 20 heads gained the attribute, and one contradictory piece
(`wm_isengard_berserker_sword_a01_blade`, which excluded `thrust` while declaring `<Thrust>` Pierce
1.76) had the vestigial damage element removed — vanilla ships zero blades in that state.

## Root cause

**The authoring convention was undocumented, and the nearest documented example teaches the opposite.**
[weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md) carried exactly one line about
the attribute (*"`swing` to make a head thrust-only (spears)"*) — 1 of the 5 values in shipped data, and
nothing about `thrust`. Its **swing-only axe-head example correctly omits the attribute**, because axe
descriptions (`onehanded:shield:axe`, `twohanded:widegrip:axe`) carry no `thrust` token. Copying that
shape to a mace head is wrong, because `Mace` is `onehanded:block:shield:tipdraw:swing:thrust`. Several
of the 20 heads are even named "Orc Axe" while being authored into `Mace` — **the description decides,
the name misleads.**

Three contributing properties:

1. **The tokens are name fragments, not flags.** `GetItemUsage()` (`Crafting.cs:423-447`) removes
   tokens and joins the survivors with `_` to form an `item_usage_sets.xml` id. Nothing checks the
   result resolves; an author has no feedback signal.
2. **The generator passes the attribute through but never infers it.** `manifest.py:176-186` collects
   unrecognised `<Blade>` attributes into `PieceSpec.attrs` and `render_pieces.py:58-61` emits them, so
   the sanctioned pipeline *can* express it (proven by rendering a synthetic piece) — but it does not
   derive it from the absence of `<Thrust>`, so a manifest author who doesn't know the rule ships the
   bug through the automated path too.
3. **No validator covers it.** `validate_moduledata.py` checks cross-references, not
   damage-vs-usage-set coherence. This is the #352 shape again — *silent wrong data in a crafting
   piece* — minus the load hang that eventually forced #352 into the open.

## Findings from the review pass (2026-07-26, 3 adapted agents)

The skill's five core agents were skipped deliberately: they check ADR compliance, TaleWorlds
signatures, hot-path allocations, and tests/IoC, and this changeset is XML-only with no repo file
touched. Step 2c expansion ran three agents matched to the risk instead.

| # | Sev | Finding | Category | Why missed | Outcome |
|---|-----|---------|----------|------------|---------|
| 1 | MED | The one-off fix script read with plain `utf-8` and wrote in text mode — the exact shape `tools/README.md:19` forbids (leaves a stray `U+FEFF` on a BOM'd file). Harmless here: verified no BOM, non-ASCII runs byte-identical, LF preserved | tooling-io | `.claude/rules/moduledata-validation.md` is paths-scoped to **repo** ModuleData dirs; the edited file lives in the game install, so no convention loaded. `tools/README.md`'s XML I/O rules are not auto-loaded by any rule | Recorded; script is scratchpad-local and already run. Lesson appended below |
| 2 | MED | Same script had no internal `.bak` write (`tools/README.md:21`) and no post-write parse check before overwriting a live, non-git file | tooling-safety | Same root cause as #1. Both safety nets were supplied out-of-band (manual backup taken, parse verified after) | Recorded; same lesson |
| 3 | LOW | `sm_rh_loke_*` / `sm_rh_drag_*` / `sm_dg_khml_*` mace items are tagged `culture="Culture.khuzait"` despite Rhûn / Khamûl naming | data-content | Pre-existing, unrelated to this change | Not touched — follow-up candidate |
| 4 | LOW | `wm_sauron_mace`'s `sauron_civ_equipment` / `sauron_bat_equipment` rosters have no discoverable consumer (no NPCCharacter, no C#, no other XML) | data-content | Pre-existing | Not touched — follow-up candidate |
| 5 | LOW | `crafting_templates.xslt`'s `UsablePieces` disagrees with `weapon_descriptions.xslt`'s `AvailablePieces` for `TwoHandedPolearm`, `OneHandedAxe`, `TwoHandedAxe`, `Pike`, `TwoHandedMace` — pieces selectable in the smithy that never produce a named item, or vice versa. `Mace` and both sword templates match exactly | xslt-reachability | Out of scope for this ask; the usage-set audit used the authoritative `AvailablePieces` gate | Not touched — the strongest follow-up candidate |
| 6 | LOW | Possible negligible value shift on the two berserker swords from removing the thrust term in `CalculateTierMeleeWeapon`'s `Max()` — the swing term's coefficient was already >2× the thrust term's, so almost certainly no-op | balance | Reasoned from the formula, not simulated | Accepted unproven |

Zero HIGH or CRITICAL. The one plausible HIGH — that a blade with no `<Thrust>` might fail to compose
a `WeaponComponent`, which makes `ItemObject.Deserialize:472` unregister the item and substitute Trash,
and one affected item sits in Isengard's `banner_bearer_replacement_weapons` (`taom_spcultures.xml:1552`,
live #360 territory) — was ruled out by named vanilla precedent: `star_falchion_sword_t3` and
`pointed_falchion_sword_t4` (OneHandedSword, `cleaver_blade_3/4`), `reaper_falx` and
`cleaver_2hsword_t3` and `battania_2hsword_4_t4` (TwoHandedSword, `battania_blade_6/7`) are all shipped
items built from swing-only blades on exactly those templates.

## The audit gap this pass closed

The first-cut audit checked each piece against each description **individually**. That is
structurally insufficient: `GetItemUsage()` unions the exclusions of **every piece in the weapon**, so
a cross-slot combination can produce a name no single piece would. Re-run as a per-slot cross-product:
**47 reachable combinations → 27 distinct names → 0 missing** (vanilla baseline 66 → 35 → 0). For
`Mace` only one union is reachable, because its 37 pieces are 20 Blades and 17 Handles with no Guard or
Pommel.

**Also corrected during the pass:** an earlier count of "682 pieces" came from
`grep -c '<CraftingPiece'`, which also matches the `<CraftingPieces>` root line. The real count is
**681**, identical before and after the edit.

## What was NOT changed, and why (vanilla comparison)

Two candidate defects turned out to be idiomatic vanilla practice. Recording them so a future pass
doesn't "fix" them:

- **10 fully-inert exclusions stay.** Vanilla ships **17 of its own 93** — `mace_head_31`–`mace_head_39`
  exclude `thrust` while appearing only in `TwoHandedMace` (`twohanded:axe`, no `thrust` token), five
  `spear_handle_*` exclude `long` while appearing only in Javelin descriptions, plus `spear_blade_16`
  and `sickle_blade_1`. Tagging a head by its own nature rather than its description's token list is
  deliberate.
- **The `widegrip` spread stays.** It selects an animation family, not a capability — both sets allow
  mounted swings; `twohanded_widegrip_axe` uses staff animations (4-way `act_guard_*_staff`) and
  dropping it selects the 2H-sword family (up/down `act_guard_*_2h`). 8 of 32 `TwoHandedAxe` handles
  exclude it with no length rule (96.04 excludes, 96.40 keeps; 145 excludes, 160–180 keep).
  Inconsistent feel, not a defect — a per-weapon judgement call, not a scripted sweep.

## Preventive actions

**Done this session:**

- **New reference** [`docs/reference/item-usage-features.md`](../reference/item-usage-features.md) —
  mechanism with file:line, full token table, the vanilla convention table, the reachable-union audit
  method, and the inert-is-not-a-defect warning.
- **`weapon-creation-workflow.md`** — the axe-head example now carries an explicit
  do-not-copy-this-to-a-mace-head callout; the field-reference row states both directions of the rule;
  a one-line rule added after the halberd note.
- **`weapon-xml-pipeline.md`** — documents that unrecognised piece attributes pass through verbatim,
  that the attribute is mandatory for a swing-only head in a `thrust`-bearing description, and that the
  generator neither infers nor validates it.
- **Lessons** appended to `lessons/xslt-moduledata.md` and `lessons/build-tooling-workflow.md`.

**Candidate, not built:** a `validate_moduledata.py` check (or a standalone auditor) that recomputes
every reachable piece × description usage-set name and flags (a) a name absent from
`item_usage_sets.xml`, (b) a head declaring damage for an attack its composed set lacks, (c) a
swing-only or thrust-only head missing the exclusion its description needs. All three are mechanical —
the audit logic already exists in this session's transcript and would fit the schema-driven validator.
Worth doing before the next culture weapon pass.

## Verification state

- Diff-derived change set: 21 hunks, all intended (20 attribute insertions + one 3-line `<Thrust>` deletion).
- Parses under both PowerShell `[xml]` and ElementTree; 681 pieces and 681 unique ids before and after; no BOM introduced; LF endings and all 27 non-ASCII byte runs preserved.
- Audit deltas: swing-only heads missing `excluded=thrust` **20 → 0**; exclusion-vs-damage contradictions **1 → 0**; effective (piece × description) pairs **31 → 51**; invalid usage-set names **0 → 0**.
- Save compatibility: crafted items recompose from their piece list every load (`ItemObject.cs:469` → `Crafting.CreatePreCraftedWeaponOnDeserialize`, `Crafting.cs:1066`); ids unchanged, so existing saves pick this up with no migration.
- **Owed:** in-game smoke — craft or spawn a Gundabad/Mordor/Dol Guldur mace and confirm the thrust attack is gone and the item card shows swing only. GitHub issue still to file.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/item-usage-features.md](../reference/item-usage-features.md)
- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/xslt-moduledata.md](lessons/xslt-moduledata.md)

<!-- backlinks-end -->

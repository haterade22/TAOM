# RCA — Erebor / Iron Hills equipment sweep + noble crossbow uplift (2026-07-30)

/ deep-review, 4 agents (tooling correctness, game-data correctness, data flow + XML integrity,
completeness). 11 confirmed findings, 6 of them HIGH. Every finding below was re-verified against
the source by the orchestrator before being actioned, per `.claude/rules/evidence-over-claims.md`.

**Nothing shipped broken** — the review ran before commit and all six HIGH findings are fixed in the
same session. But four of the six were *silent* balance corruption: the XML was valid, both
validators passed, the build and 4,529 tests were green, and the file would have looked correct to
every automated gate in the repo.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | "Prefer the globally least-used item" systematically selected end-tier exclusives. 25 items dropped ≥10 levels; `sk_dwarf_iron_chest_elite_d` went from the level-46 royal warden to a **level-11 recruit** (−35) | Game data / balance | Treated rarity as "underused" when rarity was the *tier marker*. The whole objective function was inverted for exactly the items it most wanted to place | Derive a per-item tier floor from the lowest-level troop already wearing it; never place below it. New lessons entry (below) |
| 2 | HIGH | `sm_dwarf_erebor_1h_axe_b2` (damage 2.96, the strongest 1H axe) spread from level-46-only to seven troops down to level 21 | Game data / balance | Same-stem was assumed to imply same-stats. It does not: within `1h_axe` the damage factor spans 2.28–2.96 and blade length 20–44 | Weapon reach/damage band (±10%) alongside the armour band |
| 3 | HIGH | `sm_dwarf_erebor_axe_2h_a2` (blade length **20.17** vs 40–44 for every sibling) given to the level-36 Mountain Guard and level-36 Veteran Axe-Guard, while the level-21 arbalest's *sidearm* got the 43-unit 2.96-damage one | Game data / balance | Crafted melee items have **no `<Weapon>` element** — reach and damage live on the blade crafting piece. The stat check read `<Weapon>`, found nothing, and silently compared nothing | Load `CraftingPiece` `BladeData` and compare `blade_length` / `damage_factor` |
| 4 | HIGH | Shield inversion: the largest metal shield dropped level 46 → 21; the level-36 shield specialist lost 2 of 3 rosters to smaller shields; the biggest tower shield in the data debuted at level 11 | Game data / balance | Weapon slots skipped the armour tolerance entirely and had no substitute check | Covered by the same reach/power band |
| 5 | HIGH | The three new Crossbow values (175/225/275) are off-formula; `tools/rebalance_troops.py --dry-run` wanted all three back at 130/170/205. The next `--apply` would have reverted the fix silently | Tooling / durability | Never asked whether another tool *owns* the field being hand-edited | Ids added to `SKIP_TROOP_IDS` + recorded in `docs/features/troop-skill-balance.md`. New lessons entry (below) |
| 6 | HIGH | Script not idempotent — a second run planned 3 further substitutions that placed zero new items | Tooling | Single-pass greedy over a counter mutated during the same pass is not at its fixpoint. Never re-ran the tool after applying | Iterate `build_plan` to a fixpoint; re-run now plans 0 |
| 7 | MED | `utf-8-sig` on **write** prepends a BOM unconditionally (the read side is conditional). Harmless here; 11 of 16 sibling troop files have no BOM and this script is an obvious retarget template | Tooling | Assumed the codec was symmetric | Detect `had_bom`, `write_bytes` with the BOM re-prepended — the documented `tools/README.md` idiom |
| 8 | MED | No backup before overwriting a tracked game-data file that had uncommitted edits | Tooling | Leaned on git without checking the file was clean | Writes `.bak-ereborsweep` (non-`.xml`, so the engine glob ignores it) |
| 9 | MED | Index-drift guard checked only the 272 planned indices. A drift landing on another copy of the *same* id passes silently and writes into the wrong roster — and this file is full of repeated ids by construction | Tooling | Spot-check mistaken for a proof | Assert full count **and** full sequence parity between the regex scan and the ElementTree walk |
| 10 | MED | CHANGELOG stated all five unplaced items were "worn only by `iron_hills_noble_*` troops". True for 3 of 5 — one family is worn by four non-noble troops, and one item has no stem sibling at all | Fabrication (`evidence-over-claims` §C) | Wrote a causal explanation from a plausible mental model instead of querying the data. The *count* was verified; the *reason* was invented | Rewritten with the real per-item causes |
| 11 | LOW | Save-compat line claimed "troops already in a party are unaffected" | Correctness of prose | Troop skills live on the shared `CharacterObject`, rebuilt from XML each launch | Corrected |

## Root-cause pattern: the stat check that read nothing

Findings 1–4 are one bug wearing four hats. The sweep's safety argument was "only swap items that are
equivalent," and equivalence was measured by `<Armor>` totals. That measure is:

- **absent** for weapons and shields (no `<Armor>` element) → findings 2, 3, 4;
- **blind to material** — mail and plate score the same and look nothing alike → finding 1;
- **blind to tier**, because the armoury has no tier field and the names actively lie
  (`sk_dwarf_iron_chest_heavy_e` = 118 armour outranks `..._elite_f` = 100).

The deeper error is that I *noticed* the weapon gap and guessed past it. Mid-implementation I checked
within-class weapon stats, saw that crafted melee items had no `<Weapon>` element, wrote that their
stats "derive from crafting pieces… likely comparable," and moved on. `.claude/rules/troops.md`
already says names do not imply tier and to grep the stats before tier-ordered picks. The rule was
loaded, quoted in the plan, applied to *ranged* weapons — and then not applied to melee, because the
stats were one indirection away instead of on the item.

**"I could not find the stat" is not evidence the stat does not matter.** It is evidence the lookup
is not finished.

## Why each review agent caught or missed these

| Agent | Outcome |
|---|---|
| Game-data correctness | **Caught 1–4.** The only agent asked to compare old vs new *semantically* — armour totals, blade stats, min-user-level per item — rather than check that the file is well-formed. Every HIGH balance finding came from here |
| Tooling correctness | **Caught 6–9.** Step 2c of `/deep-review` mandates this agent when a changeset adds file-writing `tools/**/*.py`; it earned its place twice over. It ran the tool a second time, which is how non-idempotency surfaced |
| Data flow + XML integrity | **Caught 10.** Verified 17/17 CHANGELOG numbers — the numbers were all real, which is exactly why the one *unverified causal claim* stood out. Structurally could not catch 1–4: every item id resolves, so integrity checks pass on a tier-inverted file |
| Completeness | **Caught 5, 11.** Found the rebalancer conflict by asking "does another tool own this field?" — a question none of the other agents' rule sets contain |
| Standards / API compatibility | Not launched. No C# in the changeset; adapter, Harmony-category and GameModel rules had nothing to bind to. Recorded rather than run to produce noise |

The gap worth naming: **all four automated gates passed on the broken version.**
`validate_moduledata.py` PASS, `validate_all_troop_refs.py` PASS, build clean, 4,529 tests green — on
a file that put royal-warden plate on a level-11 recruit. Referential integrity and tier sanity are
orthogonal, and TAOM has a validator for the first and nothing for the second.

## Lessons to codify

Two entries for `docs/reviews/lessons/data-content-cultures.md`:

### Rarity is a tier signal, not an underuse signal

**Why missed:** An equipment-variety sweep optimised for "spread the least-used items" and reached
straight for end-tier exclusives — they are rare *because* only one level-46 troop wears them.
25 items dropped ≥10 levels, worst −35.
**Prevent:** When redistributing game content by usage frequency, derive a tier floor per item from
the lowest-level entity already using it, and never place below that floor. In an armoury with no
tier field, existing assignments *are* the tier data.
**Source:** `docs/reviews/rca-erebor-equipment-sweep-2026-07-30.md` findings 1–4.

### A stat you cannot find on the item may live one indirection away

**Why missed:** Dwarf melee weapons are `<CraftedItem>` with no `<Weapon>` element; reach and damage
live on the referenced `CraftingPiece` `BladeData`. A stat comparison read `<Weapon>`, found nothing,
and silently compared nothing — so a 20-unit stub blade and a 44-unit greataxe looked identical.
**Prevent:** When a stat lookup returns empty for a whole class of items, treat that as an unfinished
lookup, not as "no constraint." `.claude/rules/troops.md` already mandates grepping weapon stats
before tier-ordered picks; extend the habit to the piece tables for crafted items.
**Source:** same RCA, finding 3.

One entry for `docs/reviews/lessons/build-tooling-workflow.md`:

### Before hand-editing a generated field, check whether a generator owns it

**Why missed:** Three Crossbow values were hand-tuned in `troops_erebor.xml` without checking that
`tools/rebalance_troops.py` derives that exact field from level + culture modifiers. Its `--dry-run`
wanted all three reverted; the next `--apply` would have undone the fix with no warning.
**Prevent:** When hand-editing a value in generated or regenerable data, grep `tools/` for a script
that writes that field. If one exists, add the id to its skip list **and** record the residual in the
feature doc in the same commit.
**Source:** same RCA, finding 5.

## Outcome

| | Before review | After |
|---|---|---|
| Substitutions | 272 | 200 |
| Dead items placed | 33 | 15 |
| Items dropping ≥10 wearer levels | 25 | **0** |
| Idempotent | no | yes (re-run plans 0) |
| Crossbow values survive `rebalance_troops.py --apply` | no | yes |

Fewer items placed is the correct trade: the 23 that remain are genuine end-tier gear whose only home
is the single-roster `iron_hills_noble_*` line, plus `sm_iron_shield_b_gold` (no stem sibling) and
`sm_dwarf_iron_hammer_e` (43 units of reach against 18–24 for every other dwarf hammer — a
weapon-balance decision, not a variety swap). Placing them needs roster structure changes or a
deliberate authoring call, both out of scope for an automated sweep.

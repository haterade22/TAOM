# RCA: melee tier tolerance and the shield view (#631, 2026-11-16)

## Top-line

Two rule changes to the melee ladder went through one `/deep-review` (tier 2: Data flow and Tooling
lenses, effort high; Codex skipped, out of usage). No finding was HIGH. The one MEDIUM: the shield
rule reached the validator, the fixer and the restat, but not `generate_placeholder_weapons.py`,
which keeps its own copy of the limit checks. A placeholder for shield carriers was banded and
capped on the two handed mode its carriers never use, and the validator, which judges them in the
one handed mode, then flagged `taom_ph_gundabad_spear_a03_t5` (`MELEE_BLOW_CEILING`).

The changes under review:

- **Tier tolerance.** `melee_ladder.judge` accepts a weapon under its carrier's band when it sits in
  the band of a tier up to `under_tolerance_tiers` (2) below, because weapon skill adds damage and
  speed every tier (the maintainer's ruling of 2026-11-16).
- **The shield view.** A troop with a shield is judged on the modes a shield allows
  (`melee_catalogue.Priced.with_shield`), and a spear's one handed thrust has its own
  `OneHandedPolearm` class limit from vanilla.

Measured on the live Armory (`python tools/validate_moduledata.py`): `MELEE_LADDER_INVERSION` 270 to
125, `MELEE_BLOW_CEILING` 11 to 10 after the fix round (8 on the shared Erebor blades, by an earlier
decision; `wm_gundabad_spear_a03` and `wm_rivendell_spear_a01_black` until the planned Armory
restat lowers them).

## Root cause of the MEDIUM

The plan listed the ladder's consumers by the calls that judge troops (`findings`, `judge`,
`band`). The placeholder generator does not call those for its cap: it calls `tier_limit` and
`max_factor` row by row (`row_breaches`, `attack_cap`). A grep for the judging calls did not find it.

## Findings

| # | Severity | Finding | Outcome |
|---|---|---|---|
| F1 | MEDIUM | The placeholder generator priced shield carriers on the whole weapon | Fixed: `row_views`, `shield_carriers`; the band and every row cap use the wielded view, and a shield view also keeps the whole weapon in its class limit. Three tests (`PlaceholderShieldCarrierTests`). `taom_ph_lothlorien_spear_t6` retired: no room above `he_spear` (30.6 DPS against a tier 6 limit of 30.9) |
| F2 | UNVERIFIED | Which mode an agent with a shield draws is decided in native code | Doc says so; in-game check owed (below) |
| F3 | LOW | `with_shield` kept the couch mode of a weapon whose primary refuses a shield | Fixed: such a weapon is judged whole (10 weapons on 2026-11-16, e.g. `dale_war_spear_a`); test `test_a_weapon_whose_primary_mode_refuses_a_shield_is_judged_whole` |
| F4 | LOW | The held view takes its class from the first shield mode, not from the attack it prices | Follow-up |
| F5 | LOW | "Carries a shield" reads any battle set; exact on today's data, nothing enforces it | Follow-up |
| F6 | LOW | Doc numbers: inversions 125, not 126; the restat went from 1 blade (trunk's), not 0, to 10 | Fixed in melee-damage-model.md |
| F7 | LOW | Wording: the fixer's substitute must be in the strict band; the README said the report calls `findings()` | Fixed in melee-damage-model.md and tools/README.md |
| F8 | LOW | The warning showed the carrier's band but not the floor the tolerance accepts | Fixed: `melee_ladder.tolerance_floor`, "accepted from N" in the message; test `test_an_under_finding_names_the_tolerance_floor` |
| F9 | INFO | The fixer's upgrade ceiling compares weapons as each troop wields them | Documented |
| F10 | LOW | `carrier_caps` docstring: an unworn sibling under a shield anchor is held to its class limit only in the other modes | Docstring fixed |
| F11 | LOW | `analyze_melee_ladder.py` and the roster page still price the whole weapon | Follow-up |
| F12 | LOW | Nothing warns when no shield ids load (every troop would read as shieldless) | Follow-up |

## Not verified

- The engine's choice of usage for an agent with a shield (F2). The flags are read in
  `TaleWorlds.Native.dll`; managed code reads `requires_no_shield` only in `CampaignUIHelper`.
- No game run. In game: a Gondor spearman with a shield thrusts one handed and never swings two
  handed; a Lorien rider holds `he_spear` and couches it.

## Follow-ups

FOLLOW-UP #631: take the held view's class from the mode that gives the priced attack, not from the first shield mode (F4). No weapon differs today; a couch mode listed first would.
FOLLOW-UP #631: read "carries a shield" per battle set, or add a gate that fails when a troop has a shield in some sets only (F5).
FOLLOW-UP #631: price shield carriers through `with_shield` in `analyze_melee_ladder.py` and the roster page (`docs/reference/troop-rosters.html`), so the report agrees with the gate (F11).
FOLLOW-UP #631: warn in the validator, fixer, restat and generator when `load_shield_ids` returns no id (F12).
FOLLOW-UP #631: the Numenorean bastard swords' one handed mode sustains 37 DPS against vanilla's median 61; a blade restat cannot raise only that mode. The maintainer decides whether their shield carriers keep them.

## Lesson

Appended to [build-tooling-workflow.md](lessons/build-tooling-workflow.md): a rule change to a
ladder reaches every tool that prices against the ladder, not only the ones that call its verdict.

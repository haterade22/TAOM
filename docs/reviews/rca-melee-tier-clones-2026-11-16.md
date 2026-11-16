# RCA: tier clones for under-armed troops (#631, 2026-11-16)

## Top-line

58 tier clones (copies of the weapon a troop carries, with a raised blade and the next Roman numeral of
the donor's line) went through one `/deep-review` at tier 2: Data flow, XML and Tooling lenses, effort
high. Codex was skipped (out of usage, maintainer's word). No finding was CRITICAL or HIGH. The one
MEDIUM: the spec rules that keep the swap rule to one match ran only inside `build()`, which needs the
installed Armory, while three troop generators read the same spec with no check, and no test covered the
overlap rule.

Measured on the live Armory (`python tools/validate_moduledata.py`): `MELEE_LADDER_INVERSION` 125 on trunk
c25b221aa to 40 with the clones; `MELEE_BLOW_CEILING` stays at 9. The XML lens counted 124 to 39 on its own
run; the difference is one warning between two runs of the same tree on a moving live install.

The full verify before the review found two failures the clones caused, both fixed before the review:
`StarterKitCoverageTests` did not read a clone as its donor (10 career slots), and `FlagScopeTests` used
`troops_dolguldur.xml` as a sample with findings, which the clones cleared.

## Root cause of the MEDIUM

The spec checks were written inside `build()` because `build()` was the only reader when the tool had
19 hand-made entries. The tier clones made the spec the source of a swap rule that four writers read
(`build()`, `realm_troops.py`, `generate_lothlorien_troops.py`, `generate_snow_orc_troops.py`), and the
check did not move with the rule.

## Findings

| # | Severity | Finding | Outcome |
|---|---|---|---|
| F1 | MEDIUM | Spec checks (tag, unique id, overlap) ran only in `build()`, which needs the Armory; no test for the overlap rule | Fixed: `check_spec` runs in `load_spec`, adds "tier inside tiers" and "one craft row per entry"; `CheckSpecTests` (7 tests) |
| F2 | LOW | `live_line_faults` copied `main`'s loading code, which is how the shields argument went missing | Fixed: one loader, `plan_from_install`, for both |
| F3 | LOW | `assign_names` had no caller in the repo, and the shipped names were reordered by price outside it | Fixed: deleted with its tests; the docs say the names were set at planning and `line_faults` holds them |
| F4 | LOW | A numeral above XXXIX dropped a weapon out of its line without a word | Fixed: `roman` raises outside 1 to 39; `LineNumeralTests` |
| F5 | LOW | `line_faults` skips a step when a neighbour has no price | Follow-up; no line has an unpriced member today |
| F6 | LOW | The `placeholder-weapons` gate globs missed `taom_schema.py` and `generate_new_faction_kingdoms.py`, which feed the swap rule; the README counted 74 lines with no command | Fixed: two globs, the gate note names the line and owed-swap refusals, the README names the command |
| F7 | LOW | `StarterKitCoverageTests` accepted the clone id as well as the donor, unlike the Python rule; the new test's name claimed more than it checked | Fixed: only the donor counts; test renamed `PlaceholderDonors_NoDonorIsItselfAPlaceholder` |
| F8 | LOW | `generate_enlistment_rosters.py` read clones as items, so a reseed would hand clones to enlisted players | Fixed: `placeholder_donors`, read in `parse_troops` and the troop gear index; `ParseTroopsPlaceholderTests`. Repair dry run unchanged (0, 0, 94 dropped) |
| F9 | LOW | `promote_borrowed_cultures.py` writes `troops_lindon.xml` whole without the swap rule | Documented; follow-up. The promoter refuses Lindon today and `--verify` would catch a revert |
| F10 | LOW | `fix_melee_ladder.best_carried` skips unpriced items, so a clone on an upgrade target sets no ceiling | Follow-up |
| F11 | LOW | The band uses the first culture's craft row through the clone id, so `craft_troop_prefix` never applies (`taom_ph_sm_uruk_halberd_b_t9`, carried by a Black Numenorean, sits about 3 DPS under its carrier's target, inside the band) | Follow-up; F1 now refuses an entry whose cultures differ in craft row |
| F12 | LOW | `docs/reference/troop-rosters.html` held no clone | Fixed: regenerated |
| F13 | LOW | A commissioned soldier hero takes its template kit, so a clone can reach the player and be sold | Documented in placeholder-weapons.md; same route as the first 19 placeholders |
| F14 | LOW | `tools/placeholder_weapons.json` was written with LF in a CRLF checkout | No change: the index is LF either way |

## Not verified

No game run. Owed (the XML lens's list): the encyclopedia page of `gondor_da_swan_knight` shows
"[Gondor] Swan Knight Sword B II" with more damage than the donor; `gondor_da_cavalry` can couch its
lance clone; `rohan_edoras_meduseld_master_spearman` can brace on foot; `rgl_log` has no
"is not a valid" line naming a `taom_ph_*_blade`.

## Follow-ups

FOLLOW-UP #631: compare a placeholder in `line_faults` with the last priced member before it, so an unpriced neighbour does not hide a DPS fall (F5).
FOLLOW-UP #631: apply `placeholder_for_troop` in `promote_borrowed_cultures.py`, or list its whole-file outputs in `GENERATED_TROOP_FILES` (F9).
FOLLOW-UP #631: price placeholders in `fix_melee_ladder.best_carried`, so a clone on an upgrade target sets the upgrade ceiling (F10).
FOLLOW-UP #631: band a placeholder on its carriers' shared craft row (through `craft_troop_prefix`) instead of the clone id's (F11).

## Lesson

Appended to `docs/reviews/lessons/build-tooling-workflow.md`: a check moves with the rule it guards.

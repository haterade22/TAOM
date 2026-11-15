# Codex independent pre-review: weapon and armour rebalance (#1271), 2026-11-14

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, run no git command that changes anything. Report
defects with evidence (file, line, the input that fails). Engine facts come from the installed game
(`pwsh tools/taom-src.ps1 path <Type>`), never from a name.

## What to review

The uncommitted change in this worktree (branch `balance/weapon-armour-vanilla`, base fccccd353):
`git status --short` and `git diff` list it. About 45 files: Python tools under `tools/`, their
tests, two JSON specs, one generated XML file, two C# files under `Main/Features/Arena/`, and docs.

The builders' summary of the final state (rules, constants, gates, counts, the apply sequence, open
items, what is not verified) is in `docs/reviews/raw/build-summary-weapon-armour-2026-11-14.md`.
Read it first, then `docs/features/armor-balance.md` (the kingdom-cap curve, "Back to the vanilla
range", the apply sequence) and `docs/features/melee-damage-model.md` ("Class limits" onward).

## Intent (the maintainer's decisions; do not relitigate them)

TAOM weapons and armour sat far above vanilla Bannerlord (a one handed sword displayed 140; the best
troops wore 112 body armour against 67 for vanilla's best troop). Both return to the vanilla range,
spread over ten troop tiers, ranked by each culture's smithing.

- Armour is judged on what a troop wears in total per body part. Best ordinary troop body 67, lord
  at most 5 more. Hero-only kit is clamped down and never raised. Troll kit is out of scope.
- Secondary stats (chest arm and leg, pauldron arm) are 0 below the heavy band and capped above it.
- One handed weapons display under 100. The one-blow rule is a guideline: an elite (tiers 6 to 10)
  killing a tier 1 to 3 troop in one blow is wanted. The troop skill curve is not lowered.
- Nothing is applied to the live Armory yet. These tools will then rewrite about 2,750 armour items
  and about 200 blade pieces in an unversioned module outside git. A wrong write is expensive:
  that is the main risk.

## Known suspects (a first Claude review found these; the fixes are in the tree: verify each fix)

1. `tools/restat_melee_blades.py`: the blade target and limits now come from the carriers
   (`carrier_contexts`, `carrier_caps`); blades shared by one handed and two handed weapons are not
   moved (`mixed_blades`) except down to a class limit; a second run must plan nothing. Try to make
   it raise a blade above a carrier's tier limit, lower a hero blade below its hero limit, move an
   exempt-only blade, or plan again on its own output.
2. `tools/generate_placeholder_weapons.py`: k is capped at the lowest real carrier's tier limit.
   Its `--verify` fails until the Armory restat by design: is the stated order (restat, then
   regenerate) sound, and can the committed XML be wrong in between?
3. `tools/fix_melee_ladder.py`: a swap must fit the carrier's tier limit; 456 slots are refused
   for that reason. Can a swap still give a troop a weapon another rule forbids (culture pool,
   shield parity, one weapon in two slots)?
4. `tools/rebalance_armor.py`: `_roster_first_plan`, `clamp_armour`, `kingdom_secondary`, the
   Mordor hero routing, `--backup-tag` now required with `--apply`, weight not written under
   `--keep-weights`. Can any path raise a hero-only or unworn item, write 0 on a primary, touch
   troll, generator-owned (`sk_ll_`, `sk_gb_orc_`) or HorseHarness items, or differ on a second run?
5. `tools/derive_armor_tiers.py`: `heroWorn` from `characters/`, `named_companions/` and now
   `lords.xslt` templates and direct `<equipment>` overrides. Is any hero-worn item still missed?
6. `tools/taom_schema.py`: `_worn_armour_limits` (per-slot draw: `Equipment.GetRandomEquipmentElements`
   re-rolls the set per slot when the seed is not -1; verify that reading), the placeholder pricing
   in the melee passes, `WORN_ARMOUR_CEILING_SEVERITY`. Does any gate pass silently when it checks
   nothing?
7. `Main/Features/Arena/TournamentPrizeRules.cs` and `TournamentService.cs`: armour prizes are now
   judged by the armour class table, and the Tierf floor stays for items with no table class. Is
   the behaviour for weapons, shields and harness unchanged? Is the rule still pure and tested?
8. `tools/generate_starter_armor.py`: starter values are the smaller of the literal and the light
   band. Does a starter piece ever exceed what a tier 1 recruit of that culture wears?
9. The apply sequence in `docs/features/armor-balance.md`: walk it against each tool's real flags.
   Missing step, wrong order, a step that writes the live Armory without a backup, a step that
   writes a maintainer-handed file without `--out`, a repo file the sequence rewrites that the
   second commit list omits.
10. Tests that would still pass if the logic were wrong, and gates this change weakens or silences.

Also look for what nobody listed: consumers of armour or weapon numbers outside the diff (C# that
reads armour values or weapon damage, prices, shops, kits, co-op), and docs that state a fact this
change makes false.

## Output

Findings grouped by severity (P1, P2, P3), each with file, line, evidence and a concrete fix. Then
what you verified and found sound, and what you could not verify.

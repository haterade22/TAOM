# RCA: Codex adversarial pass on the ranged ladders (#582, #588, 2026-09-13)

**Top line.** Codex (gpt-6-astra, ultra, `docs/reviews/raw/codex-adversarial-ranged-ladders-2026-09-13.md`)
reviewed the seven commits `d20838e4`..`02666e32`: the ladder library, spec, generator, roster tool,
the `RANGED_LADDER_INVERSION` gate, the militia +15 step and the tracked HTML. It reported four P2
and one P3, all confirmed on re-reading, zero false positives, and disputed five of the eight Known
Suspects with counted evidence (0 prefix matches outside a claimed file, 0 mixed-class troops, 13/13
loc files, 8/8 newline combinations idempotent). The gameplay defect: seven horse archers (Harad R
and V, Rohan E, Dunland R and V) were rostered into cells whose donor bow carries `item_usage="long_bow"`,
a usage set Native flags `requires_no_mount`, so they spawn holding a bow they never draw. Zero of
them had that problem before the rewrite; all seven carried a vanilla steppe bow. The second data
finding corrected a claim in the feature doc: `is_merchandise="false"` (`ItemObject.NotMerchandise`)
keeps an item out of the shops AND out of casualty loot (`DefaultBattleRewardModel.GetRandomItem`
lines 125 and 153), so a ladder bow never drops; the page said loot still dropped them.

## Findings

| # | Codex | Mine | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | P2 | HIGH | Seven mounted troops hold a `long_bow`-usage ladder bow. Native's `long_bow` set is `base_set="bow"` plus `requires_no_mount` and `requires_no_shield`; the engine tooltip says "Can't use on horseback" and the AI keeps the bow on its back. | Missing vanilla gate (item_usage flags) | The rewrite compared launchers by class and speed only; `item_usage` rode along from the donor unread, and the seven troops' OLD bows (`composite_steppe_bow`, `steppe_heavy_bow`) were `bow`-usage, so nothing in the before/after diff looked wrong. The roster tool had no notion of a mounted troop. | `Launcher.usage`, `RangedTroop.mounted`, `mount_barred_usages()` read from the install's `item_usage_sets.xml` (None when unreadable, and every caller says it skipped), `mount_conflicts()`, `planned_edits(barred=)` refuses, validator warning `RANGED_MOUNT_USAGE`. Fix in data: a per-line `usage` override in the spec (`{"Bow": "bow"}` for Harad and Dunland, the Armory's own `wm_mirkwood_bow_a02` "- Horse" pattern, same mesh) and Rohan's E band on the line's `composite_steppe_bow`. `--verify` checks the usage too. |
| 2 | P2 | Doc | `docs/features/ranged-ladders.md` said loot "still drops them". `GetRandomItem` skips every `NotMerchandise` slot before rolling. | Stale claim about the engine | The doc author reasoned from the attribute name (a shop flag) and did not read the loot model; Suspect 3 asked Codex to find every reader of `NotMerchandise` and it did. | Doc corrected: the ladder bows are troop-only by construction, the donors stay in shops and loot for the player. This is the intended shape (130 near-duplicate bows in the loot pool would be the defect), recorded as a decision, not a fix. |
| 3 | P2 | MED | `inversions()` compared MAX with MAX, so a higher troop with a slow alternate set beside a fast one hid an inversion the engine can spawn (it draws each set independently). Inert on the committed rosters (one launcher per class per troop). | Logic error (wrong side of the comparison) | The rule was written from the single-launcher case and the docstring rationalised MAX for both sides. | The troop that should be faster is judged by its slowest set (`troop_min_speed`); a fixture with a fast and a slow set on the higher troop pins it. |
| 4 | P2 | MED | A troop file that does not parse vanished from the gate with no finding. `load_ranged_troops` recorded the failure only when handed a list, and the validator never handed one. | Gate that quietly checks nothing | The `failures` plumbing was added for the report tool two commits earlier and not threaded into the gate; the gate's tests fed it well-formed fixtures only. | The gate emits `RANGED_LADDER_INVERSION` with `entry_id="(file)"` naming the file and saying "not checked is not clean"; a test writes an unclosed tag and expects exactly that. |
| 5 | P3 | LOW | `validate_spec` rejected a whole-file line placed before the prefix lines that carve from it, though `line_of` tries prefixes first regardless of order. The documented reorder operation could be refused. | Convention inconsistency (two readers of one rule) | The validator's comment described an ordering `line_of` never required. | Duplicate whole-file ownership is reported only when both claimants lack prefixes; tests cover both orders. |
| S7 | Suspect confirmed | LOW | No test exercised the gate through `Validator.run()` with launchers present, and an install with an empty Modules directory passed with no ladder skip notice. | Test coverage / skip notice | The gate tests called `_ranged_ladder_inversions()` directly; the registry-size floors did not include launchers. | `("launchers", 30)` added to the suspect-registry floors; `Registries.mount_barred_usages`; tests for the registry without an install and with an empty one. |

Disputed suspects, so nobody re-opens them: prefix precedence (S1, 0 troops fall through), mixed-class
troops (S2, 0 exist), Isengard militia arithmetic after the Bow/Crossbow swap (S6, all eight values
reproduce), the loc-row newline handling (S8, eight combinations, idempotent and exact on revert),
English loc resolution (S5, inline text wins and matches 130/130). Suspect 4's ammo half: 0
class-mismatched launcher/ammo sets; Isengard's crossbow donor is `crossbow_light`, which is not
mount-barred.

## Root-cause pattern: a clone copies every attribute, and the tool read two of them

The generator promised "a verbatim clone with only id, name and missile_speed changed", and kept
that promise; the roster tool then treated the clone as "a Bow at speed N". Every other attribute
of the donor became a property of every troop in the band, including one (`item_usage`) that
decides whether the troop can use the item at all. The same shape produced the shield-and-polearm
trap in `CLAUDE.md` (a usage set resolved from the pieces, an AI that never draws the weapon). The
general form: when a tool assigns an existing item to a new troop, enumerate the item's usage
constraints against the troop's spawn state (mounted, shielded, race), not only its class.

The loot finding is the older pattern, a doc claim reasoned from a name rather than read from the
model; `evidence-over-claims.md` §C already names it.

## What Codex did that the six-agent review did not

- Executed the question "which mounted troop holds which usage" as a table over all 16 files rather
  than sampling; the deep-review agents were briefed on classes and speeds and checked those.
- Read `DefaultBattleRewardModel` and `MapEvent:1602` for `NotMerchandise` because the prompt asked
  for EVERY reader of three fields; it found the loot path and the tournament path, and disputed the
  pricing/power path with the bodies.
- Reproduced the empty-Modules run (`PASS`, exit 0, no ladder notice) instead of asserting it.

## Lessons codified

- `docs/reviews/lessons/data-content-cultures.md`: a rider cannot draw a `long_bow`; a tool that
  hands an item to a troop checks the item's usage flags against the troop's spawn state.
- `docs/reviews/lessons/testing-qa.md`: a rule over "the troop's speed" needs to say which set, and
  the side that must be faster is judged by its worst set.
- `.ai/review-reference.md`: Codex's usage-flag table and its every-reader sweep, so the next
  briefing asks for both by name.

## Not findings

- `requires_no_shield` on a bow: Native itself ships twelve Wolfskins sets (three `default_group="Ranged"`
  troops) with a `long_bow` bow beside a shield, so the pairing is native-authored and is not gated
  here. Three Gondor troops (`gondor_osg_archer`, `gondor_osg_longbowman`, `gondor_brv_shadowhunter`)
  carry it, as they did before #582; whether their shield delays the draw is UNVERIFIED in game.
- `difficulty` on a clone: read by the player's inventory equip gate and the tooltip, passed to
  native by `MissionWeapon.GetWeaponData`; no managed spawn, AI or loot path reads it (Suspect 3,
  native side UNVERIFIED and left so).

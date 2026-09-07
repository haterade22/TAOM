# RCA: javelin skirmishers tagged Ranged with their skill budget on a bow they do not carry (2026-09-06)

**Top line:** A player screenshot of the Gondor troop tree showed the Harondor skirmisher line. All
three rungs carry a javelin and a sword, no bow, and are tagged `default_group="Ranged"`.
`gondor_har_javelineer` shipped Bow 170 / Throwing 40, so its entire ranged skill budget sat on a
weapon it cannot use, and the engine fought it as a backline archer while it held a frontline
skirmisher kit. Two more troops in Rhûn had the same shape. Five troops reclassified to Infantry and
restatted: Throwing now takes the Ranged Bow curve (85 / 130 / 170 for Harondor, 195 / 235 for the
two Naffatun) and the inert Bow and Crossbow drop to the Infantry floor. The systematic half, the
other 87 thrown-weapon carriers, is blocked on a broken Armory install and tracked in #555.

## Findings

| # | Sev | Bug | Category | Why the data had it | Preventive action |
|---|-----|-----|----------|---------------------|-------------------|
| 1 | **HIGH** | 5 troops carrying a thrown weapon plus melee and no bow are `default_group="Ranged"`. For a line troop `IsRanged` and `IsMounted` come from `default_group` alone, never from equipment (`BasicCharacterObject.cs:494-496`; heroes are the exception, `CharacterObject` overrides both), so they are deployed, commanded and simulated as archers. | Enum contradicting equipment | `default_group` fails soft. `FetchDefaultFormationGroup` returns -1 on an unknown value rather than throwing, and a *valid but wrong* value produces nothing at all: no log line, no crash, no validator code. | Reclassified to Infantry. Convention written into `docs/modding/troops.md`. A `THROWN_MELEE_MISGROUPED` validator gate is designed and blocked on the install (#555). |
| 2 | **HIGH** | The same 5 carry the full Ranged Bow curve (85 to 235) and a Throwing skill of 20 to 65, on a kit with no bow and two javelins. | Generator gap | `detect_weapon_specialization` has a `_swap_bow_crossbow` rule and no throwing equivalent. The `naffatun` name keyword that used to fire a swap here was removed in #340 as a false positive, correctly, and nothing replaced it. | `THROWN_PRIMARY_TROOP_IDS` plus `_throwing_archer_parity` in `rebalance_troops.py`: Throwing takes `RANGED_BASELINES[level]['Bow']` plus the culture's Bow modifier. |
| 3 | MED | Flooring `sagarun_naffatun`'s Crossbow 160 to 10 is a real regression across a real edge: its parent `sagarun_crossbowman` genuinely carries a crossbow at 160. | Ladder rule collision | The ladder rule cannot distinguish "child dropped an inherited value it never used" from "child got worse", because both look like a decrease. | `RESPECIALIZATION_EXEMPT_EDGES`, per edge and per skill, mirrored in `rebalance_troops.py`, `taom_schema.py` and `TroopUpgradeSkillMonotonicityTests.cs`. |
| 4 | MED | `rebalance_troops.py` hardcoded `DEFAULT_GAME_MODULES = E:\Steam\...` and did not use `tools/_gamedir.py`, so it errored at startup on any machine without an `E:` drive. | Path drift | #404 established `_gamedir` as the convention and 32 tools adopted it; this one and its read-only sibling were the outliers. | Resolved through `_gamedir`, so `$BANNERLORD_GAME_DIR` wins. `analyze_troop_balance.py` reads the same constant and was fixed with it. |
| 5 | INFO (not fixed) | Gondor's `CULTURAL_MODS['gondor']['Throwing'] = -10` penalises the one Gondor line whose identity is javelins. | Balance question | A single scalar shared by every Gondor troop. | Deferred to #555. `detect_culture` already has the id-prefix routing pattern (`iron_hills_*`, `mordor_uruk_*`, `orthanc_*`) if Harondor should get its own key. |

## Why it survived for months

Three silencers, and it takes all three to hide something this visible on a troop card.

1. **`default_group` fails soft.** There is no exception, no log line, and no validator code for a
   value that is valid but contradicts the equipment. The nearest existing gate, `MOUNTED_DWARF`, is
   the only cross-check of `default_group` against carried items in the codebase.
2. **The wrong values were inert, and that was written down as acceptable.**
   `docs/features/troop-skill-balance.md` listed `sagarun_naffatun` inheriting Crossbow 160 "though
   it throws javelins" as a deliberate cost, alongside a genuinely benign case. Once a defect is
   documented as a decision, nobody re-asks the question. Inert only means harmless if the weapon the
   troop *does* carry is served, and the same sentence was the evidence it was not.
3. **The auditor shares the generator's curve.** `analyze_troop_balance.py` imports
   `rebalance_troops.py`'s formula verbatim, so a troop that is on-curve but wrong is invisible to it
   by construction. This is the same structural blindness recorded in the lessons file for #340, fixed
   there for weapon identity and still present for formation class.

## What made the fix narrow, and why that is not a preference

The reference install's `LOTRLOME_Armory` is a shell: every `ModuleData` subfolder holds 0 files, there
is no `SubModule.xml`, `AssetPackages` holds 0 `.tpac`, and there is no `Assets` directory, against a
catalogue header recording `tpacs=4364 metameshes=4456`. Measured consequence: of the 315 distinct
weapon-slot item ids the troop files reference, **247 do not classify**, bows included.

So the honest predicate for this rule, "carries a thrown weapon and neither bow nor crossbow", is
unsafe here: an unclassifiable Armory bow reads as no bow, and a full `--apply` would hand real archers
the throwing curve across the roster. The trigger is a hardcoded 5-id set instead, with a
`'Throwing' in weapon_classes` conjunct so a run against a registry too broken to see the javelin does
nothing rather than writing a number derived from nothing. #555 restores the install and swaps the set
for the predicate.

Worth noting separately: `rebalance_troops.py` ran happily against that crippled registry and would
have written wrong data mod-wide had `--apply` been used instead of `--fix-monotonicity --restat`. A
coverage guard is item 4 of #555.

## The blast radius of the reclassification, which the fix under-documented

The change was planned on the assumption that `default_group` governs formation placement and little
else, and that wage and auto-resolve were unaffected. **The deep review refuted that**, and the
refutation is the most useful thing this RCA records. `default_group` is not a display attribute; it
is the input to a dozen systems.

Verified directly against installed v1.4.8 while triaging the review:

* **Auto-resolve battle power keys on it.** `DefaultMilitaryPowerModel.GetTroopPowerContext` returns
  `PowerFlags.Archer` when `troop.IsRanged` and `PowerFlags.Infantry` otherwise; that flag indexes a
  terrain-plus-side modifier table applied as a percentage on the troop's simulated power. TAOM's
  `TaomMilitaryPowerModel` overrides **only** `GetDefaultTroopPower`, so it inherits this path
  unchanged. These five troops' simulated strength therefore moves, and the direction depends on
  terrain and on whether they attack or defend. They lose the archer siege-defence bonus and gain the
  infantry forest posture.
* **TAOM's own `WageModifierService` branches on `IsInfantry` / `IsRanged`** to pick which recruitment
  perk category applies. These five moved category.
* **They just became eligible to carry banners.** `BannerBearerService` gates on
  `AllowedFormationGroups`, which ships as `["Infantry"]` only. The config's own comment explains the
  restriction exists because a bearer swaps its weapons for a banner and a sidearm, so making an
  archer a bearer wastes its bow. These troops have no bow to waste, so the new eligibility is
  defensible, but it was neither intended nor predicted.
* **Mixed formations reposition them.** `AgentCombatAdapter` reads `CharacterObject.IsRanged` and
  `FormationLayoutService` places them in melee ranks rather than a ranged slot.

Reported by the review's engine sweep, consistent with the above but not individually re-decompiled
here: per-troop wage and recruitment price are genuinely unaffected (both are tier-keyed), but
`DefaultPartyWageModel.GetTotalWage` builds Ranged and Infantry subtotals that feed perk-gated wage
reductions; `DefaultCombatSimulationModel`, `DefaultCombatXpModel` and `DefaultPartyTrainingModel`
each gate perks on the same flags; garrison request weighting, the Lord's Hall archer cap, and the AI
`PreferredUpgradeFormation` upgrade weighting all read formation class; and the party screen,
encyclopedia filter tabs and encounter "command as" menu options are keyed on it too.

**None of this changes the verdict.** The five troops carry a javelin and a sword and belong in the
infantry line; being simulated as infantry is the correction, not a side effect. The defect was in
the claim, not the change: a blast radius was asserted from a plausible reading rather than from an
enumeration of the engine's consumers.

## Why the deep review caught what it caught

| Agent | Outcome |
|---|---|
| Standards | Found the missing pinning test for the new exemption, by comparing against the militia mirror's own convention in the same file. |
| Engine / API | **Found the two wrong claims.** It was the only agent that decompiled the installed DLLs rather than reading TAOM's docs, which is exactly why it saw that `CharacterObject` overrides `IsRanged` for heroes and that the auto-resolve model reads the flag. |
| Efficiency | Correctly reported almost everything N/A (no runtime C# in the changeset) and found the latent `RANGED_BASELINES` level-1 silent no-op. |
| Completeness | Found the untested throwing rule by noticing the sibling swap had four tests and this one had none. |
| Data flow | Found that nothing enforced the three-way mirror, that the allowlist ids are never validated against the live troop set, and that `analyze_troop_balance.py` is an unnoticed fourth consumer of the ladder rule. |
| Tooling correctness | Confirmed the writes are byte-faithful and scoped to five troops, confirmed idempotency, and confirmed the `_gamedir` swap did not double the `Modules` suffix. |

The pattern worth keeping: **the agent that read the engine disagreed with the agents that read the
docs, and the engine was right.** Two of the five core agents implicitly trusted TAOM's own
documentation of `default_group`, which is where the wrong claim already lived.

## Verification

* `--fix-monotonicity --restat <5 ids> --dry-run` reported `Changed: 5, Unchanged: 845, Skipped: 7`
  and `Monotonicity clamp: raised 0 skill values`, so the exemption held and nothing else moved.
* Every written value matched the number predicted from the baseline tables before the run.
* `python tools/validate_moduledata.py --code UPGRADE_SKILL_REGRESSION` reports `PASS`.
* `TroopUpgradeSkillMonotonicityTests`: 4 passed, 0 failed.
* The validator's 34,644 errors on a full run are the gutted install (33,429 `BROKEN_ITEM_REF`,
  1,207 `LANDLESS_CULTURE`), not this change.
* Ten tests added post-review in `tools/tests/test_rebalance_equipment.py`, all passing:
  `ThrowingArcherParityTests` (6) and `RespecializationExemptionMirrorTests` (4).
* The mirror-drift test was proven non-vacuous rather than assumed: its regex extracts the real entry
  from the `.cs` file, and against three simulated drifts (a dropped skill, an extra edge, a renamed
  troop) it reported a mismatch in every case.

## Preventive actions

| # | Action | Where |
|---|--------|-------|
| 1 | A javelin is not a reason to write `Ranged`; a real bow plus a javelin still is | `docs/modding/troops.md` |
| 2 | The hero exception to the `default_group` rule, and that a javelin never makes even a hero count as ranged | `docs/modding/troops.md` |
| 3 | `THROWN_MELEE_MISGROUPED` validator gate, modelled on `MOUNTED_DWARF` | #555 item 3 |
| 4 | Registry coverage guard so the writer refuses a run it cannot classify | #555 item 4 |
| 5 | Deleting a false-positive heuristic requires naming what now serves the rows it served correctly | lessons entry below |
| 6 | A hand-copied constant needs a test that parses every copy and diffs them, not a comment saying they must agree | `RespecializationExemptionMirrorTests` |
| 7 | An id allowlist needs a staleness test against the live data, or it rots on the next rename | `test_exempt_edges_name_troops_that_still_exist` |
| 8 | Before claiming a blast radius for an engine-read enum, enumerate the engine's consumers | lessons entry below |

## Lessons entries

One new, two recurrences. **The two recurrences are the more important finding**: both rules already
existed and were not followed, so the preventive action is to strengthen them rather than to add a
third copy of the same idea.

* NEW: `docs/reviews/lessons/data-content-cultures.md`, "Deleting a false positive is not the same as
  meeting the need it was faking."
* RECURRENCE: `docs/reviews/lessons/gamemodels-services.md`, "A cross-language constant needs a test
  that reads the other language." Written 2026-09-03 for #537. This changeset repeated it **three
  days later**, with a three-way hand-copied table and a comment saying all three must agree. The
  strengthened form: writing the words "must be kept in sync" is itself the trigger to write the
  test, in the same commit.
* RECURRENCE / GENERALISED: `docs/reviews/lessons/gamemodels-services.md`, "A data fix that moves a
  tier is a gameplay change, so enumerate what reads that tier." The rule was written about `level`
  and `Tier`; the same failure happened here with `default_group`. Generalised in place to any troop
  attribute the engine reads.

The uncomfortable summary: of the eight findings this review produced, the two most serious were
already covered by written rules in this repository, one of them three days old. The review worked;
the rules did not fire on their own, because nothing in the authoring path asks the question at the
moment the copy or the enum edit is made.

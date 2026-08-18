# RCA: Black Numenorean line (Mordor), 2026-08-17

Seven Claude review agents plus a Codex adversarial pass over a data-only feature: 78 armour items,
22 crafting pieces, 7 crafted weapons, 6 shields, 13 troops, and wiring across four ModuleData files.
Four new Python generators, three of which write outside the repo.

**Headline:** every gate was green before the review. `validate_moduledata.py` PASS,
`validate_all_troop_refs.py` 0 missing, 571 Python tests, 6,655 C# tests, `lint_docs.py` clean. The
review still found one HIGH tooling defect, four HIGH balance defects, and two silently-green
validators. **Green gates measured almost nothing about the part of this feature that mattered.**

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Every new blade beat both hero blades on cut (3.8-4.0 against the Witch King's 3.5) | Balance | I adopted "above uruk, below hero kit" as the constraint and never checked it was satisfiable. It is not: the shipped uruk troop blade already out-cuts hero kit at 3.74. I anchored on the two hero blades I looked up and never sorted the full blade population | Lessons entry: when you state a bracketing constraint, sort the actual population first and confirm the bracket is non-empty |
| 2 | HIGH | The T5 Initiate was the least-armoured level-26 troop in the game (50 against a cohort median of 157) | Balance | I mapped mesh tier names one-to-one onto curve tiers, which is correct for the ITEMS and says nothing about the WEARER. I noticed the risk, wrote it into the plan as a flag, and then deferred it to a tool that structurally cannot answer it | Lessons entry: item-tier correctness and wearer-level correctness are two different checks. Run the level-cohort comparison |
| 3 | HIGH | `rebalance_armor.detect_tier` mis-tiered 45 of 78 items as elite; a scoped rebalance run would have flattened the set | Tooling | `elite_keywords` contained the line name `'black numenorean'`. I never asked what the shared balance tooling would make of a new id prefix and display-name convention | Fixed at source (keyword removed). Lessons entry: when adding a culture/line whose display name contains an existing tier keyword, grep the keyword lists first |
| 4 | HIGH | The weapon generator wrote its XSLT files and exited 0 when a template was NOT FOUND | Tooling | I wrote the docstring naming this exact failure mode ("a piece missing from 2 or 3 makes the weapon fail to load with NO log line, which is why all three are written in one pass"), implemented the detection, and never implemented the consequence | Lessons entry: when a docstring names a catastrophic failure mode, the guard for it needs a test that exercises the failure path, not just the happy path |
| 5 | HIGH | `Main/_Module/SubModule.xml` declares no `<DependedModule Id="LOTRLOME_Armory"/>` | Data flow | Pre-existing, not introduced here. Missed because I checked that item refs RESOLVE, never that the module providing them is DECLARED | Reported, not fixed (it makes TAOM unlaunchable without the Armory and needs a provenance row). Own issue |
| 6 | MED | A T8 "promotion" put the rider on a worse horse (`charger` 48/22 against `t2_empire_horse` 50/26) | Balance | I picked mounts by name plausibility ("charger" sounds like a knight's horse) and never read their stats | Lessons entry: never select an item by name semantics; read the stat block |
| 7 | MED | Shields sat above Gondor's ceiling (560 against 520) rather than under it | Balance | I anchored on Mordor's internal ladder and forgot the cross-culture #342 rule applies to shields too | Covered by the #2 lessons entry (compare across cultures, not only within one) |
| 8 | MED | `generate_clan_heraldry.py` would delete all 13 stacks from 15 templates if re-run | Data flow | I looked for what consumes troop ids; I did not look for what REGENERATES the files I edited | Lessons entry: after editing a generated file, grep `tools/` for the generator that owns it |
| 9 | MED | Armour writer inserted LF into CRLF files (328 lines, `\r\n\r\n\n` seam) | Tooling | I wrote per-file EOL detection in three of the four scripts and omitted it from the fourth | Now in `tools/README.md`: inserted text must carry the file's endings, chosen by majority |
| 10 | MED | Backup was unconditional, so a same-day partial re-run overwrites a pristine backup | Tooling | Three scripts guarded it, one did not | Documented in `tools/README.md` alongside the backup rule |
| 11 | MED | `.gitignore`'s `*.bak` misses `.bak-blacknum-*`, and two scripts write backups inside the repo | Tooling | The dated-sidecar convention post-dates the gitignore line | Widened to `*.bak*` |
| 12 | MED | 6 `merchant_cost` values were dead data and off the file's documented band | Data | I invented a price ladder without checking the field is only consumed for troops in the elite-emissary allowlist | Removed. Lessons entry: before authoring a value, find its consumer |
| 13 | LOW | A justification comment cited "the 185 chests already in this file"; the real count is 111 items | Accuracy | I took a number from an agent report that had counted `<Item` and `<ItemComponent` together, and wrote it into a code comment without re-measuring | Direct instance of the evidence-over-claims rule. See below |
| 14 | LOW | Troop weights used 1.5, the only non-integer weights in a 100-entry file | Data | I invented a tier between the documented 1.0 and 2.0 | Normalised to 2.0 |
| 15 | LOW | Feature doc claimed `FaceGen.GetRaceOrDefault` "returns 0 when unknown"; it is a bare dict indexer that throws | Accuracy | I inferred the behaviour from the method NAME | See below |

### Codex round (dispatched in parallel, returned after the Claude agents)

Verdict DO NOT SHIP: 1 HIGH, 3 MEDIUM, 3 LOW. It agreed with the Claude agents on the heraldry
landmine and the two validator blind spots, and found three things they did not.

| # | Sev | Finding | Why the seven Claude agents missed it |
|---|---|---|---|
| C1 | HIGH | `generate_clan_heraldry.py` would delete the line: **simulated** it, 15 templates going from 13 stacks / max sum 3500 to zero stacks / max sums 18-34 | The data-flow agent found it too; Codex added the simulation that proved the magnitude |
| C2 | MED | The weapon writer still wrote the crafting-pieces file **before** validating either XSLT, so the fatal-on-NOT-FOUND fix left a partial-write window open | The tooling agent found the original defect; nobody re-audited the ordering after the fix. **A fix for a finding is new code and needs the same scrutiny as the original** |
| C3 | MED | T5 and T6 both sat at 96 armour, so the upgrade granted zero survivability | This was **introduced by my own fix** for the balance agent's finding 2: I reverted T6 to the med row to avoid orphaning meshes, trading a real gameplay defect for a cosmetic one |
| C4 | MED | The `mordor_num_` prefix exemption covers an unbounded namespace, and "AI-only" is factually false because the vassal reward hands `mordor_num_vet_infantry` to the player | Both the standards and data-flow agents checked the prefix matched exactly 13 troops **today** and called it correctly scoped. Neither asked what it would match tomorrow. Codex decompiled `DefaultVassalRewardsModel` to disprove the "AI-only" claim |
| C5-C7 | LOW | Stale roster-tier map, missing backup guard in the fourth script, a doc path with a doubled `tools/tools/` segment | Small, and each was a place where I had written the correct thing in three of four spots |

**The lesson C2 and C3 share, and it is the sharpest one in this RCA: my fixes were not reviewed.**
The seven agents reviewed the changeset as it stood when they were dispatched. Everything I changed
in response to them went out unreviewed, and two of those changes were themselves defects, one of
which (C3) was a straight regression that made the gameplay worse than the finding it fixed. Codex
only caught them because it happened to be running long enough to see the fixed tree.

**Preventive action:** after a fix round, re-run at least the agent whose finding you fixed, against
the fixed tree. `/deep-review`'s own fix-loop guidance already says to re-run after fixes land; this
session did not, and it cost two MEDIUM defects.

## Root-cause pattern: I verified the things that were checkable and asserted the rest

Findings 1, 2, 6, 7, 12 and 14 are one failure repeated. In each, a number needed a comparator
population, and I anchored on the two or three examples I had already looked up instead of sorting
the whole set. Every one of them was answerable with a script over data already on disk, and the
balance agent answered all of them that way in a single pass.

The shape is specific and worth naming: **I treated "I checked some real examples" as equivalent to
"I checked the population."** The uruk blade that invalidated my whole weapon premise was in a file I
had already read. The level-26 cohort that showed the Initiate was last was one query away. Nothing
here needed the game, a decompile, or information I lacked.

Findings 13 and 15 are the same disease in the documentation layer: a count relayed from an agent
without re-measuring, and an API behaviour inferred from a method name. `evidence-over-claims.md` §A4
already says a subagent report is a claim, not evidence, and §C already forbids stating a signature
or API behaviour not read this turn. I broke both while writing a document whose stated purpose is
that a future session should not have to re-derive anything.

Findings 4, 9, 10 and 11 share a different pattern: **the four generators drifted from each other.**
Three had majority-EOL detection, one did not. Three guarded the backup, one did not. One had a
fatal-path guard the others did not need. Writing four sibling scripts in one session produced four
slightly different conventions, which is exactly the drift that the four pre-existing armour
generators demonstrate at larger scale (all four went stale together in July).

## Why each review agent missed what it missed

- **Standards (haiku):** returned clean, correctly. Its rule set is C# architecture and prose style;
  this changeset is 1 test-file edit and a lot of XML. It verified 6 doc claims against real data and
  they held. No gap, just limited jurisdiction.
- **API compatibility:** verified all four engine claims and caught finding 15. It could not catch
  the balance findings because they are not API questions.
- **Tooling correctness:** caught 4, 9, 10, 11, 13 and the `merchant_cost` reachability half of 12.
  This agent fired only because the changeset added `tools/**/*.py` that write outside the repo. Had
  the same XML been hand-authored, none of these would have been found.
- **Data flow:** caught 5 and 8. Both are "what else touches this?" questions, which is precisely its
  charter, and both were invisible to every other agent.
- **Balance / data correctness:** caught 1, 2, 6, 7, 12, 14, and confirmed the curve conformance was
  exact. **This agent found more real defects than the other six combined**, and it exists only
  because the user's instruction was "ensure you balance the armor and weapon stats" and I wrote a
  dedicated agent for it. The five standard deep-review agents have no balance charter at all.
- **Completeness / XML integrity:** caught the missing GitHub issue and the undocumented tools. Also
  produced one wrong finding (it called this feature's own Codex prompt "unrelated faction-economy
  work"), which is a reminder that agent findings need spot-verification before action.

**The structural gap: `/deep-review`'s five core agents contain no balance or game-data agent.** For
a data-only content feature, four of the five have almost nothing to check. The two agents that found
nearly everything (tooling correctness, balance) were both added ad hoc for this changeset. The
skill's Step 2c does mandate a tooling agent when `tools/**` scripts write files, which fired
correctly. There is no equivalent trigger for content data.

## Preventive actions taken in this changeset

1. `rebalance_armor.py`: removed the `'black numenorean'` elite keyword with the reasoning inline.
2. `generate_black_numenorean_weapons.py`: NOT FOUND is now fatal before any write, verified against
   a renamed-template fixture (exit 1 on failure, exit 0 on the good path).
3. `tools/README.md`: documented the third sanctioned I/O idiom, the majority-EOL rule for inserted
   text, and the backup-guard rule.
4. `.gitignore`: `*.bak` widened to `*.bak*`.
5. `test_armor_curve_invariant.py`: added `CURVE_IMPORTERS` (asserts a curve-importing generator
   never regrows a private table) and a discovery test that fails when a new `generate_*_armor.py` is
   classified in neither list. **That discovery test immediately found four pre-existing unguarded
   generators**, recorded in `LEGACY_UNPINNED` with the reason each cannot currently be pinned.

## Preventive actions recommended, not taken

- **`/deep-review` needs a balance/game-data agent trigger** for changesets touching
  `Main/_Module/ModuleData/troops/**`, `LOTRLOME_items/**`, or `taom_partyTemplates.xml`. This is the
  single highest-value change suggested by this RCA.
- `Main/_Module/SubModule.xml` `<DependedModule Id="LOTRLOME_Armory"/>` plus a provenance-register
  row (finding 5).
- `generate_clan_heraldry.py` should refuse to shrink a template it did not author, or
  `clan_heraldry/mordor.json` should be regenerated (finding 8).
- `validate_mesh_refs.py`'s `tpac_paths_for_modules` should glob `Assets/**` as well as
  `AssetPackages/*.tpac`, or the Armory will stay outside the #352 hang gate forever.
- `validate_all_troop_refs.py`'s prefix regex covers 24 of this feature's 79 item refs. The `sm_`
  prefix carries real armour, not just props.
- `GOVERNED_STATS['body']` does not include `arm_armor`, which is the stat every Mordor chest
  actually writes, so `check_curve_invariant()` cannot see two-tier violations there. Both Gondor and
  Mordor chests tie the bound today.

## Lessons to append to `docs/reviews/lessons/`

To `data-content-cultures.md`:

- **A bracketing constraint must be shown non-empty before you design to it.** "Above X, below Y" is
  worthless if Y < X in the shipped data. Sort the population first.
- **Item-tier correct and wearer-level correct are different checks.** A light-tier item is correctly
  statted and still wrong on a level-26 troop. Compare against the level cohort, not just the curve.
- **Never pick an item by what its name implies.** `charger` is slower than `t2_empire_horse`.
- **Before authoring a value, find its consumer.** `merchant_cost` is read only for troops in the
  elite-emissary allowlist; six values were written for troops that are not in it.

To `build-tooling-workflow.md`:

- **When a docstring names a catastrophic failure mode, the guard needs a test on the failure path.**
  The detection existed and the exit did not, in the script whose whole reason for writing all three
  files in one pass is that failure mode.
- **Sibling scripts written in one session drift from each other.** Diff them against each other
  before shipping: EOL handling, backup guards, replace counts, dry-run reporting.
- **After editing a generated file, grep `tools/` for its generator.** A regeneration is a silent
  revert.

# RCA: the armour mesh-tier ladder (#609) deep review

**Scope.** Six review agents (standards, engine semantics, efficiency, completeness, cross-system
data flow, tooling correctness) over the uncommitted #609 changeset: `MESH_TIER_LADDER` and its
five functions in `tools/rebalance_armor.py`, the new `tools/fix_armour_mesh_ladder.py`, the
`ARMOUR_MESH_TIER_LADDER` validator pass, the 11 rewritten `troops_*.xml`, the restatted Armory
(live + mirror), three test files and the docs. No C# changed. Standards passed bar one wording
issue, efficiency passed (the validator pass costs ~10 ms of a 5.5 s run under a 45 s hook budget),
completeness passed (32 tests green, issue, CHANGELOG, doc, README row, rule row, lesson).
The engine agent verified all five assumptions the tooling rests on against the installed v1.5.3
DLLs (`Equipment.GetRandomEquipmentElements`, `EquipmentIndex`, `Equipment.IsItemFitsToSlot`,
`DefaultItemValueModel.CalculateArmorTier`, `DefaultCharacterStatsModel.GetTier`). The data-flow
agent reproduced every number in the feature doc from the live state (65 over-dressed after the
fix = validator = fixer, 1,283 under-dressed, 0 errors, 0 stat decreases against the `.bak`
files except the two documented reverts, the 9 cape appends the only slot-shape change) and found
one inconsistency. The tooling agent found two MEDIUM and two LOW.

**Fix state.** Every confirmed finding is fixed in the same changeset and the pipeline was re-run
from HEAD in one pass so the committed rosters are what the tool produces: 186 swaps over 118
troops, 60 Armory items restatted (59 up, 1 down), the tools suite green, the validator at 0
errors with the same 4 surfaced `UPGRADE_ARMOUR_REGRESSION` rows and the same 7
`CROSS_CULTURE_ARMOUR_INVERSION` cells as before the review.

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM (confirmed) | `plan()` took ONE anchor snapshot before the loop, so two troops demoted onto the same unworn variant in one run could not see each other: the level-16 `goblin_grunt` and six level-21 orcs all landed on `sk_md_orc_inf_chest_med_d`, which then priced at the medium band for the level-21s. The first apply had already produced this by accident across two passes. | stale state within a run | The picker was designed per pair ("nearest anchor band"), and the test fixtures each demoted ONE troop; nothing asked what the second pick sees. The author's own first-apply RCA ("two `_med_` chests can sit two bands apart") fixed the pre-run anchors and stopped there. | Troops are placed lowest level first and every pick updates `anchors[new]`; a test demotes two troops onto one unworn line and asserts they spread. The pipeline was re-run from HEAD so the data matches. |
| 2 | LOW (confirmed) | `split_id` bounded the tier token with `(?=_\|$)` while `mesh_tier_of` matches a substring, so 14 Armory ids of the shape `rivendell_torso_lord3_silver` / `thenn_armor_med1` were tiered by the validator and unsplittable by the fixer (they fell into the hand-decision bucket rather than a wrong swap; the two worn ones sit on level-46 lancers, legal). | two readers of one token | The split was written from the `_lord_c` shape in front of the author; the digit-suffixed Rivendell and Thenn lines were never grepped for. A shared `mesh_tier_of` was the stated invariant, but the SPLIT is a second parser of the same token and was not held to it. | `(?=[_0-9]\|$)`; a test pins the three digit-suffixed shapes and that `_lordly` stays untiered. |
| 3 | LOW (confirmed) | The change table printed no per-culture totals and no equipment-line count, while the under-dressed table (report only) had per-culture counts; the section a human is about to `--apply` had less summary than the one that writes nothing. | reporting parity | Each print function was written for its own list. | Per-culture counts, line total, and an `anchor L16, a band below` marker on every pick that lands off the troop's band (85 of 186 on the day), so the roster pass can read where the line had nothing at the band. |
| 4 | LOW (wording) | The CHANGELOG said "Backups `.bak-meshladder-609` beside each file" in a bullet that also described the troop-XML swaps, reading as if the fixer made them; the fixer writes only git-tracked rosters and the `.bak` files are the Armory restat's. | claim scope | A sentence written for the restat sat in the bullet for the whole pass. | The sentence now says "beside each Armory file"; the fixer docstring states git is its backup and names the writer's two inherited limits (not comment-aware; a troop's culture is its file's). |

**Not findings, recorded because an agent raised them.** The engine agent read
`GetRandomEquipmentElements` as grouping all armour slots on one set index; the code it quoted
re-rolls the three indices at the top of every loop iteration when a seed is passed, and campaign
battles always pass one (`.claude/rules/troops.md`), so per-slot mixing stands for battle spawns
and the fixer's "same old item to same new item in every set" invariant is the right one. The
tooling agent's "cross-band anchor share" on `sk_gb_uruk_chest_med_a` (level-21 fighters on a
level-16 anchor) is the ladder's own boundary: level 21 may wear medium mesh only, its stat band is
heavy, and every medium variant in that line is worn from 16; the marker in finding 3 makes it
visible, the choice is Mike's ladder, not a tool defect. The 4 `UPGRADE_ARMOUR_REGRESSION` rows
were traced edge by edge: no child's kit changed; each parent's freed mesh rose to its real band.
They belong to the under-dressed pass.

## Root-cause pattern

Both real code findings are the same shape: a rule stated once ("price by the lowest wearer",
"the tier token") implemented in two places that read different state. The anchor snapshot read
the rosters before the run while the loop changed them; the split read the token with a stricter
boundary than the tiering did. The fix in both cases was to make the second reader consult the
first's state (update the anchors; accept what `mesh_tier_of` accepts), and to pin it with a test
that exercises two of something, not one.

## Why each agent missed these

- **Standards** (haiku): read for convention compliance and prose; caught the wording, not the
  algorithm. Correct scope.
- **Engine** (sonnet): verified engine claims; the findings were in TAOM tooling.
- **Efficiency** (haiku): timed the code; a stale snapshot is not a cost.
- **Completeness** (haiku): counted tests and docs; it noted every public function had a test and
  did not ask whether any test exercised two troops at once.
- **Data flow** (sonnet): found the token divergence (finding 2) by grepping the live Armory for
  ids the two readers disagree on, exactly the cross-file trace the agent exists for; it also
  verified the anchors on the FINAL data and so saw finding 1's effect (`levelSpan` 5 on `med_d`)
  without naming the mechanism.
- **Tooling** (sonnet): found finding 1 by constructing the two-troop case from the apply log and
  the regenerated map, and finding 3 by comparing the two print functions.

## Feedback memories to codify

None new. The lesson in `docs/reviews/lessons/data-content-cultures.md` for #609 gains one
sentence: a picker that ranks by an anchor must update the anchor as it places, or the second
troop is placed against a world the first troop already changed.

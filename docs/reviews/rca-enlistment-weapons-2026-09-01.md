# RCA: the enlistment service kit had no weapons, and the first fix reproduced the bug (#525, #526)

**Date:** 2026-09-01
**Scope:** `Main/Features/Enlistment/Equipment/**`, `tools/generate_enlistment_rosters.py`,
`tools/audit_enlistment_roster_coverage.py`, `tools/audit_polearm_shield_parity.py`,
`Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml`
**Review:** 11 dimensions, 138 agents, every finding adversarially verified by three
perspective-diverse verifiers (claim accuracy / reproduction / materiality), majority to survive.
42 raw findings, 32 survived, 3 more from a completeness critic.

## Top line

Players reported drawing armour and never a weapon. They were right: the shipped rosters held 374
armour elements and zero weapon slots. The fix rebuilt the kit around
`enlist_{culture}_{assignment}_{rank}`.

**That fix then shipped 15 rosters that still had no weapon**, every one of them a Support cell, and
a full green suite plus two green gates plus `validate_moduledata.py` all passed on it. The defect
under repair was reproduced inside its own repair and nothing in the project could see it.

The single most useful sentence in this document: **every gate we had asked what a kit must NOT
contain, and none asked what it MUST.**

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|-----------|-------------------|
| 1 | HIGH | 15 Support rosters shipped with armour and zero weapons. `support_kit()` returned `{}` when the donor carried no OneHanded item; `pick_donor()` merged that empty map into the armour map and emitted the cell anyway. Worse than a thin kit: `EnlistmentRosterResolver` probes EXISTENCE, so a present-but-weaponless cell ENDS the fallback walk and shadows the armed kit the player would have descended to. | Missing lower bound | The generator's absent-cell contract was applied to the donor gate and not to the weapon-stripping path, which was written later and in a different function. No gate asserted weapon presence. | Suppress the cell when the weapon map is empty, for every assignment. Lower bound added to the Python auditor AND to a shipped-data C# test. Support now takes any melee sidearm; all 15 donors carried a spear or two-hander. |
| 2 | MED | No gate anywhere asserted that a kit contains a weapon, which is why #1 passed everything. | Gate asymmetry | Every rule was written as a prohibition (slot allowlist, no mounts, no Item4, no shield for support, ammo pairing). Prohibitions cannot express the feature's purpose. | `check_content` now fails a roster with no melee or ranged class. `EveryRoster_CarriesAtLeastOneWeapon` pins it where `dotnet test` runs. |
| 3 | MED | `ASSIGNMENT_GROUPS` mapped 3 of the 4 `default_group` values, so 23 HorseArcher troops belonged to no donor pool. Gundabad had no cavalry soldier while two of its horse archers sat under the cap; Rohan's and Rhun's signature horse archers could seed neither kit. | Enum coverage | The repo has an enum-coverage check for C# enums (deep-review Agent 5 rule 2). Nothing applied it to a Python dict keyed on engine data, and a 1:1 map reads as complete. | Values are tuples; `ALL_TROOP_GROUPS` derived from them; `parse_troops` raises if the troop data carries a group nobody maps. |
| 4 | MED | 18 (culture, assignment) chains emitted a byte-identical kit at two or more ranks; bluecraig and mistymountainorcs collapsed to one kit at all four. The ledger spends a draw per rank, so a promotion handed back duplicates of what the player was wearing. | Cross-cell invariant | Every check was per-roster. Nothing compared two cells to each other, so a chain that does not progress looks identical to one that does. | Generator suppresses a kit already emitted at a lower rank in the same chain (the resolver descends, so the outcome is unchanged and one roster replaces several). `NoRankChain_IssuesTheSameKitTwice` pins it. |
| 5 | MED | 17 chains issued strictly WORSE armour on promotion (Erebor infantry 176 to 99 at the first promotion; Dolguldur archer 175 to 85 at the last). | Invalidated invariant | Donor trees are not monotonic in armour, and the hard cap picks by band proximity alone. Nothing compared a cell to the rank below it. | `pick_donor` takes a per-hit-zone floor, raised as each rank is emitted, applied as a filter that yields rather than lose a cell and prints the waiver when it does. The first version of this fix used a single-stat proxy and was itself defective; see finding 10. |
| 6 | MED | Honourable discharge reclaims the kit by item id, one per ledger entry, draining the unmodified stack with no provenance. Since the ledger now holds ammunition, a player who refilled arrows from loot has one of his own stacks taken. | Scope ripple | The reclaim was written when the ledger held only armour. Nothing in the changeset looked at the OTHER end of service. | Documented as a known limitation in `docs/features/enlistment.md` and filed as a follow-up; the real fix needs an item-class surface `IItemPoolAdapter` does not expose. |
| 7 | LOW-MED | Documentation-accuracy cluster: "13 rosters" was 13 findings across 10 rosters; "every Mordor player start across all four archetypes" was 4 of Mordor's 10; a tool comment claimed 94/55 where the tool printed 97/58; a comment dated the same day measured 374 `<Equipment>` elements in a file that had 2,019; "the cultures missing cells are exactly the race-divergent ones" was 15 of 20 cultures. | Never-Fabricate breach | Numbers were written from the run that produced them and not re-measured after later commits in the same session changed the artefact. A measurement is only true of the artefact it was taken from. | All corrected. See the pattern below. |
| 8 | LOW | The resolver's stated rationale ("culture outranks assignment as a RENDERING invariant, because cross-race armour clips") does not hold: the roster is keyed on the COMMANDER's culture, so it cannot know the player's race under either ordering. | Plausible-but-wrong rationale | The argument was constructed to justify a decision already made, and it sounded right. Nobody asked whose skeleton wears the gear. | Rationale replaced with the true one (#427/#431: issuing another faction's kit is the reported defect). The clipping hazard is real and is now recorded as a known limitation instead, where it belongs. |

## Root-cause patterns

### A. Gates enumerated prohibitions; none expressed the purpose

Finding 1 is the whole document. The auditor was rewritten specifically for this change and given
six rules, all of the form "must not": slot allowlist, no mounts, no `Item4`, no duplicate id, no
empty roster, no shield on support. It also gained per-assignment content rules, which look like
requirements but are conditional ("an `_archer_` roster must carry a bow") and therefore vacuous on
a roster with no weapons at all.

The feature's one-sentence purpose was "the kit contains weapons". Nothing asserted it.

**Prevent:** when a gate is written FOR a defect, the first rule in it states the defect's negation
as a positive requirement, before any refinement. A gate whose rules are all prohibitions cannot
fail on the absence of the thing it exists to guarantee.

### B. Narrowing a pool silently invalidates every guarantee the wider pool provided

Filtering donors by `default_group` was one decision. It invalidated three separate implicit
guarantees, and the session noticed exactly one of them:

| Guarantee the whole-culture pool provided | Status |
|---|---|
| An in-band donor almost always exists | NOTICED, became the hard cap |
| Some donor carries a OneHanded weapon | MISSED, became finding 1 |
| The next rank's donor is not worse-armoured | MISSED, became finding 5 |

Having caught the first, the session treated the risk as discharged. It was the same class of
consequence three times over.

**Prevent:** when a change narrows a candidate set, enumerate what the wider set was implicitly
guaranteeing and check each one separately. Finding one such guarantee is evidence there are more,
not evidence you are done.

### C. A measurement is true only of the artefact it was taken from

Finding 7 is five instances of one mistake: a number measured mid-session, written into prose, and
left standing after a later step in the SAME session changed the thing measured. The repo has
shipped this before, which is why `docs/features/enlistment.md` said 68 when the file held 84.

**Prevent:** re-run every quantitative claim against the final artefact immediately before the
commit, not when the number is first learned. Treat a number in prose as a cached value with no
invalidation.

## Why the implementation missed these, and the review did not

The 11 review dimensions caught all of them; the implementation session caught none. Worth being
precise about why, because "run a review" is not a transferable lesson.

- **Finding 1 needed someone to ask what the shipped file CONTAINS**, not whether it validates. The
  implementation checked the census for forbidden slots (`Horse`, `Item4`) and confirmed zero. It
  never censused for required ones. Four dimensions found it independently within minutes by
  parsing the file and asking the opposite question.
- **Findings 4 and 5 needed cross-cell comparison.** Every artefact the implementation produced was
  per-roster, and so was every check. The review's generated-data dimension diffed rosters against
  each other, which no gate and no test had ever done.
- **Finding 3 needed a census of the INPUT vocabulary.** The implementation enumerated the four
  `ServiceAssignment` values (a C# enum, visible) and mapped them to `default_group` strings
  (engine data, not enumerated). Counting the distinct values actually present in `troops_*.xml`
  is a five-second command nobody ran.
- **Finding 6 needed someone to look at discharge**, which is not in the changeset. Only the
  completeness critic, whose explicit brief was "what did nobody look at", went there.
- **Finding 8 needed someone to ask whether a stated reason is true**, rather than whether the
  decision is right. The decision was right. The reason was not.

The transferable part: the highest-yield dimensions were the ones asking the *inverse* of what the
implementation had asked, and the one asking what was *not* in the diff.

## Lessons to codify

Appended to `docs/reviews/lessons/`:

- `data-content-cultures.md`: a generated data gate must assert the positive property the generator
  exists to produce, and cross-cell invariants need cross-cell checks.
- `testing-qa.md`: narrowing a candidate set invalidates the wider set's implicit guarantees; find
  all of them.
- `localization-ui.md` (already appended during implementation): the translation cache is keyed by
  string id, so rewording English silently refills the stale translation.

## Second pass: Codex, against the fixed tree

An independent Codex review (gpt-5.5, xhigh) ran after the fixes above and returned 2 HIGH, 4
MEDIUM, 2 LOW. It disputed none of the internal findings and found five more things, which is the
argument for running both: the internal review is broad, Codex re-derives from the engine.

| # | Sev | Finding | Why the internal pass missed it |
|---|-----|---------|--------------------------------|
| 9 | HIGH | `EveryCultureWithAnyRoster_ResolvesForEveryAssignmentAndRank` derived its culture list from the roster file it was auditing. Deleting every `enlist_vlandia_*` row removed Rohan from the test's own input; renaming them to `enlist_rohan_*` made it accept a StringId that does not exist. Both stayed green while runtime Rohan fell to the neutral default. | The internal pass verified the test PASSES and that its assertions are meaningful. Nobody asked what MUTATION it would survive. A self-derived input set is invisible unless you try to break it. |
| 10 | HIGH | The armour floor summed one "primary" stat per item. A body piece contributes body AND leg armour, a cape body AND arm, so an Aserai cavalry promotion lost protection in all four zones while the proxy rose. `rebalance_armor.py:146` already recorded this exact blind spot. | The floor was added DURING the internal fix round, so no dimension reviewed it. A fix written in response to a review is unreviewed code. |
| 11 | MED | `--seed-missing` skipped every row with a truthy `why`, and successful rescue rows carry `why="RESCUE, cap waived"`. Deleting a rescue roster and running the advertised repair would not restore it. | Introduced by the duplicate-suppression fix minutes earlier. Same cause as 10. |
| 12 | MED | The `KNOWN_FAILURES` ratchet keyed on `(roster, item)` with no count, so 10 keys suppressed 13 occurrences and a roster gaining a SECOND copy of the same unusable polearm would file as old debt. | The internal pass checked that the ratchet cannot suppress a DIFFERENT new item, and stopped there. Multiplicity is a third case neither of us had a rule for. |
| 13 | MED | Umbar kits issue Dunland and Rohan armour and a Noldor bow. Outside the documented Anorien carve-out. | The internal generated-data dimension looked for race mismatches against the roster's race, not against the culture's own dominant asset folders. Now surfaced by a per-run advisory (#528). |
| 14 | LOW | Four comments called `Item4` "the banner slot". The installed enum names it `ExtraWeaponSlot`; a banner is one eligible occupant. | Verified the VALUE (4) against the engine and not the NAME. |

**The pattern in 10 and 11 is the important one: two of the six are defects in code written to fix
the first review.** A fix produced under review pressure gets less scrutiny than the code it
replaces, and the review that prompted it has already finished. Re-running the gates is not the
same as re-reviewing the fix.

Codex also confirmed, by independent derivation, the things it was asked to attack: the resolver's
double generator enumeration is correct for every reachable input, the three suppression rules do
not starve any of the 320 requests, `ServiceContentRecord` normalises an invalid persisted
assignment to Infantry via `Enum.IsDefined` so old saves cannot deserialise to garbage, and the
Support weapon policy contradicts neither `BattleFormationPolicy` nor `AssignmentSkills`.

## Follow-ups not fixed here

- **#526** the 13 ratcheted shield+polearm findings across 10 rosters, including 8 Mordor player
  starts. Held in a dated `KNOWN_FAILURES` ratchet that fails if an entry stops matching.
- **Discharge reclaims fungible ammunition** (finding 6). Documented, issue filed.
- **bluecraig and mistymountainorcs ship one troop each**, which is a content gap rather than a
  generator defect: their kits cannot progress because there is nothing to progress to.

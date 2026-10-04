# RCA: Uruk-hai skirt moved to the body slot, review findings (2026-10-04)

Review: a four-lens deep review (XML and ModuleData, completeness, data flow, design) of moving
`sk_uruk_hai_skirt_a1` from the cape slot to the body slot on `urukhai_champion` and `urukhai_berserker`
(#729), with an adversarial verifier on each finding, then one convergence pass on the fixes.

## Top line

The change the maintainer asked for, two `slot` attributes, was correct: the engine accepts body armour
only in the body slot (v1.5.3 `Equipment.cs:482-483`), and after the move plan 038's slot rule refuses no
assignment across all 5,143 characters of the five modules. No lens found a defect in the data. The
findings sit around it: text in the tools, docs and the ModuleData rule that described the old placement,
a validator allowlist whose stated reason the move made false, a release-note claim about the look that
would have been wrong for the Berserker, the skirt's stats going live for the first time, a missing
issue, and the missing slot-fit gate that let the defect live for four and a half months. Each was fixed
or decided by the maintainer: the stats stay as authored, both troops stay on the allowlist with its
reason corrected, #729 tracks the fix and its in-game check, and the slot-fit gate is a follow-up.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| S1 | LOW | `_BODYLESS_BY_DESIGN` still listed both troops with text saying their skirt sits in the cape slot as the chest stand-in; the move made that false (lens 7 XML-1, lens 4 C1, lens 5 F1; verified, MEDIUM to LOW) | Allowlist reason restates a data fact | The entry's reason described where an item sat, so it changed meaning when the data changed, with no check tying the two | Maintainer's decision: keep both entries (their body and cape stay out of the armour comparisons); the stated mechanism corrected in `taom_schema.py`, the two fixers and both docs; lesson below |
| S2 | LOW | Seven places stated the skirt-in-cape placement as current fact, two of them as a "70-armour skirt" where the validator sums 89 (lens 4 C2, lens 5 F2, lens 6 D6-1, lens 7 XML-3) | Stale text after a data change | Each sentence restated the data instead of the rule | Reworded to the rule, the test comment put in the past tense |
| S3 | LOW | The planned release note said the skirt never rendered; for the Berserker it did, as his race's underwear (lens 4 C4, lens 6 D6-2) | Look claim from the item alone | The review brief read the slot and the item, not the race's `skins.xml`, where an empty body slot draws `underwear_bottom_mesh` | The commit body and #729 say the Berserker looks the same and the Champion changes; lesson below |
| S4 | LOW | The skirt's stats (20.7 kg, arm armour 38, the highest of any Isengard body piece) apply in game for the first time (lens 5 F3) | Balance consequence | The rebalance pipeline statted the skirt as worn while the engine refused it | Maintainer's decision: keep as authored, judge in game (#729) |
| S5 | LOW | No issue tracked the fix or its in-game check (lens 4 C3) | Process | The fix came as a direct request | #729 filed with the checklist; `triage-needs-ingame` at close |
| S6 | LOW | No commit-time gate models `Equipment.IsItemFitsToSlot`, so a mis-slotted item ships silently (lens 7 XML-2, lens 5 F4, lens 6 D6-3) | Gate coverage | Only plan 038's report tool models the rule, and it exits 0 | Maintainer's decision: an `ITEM_SLOT_REJECTED` error in `validate_moduledata.py` as its own change, listed in #729 |
| C1 | LOW | The corrected text called the two troops the Uruk-hai "capstones"; they sit mid-tree (swordman, champion, berserker, nazg-hai), so three upgrade edges lose the Body and Cape comparison, not one (convergence D1) | Wrong label carried into new text | The label came from the rule row written 2026-09-02 and was copied into each rewrite | The two troops are named in all four places |
| C2 | LOW | The fixer's example still described the old cape placement (a nazg-hai's pauldron against a berserker's skirt). The edge the Cape exclusion now decides is swordman to champion, and the Body exclusion keeps the nazg-hai's plate (79) from reading as a drop from the skirt (89) (convergence D2, verified in part) | Stale example | The rewrite changed the mechanism sentence and kept the example word for word | The example names the two edges the exclusions now decide; the validator comment names both cases |
| C3 | NIT | Three texts the first sweep missed: a test docstring's "three", an over-general docstring, an unwrapped comment (convergence D3 to D5) | Incomplete sweep | The sweep searched for the old phrases, not the old counts | Fixed |

## Root-cause pattern

Text and allowlist reasons that restate a data fact (which slot an item sits in, how many troops an
exemption covers) rot the moment the data changes, and nothing ties them back. The defect itself had no
gate: the engine refuses a mis-slotted item without an error a player or the validator sees, so it lived
from the #212 revamp (`2787a6db`, 2026-05-23) until plan 038's audit modelled the rule.

## Why each agent missed these

The four lenses found every finding here; the earlier work missed the defect itself.

- The #212 revamp script and its review had no model of the engine's slot rule, and `validate_moduledata.py`
  checks references, not slot fit.
- Plan 038's audit found the refusal (its review, 2026-10-03) but is a report tool that exits 0.
- The review brief for this change described the look from the item's flags and missed the race's
  underwear mesh (S3); the completeness and design lenses caught it from `skins.xml`.

## Feedback memories to codify

- When a data change moves or removes what an allowlist entry or a doc sentence describes, grep the
  allowlists and the docs for that troop or item and change them in the same commit
  (`lessons/data-content-cultures.md`).
- Before describing how a troop's look changes, read its race's skin in `skins.xml`: an empty body slot
  draws the race's `underwear_bottom_mesh` (`lessons/data-content-cultures.md`).

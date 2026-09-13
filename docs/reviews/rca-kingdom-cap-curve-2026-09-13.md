# RCA: the kingdom-cap armour curve (#583) deep review

**Scope.** Six review agents (standards, engine compatibility, efficiency, completeness,
cross-system data flow, tooling correctness) over the uncommitted #583 changeset after it had
been applied to both Armory trees: the cap curve in `tools/rebalance_armor.py` (`KINGDOM_CAPS`,
slot and band ratios, prefix routing, `--tier-source roster-first`, the binary writer with
backups), the anchor changes in `tools/derive_armor_tiers.py`, the kingdom invariant in
`tools/analyze_armor_balance.py`, the reference-cap scaling in the `CROSS_CULTURE_ARMOUR_INVERSION`
gate and in `tools/analyze_kingdom_armour.py`, the ladder repair's 12 troop files, and the docs.
No C#. Standards, efficiency (dry run 0.23 s, apply 0.2 s per tree) and completeness passed.
Compatibility verified all five engine claims on the installed 1.4.8 DLLs (flat, independent,
`num > 0`-guarded modifier bonus; the legendary deltas 3/5/7/9/12; `material_type` read only by
sound and FX code; no clamp on load; the tier and price formula) and added one consequence to the
doc. The data-flow agent reproduced the applied values by hand on twelve items and the 0/1,835
anchor-consistency sweep, and found two real inconsistencies. The tooling agent found two MEDIUM
latent defects and one LOW in the writer.

**Fix state.** Every confirmed finding below is fixed in the same changeset; the tools suite is
green, the live tree still plans nothing under a dry run, both Armory trees stay byte-identical,
and the gate still names the same seven cells with the tool's preview agreeing.

---

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM (confirmed) | `derive_armor_tiers.derive()` kept keyword-first precedence (`_heavy_` in the id beat a level-46 wearer) while the writer's `--tier-source roster-first` reads `anchorLevel` directly. The applied values were right; the map's `tier`, `target` and `status` columns, and the `ROSTER-TIERS.md` report anyone reads for restat candidates, were wrong for 682 of 1,861 worn items (651 numerically), under a docstring claiming the two "cannot disagree". | two consumers, one claim | The writer was given its own precedence function instead of the map's, and the shared-function claim was written about `level_to_band` (which IS shared) while the precedence around it was not. The first review's lesson ("a rule reused by a second consumer answers the second consumer's question") was applied to the exempt set and the alias table, not to this pair. | `derive()` is anchor first, keyword only for unworn kit, civilian keyword kit civilian whoever wears it; a test drives `derive()` on a fixture and pins all three. |
| 2 | MEDIUM (confirmed) | The gate and the tool scaled a troop's total by its CULTURE's cap while items are routed to a cap by id prefix and folder. An Umbar noble in full Black Numenorean plate (57-cap kit) was scaled by Umbar's 44 and read 29% higher than it was; 33 troops across six cultures wear off-cap kit. None of the seven flagged cells was driven by it; Umbar's inflated value sat in the comparison field of two of them. | scaling granularity | Culture-level scaling was the quick reading of "each kingdom has a cap". The item routing (`kingdom_key`) already existed in the writer; the gate did not reuse it because the validator's registry carried no item folder. | `Registries.item_folder` (`build_item_folders`), `taom_schema.item_cap_for(item_id, folder)` routes through `rebalance_armor.kingdom_key`, `_slot_armour_avg` scales per item, `analyze_kingdom_armour.troop_scaled_total` mirrors it; the `kingdom_cap_for` culture map is deleted. A test puts identical 200-armour kit on two cultures with different folders and asserts the verdict follows the item's folder. |
| 3 | MEDIUM (latent) | `_roster_first_tier`'s civilian guard compared the map's `tier` against `'civilian'`, a value `id_keyword_tier` could never produce (its keyword list had no civilian branch), so a civilian item whose id also carried a tier word would have been curved onto that band. No live instance (the civilian dresses carry no tier word). | dead guard | The guard was written against the writer's own `detect_tier` vocabulary, which does know civilian words, not against the map's detector that actually feeds it. | `id_keyword_tier` returns `civilian` for `_civ` / `civilian` ids before any tier word, and `derive()` honours it over the anchor; tests pin the detector and the map. |
| 4 | MEDIUM (latent) | `apply_changes_via_regex` matched the first `id="<id>"` in the file, including one inside an XML comment; the Armory keeps commented-out `<Item>` blocks for reference (three exist today, none in the `*_armors.xml` files this tool opens). An edit would have landed in the dead copy, reported success and left the live item untouched. | regex vs comments | The block regex predates this pass and the comment case never came up in the five armour files it reads. The masking corollary in `.claude/rules/moduledata-validation.md` is about writing, and this was a read. | `_inside_comment` skips a match whose nearest preceding `<!--` is unclosed; a test puts a commented copy before the live item and asserts only the live one moves. |
| 5 | LOW (confirmed) | `out.lstrip(b"\xef\xbb\xbf")` is a character-set strip, not a prefix strip. Harmless on every real file (the next byte is `<`), wrong in principle. | bytes API | Read as "strip the BOM". | `out[3:] if out.startswith(BOM) else out`. |
| 6 | LOW (docs) | `docs/modding/balance-levers.md:139` still calls `SLOT_BASELINES` and `CULTURAL_MODS` the source of truth. | stale doc | Another session holds that file uncommitted; the sweep found it, the edit waits for the file. | Line corrected once the file is free (tracked in the CHANGELOG entry's owed list). |

**Not a finding.** The tooling agent's reconstruction from the `.bak` files counts 2,507 changed
and 319 untouched where the doc says 2,510 and 316; the doc quotes the tool's own summary line
from the real run, and the reconstruction ran against a map re-derived after the ladder repair.
The reverse routing sweep (every folder and prefix to a cap or the legacy path), the `starter_*`
twins (never opened by the curve loop, still at their #569 floors), the ladder repair's anchor
stability (0 of 1,835 worn items whose live stat disagrees with its anchor band), both-tree parity
(101 files, 81 backups each) and the headline counts (58 edges, 197 swaps, 12 files) all
reproduced exactly.

## Root-cause pattern

Findings 1 and 2 are the previous review's lesson in a new pair: a rule reused by a second
consumer answered the first consumer's question. The map and the writer share `level_to_band` but
not the precedence around it; the gate and the writer share the caps but not the routing. Both
times the shared piece was the innocent one and the divergence sat one call above it. Findings 3
and 4 are a vocabulary mismatch and a regex written for a file shape that had not yet appeared.

## Why each agent missed or caught these

- **Standards** reproduced every stated number and passed; precedence and routing are outside its
  checklist.
- **Compatibility** verified the engine claims and added the tooltip-tier consequence; that was its
  remit.
- **Efficiency** measured everything and found nothing, correctly.
- **Completeness** checked coverage and found the stale doc line (finding 6).
- **Data flow** found findings 1 and 2 by recomputing twelve items by hand against the map and by
  quantifying which troops wear off-cap kit: the two questions the map's columns and the gate's
  scaling claimed to answer.
- **Tooling correctness** found findings 3, 4 and 5 by reading the writer's guards against the
  vocabulary that actually feeds them and by scanning the live Armory for the comment shape.

## Feedback memories to codify

One lesson, appended to `docs/reviews/lessons/build-tooling-workflow.md`: when two tools are said
to share a rule, the shared symbol is not the claim; test the pair on a fixture where the
precedence or routing around that symbol differs. No new memory file.

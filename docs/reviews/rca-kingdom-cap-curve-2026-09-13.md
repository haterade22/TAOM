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

## Codex adversarial pass (gpt-6-astra, ultra, 2026-09-13)

Dispatched after the deep review's fixes, on the working tree that became `e4de8a78`..`b41721ab`.
Verdict: no P1, four P2, three P3, all confirmed here against the source, the live Armory and the
installed 1.4.8 DLLs; zero false positives. Codex reproduced the applied values (derive then dry
run: Changed 0), the ladder repair (0 edges, 0 swaps), the validator (0 errors, 7 cells) and the
tree parity (88 XML and 81 backups identical) before it looked for defects, and disputed three of
its eight Known Suspects with executed arithmetic (S1 the zero-secondary case: no live instance;
S6 the comment regex: zero comment markers outside parsed comment spans across 131 files; S8
retained plate on medium pieces: the twelve are all excluded Dain kit). Its full-suite run could
not reproduce the green suite (no `pytest` in its sandbox for one file, a `PermissionError` on a
settlement-floor fixture); both are its environment, not the change.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 7 | P2 (confirmed) | `check_kingdom_curve_invariant` proved the PRIMARY stat only while its docstring, the feature doc and the CHANGELOG said no legendary roll passes the elite piece. A secondary keeps its item's own ratio, and where that ratio is small the flat bonus outgrows the tier gap: the live Isengard pauldrons roll medium arm 5 + 5 against elite 7. The live sweep in `analyze_armor_balance.py` lists 50 roster-backed secondary cases (40 chest `leg_armor`, 10 shoulder `arm_armor`), every one an inherited ratio the restat preserved. | claim wider than the proof | The pure function was written for the constants, the sentence about secondaries was written from the ratio rule ("they follow the primary") without asking whether a ratio can be small enough for the bonus to matter. | Docstring, doc and CHANGELOG say primary only and point at the live sweep as the secondary judge; the 50 cases go to the roster pass, and a secondary floor per band is recorded as a curve decision, not a fix. |
| 8 | P2 (confirmed) | `ceilings()` judged an unworn item's tier through `tier_from_value(primary, slot, folder)`, which routes by folder alone, so an `sk_dg_` item in the rhun folder at its own elite value (41) read as Rhun heavy (43 is nearer than 51) and left the reserve: 50 helmets and 12 chests, Rhun's reserve 114 where it is 176. | a new routing dimension not reaching a caller | The deep review had just repaired this function's CULTURE (wearer to folder); the cap model added a second dimension, the item id, and `tier_from_value` was never given it. Same pattern as the two deep-review findings, one call further down. | `tier_from_value(..., item_id=None)` routes through `calculate_stats`; the overview and the `--weights-only` path pass the id; a test puts a sub-line item in a shared folder and asserts it stays in the reserve. |
| 9 | P2 (docs) | The doc said nothing in the shop or loot code filters on the display tier. `DefaultItemCategorySelector` files an armour item as Garment / Light / Medium / Heavy / UltraArmor by that tier and `WorkshopsCampaignBehavior` picks a workshop's output by category, so an Erebor light chest that moved from Tier3 to Tier4 changed which workshop makes it. | an absolute claim from a partial read | The compatibility agent checked the tooltip and the price and generalised to "nothing filters". | Doc rewritten: no ban, but category and price follow the tier. |
| 10 | P2 (docs) | How-To lines still said `CULTURAL_MODS[culture]` sets a culture's protection (Gondor at protection 99 still writes a 57 chest), that `--no-lower-armor` preserves material (it stopped doing so on 2026-06-30), that `--tier-source roster` skips every unworn item (it skips only keyword-less ones), and a loader comment still called the map keyword first. | stale How-To | The sweep for stale statements covered the sections that describe the curve, not the older How-To rows below them. | Rows rewritten to the caps and the explicit preservation flags; the comment corrected. |
| 11 | P3 | The curve view's prediction is a generic benchmark (the culture's default line at the band, every slot filled, legacy-proportion secondaries), presented as if it were a per-item target; a Black Numenorean in `troops_mordor` is held against the orc cap. | unlabelled benchmark | The table header described the arithmetic, not its limits. | Header and doc label it; the function docstring says what it is not. |
| 12 | P3 | The overview had no view of kit from another line (an Umbar noble in Black Numenorean plate, a Rhun archer in Dol Guldur's helmet) or of uncurved vanilla kit above the culture's elite slot value, both of which the roster pass needs and neither of which the gate shows once every item is scaled to its own line. | missing observation | The gate's scaling made the cases invisible by design; nothing surfaced them as data. | `off_line_kit`: 162 rows over 33 troops (Isengard orcs in Mordor orc kit 54, Rhun in `sk_dg_` kit 53, Mordor militia in Black Uruk kit 36, Umbar in Black Numenorean plate 15, the elves in Gondor kit 4) and 11 uncurved rows (Dunland's `tall_helmet` 38 and `plumed_helmet` 47 over 36, Harad's `aserai_scale_armor_on_chain` 51 over 44 and `strapped_mail_chausses` 23 over 22). A Mordor troop line named `mordor_num_` or `mordor_uruk_` is held to its own cap, as `ranged_ladders.json` routes it, so the Black Numenorean and Black Uruk troops are not mislabelled as imports. Tests pin both lists and the sub-line rule. |
| 13 | P3 | One live secondary is path-dependent by a point: `rivendell_torso_heavy_tier3_silvergoldb` passed through two bands (60/40/35 to 68/45/40 to 57/38/34) where a direct pass gives 33, because each apply rounds from the item's CURRENT ratio. | rounding history | Idempotency was tested (a second apply changes nothing); a re-anchor between applies was not. | Documented as the convention; the property that holds is that a dry run after any apply plans nothing. |

**Root-cause pattern, continued.** Findings 8 and 12 are the deep review's pattern a third and
fourth time: the cap model added a routing dimension (the item id) and one caller (`tier_from_value`)
and one consumer (the overview's benchmark and observations) were still keyed on the folder or
the file culture. Finding 7 is the other recurring shape: a proof over constants whose prose
claimed the live property. Both lessons are in `lessons/build-tooling-workflow.md`.

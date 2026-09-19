# RCA: second deep review of the ranged rebalance (#617, 2026-09-18 evening)

**Top line.** Mike asked for the first review's write-up
(`docs/reviews/rca-ranged-rebalance-2026-09-18.md`) to be audited and for a new review of #617 end to
end, XML as critically as the Python. By then the five #617 commits (`b37fc22d`, `697cbdb6`,
`c3592f62`, `8465cc98`, `8800aa4c`) were pushed and inside tag `v2.0.30`, released to players at 19:50,
so this reviewed shipped work. Eight lenses ran as `deep-reviewer`, in two waves of four (Standards,
Engine compatibility, Data flow, XML; then Efficiency, Completeness, Design, Tooling), and all eight
reported. The shipped data came back clean a second time, independently: the 16 troop files (559
ladder-to-ladder slot swaps and 186 skill values, nothing else), the 123 generated items (differing
from their donors only as intended), the 65-item restat and the 156 translated files. What the review
found was in the tools, the records, and three decisions that were Mike's to make. One HIGH (the
roster tool could write rosters onto an item nothing defines, exit 0), no CRITICAL. The first RCA
carried one fabricated fact and five smaller inaccuracies, and one of its fixes rested on a 1.4.8
engine rule. A deep review of the fixes followed on 2026-09-19, with three MED findings (a
quote-pinned guard, a report still judging ladder archers by the curve, and three wrong sentences in
this round's own corrections); see "The fix-diff review" below.

## Findings

| # | Lens | Sev | Bug | Category | Why missed | Fix and prevention |
|---|---|---|---|---|---|---|
| 1 | Tooling | HIGH | `rebalance_ranged_ladders.py` merged `retired_ladder_launchers` placeholders into the launcher index BEFORE its "item must exist" guard. The retired-id regex matches `t<N>` too, so a current cell no file defines (generator never ran, Armory reinstalled) became a placeholder: `--apply` pointed more troops at it with exit 0, and a deleted items file read clean. | Guard computed after synthetic data is merged | `retired_ladder_launchers` was written for the band-to-tier migration and its tests used band ids only; the guard predates it, and nobody re-read the guard when a merge went in above it. | A planned id among the placeholders stops the run (exit 2, report mode included). Tests `test_apply_refuses_a_cell_the_rosters_name_but_no_file_defines`, `test_a_deleted_items_file_is_not_a_clean_run`. Lesson: testing-qa. |
| 2 | XML | MED | `tools/translation_cache/<lang>.json` (12, tracked) held the 130 retired ids and none of the 123 tier ids. `rebuild_translation_files.py`, the recovery after an Armory reinstall, resolves override, then cache, then English, so it would write English over all 1,476 carried names; the translator would pay to redo them. | Rename treated as an addition, second half | Repeat offender: `docs/reference/localization-map.md:20` already said any tool rewriting translated text must update the cache (`retune_career_health.py` is the worked example). The sync tool was written against the loc files only, and no rule that loads on `tools/**` carries the cache requirement. | The sync tool keeps each cache in step (current ids from the live rows, retired ladder ids dropped, nothing else touched; `--verify` checks it). Applied: exactly -130 / +123 lines per cache. Lesson extended: localization-ui. |
| 3 | Engine | MED | The first review's `skill_template` fix rested on "the engine reads the template, never the inline skills". On the installed 1.5.3, `BasicCharacterObject.Deserialize` (`:337-365`) copies the template into a fresh `MBCharacterSkills` and then applies the inline rows; 1.4.8 (`:355`, `if (mBCharacterSkills == null)`) ignored them. The refusal of a templated ladder troop rejected a configuration that works. | Engine rule carried across a version | The rule came from `taom_schema.py`'s `SKILL_TEMPLATE_SHADOWS_SKILLS`, whose comment cites "v1.4.8, BasicCharacterObject.cs:337-358"; the first review's engine lens hit the rate limit and the check was done by me on the dump for the missile formulas only. The 1.5.x bump did not re-verify rules the validator states. | Mike: correct #617 only. The refusal is gone, a templated ladder troop takes its cell, templated edges are still skipped (templates are not resolved), and the text in the tool, feature doc and first RCA now names both engine versions. The validator gate (still an ERROR on the 1.4.8 premise) goes to #626. Lesson: adapters-taleworlds-api. |
| 4 | Data flow | MED | #617 flattened the Iron Hills noble crossbowmen from their #366 hand-tune (Crossbow 175 / 225 / 275) to the Erebor cells (120 / 160 / 195) while `rebalance_troops.SKIP_TROOP_IDS` and two feature docs still claimed the hand-tune; the skip ran before `ladder_cells`, so the clamp's cell guard never covered them. | A second writer ignored the first writer's protection list | #617's roster tool never read `SKIP_TROOP_IDS`: the list lives in the other writer and nothing tied the two. The same family as the first RCA's items 4 to 7. | Mike kept the ranking (a crossbow's edge is its item; Crossbow skill adds no damage on 1.5.3). The ids left `SKIP_TROOP_IDS`; a rebaseline now gives them their cell and leaves them as they are on disk; the docs record the reversal. Test `test_the_iron_hills_nobles_take_their_erebor_cell_in_a_rebaseline`. Lesson extended: data-content-cultures. |
| 5 | Data flow, Engine (independently) | MED | Field Commission (`HeroCommissionAdapter`) copies a troop's first battle set onto the new companion, so a commissioned archer keeps its ladder bow as saved hero equipment; 9 cells exceeded `hero_ceiling` (a T10 Ithilien ranger's bow hits 119, cap 90), 12 once the crossbow decision below raised Gondor's T7 and T8 and Rhun's T7 crossbows (107 / 126 / 107, cap 105). The first RCA's item 10 said a ladder item cannot reach a player. | Runtime C# path outside the XML gates | Item 10 was refuted on vanilla paths only; `hero_launchers` skips `troops/` by design; the C# that moves troop kit onto a hero was in nobody's scope, and the first review's engine agent had failed. | Mike: swap a ladder launcher for the line's donor at commission, a C# change with its own issue (#625), TDD and review. Documented in the ceiling rule row, the feature doc and the validation doc as the path the sweep cannot see. |
| 6 | Tooling | MED | The sync tool kept any `ladder_*` row its regex could not see (attribute order, quotes, a row split over lines) as an ordinary line: a current id written twice, `--verify` passing. | Parser blind spot | Census showed every row canonical today; the fixtures only used the canonical shape. | A ladder row the tool cannot parse stops the run. Test `test_a_ladder_row_the_tool_cannot_parse_is_refused`. |
| 7 | Tooling | MED | The sync tool wrote 156 live files with no backup, against the convention its own docstring cites. | Convention skipped | Treated as single-use with the mirror's history as the backup. | `.bak-rangedsync` once per live file; the mirror (git) gets none. Test `test_apply_backs_up_each_live_file_once`. |
| 8 | Tooling | MED | The restat and sync tools never validated the spec: a `donor_stats` row with misspelled keys matched its weapon, set nothing and `--verify` said OK; an unreadable spec was a traceback. | Unvalidated input | The generator and roster tool validate; the two newer tools did not copy it. | `rl.validate_restat_tables` (extracted from `validate_spec`) in the restat; full `validate_spec` in the sync. Tests `test_a_donor_row_with_no_known_stat_is_refused`, `test_a_spec_that_contradicts_itself_or_cannot_be_read_is_refused`. |
| 9 | Completeness | MED | The `lotraom-assets` mirror is 3 commits ahead of origin (#609's `6de86962`, #617's `391f69b7` and `c77a842f`) while v2.0.30 ships troops that reference the items; nothing in the repo recorded it. | Owed action unrecorded | The only record was a memory line. | Recorded here, in the CHANGELOG and on #617. Mike did not choose to push it this session. |
| 10 | Completeness | MED | `check_external_loc_coverage.py` is red today and the first RCA and CHANGELOG described its remainder as closed. | Red gate with no owner | The remainder is other features' rows, so it was read as not ours. | Named: 12 ids missing from every `loc_LOTRAOM_horses.xml` (the #602 bardings and #616 spider mounts) plus identical-to-English rows; #617 adds only DE's three inherited "Arbalest" rows. Owner: #602 and #616. |
| 11 | Standards | LOW | The CLAUDE.md trap row #617 wrote is 433 characters against the 400 cap; `lint_docs.py --fail-on-drift` exits 1 on it. | Budget | The hook that should deny it logs no decisions; how it landed is unknown. | Row rewritten at 396. The file-size overage (48 KB against 46 KB) predates #617. |
| 12 | Standards | LOW | The `RANGED_DAMAGE_CEILING` rule row missed the player rosters the first fix added and quoted pre-restat numbers nobody measured ("97 to 130, accuracy 100"). | Stale rule text | Rule rows are not regenerated from the code. | Row and validation doc rewritten from a measurement: the 38 donors ran 62 to 130 at accuracy 70 to 100; nine hero-reachable ones hit 92 to 112. |
| 13 | Data flow | LOW | The enlistment quartermaster's `enlist_*` rosters (268, 84 of them carrying a launcher; applied to the player at runtime) were outside the ceiling sweep. No breach (max `crossbow_d` 93). | Runtime-applied data outside the sweep | The first fix listed the prefixes it knew. | `"enlist_"` in `PLAYER_ROSTER_PREFIXES`; the test covers it. |
| 14 | Data flow | LOW | Between a generator run and the roster apply, a `rebalance_troops.py` rebaseline could not class a retired id and would give archers the level-curve Bow. | Two writers, retired ids known to one | Only the ladder tool had `retired_ladder_launchers`. | `undefined_ladder_ids` stops the rebaseline. Test `test_a_ladder_id_no_file_defines_stops_a_rebaseline`. |
| 15 | Completeness, Engine | LOW | Records: `docs/features/moduledata-validation.md` still stated the band model; the feature doc said "four tools", named two test files, ran the translation step after the restart and had no mirror or loc-gate step; "227 slot edits" was 227 troops (559 slots); `.claude/rules/troops.md` told authors to pick bows by vanilla stats; the sync tool was absent from the feature map, the localization map and the translator guide; the docs omitted the per-spawn `ItemModifier`, said TAOM overrides no model in the chain, called the arrow's `missile_speed` dead and said nothing reads `difficulty`; the generator docstring still said loot drops ladder bows. | Doc drift | Written before the fixes, or from a vanilla-only read. | All corrected. |
| 16 | XML | LOW | Two donor names read "[Mordor] Black Numenorean Numenorean Steelbow I / II", and the four `ladder_mordor_num_bow_t6..t9` names (and their translations) inherit it. | Inherited data | Predates #617. | Follow-up: fix the two donor names live and in the mirror, regenerate, re-translate four ids. |
| 17 | Standards | LOW | Commit hygiene: `b37fc22d` (73) and `c3592f62` (75) subjects over 72; two body lines over 72; `8465cc98` and `8800aa4c` carry a `Co-Authored-By` trailer, which CLAUDE.md forbids. | Commit rules | The subject hook checks the version label, not length; I followed the harness's attribution reminder over CLAUDE.md, which the reminder itself says to obey. | History is pushed; this review's commits carry no trailer. |

### The audit of the first RCA

Every count, attribution and refutation was checked against evidence: the agent accounting (three
finished, three failed on the session limit, per the transcript's notifications), all translation
counts (130 retired and 0 tier rows per language before the sync, 15 per `loc_gondor.xml`, and the
gate's +132 to +137 reconciled row by row), item 5's recruits, item 9's checker, item 13, the
rate-limited-agent checks and the C# run all held. Wrong:

- **Item 6, "why missed": fabricated.** It said the validator's militia regex "was widened later and
  the original never followed". Both were born in one commit, `c7392d5a` (2026-08-31), already
  different. Nobody had checked the history before writing it.
- **Item 7:** the 1.4.8 engine premise (finding 3 above).
- **Item 10:** refuted on vanilla paths only (finding 5 above), and "no AI or spawn reader" needs
  "managed": `MissionWeapon.GetWeaponData` hands `difficulty` to native.
- **Top line:** "two findings were refuted with engine evidence"; the table has one. "Everything
  else was latent" missed that item 9's handbook drift was live.
- **Root-cause pattern:** "items 4 to 8"; item 8 is a coverage gap with one implementation, not a
  twin predicate.
- **Stale since the release:** the sidecar sentences (the v2.0.30 release's
  `sweep_module_backups.ps1 -Apply` moved the modules' 90 backup sidecars to
  `E:\Bannerlord_Backups\module_bak_sweep_2026-09-18\`, per its CHANGELOG entry).
- **Its commit message** counted "13 findings from the six-agent review"; item 13 was found while
  closing item 1.

Each is corrected in place, marked **[corrected]**, with this file linked from its head.

## Decisions Mike made during the review

- **Crossbows against bows.** "Crossbow should hit harder than a bow because of reload", and
  "crossbows also have a lot more accuracy", within a kingdom. Measured per kingdom, the rule held
  inside every line but broke at kingdom level in Gondor at T7 and T8 (Ithilien and Blackroot bows
  out-hit Gondor's crossbows) and on accuracy wherever a kingdom fields both. Applied: `crossbow_bonus`
  3 to 9 (the most the 99 cap and the strict tier rise allow) and the crossbow damage curve at T7 to
  T10 96 / 103 / 110 / 118 to 107 / 126 / 134 / 142. Every crossbow now has at least 42% less spread
  than its kingdom's best bow at the same tier and out-hits it. The 27 crossbow items were regenerated
  in the live Armory and the mirror (only their accuracy, and the T7 and T8 damage, moved); pinned by
  `test_a_crossbow_out_hits_and_out_aims_its_kingdoms_bows`. My first preview called the rule "already
  true everywhere"; it had compared each line with itself only, which I said when the kingdom check
  disagreed.
- **Iron Hills nobles:** keep the ranking (finding 4).
- **Field Commission:** swap to the line's donor (finding 5).
- **Templates:** correct #617 only (finding 3).
- **Also:** comment on #617; fold the class predicate. Not chosen: pushing the mirror; the report's
  `mounted` column change.

## Improvements applied (Step 4)

Behaviour-preserving unless noted, each with the suite green before and after:
`hero_launchers` skips files with no `<EquipmentRoster` or `<NPCCharacter` bytes (361 to 163 ms,
output identical); the restat's `plan` reuses `main`'s `locate` and reads each file once; its class
constants come from `ranged_ladder`; `band_order` and `troop_speed` (callers gone) deleted;
`validate_spec` judges a line's cells by the problems its own iteration added, not by searching
message text; one `TIER_NUMERAL_RE` for the generator and the sync tool (the generator's dry run
renders byte-identical names); `_gamedir.ASSET_REPO` and `armory_trees` replace three copies;
`troop_weapon_classes` reads battle sets only (Mike approved; it changes the classes of three troops,
the two imladris recruits with a civilian bow and `erebor_reg_mattock_warrior` with a civilian
one-hander, and the curve computes the same skills for all three); `rebalance_troops.MILITIA_BINDING_RE` IS the validator's regex.

Not applied: the duplicate `validate_spec` call and `flight_range`'s brute force (both #582 code,
follow-ups); the report's `mounted` column (Mike declined); one comment masker for three tools (their
contracts differ and the change would touch the validator's shared scans); retiring bands for a
per-tier donor map and moving the translation carry-over into the generator (follow-ups that change
the spec shape and the tracked HTML); exit codes that tell a crash from drift, newline choice by
majority, and a byte-exact generator `--verify` (tooling follow-ups on pre-existing code).

## Root-cause pattern

Findings 1, 4 and 14 repeat the first review's family one step further: **a guard or a protection
list that one tool keeps is invisible to the next tool that writes the same data.** Finding 1 is the
same shape inside one tool: a guard written before a merge above it existed. Findings 2, 3 and 5 share
a second shape: **a fact true in one place (the cache beside the loc files, the 1.4.8 engine, vanilla's
loot paths) was taken as true for the neighbouring place without looking.**

## Why each lens caught what it caught

The first review's engine agent and two XML agents failed on the session limit, so their share was
done by script and on the dump; this review ran waves of four and lost none. Data flow and Engine
found the Field Commission path independently, from opposite ends (the XML sweep's blind spot, and
the question "does any path give a player a NotMerchandise item"). The fabricated history in the
first RCA was caught only by reading `git log -S` for both regexes, which no lens is asked to do: an
RCA's "why missed" is the least checked sentence in a review.

## The fix-diff review (2026-09-19)

Mike asked for the fixes themselves to be deep-reviewed before commit. The same eight lenses ran on
the uncommitted diff; the session limit killed two agents of the first wave (Standards, Engine
compatibility), which were rerun three wide. All eight reported. Standards passed, Efficiency found
nothing in the changed code, and no finding touched the shipped troop or item data.

| # | Lens | Sev | Bug | Why missed | Fix and prevention |
|---|---|---|---|---|---|
| F1 | Tooling | MED (latent) | `rebalance_troops.undefined_ladder_ids`, written in this round, matched ladder refs with a regex pinned to `id="..."`, so a single-quoted `id='Item.ladder_...'`, the same reference to the engine, read as absent. No troop file uses single quotes today. | Repeat offender: the militia regexes drifted on quote style until #617's first review, and the prefix-matcher lesson in `xslt-moduledata.md` covers the attribute half of the same class. I copied the regex idiom beside it. | Read with ElementTree (any quote, comments dropped, every element's `id`). A single-quoted row joined `test_a_ladder_id_no_file_defines_stops_a_rebaseline`. Lesson extended: xslt-moduledata. |
| F2 | Data flow | MED | `analyze_troop_balance.py` judged 179 of the 227 ladder archers against the level-curve Bow or Crossbow, so its report listed the ranked values as deltas to fix. | #617 gave the skill a second writer (`ladder_cells`) and taught `rebalance_troops` about it; the read-only report imports `calculate_skills` and nobody listed it as a consumer. Finding 4's family. | `analyze()` merges `rb.ladder_cells` as `process_file` does. Test `test_the_balance_report_references_the_ladder_cell` (125 against 100 without it). |
| F3 | Engine | MED | Three sentences this round wrote as corrections were wrong: the arrow's `missile_speed` "feeds its tier and price" (`DefaultItemValueModel.CalculateAmmoTier` reads damage and stack size only); `lords.xml` has "empty inline blocks" (all 1,164 templated lords carry 18 inline rows); "a troop-versus-troop arrow is pure engine" (the career `TroopDamage` / `TroopResistance` passives and the Infantry ally buff reach troop hits). | Finding 15 replaced old wording with new facts, and I wrote them without reading `CalculateAmmoTier`, counting the lords' rows or re-reading `TaomAgentApplyDamageModel`. The same shape as the first RCA's fabricated "why missed": a correction is a new claim, and it got less checking than the claim it replaced. | Rewritten from the decompile and a count in the tool docstring, the feature doc and `balance-levers.md`. The lords' 18 rows go to #626: 1.5.3 applies them over the template, 1.4.8 ignored them. Lesson: misc. |
| F4 | Engine | LOW | Gaps: the 27 ladder crossbows roll vanilla's `crossbow` modifier group (legendary +4 damage and +15 speed, down to cracked -10 and -6), which the docs never quoted; a Custom Battle rolls no modifier and gives both classes one spread formula, so the owed smoke "a crossbow line against a bow line" cannot show the accuracy rule; campaign movement and unsteady penalties favour crossbows further. | Not asked. | In the feature doc; the smoke step now asks for a campaign battle. |
| F5 | Tooling | LOW | The sync tool read the translator cache after the loc files were planned, outside its refusal path: a corrupt or non-object cache was a traceback, and a mistyped `--cache-dir` planned a fresh cache folder with exit 0. | The cache joined a tool written around loc files. | Read inside the refusal path; a missing folder or unreadable cache exits 2 with nothing written. Test `test_a_cache_the_tool_cannot_read_is_refused`. |
| F6 | Tooling | LOW | The generator still wrote `.bak-rangedladder` sidecars into the mirror, which this round had stopped for the restat and sync tools. | The rule was fixed per tool, not per convention; the third writer was not grepped. | Sidecars in the live tree only (apply and revert); the generator test asserts none in the mirror. |
| F7 | Data flow | LOW | The militia comments called the tools' regex "the one reader" (`TroopUpgradeSkillMonotonicityTests.LoadMilitiaBoundIds` keeps a C# copy) and dated the quote drift to the second review (`8465cc98`, the first, fixed it); the class-predicate fold changed three troops, not two; `test_donor_table_stays_under_the_hero_ceiling` compared bows to the Crossbow cap; nothing pinned that the shipped `enlist_*` rosters reach the ceiling sweep. | Written from memory. | Corrected; the ceiling test is per class; `test_hero_launchers_sweeps_the_shipped_enlistment_rosters`. |
| F8 | XML, Completeness | LOW | Records: no CHANGELOG entry yet; #617's body still said crossbows +3; finding 5's nine cells had become twelve; `troop-skill-balance.md` still put the Iron Hills line in `SKIP_TROOP_IDS` and stated the 1.4.8 template rule unqualified; `field-commission.md` had no #625 line; the feature memory said #617 was unpushed; neither #617 review had a REVIEW-LOG entry. | The fixes were in, their records not yet. | All written. |

**Design, five proposals, all applied with the suites green:** one `rl.battle_sets` iterator for the
ladder and the level curve; the sync keeps the rows `rewrite` derived instead of re-parsing its own
output; one ladder-id regex (`rl.LADDER_ID_RE`) where there were two; the both-tables check moved
into `validate_restat_tables` (test `test_an_id_in_both_tables_is_refused`), which is the one
behaviour change: the generator, roster tool and sync now refuse that spec too, and the shipped spec
has no such id; `strip_tier_numeral` deleted for the regex it wrapped. `undefined_ladder_ids` reads
every set, civilian ones included, on purpose: a civilian slot naming no item is a broken ref too.

**Not applied, follow-ups outside this change:** six tools under `tools/` still default to a
`lotraom-assets\v1.4` mirror that no longer exists; the sync tool would skip, without a word, a
language lacking a `loc_<folder>.xml` (all 12 languages carry all 13 today); the translation cache
keeps ids no loc file has; the validator filters `--code` after running every pass.

**Verification after the fixes:** tools suite 1,773 passed; the generator, restat and sync
`--verify` OK in both trees; the roster tool 0 pending edits, 0 inversions; the validator exit 0, no
error, no `RANGED_*` finding.

## Lessons codified

- `docs/reviews/lessons/testing-qa.md`: a guard must run on the real data before synthetic
  placeholders are merged in.
- `docs/reviews/lessons/adapters-taleworlds-api.md`: an engine rule cited with a version is a claim
  about that version.
- `docs/reviews/lessons/localization-ui.md` (extended): a rename moves the translator cache too.
- `docs/reviews/lessons/data-content-cultures.md` (extended): a second writer inherits the first
  writer's protection list.
- `docs/reviews/lessons/misc.md` (fix-diff F3): a correction is a new claim; measure it before
  writing it.
- `docs/reviews/lessons/xslt-moduledata.md` (extended, fix-diff F1): a ref matcher is quote-agnostic
  too; parse the XML when the question is "which element names X".

## Open follow-ups

- Field Commission swap to the donor (C#, #625).
- `SKILL_TEMPLATE_SHADOWS_SKILLS` and the upgrade-edge skip on the 1.5.3 merge rule (#626), and
  whether the 1,164 lords' 18 inline skill rows, applied since the 1.5.x bump, retuned them.
- Push the mirror when Mike chooses (3 commits, #609's included).
- The doubled "Numenorean" donor names; the #602 / #616 translator runs that turn the loc gate green.
- The tooling and design follow-ups listed above; `check_handbook_attributes.py` comparing values
  (from the first RCA).
- In-game: full restart, `/armory-audit`, a Custom Battle, one campaign battle with a crossbow line
  against a bow line of the same kingdom (Custom Battle cannot show the accuracy rule), one
  non-English client.

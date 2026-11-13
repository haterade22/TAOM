# Lane docs: findings (baseline 7087fa633, 2026-11-12)

Scope read: docs/ (1,762 .md files by `git ls-files 'docs/*.md'` counting nested, 1,827 files all kinds), README.md,
plans/README.md, and the docs an AI session is told to read (AGENTS.md, CLAUDE.md, .ai/, docs/ai-includes/,
.claude/rules, the skills' links). docs/adrs, CHANGELOG.md, translation files, and the two Kimi-corrected
feature docs were skipped.

## Lint numbers (cited, not re-derived)

`python -B tools/lint_docs.py --summary` at the baseline: dead_links 0, stale_versions 0, orphan_features 0,
backlinks_prose 0, missing_features 0, config_drift 0, version_mismatch 0, ai_dashes 0, rule_drift 0,
adr_symbols 0, **context_budget 5**, total_findings 5. The five are all `.claude/` size warnings
(CLAUDE.md with imports 25,590 of 25,600 B; gui-ui.md 16,699 B, harness-facts.md 21,456 B and
hook-authoring.md 13,404 B over the 12,288 B path-rule cap; always-on rules 16,370 of 16,384 B). They belong to
the harness lane.

## Overall verdict

The doc corpus is in better shape than its size suggests. Mechanical checks I ran found very little rot:

- 111 backticked repo-style paths in 118 entry/reference docs do not resolve in `git ls-files`; all but a
  handful are paths in other repos (yotthani MithrilForge `docs/engine/*`, `docs/balance-sim/*`), runtime
  folders (`.claude/logs/`), placeholders (`vX.Y.Z`) or sentences that say the file is gone.
- Feature docs are fresh: for every Main/Features folder that has a same-named doc, no doc is more than 5 commits
  behind its code (script `drift.py` in my scratch folder; worst non-test case TroopProgression, 5 commits,
  46 insertions).
- Backticked identifiers ending in Service/Adapter/Tests/Provider/Hook/Gate/Module/Taom* in
  docs/features, docs/modding, docs/reference: 30 do not occur in any repo source file, and I read the context of
  the TAOM-looking ones (`ChariotConfig`, `ChariotMissionBehavior`, `EquipmentPayoffCalculator`,
  `KeepHeroRaceCampaignBehavior`, `SmashKnockdownGate`): every one is deliberately historical or from another repo.
- `docs/reference/rules-catalog.md` and `hooks-catalog.md` name every file in `.claude/rules` and
  `.claude/hooks` and name nothing missing (loop over `git ls-files`).
- Every skill named in CLAUDE.md exists under `.claude/skills/`; every script, test class and gate named in
  AGENTS.md and the orientation trap index exists.
- Only 69 long lines (160+ chars) are duplicated across files in all non-review docs; the big pairs are
  release posts and the community kit (by design).

So the findings below are the exceptions plus structure/navigation cost.

---

### [DOC-01] Fix the wrong statements an author would act on (6 small groups)

- **Evidence**:
  - `docs/cultures.md:130-131` says "12 Wanderer NPCs ... `_0` through `_11`" and `docs/cultures.md:140` says
    "Create `Main/_Module/ModuleData/taom_equipment_sets_{culture}.xml`". The same file says at line 19
    `equipmentsets/taom_equipment_sets_*.xml`; `git ls-files Main/_Module/ModuleData | grep equipment_sets`
    shows every file under `equipmentsets/`; `docs/modding/recipe-add-a-culture.md:357-361` records both errors
    ("20 files, all under equipmentsets/", "210 wanderers over 20 cultures, 17 at exactly 10") and
    `docs/ai-includes/new-culture-authoring.md:299` has the right path. The 2026-11-03 pass fixed only the
    "Gaps" table of this file (line 199).
  - `docs/migration/dr3-maintenance.md:236` says the Foundation classes were "adopted from BetaDeps v0.7.5.1 ...
    via clean-room rewrite under MIT". `docs/reference/provenance-register.md:75` records BetaDeps as
    `UNKNOWN` licence, `behavioural-port`, and register line 21 says "Do not claim [clean-room] unless that
    procedure was actually followed"; register lines 637-647 retract the claim. `docs/modding/module-dependencies.md:845` already flags the
    line as wrong and nothing corrected it.
  - `docs/INDEX.md:19` sends a regression hunter to "auto-memory `feedback_<symptom>.md` files"; the
    category files' index says those were retired in the 2026-06-24 reorg
    (`docs/reviews/LESSONS-LEARNED.md` intro) and MEMORY.md is now an index of resume cards. Should point to
    `docs/reviews/lessons/<category>.md` (`grep -n '^### '`).
  - `docs/INDEX.md:29` says the modding handbook has "38 chapters"; `git ls-files docs/modding | grep -vc README`
    prints 40, and `docs/modding/README.md:334` says 41 files "this one included" (computed by
    `ls docs/modding/*.md | wc -l`). Drop the number or name that command (CLAUDE.md: no counts nothing computes).
  - Tools moved to `tools/oneoff/` while docs keep the old path: `docs/kitbash/erebor/runes.md:47,101,111,125`
    (`tools/stamp_erebor_runes.py`, exists only as `tools/oneoff/stamp_erebor_runes.py`),
    `docs/kitbash/erebor/blender-build-prompt.md:98` (`build_erebor_kitbash.py`), and
    `docs/migration/templates/equipment-rosters.md:634-647`, `troops-and-parties.md:576-577`
    (`migrate_equipment_type_1_4_3.py`, `validate_equipment_flags_1_4_3.py`, both under `tools/oneoff/`).
    Found with `movedtools.py` (a path under `tools/` named in docs, absent in `git ls-files`, present under another folder).
  - One stale code comment: `Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs:18-19`
    says "category Patch0_BattleScenes is commented out in SubModule.cs"; `Main/SubModule.cs:495` calls
    `TryPatchCategory("Patch0_BattleScenes")` (re-enabled 2026-06-01 per the comment at 490-494).
    docs/reviews/doc-pass-2026-11-03-open-questions.md recorded this as "code is out of scope".
- **Impact**: `docs/cultures.md` is the culture checklist an agent opens first: it tells the author to
  create a file in the wrong folder (the engine load list names `equipmentsets/`) and to author 12 wanderers
  when 17 of 20 cultures hold 10. The dr3 line states a licence derivation that the register says must not be claimed.
- **Effort**: S (hours; all edits are text).
- **Risk**: LOW. Prose only; the code comment edit changes no behaviour. Do not touch `docs/adrs/`.
- **Confidence**: HIGH (each line read at baseline, each target checked with git ls-files or git grep).
- **Fix sketch**: Correct the quoted lines to what the cited sources say; reword dr3-maintenance.md:236 to
  "behavioural port, provenance uncleared (see the register)"; edit INDEX.md:19 and :29; repoint the six kitbash
  and migration-template paths to `tools/oneoff/`; update the comment in the patch file.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (docs plus one comment line).
- **Issue**: none found in the baseline tree.

### [DOC-02] Add the missing `Realms` row to feature-map.md and link the rows that lack their doc

- **Evidence**: `grep -c 'Features/Realms/' docs/reference/feature-map.md` prints 0 while
  `Main/Features/Realms/` is a feature with several commits dated 2026-11-12 (`git log -- Main/Features/Realms`)
  and has `docs/features/realms.md` (183 lines). `grep -n -i realms docs/reference/feature-map.md` only hits the
  Cardolan row (line 115). AGENTS.md "Documentation duty" requires a feature-map row per completed feature. Beyond Realms,
  41 table rows have no `features/` link (`grep '^| ' | grep -vc 'features/'`), including Arena, CharacterCreation,
  CulturalFeats, SiegeDefense, SpecialResources, SettlementGuards, NamedCompanions, RevoltTuning, MainMenuCustomizer
  though each has a `docs/features/<name>.md`. 60 of 298 feature docs are not linked from feature-map.md by file name
  (loop of `grep -q $basename`).
  Also `Client/Features/Realms` and `Client/Features/SettlementNameplateRelation` have no `Client/Features/<f>` path in the map.
- **Impact**: `.claude/hooks/detect-docs-gaps.sh` and `lint_docs.py` `missing_features` only check that a doc exists for
  a folder; nothing checks the map row, so Realms went unmapped. A session mapping
  "feature to code" for Realms finds nothing.
- **Effort**: S for the rows; M if a gate is added (lint_docs check: every `Main/Features/<X>` folder name occurs as `Features/<X>/` in the map).
- **Risk**: LOW. `PatchCategoryNumberTests` parses feature-map rows for patch numbers (harmony-patch-registry.md header), so a new row must cite only Realms' own patch numbers.
- **Confidence**: HIGH.
- **Fix sketch**: Add the Realms row (path, patches, doc link, one clause). Add `docs/features/<name>.md` links to the 41 link-less rows in the same edit. Optional: extend `lint_docs.py` with the folder-to-row check (all 202 Main folders except Realms pass it today: loop over `ls Main/Features` printed one miss).
- **Delta**: introduced (Realms landed in v2.0.38; the 41 link-less rows are older).
- **P1**: no.
- **Plan candidate**: yes (rows now; gate as optional second step).
- **Issue**: none found.

### [DOC-03] Tell readers to grep the three navigation maps, and finish INDEX.md coverage

- **Evidence**: `wc -c`: `docs/INDEX.md` 116,862 B (520 lines, 357 entries, avg 255 chars), `docs/reference/feature-map.md`
  184,787 B (median row 572 chars; 48 rows over 1,000), `docs/reference/doc-lookup.md` 50,513 B. Together 352 KB, roughly
  88,000 tokens if read whole. INDEX.md's header calls itself "Start here" and none of the three headers says
  to search instead of reading (`grep -n -i 'grep'` finds no instruction in any header). 74 of 298 feature docs are not linked from INDEX.md
  by file name (examples: realms.md, player-switcher.md, mumakil.md, hero-auras.md, naval-travel.md, signature-strikes.md,
  war-pledge.md, staged-siege.md); 241 of 298 are not in doc-lookup.md (that file is task-keyed by design).
- **Impact**: a session that follows "start here" reads up to 30k tokens of INDEX before finding anything, then misses a quarter of the features anyway. The feature-map header says "one clause of purpose" and "trim a row when you next touch it; no bulk rewrite", so bulk trimming is decided against and I do not propose it.
- **Effort**: S (one header sentence each) plus M to add the 74 INDEX lines (or S to state in INDEX that feature-map.md and `ls docs/features` are the complete lists and INDEX is curated).
- **Risk**: LOW. Prose.
- **Confidence**: HIGH on sizes and counts (commands above); MED on how often sessions read the files whole (no log measured, UNVERIFIED).
- **Fix sketch**: Add to each header "Search this file (`grep -n -i <term>`); do not read it whole." Decide in INDEX.md whether it is complete or curated and say so.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes for the header lines; the 74 lines are a maintainer choice (curated vs complete).
- **Issue**: none found.

### [DOC-04] The modding handbook still lists defects in other docs that were fixed since

- **Evidence**: The handbook (written 2026-09-05) keeps a line for each stale doc it found. Re-checked at baseline:
  fixed since: `docs/modding/configs-balance.md:410` (race-age-system.md "Current Race Values" at `docs/features/race-age-system.md:65-83` now matches: human 200, comes of age 18), `configs-factions-and-world.md:444-446` (no "11 natural" / "33 hostile" text remains in `docs/features/diplomacy.md`), `id-cheatsheet.md:466` (`docs/reference/taom-map-settlement-naming.md:91` now reads "all of them since castle_K4 moved"), `module-map.md:573-575` (`docs/reference/main-map-vista.md:142-143` states the 2026-11-03 AssetPackages), `open-questions.md:154` (localization-map.md has no "eleven" text and the test is now the 13-file version), `open-questions.md:81` and `equipment-rosters.md:405-407` ("xml-data.md cites a memory file that does not exist": `git grep equipmenttype_civilian -- .claude` is empty).
  Still true: `docs/cultures.md` (DOC-01) and `dr3-maintenance.md:236` (DOC-01).
- **Impact**: a reader trusts a note that points at a defect that is gone, and `docs/modding/open-questions.md` (208 lines, 119 rows) misleads about which questions are open.
- **Effort**: S.
- **Risk**: LOW.
- **Confidence**: HIGH for the six fixed entries (each target read); MED that no other entry in the 119 rows is also answered (I read all "stale" and "UPSTREAM FIX NEEDED" rows only).
- **Fix sketch**: Delete the six entries; for open-questions.md add a dated "re-swept" line.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (bundle with DOC-01).
- **Issue**: none found.

### [DOC-05] doc-pass-2026-11-03-open-questions.md: say which questions the repo has since answered, and link it

- **Evidence**: `docs/reviews/doc-pass-2026-11-03-open-questions.md` is 119,441 B, 462 bullets (`grep -c '^- '`) in 245 doc sections, and
  only `CHANGELOG.md:9320` refers to it (`git grep` outside the file), so no doc or hook points a reader at it.
  Two bullets already carry "Settled 2026-11-12" (caravan-trade, career-quest-system). Answered by the repo since:
  - "career-system.md line 642 and gui-sprite-system.md line 356 still say 70 careers" (career-cc-selection section): `docs/features/career-system.md:642` reads "49 of the 91 careers" and `git grep -E '\b70 careers' -- docs/features docs/modding docs/reference` returns nothing.
  - "CharacterCreationCultureStageVM_OnCultureSelection_Patch and ..._BodySync_Patch not described" (character-creation section): `docs/features/character-creation-body-properties.md` names both (4 hits). The doc under question is on the skip list.
  - "Main now has about 70 Taom*Model classes" (bannerlord-together-compat): `docs/reference/gamemodel-registry.md:3` states 70 and `lint_docs.py` checks it (model_registry 0). The doc still says 13 at `bannerlord-together-compat.md:389` and `:491`, so that bullet is answered but its fix is still owed.
  - 46 bullets (`grep -c -i` on "live install / live module / cannot be read from this worktree / in-game") need the live TAOM_Map, Armory or the game and cannot be answered from the repo.
  - Still open and checkable: `docs/features/bandit-management.md:346-347` ("5 new LOTR bandit cultures", "10 new party templates" vs 9 and 18 at lines 255, 270, 309); the stale patch comment (DOC-01).
- **Impact**: 462 unlinked leads decay; a re-run of the doc pass cannot tell which are done.
- **Effort**: S (status line) to M (re-sweep all 462).
- **Risk**: LOW. The file is evidence; do not delete bullets.
- **Confidence**: HIGH for the four answered items above; the other 450+ bullets UNVERIFIED.
- **Fix sketch**: Add a "Status 2026-11-12" paragraph at the top with the answered items and the 46-bullet "needs live data" count, and one INDEX.md line linking it. Edit nothing else.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no (decision): the maintainer owns whether the 462 leads get a second sweep.
- **Issue**: none found.

### [DOC-06] docs/reviews holds 16.8 MB of raw review evidence in one flat folder; an index is the smaller fix than an archive move

- **Evidence** (script counting `git ls-files docs/reviews` by filename prefix; bytes are file sizes):
  rca-* 623 files, 6,484,640 B; codex-*.prompt.md 293 files, 2,372,259 B (plus 15 other codex files, 236,061 B);
  adopt-* 48 files, 1,923,904 B; deep-review-* 49 files, 1,205,275 B; lessons/ 13 files, 2,679,647 B; misc 33 files,
  1,867,528 B (REVIEW-LOG.md 706,048 B, agents-md-review-lessons-archive.md 163,128 B, a 116,670 B `_clan_registry.json`
  that is clan data, not a review, `progress.png`). Total 1,062 files, 16,769,314 B.
  No index lists the RCAs. docs/INDEX.md:19 says "search for `rca-<feature>-<date>.md`".
  An archive move would break: 1,052 references to `docs/reviews/rca-*` from outside docs/reviews
  (`git grep -o`), 28 of them in `.cs` comments (e.g. `Main/Features/BannerBearers/Models/TaomBattleBannerBearersModel.cs:28`),
  2,869 inside docs/reviews, `.githooks/pre-commit:93` (`docs/reviews/rca-.*\.md`), `.claude/hooks/check-doc-config-drift.sh:122`,
  and the `/improve` workflows (`.claude/skills/improve/workflows/review.js:201-202` build `docs/reviews/deep-review-*` and `rca-*` paths).
  `docs/archive/` already exists (49 files, 442,874 B) but is not the target of any review path.
- **Impact**: `ls docs/reviews` and Glob results are 1,000+ names long; `docs/reviews/_clan_registry.json` is cited by
  `docs/features/clan-heraldry.md:66` and `dale-lord-expansion.md:68,96` as a registry, a hidden dependency of a tool in a review folder.
- **Effort**: S for a generated `docs/reviews/INDEX.md` (one line per RCA: date, slug, issue) built from filenames and each file's H1; L for a move.
- **Risk**: LOW for an index. HIGH for a move (the references above).
- **Confidence**: HIGH on counts; MED on whether an index saves real context (sessions Glob by name today; UNVERIFIED).
- **Fix sketch**: Do not move. Optionally add a generated index (new tool, or a section of `tools/build_backlinks.py`), and move the clan registry only together with its writer `tools/clan_registry.py` and the three docs.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no (decision): archive-versus-index is the maintainer's call, and nothing is to be deleted.
- **Issue**: none found.

### [DOC-07] lessons/build-tooling-workflow.md is one flat 4,994-line file with 426 headings and no `##` sections

- **Evidence**: `wc`: 4,994 lines, 534,499 B; `grep -c '^### '` 426; the heading list alone is 41,493 B
  (`grep '^### ' | wc -c`), the largest of the 13 category files (testing-qa 262 headings, 26,898 B; the rest 11 to 203).
  `grep -n '^## '` finds only the trailing "Referenced by". `docs/reviews/LESSONS-LEARNED.md` and `.claude/rules/hook-authoring.md:11` tell sessions to run `grep -n '^### '` and read the entries that apply, so every session pays about 10k tokens for the heading list of this one file.
  55 references to the file name from outside docs/reviews (`git grep -c`), and other docs cite lessons by title.
  A keyword split of the headings (hook/gate, git, harness/AI, scripts/OS, build/release) leaves 187 of 426 in "other", so no clean split exists.
- **Impact**: ~10k tokens per use for the heading scan; slow lookups.
- **Effort**: M.
- **Risk**: MED: 55 outside references plus titles cited elsewhere; `/review-codex` and `/deep-review` append by category file name.
- **Confidence**: MED (the cost is real, the best split is a judgement).
- **Fix sketch**: Cheaper than a split: add `##` area groups inside the file (hooks and gates, git, scripts and OS, harness) and reorder entries under them in one pass, so `grep -n '^##'` is a 6-line table of contents and links by title still resolve. A file split is the second step only if that is not enough.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no (decision): grouping 426 lessons needs the maintainer's taxonomy.
- **Issue**: none found.

### [DOC-08] Three culture-creation procedures overlap, and the oldest one is the one that drifted

- **Evidence**: `docs/cultures.md` (237 lines, "Culture Creation Checklist"), `docs/modding/recipe-add-a-culture.md`
  (the handbook recipe, measured 2026-09-05), and `docs/ai-includes/new-culture-authoring.md` (37,085 B, the AI authoring guide that CLAUDE.md names) each give the file list and steps for a new culture. They disagree on the equipment-set path and the wanderer count (DOC-01); the code agrees with the recipe and the guide (20 files under `equipmentsets/`, 17 of 20 cultures at exactly 10 wanderers, per `recipe-add-a-culture.md:357-399` measured commands). `git grep -c 'cultures.md'` shows it linked from INDEX.md (2), new-culture-authoring.md (3), orientation.md (1).
- **Impact**: the third copy is the one most likely to be the first one read; two of the three need the same fix every time the process changes.
- **Effort**: S.
- **Risk**: LOW.
- **Confidence**: MED (I did not diff every step of the three).
- **Fix sketch**: Cut `docs/cultures.md` to its "Current State" section and one pointer to `docs/ai-includes/new-culture-authoring.md`; keep the sharing table. Then DOC-01's cultures.md edits are a deletion.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes.
- **Issue**: none found.

### [DOC-09] docs/community/bannerlordmodding-lt.zip is a stale binary copy of the folder beside it

- **Evidence**: `docs/community/bannerlordmodding-lt.zip` (103,758 B) holds 12 files, all 12 differ from `docs/community/bannerlordmodding-lt/**` even after CRLF normalisation (README.md 6,522 B in the zip against 13,062 B on disk; custom_creature_updates.md 60,491 against 64,396 B). The zip was last committed 2026-09-26 (719074072); the folder 2026-11-06 (ca6312a90). Nothing links the zip (`git grep bannerlordmodding-lt.zip` hits only a changelog line); the folder README (177 lines) never mentions it.
- **Impact**: a person handed the zip gets a superseded text; 104 KB of binary in git history grows with each regeneration.
- **Effort**: S.
- **Risk**: LOW, but it may already have been sent to the upstream site owner.
- **Confidence**: HIGH on the staleness; the zip's purpose is UNVERIFIED.
- **Fix sketch**: Ask whether it is still wanted; if so regenerate it from the folder and name the command in the README, else remove it.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no (decision): the maintainer says whether the zip is a hand-off artifact.
- **Issue**: none found.

---

## Considered and rejected

- Moving docs/reviews files into `docs/archive/`: 1,052 outside references, 28 in `.cs` comments, a pre-commit glob and a hook (DOC-06).
- Splitting build-tooling-workflow.md by keyword: 187 of 426 headings fall in no group (DOC-07).
- Trimming feature-map.md rows in bulk: the file's own header says "Trim a row when you next touch it; no bulk rewrite" (decided).
- A duplication finding for AGENTS.md versus docs/ai-includes (git-and-commits, completion-workflow, code-quality, testing-guide): read them, they are short, point at each other, and carry no copy of the same table.
- Path rot in docs/reference and docs/ai-includes: the 111 unresolved paths are external-repo or runtime paths (see overall verdict).
- Feature docs behind their code: `drift.py` found none worse than 5 commits.
- docs/audits (30 files, 1,044,753 B, last touched 2026-05 to 2026-09) and docs/issues-drafts (19 files): raw evidence with 64 references from `doc-lint-2026-05-27.md` alone; an index or archive move is a maintainer decision and is not worth a plan.
- docs/ai-includes/code-quality.md and testing-guide.md: short, accurate, gated by `NullableRatchetGateTests` and the shipped-data tests.

## What I did not cover

- docs/adrs/*.md (not mine). Noted only: `docs/adrs/010-knowledge-base-architecture.md:204` names `tools/search_docs.py`, which the same paragraph says is deferred indefinitely, so no edit is implied.
- CHANGELOG.md, docs/changelog-archive, translation files, and the two Kimi-corrected feature docs (character-creation.md, career-cc-selection.md).
- The other 455 bullets of the 2026-11-03 open-questions file: I verified only the ones in DOC-05.
- Claims inside the six largest feature docs (career-system.md 841 lines, enlistment.md 3,007, seat-of-seeing and build-specs): I ran identifier and path checks over them but did not read them; numeric constants and test counts in them are UNVERIFIED.
- Numbers that depend on the live `TAOM_Map` and `LOTRLOME_Armory` installs (settlement counts, Armory item counts, wanderer counts beyond the recipe's own measurement).
- Engine line citations in docs/reference/engine/*.md against v1.5.7 DLLs (no taom-src run).
- Whether `.claude/hooks/detect-docs-gaps.sh` output is read at session start (I did not run it).
- docs/localization, docs/migration, docs/research, docs/scenes, docs/kitbash content correctness beyond the moved-tool path check.
- How often sessions read INDEX.md or feature-map.md whole: no usage log was read, so the token costs in DOC-03 are size arithmetic (bytes divided by about 4), not measurements.
- I ran `python -B tools/lint_docs.py --summary` and `python -B tools/lint_docs.py` once each; I did not run `detect-docs-gaps.sh` or any writing tool. Scratch scripts (pathcheck.py, flagcheck.py, movedtools.py, tokens.py, drift.py, dupes.py) are in E:\repos\taom-improve\2026-11-12\scratch\lane-docs.

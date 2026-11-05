# K2: completeness critic, part B (skip rows other than SKIP-FEATURE)

Run 2026-11-04-port148, read-only verifier. Refs read: origin/bannerlord-1.4.5 at 1b00d1acd, origin/bannerlord-1.5.x at
e45f6df2c, origin/fix/public-armory-ids-145 (the public 1.4.8 build). Engine: v1.4.8 dump at
`C:\Decompiled_Bannerlord\_categories_v1.4.8`. Input: `SP\agg\classified.jsonl`, `SP\agg\wp_map.json`, `SP\agg\groups.json`.

## Scope and method

229 rows are in scope (counted from classified.jsonl): SKIP-CONTENT 81, SKIP-BOOKKEEPING 72, SKIP-1.5-ONLY-FIX 45,
SKIP-1.5-ENGINE 17, SKIP-SUPERSEDED 8, SKIP-ALREADY-ON-145 6.

1. Every row's files were split into code, tests, tools, harness, engine docs or lessons, and data, and each path was
   checked against `git ls-tree -r --name-only origin/bannerlord-1.4.5` (6,695 paths). Every row that touches a code,
   test, tool or harness file present on 1.4.x had its hunks read (about 60 rows).
2. SKIP-ALREADY-ON-145: each claimed twin was checked with `git merge-base --is-ancestor <twin> origin/bannerlord-1.4.5`,
   a per-file comparison of the added and removed lines, and `git log -S` on origin/bannerlord-1.4.5.
3. SKIP-SUPERSEDED: the superseding commit's own verdict and WP were looked up.
4. Engine claims that decide a skip were re-read in the v1.4.8 dump.
5. Nothing was built or tested. The data checks (tuple flatten compare, civilian-roster scan, hideout pairing, village
   culture and bound check, Dunland groups) were read-only Python over `git show` output and the installed module XML
   under `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules`.
6. `git show <ref>:.claude/...` was run with `MSYS_NO_PATHCONV=1`. One early `git cat-file` without it falsely reported
   `.claude/hooks/check-moduledata-validation.sh` absent; the tree listing and a re-run with the variable set both show
   it present.

## Result

10 rows are misclassified. None is P0 or P1; one is P2. No skipped commit hides a perf, memory, load-hang or freeze fix
for 1.4.x. What the skips hide is smaller: two lesson and comment payloads the ALREADY-ON-145 twins never carried, four
engine-agnostic gates inside content or engine-bump commits, one tool fix that WP103 would orphan, one latent tool defect,
one troop data defect live on the public build, one wrong harness rule, and one v1.4.8-valid engine fact.

| # | Commit | Current | Proposed | Pri | Group key | WP |
|---|---|---|---|---|---|---|
| F1 | 1571c2f91 | SKIP-ALREADY-ON-145 | PORT-PARTIAL | P3 | issue-601-preload-body-guard | 105 |
| F2 | 569f6750d | SKIP-ALREADY-ON-145 | PORT-PARTIAL | P3 | issue-729-item-slot-rejected | 132 |
| F3 | 540848795 | SKIP-1.5-ENGINE | PORT-PARTIAL | P3 | gui-prefab-copy-drift | 132 |
| F4 | 9bfd0f411 | SKIP-1.5-ONLY-FIX | PORT (only if WP103 adopts plan 020) | P3 | plan-020-changelog-at-release | 103 |
| F5 | 0799af376 | SKIP-CONTENT | PORT-PARTIAL | P2 | issue-1063-feral-orcs-hideout-guard | 111 |
| F6 | 4b207f951 | SKIP-CONTENT | PORT-PARTIAL | P3 | map-tools-hardening | 135 |
| F7 | c625ada0f | SKIP-CONTENT | PORT-PARTIAL | P3 | harness-rules-review-2026-11-03 | 134 |
| F8 | 1641fb985 | SKIP-CONTENT | UNSURE (maintainer call) | P3 | content-dunland-troops | 129 |
| F9 | b904e02a3 | SKIP-1.5-ONLY-FIX | PORT-PARTIAL (latent) | P3 | issue-617-ranged-ladder | 135 |
| F10 | 42e48bbe5 | SKIP-1.5-ONLY-FIX | PORT-PARTIAL (lesson) | P3 | engine-fact-donotmakenewdecisions-initiative (new) | 136 |

## Findings

### F1. 1571c2f91 carries two Patch90 lessons that its 1.4.x twin does not

- The diplomacy code is on 1.4.x: d8e3777cd is an ancestor of origin/bannerlord-1.4.5, `git log -S
  'KingdomDecisionsVM_RefreshWith_AutoResolved' origin/bannerlord-1.4.5 -- Main` returns it, and the hook, test and
  SubModule hunks match.
- The lessons do not match. 1571c2f91 adds 28 lines to `docs/reviews/lessons/harmony-il.md`, d8e3777cd adds 14. The
  extra 14 lines are two Patch90 (#601) lessons, at origin/bannerlord-1.5.x `docs/reviews/lessons/harmony-il.md:578`
  ("Cost a guard's failure path as the loop it is ...") and `:585` ("A caller-driven guard's coverage is the caller's
  coverage ..."). `git log -S "Cost a guard's failure path"` on origin/bannerlord-1.5.x returns only 1571c2f91, and
  `git grep` finds neither heading on origin/bannerlord-1.4.5.
- Why it matters: fb8d73145 (PORT P0, WP105) ships `docs/reviews/rca-preload-body-guard-2026-09-15.md`, whose "Lessons to
  codify" section (line 28 at fb8d73145) says both entries were appended to harmony-il.md. A WP105 port without them
  lands an RCA that points at lessons 1.4.x does not have.
- Take: those two entries only. Test: `git grep -n "Cost a guard's failure path" -- docs/reviews/lessons/harmony-il.md`
  finds one hit, and `python tools/lint_docs.py --summary` reports no new dead link.

### F2. 569f6750d: the data is on 1.4.x but the tool comments and docs now misdescribe it

- The `troops_isengard.xml` hunk is the same in 1b00d1acd: both commits move the two Uruk-hai skirts from `slot="Cape"`
  to `slot="Body"`.
- These comments still say the skirt sits in the Cape slot on origin/bannerlord-1.4.5: `tools/analyze_kingdom_armour.py:139`,
  `tools/fix_upgrade_armour_regressions.py:235` and `tools/taom_schema.py:1346`. `docs/reviews/rca-uruk-hai-skirt-body-2026-10-04.md`
  is absent there.
- Take: the comment and docstring rewording in those three tools, the test docstring in
  `tools/tests/test_validate_moduledata.py`, the `MISSING_BODY_ARMOUR` row wording in
  `.claude/rules/moduledata-validation.md` (line 83 on 1.4.x), the RCA and the data-content-cultures lesson. The commit
  changes no logic. Adapt: 1.4.x `_BODYLESS_BY_DESIGN` (`tools/taom_schema.py:980-984`) has three entries and no
  `hill_troll`, so drop the hill-troll wording.
- Test: `python -m unittest discover -s tools/tests -t .` stays green.

### F3. 540848795 contains an engine-agnostic prefab parity gate

- The blood-feud half is 1.5.x-only, and the classifier is right about it.
- The new `TAOM.Tests/Features/Execution/PartyTupleVanillaParityTests.cs` also has
  `EachTupleCopy_MatchesVanilla_ApartFromTaomsTwoEdits`. It compares TAOM's `PartyTroopTuple.xml` and
  `PartyTroopTupleLeft.xml` copies with the installed SandBox prefabs element by element. It ignores only
  `TaomPrisonerBanners` elements and `VisualDefinition/@TransitionDuration`, so it coexists with the #902 and
  Shadowfax edits other WPs port.
- Measured today with the test's own flatten rule: both 1.4.x copies (`Main/_Module/GUI/PreFabs/Party/`, note `PreFabs`)
  equal the installed v1.4.8 vanilla files (188 and 114 elements). The gate is green on day one and fails the next time
  either copy drifts from vanilla. That is the class of bug WP132's gui-prefab-copy-drift group exists for.
- Take: that test method and its `Flatten` helper. Leave `TheExecuteButton_ShowsTheBloodFeudMark_BesideTheSkull`, the
  prefab edit and the HearthRoll hunks.
- Adapt: `RepoPaths` is absent on 1.4.x. It arrives with 35007dbb8 (WP126); otherwise inline the repo-root walk.
  `GameAssemblies.GameDir` exists (`TAOM.Tests/Migration/GameAssemblies.cs:28`). `LiveInstall` is not a 1.4.x category:
  `BindingVerification` is the only one, used 231 times. Keep the `Assert.Inconclusive` fallback.
- Also take the localization-ui lesson "Pin a GUI prefab copy to the vanilla file it replaces (#1031)".

### F4. 9bfd0f411 is the one changelog-tool fix that WP103 would orphan

- Five commits touch `tools/changelog_from_commits.py` after the fork: ae85381dc, 6e4bfedb7, 971d8e975, 5b242cee6 (all
  UNSURE, group plan-020-changelog-at-release, WP103) and 9bfd0f411 (SKIP, group plan-020-changelog-from-commits, no WP).
- 9bfd0f411 lists a commit whose subject and body exactly repeat an earlier one once, with a count of skipped repeats
  (#1009). The "absent on 1.4.x" premise holds only until WP103 ports the tool.
- Port it with WP103 if the maintainer adopts plan 020; skip it otherwise.
- Test: its own `tools/tests/test_changelog_from_commits.py` cases.

### F5. 0799af376 carries a live TAOM-vs-TAOM_Map pairing gate for the crash Patch125 guards

- `TAOM.Tests/Features/BanditManagement/BanditHideoutLiveDataTests.cs` (new) has
  `EveryHideoutCultureBanditClan_OwnsALiveHideout`. It fails when a bandit clan of a hideout-only culture has no
  hideout of its culture in the live `TAOM_Map/ModuleData/settlements.xml`. That is the new-game
  `GetInfestedHideoutCount` throw that cd06da904 (Patch125, WP111) turns into a log line.
- The fixture exists on 1.4.x: `CultureDataFixture.HideoutOnlyCultures` at `TAOM.Tests/Core/CultureDataFixture.cs:49-53`
  (8 cultures).
- Measured green on this laptop. 1.4.x `characters/clans.xml` has 8 `is_bandit` clans, and the installed TAOM_Map has
  live dens for every one: dunland_raiders 19, rhun_raiders 20, harad_raiders 34, gundabad_raiders 20, umbar_corsairs 36,
  gondor_soldiers 10, erebor_warriors 10, mirkwood_stalkers 10.
- Value: brief2 records that the desktop's live TAOM_Map now carries 1.5.x ids. This is a cheap release-time check that
  the shipped map still pairs with 1.4.x TAOM, so P2.
- Take: that test plus `LiveHideouts()` and `BanditClans()`. Replace the hard-coded `E:\Steam\...` default with
  `GameAssemblies` or `BANNERLORD_GAME_DIR` resolution. Leave the two Feral Orc tests and all Feral Orc content.

### F6. 4b207f951 widens `add_map_villages.py --check` to culture and bound drift

- Inside the Dorwinion and South Rhovanion content, the commit adds `master_attr`, `master_village_bound`, two new
  findings in `check()` (master culture differs from the table, master bound differs from the table), new `--check` help
  and OK text, and three tests in `tools/tests/test_add_map_villages.py`.
- The 1.4.x tool has neither check (`check()` at origin/bannerlord-1.4.5 `tools/add_map_villages.py:257`).
- WP135's map-tools-hardening ports (244c15d27, 87ffea9b2, af2b198e4) hand-apply other helpers and predate this commit
  (10-27 and 10-28 against 10-29), so they will not bring it.
- Take: those functions, the `check()` lines, the help text and the three tests. Leave every `Village(...)` row and the
  two `MESHES` lines.
- STOP: on this laptop the installed TAOM_Map `settlements.xml` (dated 2026-09-07) contains none of the 15 rows in the
  1.4.x `VILLAGES` table, so `--check` fails on missing rows before the new findings run. Prove the gate on the desktop or
  against the TAOM_Map package the release ships, and report the 15 missing rows to WP123 as a pairing fact.

### F7. c625ada0f holds the only fix for a 1.4.x harness rule that 1.4.x itself documents as wrong

- origin/bannerlord-1.4.5 `.claude/rules/xml-data.md:37` gives `EN=Rohan, ... B=Dunland, ... K=Easterlings`. The same
  ref's `docs/modding/id-cheatsheet.md:432` says that line "disagrees with the map", and its census at `:88-114` shows
  `EN` = `empire` (Dunland), `K` = `battania` (Khand), `V` = Rohan, and no `B` prefix.
- c625ada0f rewrites the rule line from that census; nothing else corrects it.
- Take: the settlement-prefix line only, rebuilt from the 1.4.x census. Drop `AN`, `CA`, `EL` and `RH`, which the 1.4.x
  census does not list. Leave the lord-prefix line and the custom-culture list (they add 1.5.x realms).
- Test: every prefix in the new line appears in the census table at id-cheatsheet.md:94-114 on the same commit.

### F8. 1641fb985: a troop formation defect live on both 1.4.x refs (maintainer call)

- On origin/bannerlord-1.4.5 and on origin/fix/public-armory-ids-145, `troops_dunland.xml:1705` `dunland_raven_noble_son`
  is `default_group="Infantry"` while carrying only a bow and arrows.
- `:2082` `dunland_falcon_archer`, `:2165` `dunland_falcon_wildrider` and `:2248` `dunland_falcon_noble_horse_archer` are
  `Cavalry` while carrying bows.
- For a non-hero, `default_group` is the battlefield formation (1.4.x `tools/taom_schema.py` comment near `_GROUP_ATTR_RE`
  at :867, citing `BasicCharacterObject.GetFormationClass`). So players' hunters fight as infantry and the horse archers
  as melee cavalry.
- The four retags are a data-defect fix. The skill restat in the same commit is balance. Under the brief's "troops are
  content" rule this is the maintainer's decision, so UNSURE, not PORT.
- If taken: the four `default_group` edits only, applied to both refs (their bow ids differ: `ladder_dunland_bow_e` on
  1.4.5, `_t2` on the public ref). Check with `python tools/validate_moduledata.py`.
- Gap on both lines: no validator compares `default_group` with the kit (the commit's RCA, finding F5).

### F9. b904e02a3 fixes a latent `rebalance_troops.py` defect that 1.4.x shares

- origin/bannerlord-1.4.5 `tools/rebalance_troops.py:287-301` `troop_weapon_classes` walks `.//equipment`, civilian sets
  included. Its docstring claims civilian sets carry no inline weapons.
- On 1.4.x data, 98 troops carry weapons in civilian rosters. Two carry their only bow there: `imladris_recruit`
  (`troops_rivendell.xml:6`) and `lindon_imladris_recruit` (`troops_lindon.xml:6`).
- b904e02a3 restricts the walk to battle sets, naming imladris_recruit.
- On 1.4.x the classes feed only the Bow-to-Crossbow swap (`:318-320`), the Throwing parity (`:328-330`) and the
  Polearm-to-TwoHanded swap (`:531-534`), through `:857` and `analyze_troop_balance.py:169`. A name-based scan found no
  1.4.x troop whose swap a civilian weapon flips today, so it is latent; P3.
- Take: the civilian skip only. The 1.5.x code calls `rl.battle_sets`, which 1.4.x `ranged_ladder.py` lacks, so inline
  the filter: skip `EquipmentRoster civilian="true"` and `EquipmentSet equipmentType="Civilian"`.
- Do not take `planned_skill_edits` accepting templated ladder troops. Its premise ("on 1.5.3 an inline skill row
  overrides the template") is false on v1.4.8 (`BasicCharacterObject.cs:337-357`).

### F10. 42e48bbe5's `SetDoNotMakeNewDecisions` lesson holds on v1.4.8, and 1.4.x relies on that flag

- v1.4.8 `TaleWorlds.CampaignSystem.Party/MobilePartyAi.cs:486` (in `GetBehaviors`) applies the initiative result despite
  `DoNotMakeNewDecisions` when the best initiative target is in a map event at, or besieging, the party's `TargetSettlement`.
- 1.4.x pins parties with that flag in `Main/Features/SupplyLines/SupplyCaravanService.cs:685-688` (`PinAi`) and
  `Main/Features/Refuge/RefugeService.cs:1113`. No 1.4.x doc records the exception.
- Take: the lesson entry into `docs/reviews/lessons/adapters-taleworlds-api.md`, citing the v1.4.8 line.
- Lead for the runtime-perf or freeze lens, UNVERIFIED in game: a supply caravan whose destination is besieged can take
  initiative despite its pin. Refuges hold position, so their `TargetSettlement` is probably null; UNVERIFIED.

## Skips confirmed (refutation attempted, failed)

- **ALREADY-ON-145.** d9bd8bc29 (every file but CHANGELOG equals c5b84fb41), 1e39c1b8d (f8c9c8ecb: identical patch hunk;
  the test and doc differ only in a 1.4.x adaptation; pickaxe on 'basic-tableau-refresh'), dffdf879a (55d779012 ports it
  and 9e2a39f4; pickaxe on CustomBattleLordStubs) and c8d69a0dc (ccef172ad). The extra files of c8d69a0dc concern three
  workflows 1.4.x never had, and `git grep` finds no live workflow reference in 1.4.x .claude, .ai or tools/README.md.
- **SUPERSEDED.** 5f256f704, dad9b169e and d2fa6d081 are superseded by 60d85ed7d and a4ba6b0b3 (PORT, WP134); 83758f1d6
  by c8d69a0dc (on 1.4.x as ccef172ad); 9563d5e82 by 3b848c411 (PORT, WP125); e77ef9654 by c32662872 (PORT-PARTIAL,
  WP122). f2eccd160 is superseded by 6ae924638 (WP111) and 158408c73 (WP119); it becomes needed only if 1.4.x keeps
  AutoResolveDiagnostics under 6ae924638's "renamed default-off setting" alternative. 7bac0c9d4 is superseded by 8589a3384,
  itself UNSURE (WP133); both, and 0740c632e, live or die with the RefAsm no-game mode.
- **1.5-ENGINE.**
  - 331032a1e: v1.4.8 `BasicCharacterObject.cs:337-357` applies inline skills only when `skill_template` does not
    resolve, and its two 1.4.x tool hunks are comments.
  - 59b1ff927: v1.4.8 `FinalizeCharacterCreationState` (`CharacterCreationState.cs:54-61`) sets no gold. Both flat-1,000
    operations (`Campaign.cs:1324` add, `:1630` set) run in the NewCampaign load steps (`Campaign.cs:1681-1694`).
  - 12b010626: its 1.4.x-present hunks are v1.5.6 adaptations, pins or comments.
  - 70517552c, e87cba1c1 and 79c612b2e are 1.5.x evidence only. 5b241c46d and ad1992b5b stand because 1.4.x still
    vendors NativeSkinFixes.
  - 3cdffd8a2 is right to skip, but see the WP110 caution below.
- **1.5-ONLY-FIX.**
  - These claims were checked by absence on origin/bannerlord-1.4.5: no MigrateAllTreeVersions or TreeVersion, no
    HunterPartyAdapter, WarChronicle or HiddenHosts, no FindTakeover, no FactionUI or RaceAbilities folder, no
    generate_career_kits.py, merge_live_file.py, analyze_melee_ladder.py, fix_armour_mesh_ladder.py,
    build_battle_scenes.py, realm_troops.py, orientation.md or TAOM.Client.
  - Mumakil platform files, GarrisonSwap and the Fire and Tribute files are absent too.
  - 1d8fc1d8e: 1.4.x `troops_*.xml` mount only horses, camels and wargs (`warg_dark`, `warg_brown`, `fell_warg`), never
    a spider, elephant or mumakil.
  - Shared-file hunks in 8fd7b2a60, 81851d83a, 536b79f05, f88ea740c, 42e48bbe5 (rule row) and 7247797bc reword or
    extend 1.5.x-only code.
  - c94f797e2 needs v1.5.6 `GetActionSet` behaviour; brief2 records that v1.4.8 throws instead.
  - b2cf46b76 flips a default on purpose for a 1.5.x screen. a92a744e8 cannot be refuted without a build; its named
    trigger is ASO-only.
  - b6f7ab366: the DreadAura 15 m cap is ported separately by efa32184b (WP129).
- **BOOKKEEPING.** The release commits carry version fields, release notes and 1.5.x-only release-process text. The
  1.4.x release skill has no packaging step, which settles 6df36909a's packaging hunk too. caefaa1f3's harness fact is at
  1.4.x `.claude/rules/harness-facts.md:93`. 7cd4920e7's shield fact is already in five 1.4.x docs.
- **CONTENT.**
  - The 1.4.x-present tool hunks in 68250f030, 998f054c0, 2952ccc54, eaba9588f, c3fa211d4 and 90b1a260c serve 1.5.x
    balance or content, and d9a8f46fa's shader-precompile edit is comment-only. 28a7ee625's CareerSystem edits are
    comments, and a728f35b7's character-creation edit drops a `sauron` race 1.4.x never had.
  - 9354b0b2f's `HUMANOID_ROOTS` change is unneeded on 1.4.x: the installed 1.4.8 Armory's `as_hill_troll_warrior` has
    `base_set="as_human_warrior"`.
  - 2952ccc54's armour-index Type filter is latent: the installed Armory's armour files hold no non-armour `Type` (0 found).
  - 71cc4c6d2 is already listed as a prerequisite of a69cd39df (WP135).

## Cautions for plan writers (not misclassifications)

- **WP110 (shader cache keeper, 372773d1a UNSURE).** 3cdffd8a2's header table (format word `0x0783` or `0x0784`, build
  text `123627` or `125053`) and its claim that the Kit shares the game's build number are v1.5.4 to v1.5.6
  measurements. The only `shader_mapping.bin` on this laptop (`C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11`,
  dated 2026-08-14) reads format `0x05EF` and build text `77991`. That matches neither the doc nor build 119303, so it is
  probably not the cache the keeper targets (UNVERIFIED). Measure on v1.4.8 before porting any keeper doc.
- **Lessons.** Generic lessons in skip rows reach 1.4.x through no WP; WP102's docs-lessons-union-merge only sets
  `merge=union`. The ones verified here as needed are F1, F3 and F10. Others (for example b0a93fb4c's "reword when X
  lands" grep rule, relevant to WP108) are optional.

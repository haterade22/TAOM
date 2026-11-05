# X closure: waves 4 and 5 (plans 128 to 145)

Run 2026-11-04-port148. Checks the fixes for `X-consistency-waves-4-5.md` and the `fixes.json` items of plans 128 to 144. No plan was edited.

## Result

- **Fix-list items:** 37 for plans 128 to 144; plan 145 has none. All 37 are closed as written.
- **Findings:** C1 to C10 and the eight C11 rows are closed. C6 and C11's plan 135 row each leave a leftover, listed below as items 6, 7 and 14.
- **Programme issues:** P2 to P6 are closed or need no action. P1 (the README table) is open.
- **Refuted claims:** all seven fixer refutations hold (plan 134 two, 135 one, 139 one, 141 one, 143 two).
- **Open:** 14 items, 0 HIGH, 3 MED, 11 LOW. None stops an executor; they are stale text, a row table or an unowned follow-up.

## What was read

- The finding report, `fixes.json` and the fixers' returned data.
- `git diff --no-index --unified=2` of every plan 128 to 144 against its backup, read in full.
- The surrounding step wherever an edit changed a command, a count or a hand-off.
- Both sides of each changed hand-off: 112/137/138, 136/140, 136/138/144, 134/121/123/126/135, 133/112/125, 141/138/143/144, 117/120/142, 118/144, 108/143, 122/143.
- Scans over plans 128 to 145:
  - No long dash on any added line. The nine older lines in plans 130 and 144 are the verbatim strings P5 exempts.
  - No `git checkout` with a bare double dash.
  - No he or him for the maintainer. Plan 145's hits are warg-rider `sed` strings.
  - No runnable unflagged `dotnet`. The other hits are prose or a `grep` pattern.
- Plan 134's C3 and C4 data edits, run in memory on the blobs at `1b00d1acd` and `8df90444c`:
  - All swaps, regexes and anchors match.
  - No request pool is empty once TAOM's rosters are read, though that run did not apply the stylesheet.
  - 3 rows and 153 lines go.
- Against git objects:
  - Plan 136 D3b's test names and the 87-test count, and E3's anchors.
  - Plan 137 D3's extraction sources, and the Step 2 text at `785b2703e`.
  - Plan 139 C1 against the gate source at `8caac7da5`: message formats, `Math.Min`, the prune rule, the header.
  - Plan 140 C6 against `data_gates.py` at `f1675c058`.
  - Plan 142 C6's anchors, and the evidence file on `origin/bannerlord-1.5.x`.
  - Plan 133's comment anchor at `b747cf398`, and its blob chain through `2c09a4145`.
  - Plan 135's row id and the 320 count at `9dd2f594f`.
  - Plan 141's gate at `e6745a492`.
- On the v1.4.8 dump, not the installed DLLs: `GameStateManager.cs` :257, :285, :288, :313, and `LordConversationsCampaignBehavior.cs` :334-336.

## What was not done

- Nothing was built or run: no test, gate, plan step, D3b port or `-Check`. Every Verify in these plans is unexecuted text.
- Plans were not read whole. A defect in a step no edit touched and no scan names is outside this check.
- Plan 145 has no finding or backup. It got its Status, its prerequisites table, a scan of its WP references and the style scans only.
- Plan 134's engine claims (`MergeElements`, `GetSuitableEquipmentSet`) were not re-read. The refutation rests on the row data I read plus those cited lines.
- Plan 135's claim that all 12 caches hold the 320 rows was not re-checked.
- Writes outside the report: the diffs and a simulation script in the session scratchpad folder `closure45`, and two temp files in Git Bash `/tmp`.

## Open items

### 1. README table stale (MED)

`plans/README.md` lines 166, 167, 172, 173, 178. Status blocks are authoritative, but the table is what a lane reads, and C1 and C3 exist because a lane can start early.

Fix, when the integrator regenerates:
- Row 133 Depends: `101, 104, 105, 112, 125`.
- Row 134 title: as item 13.
- Row 139 Depends: `118, 126, 129, 130, 131, 136`.
- Row 140 Depends: `104, 115, 131, 136`.
- Row 145: its priority, effort, risk and the Depends list from its Status block.

### 2. Plan 136's hand-off to WP144 disagrees with plan 144 (MED)

`plans/136-data-integrity-gates.md:876` asks for a native re-check and says a skipped table "is taken then". `plans/144-docs-engine-sync.md:1159` and `:1231` say WP144 does no new research and the audit half is not there. The re-check has no owner, and the README's "Owed after the programme" table does not list it.

Fix:
- Replace the bullet with: `- **Owed, no WP takes it**: the "native reads a row's type" modding docs, and a v1.4.8 native re-check of D3b's five native ids, soln_animations, soln_sound_event_data and NATIVE_BY_TYPE with WP137's tools. WP144 records both as deferred. If D3b recorded a skip, its table stays out until that check.`
- The integrator adds the same row to "Owed after the programme".

### 3. WP125's feature doc stays false after plan 133 B3 (MED)

`plans/133-campaign-fixes-inventory-ui.md:1121` changes only the patch comment. `docs/features/shadowfax-screens.md`, as WP125 transplants it from `b747cf398` (:275-281), still says Patch23 writes `Color1` and `Color2`. B3.1 ends that.

Fix:
- In B3.6, before Stage, replace the paragraph starting `**Why this is safe next to BannerColorPersistence.**` with the one at `git show 963218f7f:docs/features/shadowfax-screens.md` (:281-288).
- Add the path to the `git add` line and the stage list (:1152).
- Make `(20 paths)` at :1155 read `(21 paths)`.
- Make the `963218f7f` disposition row say "comment and doc paragraph".
- Verify: `grep -c 'ClanColoredCharacterCode.Recolor' docs/features/shadowfax-screens.md` prints at least `1`.

### 4. Plan 134 C4 leaves two stale citations (LOW)

`plans/134-campaign-data-defects.md:1593`. `docs/features/uncapturable-heroes.md:37` and the summary at `TAOM.Tests/Features/UncapturableHeroes/UncapturableRegistryTests.cs:21` both place the three lords' `race="uruk"` in `characters/lords.xml`. C4 deletes those rows.

Fix:
- Add both to Part C's Scope (:366 onward) and C4's docs list and stage list.
- The doc row's third cell becomes `` `lords.xslt` templates ``.
- The comment's `in <c>characters/lords.xml</c>` becomes `in <c>lords.xslt</c>`.

### 5. Plan 126 still routes `lord_1_48_*` as undecided (LOW)

`plans/126-race-and-hero-fixes.md:989`. Replace "the fix needs a race and age decision. Route to WP134 or the maintainer." with "WP134 Step C4 fixes it: today's effective race and age move into the templates."

### 6. Plan 138 names the old graphify owner (LOW)

`plans/138-harness-review-pipeline.md:344`. "graphify wiring (WP141)" becomes "graphify wiring (owed, not ported; Maintenance notes)".

### 7. Plan 143 says WP141 handed the wiring over (LOW)

`plans/143-harness-rules-sync.md:928`. "the graphify workflow wiring WP141 handed over" becomes "the graphify workflow wiring WP141 leaves owed".

### 8. Plan 143's current-state fact goes stale at E3 (LOW)

`plans/143-harness-rules-sync.md:134`. WP136 Step E3 removes the `AdapterPatternArchitectureTests.cs` citation before WP143 starts. After "which does not exist" add "(WP136 Step E3 removes the citation; if `git grep -c AdapterPatternArchitectureTests -- docs/adrs` finds nothing, this line is done)".

### 9. Plan 142's porting rule contradicts its own Step E2 (LOW)

`plans/142-docs-process-sync.md:97`. "Never carry ... `Main/Composition` or `FeatureModules`", while Step E2 now writes the module wiring D2 ported, and WP105 brings both. Delete ", `Main/Composition` or `FeatureModules`" and put "or" before `TAOM.Client`.

### 10. Docs keep the "follow us" claim plan 131 removes from the comment (LOW)

`plans/142-docs-process-sync.md:390`. These sites at `1b00d1acd` keep the claim, and no plan owns them: `docs/features/return-to-army.md` :42, :82, :206 and `docs/reference/harmony-patch-registry.md:982`. Plan 131 B5 also moves the deferral log string below `:100`.

Fix:
- Cite the source in the row as `Patch87_ReturnToArmy.cs` (grep the string).
- Add to the row: "and drop the claim that vanilla's 'follow us' line reaches this state, at those three doc sites and registry :982 (v1.4.8 `LordConversationsCampaignBehavior.cs:334-336`: nothing outputs `lord_tell_gathering`)".

### 11. Plan 136's `// TODO(WP140)` has no taker (LOW)

`plans/136-data-integrity-gates.md:699`. Plan 140 has no step for it. Its `AllowedNonDeclaredNames` at :676 belongs to a different test class. Replace `with a // TODO(WP140) comment and name it in the hand-off` with `with a comment saying why, and name it in the hand-off`.

### 12. Plan 143's new row sits under a trap row its own text contradicts (LOW)

`plans/143-harness-rules-sync.md:920`. CLAUDE.md's "Armory asset trees" row says "No cooked tree: 0 `AssetPackages/*.tpac`". Plan 143's skip reason and plan 108:104-106 say the laptop Armory and the public 1.4.8 build are cooked. No plan corrects the row.

Fix: in row 15's Take cell add "and change that row's opening to: `Desktop: no cooked tree, 0 AssetPackages/*.tpac. Laptop and public build: cooked, AssetPackages/ and no Assets/.`".

### 13. Plan 134's title omits Part C (LOW)

`plans/134-campaign-data-defects.md:1`. Part C is now always on. Use: `# Plan 134: Fix kingdom policies, Lindon's balance, alignment sides, wanderer backstories, generated lord kits and three Nazgul definitions on 1.4.x`. The README row follows.

### 14. Plan 135 still says no ported test carries `LiveInstall` (LOW)

`plans/135-localization-fixes.md:217-219` says no test carries `LiveInstall`, but `git show` finds that tag in `VanillaKeyOverrideTests` at `407b1e767` and `LanguageProcessorTests` at `e45f6df2c`, both files this plan ports. D9 names neither tag, and `plans/122-test-infrastructure-gates.md:247` treats them alike.

Fix: replace the sentence with "`VanillaKeyOverrideTests` and `LanguageProcessorTests` arrive with `[TestCategory(\"LiveInstall\")]`; delete those lines as for `RequiresGame`." Add `git grep -c LiveInstall` over both files printing nothing to A2's Verify.

## Seen outside this scope

Not counted, and left for the closure check of the plans that hold them:
- `plans/108-missing-body-load-hang.md:238` still says "regenerated later by `/verify-bindings`". WP137 Part D regenerates that snapshot.
- Plans 101 to 127 carry the other residuals the fixers named (for example 123:70, 125:30, 122's FakeDataStore note).

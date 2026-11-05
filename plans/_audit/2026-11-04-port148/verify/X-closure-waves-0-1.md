# X-closure: plans 101 to 116 (waves 0 and 1)

Closure check, run 2026-11-04, against `X-consistency-waves-0-1.md`, the fix list and the plans as they stood at this read. No plan was edited.

## Result

37 items checked: 21 from the finding report (F1, F2, F3, F4.1 to F4.4, F5, six F6 rows, F7, F8, programme issues 1 to 5) and 16 fix-list items for plans 101, 104, 105, 107, 108, 110, 112, 113 and 115. They overlap: most fix-list items are the same defect as a finding. 36 are closed. F5 is closed in plan 115 and open only for its README row. No fixer in this scope refuted anything, so there were no refutations to test.

Beyond that, the edits and the fixers' residuals leave 11 open items: 1 MED, 10 LOW. None stalls or misdirects an executor. No HIGH.

## What I read

- The finding report, the 37 keys of `fixes.json` (the scope's 9 in full), and the fixers' returned data.
- `git diff --no-index --unified=2` of backup against plan for all 16 plans. 103, 106, 109 and 116 have no diff. 102 and 114 have only the F1 sweep, as does 111.
- A scan of all 16 plans: long dashes (backup count equals current count; 102 and 115 keep their 5 quoted ones, the rest have 0), CR count (0 everywhere), he/him/his (remaining hits are quoted commit bodies or refer to the mod author Yotthani, never the maintainer), `git checkout` with a bare `--` (only the prohibitions at 103:204 and 106:300), and the hook's other checkout arms (`-f`, `--force`, `.`: only the prohibition at 115:287).
- `.claude/hooks/block-dangerous-git.sh:90-93` and `check-moduledata-validation.sh:97-105`, at the working tree and at `1b00d1acd`. WP101's 19 codes match the hook.
- Surrounding steps for each edit: 101 B7, C1, C4 and done criteria; 105 steps 0.3, 1, 12 and done criteria; 112 A1, B3, C1, C3 and disposition; 113 A1, A3, the drift list, Scope and the Part B SubModule row; 115 step 0 and C5.
- The far side of each changed hand-off: 104 and 129 (`35007dbb8`); 105 and 104 (`06252ae2c`, `MentionsOutsideComments`); 108 and 143 (Armory row, skill, drift line); 108 and 137 (`patch-targets.md`); 108 and 113 (`polls=1`); 110 and 134 (`ResolveSide`); 111 and 112 (`adapters.md`); 112 and 137 (Part D step D3); 112 and 114 (`ae1a6b553`); 112 and 113 (`MainMenuStep`, the Stop line); 112 and 109 (`RecordingLogger`).
- Source commits for quoted text: `0f3af9577` and `06252ae2c` (`FeatureModulesTests.cs:122`, only one `StripComments` in the file, full SHA); `1581d4d0a` (tool comment and note at :1299-1310, test at :1202); `1b00d1acd` (patch comment :13-16, doc :94 and :125, registry :316 with exactly two em dashes); `bb85e9284` and `ae1a6b553` (`adapters.md`; the Covered verdict holds); `85c608963` and `2afea8878` (SubModule hunks).
- `plans/README.md` table, rules and Owed section; the brief at :31, :43, :52, :85-88, :100-105, :226.

## What I did not read or do

- I did not read the 16 plans in full, only the edits and the steps around them. Defects in untouched prose are out of this pass.
- Nothing was built, tested or run. WP101's gate command, WP105's edit, WP113's A3 script text and its `Ran 191 tests` expectation are read, not executed.
- Engine claims were not re-checked: WP113's reading of `MissionState.cs:221-233` and the `WaitForMeshesToBeLoaded` callers rest on plan 108's text. Whether the gate exits 0 on the merged tree stays unknown until WP101 Step B7.
- Plans 117 to 145 were read only at the hand-off points above. Their README rows are for the later waves' closure, though I saw that rows 122, 123, 127, 139 and 140 also lag their plans' Status blocks.

## Open items

### 1. MED. WP113 A3 leaves its own doc contradicting itself, and the other sites have no owner

`plans/113-shader-stall-and-freeze-watchdog.md:441` and `:1060`. After A3, `battle-load-diagnostics.md:94` says `polls=1` is not the #352 wait, while `:125`, about 30 lines below, still says "`polls=1` there means the thread blocked INSIDE frame 1, the #352 shape". The tool's `MissionInitializeDone` hint (`:257` at `1581d4d0a`) still calls the same reading "the blocking-spin shape (#352)", so a triager is still sent there. Line 1060 says "the maintainer assigns them", but `plans/README.md` "Owed after the programme" has no row, and the brief's last section asks for an edge or a named action.

**Fix:** in A3 item 4, add a bullet after the `_format_timings` bullet: "`docs/features/battle-load-diagnostics.md` (`:125` at `1b00d1acd`): replace `the thread blocked INSIDE frame 1, the #352 shape` with `the thread blocked INSIDE frame 1, not the #352 wait`." Add to `plans/README.md` "Owed after the programme" one row: "`polls=1` means #352 wording at the sites plan 113 line 1060 lists | Not corrected by any WP; the maintainer assigns them." Remove `:125` from the list at 113:1060.

### 2. LOW. WP108 writes a sentence into a shipped doc that WP113 makes false

`plans/108-missing-body-load-hang.md:488-490`. The script writes into `docs/features/preload-body-guard.md`: "although the comment in `MissionState_TickLoading_BattleLoad_Patch.cs` names it (recorded for WP113)". A3 corrects that comment, and no plan edits the sentence. It also leaves a work-package name in a feature doc.

**Fix:** end the sentence at "before this\n" "wait.\n": delete "although the comment in `MissionState_TickLoading_BattleLoad_Patch.cs` names it (recorded for WP113)" from the script's string. The Maintenance note at 108:1569 keeps the record.

### 3. LOW. README row for WP115 still lacks WP108 (F5)

`plans/README.md:148`. The Depends cell reads `107, 109`; F5 asked for the row too. WP115's Status block and step 0 are right, and the step 0 STOP catches a wrong merge order.

**Fix:** the Depends cell becomes `107, 108, 109` (integrator).

### 4. LOW. Plan 134 still describes plan 110's corrected wording as current

`plans/134-campaign-data-defects.md:1745` and `:1863-1864`. Plan 110 no longer says WP134 refactors `ResolveSide`, yet 134:1863 opens "**Plan 110** says `MomentumEnrollmentService.ResolveSide` is WP134's to refactor" and 134:1745 calls that carve-out moot. Behaviour agrees on both sides; only the description is stale.

**Fix:** at :1745 delete the sentence "Plan 110's "WP134 refactors it" carve-out is therefore moot". At :1863-1864 replace the bullet with: "**Plan 110** records that no WP changes `MomentumEnrollmentService.ResolveSide`; this plan does not touch it (binding note, V06), so plan 110's both-sides start rule and this plan never meet."

### 5. LOW. WP104's disposition row disagrees with WP105 on the sixth wiring test

`plans/104-guarded-patch-categories.md:1223`. It says all six wiring-test edits of `06252ae2c` are skipped; WP105 Step 1 now takes `FeatureModulesTests`.

**Fix:** "the five wiring-test edits skipped (files absent on 1.4.x); WP105 Step 1 applies the `FeatureModulesTests` line when it creates that file".

### 6. LOW. WP105's review rule calls files byte-identical that its done criteria no longer hold so

`plans/105-composition-root.md:1091-1092`. "the three `TAOM.Tests/Composition` files" are listed as byte-identical to 1.5.x, but two of them differ by the two-line diffs at :1159-1161.

**Fix:** "the three `TAOM.Tests/Composition` files beyond the differences the Done criteria name".

### 7. LOW. WP103 step 0 still treats a non-zero gate as normal

`plans/103-executor-safety-harness.md:260`. It quotes the old planning figure (rc 1, 3,423 `BROKEN_ITEM_REF`) and says "Not a STOP". WP101 now STOPs on a non-zero gate, so WP103 should expect exit 0 and its "Not a STOP" would hide a regression between the two.

**Fix:** replace the parenthesis and the last sentence with: "(WP101's baseline records exit 0; planning, before the Armory refresh: 22.1 s, rc 1, 3,423 `BROKEN_ITEM_REF`). A non-zero exit is a STOP: every ModuleData commit would be denied once Part B lands."

### 8. LOW. Same stale pointer F6 fixed elsewhere in plan 108

`plans/108-missing-body-load-hang.md:238`. `taleworlds-api-snapshot/*` is "regenerated later by `/verify-bindings`"; :1592 now says WP137 Part D.

**Fix:** "(regenerated later by WP137 Part D)".

### 9. LOW. WP113's earlier-WP drift list is still short

`plans/113-shader-stall-and-freeze-watchdog.md:12-20`. F6 named WP107 and WP112, and both were added. The list also omits WP108's `Main/SubModule.cs` block (108:211) and WP111's `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs` and `taom_module_strings.xml` (111:281, :367, :252). All three of the latter sit on the drift path list, and both WPs come earlier in README order.

**Fix:** extend the WP108 clause to "`Main/SubModule.cs` (the Patch90 block) and its own section of `harmony-patch-registry.md`", and add "WP111: `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`, `taom_module_strings.xml`".

### 10. LOW. A comment falls through the WP112 and WP113 seam

`plans/113-shader-stall-and-freeze-watchdog.md:483`, with `plans/112-crash-robustness.md:905-906`. WP112 adds `// Only sets a volatile flag, so it cannot throw.` only if the Stop line is present; WP112 merges first in table order, so it is not. WP113 inserts the line without the comment, while WP112's text at :891 relies on that reasoning. The placement itself holds: `base.OnBeforeInitialModuleScreenSetAsRoot();` survives WP112's edit.

**Fix:** at 113:483, after "immediately after `base.OnBeforeInitialModuleScreenSetAsRoot();`", add "with `// Only sets a volatile flag, so it cannot throw.` directly above it".

### 11. LOW. The brief and README index still state the old plan count and map authority

`plans/_audit/2026-11-04-port148/data/brief-workflow3-decisions.md:52` and `plans/README.md:72`. Both say 44 work packages; plan 145 exists and WP101 now commits "101 to 145". Brief :52 also calls `wp_map.json` authoritative for dependencies, which brief :226 and README :107 reverse. This is the last piece of programme issue 1.

**Fix:** brief :52 becomes "44 work packages, ids 101 to 144, in waves 0 to 5, plus plan 145 added afterwards. The planning map (groups, lanes) is"; README :72 adds "(plan 145 was added afterwards)".

## Noted, not open

- WP115 step 0 STOPs on a missing `tools/audit_armory_refs.py`, which WP108 Part B brings. If WP108 lands Part A only, all of WP115 waits although only C5 needs the tool. That follows the fix as written and routes to the maintainer.
- WP108's two-part docs saying the skill and drift line "were not ported" agree with WP143's skip decisions (143:932-933), so the fixer's residual on them does not apply.
- The residual naming plan 145 as absent, and the one naming plan 137 as lacking the Part D step, are both out of date: `plans/145-late-followups.md` and 137 Step D3 now exist.

# X: cross-plan consistency, waves 4 and 5 (plans 128 to 144)

Run 2026-11-04-port148. Read-only critic: no plan was edited. Evidence was read on 2026-11-04 from the plan files and
from git objects in this clone.

## Coverage, and what this report did not do

The assignment asked for a full read of each of the 17 plans. **That was not done.** The 17 files hold about 2 MB, more
than one session can read. What was read instead:

- In full, for all 17: the Scope, Disposition, Maintenance notes and earlier-WP prerequisite sections.
- In full, for 128 and 132: Current state as well.
- In full: 143 Part G, 142 Part E, 136 Step E1.
- Spot reads of individual Steps where a lead pointed.
- By script, over all 44 plans: every commit hash, every `WPnnn` reference (2,477 lines indexed), every disposition
  table, every Depends on line, every commit subject and trailer, every patch category, long dashes and unflagged
  `dotnet` commands.
- Other plans (101 to 127): Depends on lines, disposition rows and targeted greps, not their full Status and Scope
  sections.

A defect that sits only inside a Step body, naming no WP and no commit hash, could be missed. A clean result below means
"the checks named here found nothing", not "the plan was proofread".

## Findings, most severe first

### C1. WP139 breaks the entry-point gate WP136 lands (HIGH; missing edge)

- WP136 Step E1 lands `EntryPointLineCountGateTests` with a baseline regenerated from the tree at that moment. Its
  summary: "a class not in it fails at 150 lines, a listed class fails when it grows".
- WP130 ships `BattleActionBarMissionView` at the `40385aa5b` shape. Measured on that blob, the class is 136 lines, so it
  is under the limit and not in the baseline.
- WP139 C1 applies `b4ad748f2`, `30a938689` and `0bc2a132f`. Measured at `0bc2a132f`, the class is 160 lines. The 1.5.x
  baseline has exactly that row: `TAOM.Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView 160`.
- Plan 139 line 108 knows the class grows by 24, but "EntryPoint" appears 0 times in plan 139, and its Scope excludes the
  baseline file. Plan 139 does not depend on WP136.
- Result: the full suite fails after the merge. It fails on the executor's tree when 136 is on its base, and on the
  integrator's tree otherwise. Part E's edits to 20 Main files can also grow listed classes.

### C2. The API-snapshot verify row and `/verify` Step 2d have no owner (HIGH; shared hunk nobody ports)

- WP112's disposition of `6ae924638` defers the `.ai/verification.md` `api-snapshot` row and the
  `.claude/skills/verify/SKILL.md` Step 2d to WP137 (plan 112 lines 1072, 1102, 1204). It says plan 137 did not list them.
- Plan 137 still does not: no mention of `verification.md` or Step 2d, and neither file is in its Scope.
- WP138's condition K7 ("`/verify` has a Step 2d API snapshot check (WP137)") can therefore never be true, so every step
  marked "under K7 only" is silently skipped: the snapshot README paragraphs, `snapshot_api_surface.ps1` and the
  `-Check` reproduction.
- `git show 6ae924638` confirms both hunks exist. They describe `tools/snapshot_api_surface.ps1 -Check` and
  `tools/engine_body_diff.py`, both of which WP137 brings.

### C3. WP136's hand-off to WP140 is not honoured (MED; hand-off dropped, missing edge)

- Plan 136 lines 490 and 798: `data_gates.py`'s `gui-tests` row keeps only names that exist, and "WP140 adds its own".
- Plan 140 mentions `data_gates` 0 times and `tools/data_gates.py` is not in its Scope. Its new gates would never run from
  the one command that WP136 created for data gates.
- WP140 does not depend on WP136, so `tools/data_gates.py` may be absent on its base when a lane starts it early.

### C4. WP134 reopens a decided question (MED; D13 applied inconsistently)

- The brief says a plan "never reopens" a decision. D13's test: the defect is verified on 1.4.x data and the fix needs no
  OUT content.
- Plan 134's own evidence meets that test for `9dd2f594f`: 40 wanderers, 280 missing rows, no Oaths of Service file
  touched, and the pack note's stated reason is factually wrong by date (`9dd2f594f` is 2026-10-08, `caed64ae1` is
  2026-10-20).
- It still ships Part C OFF, gated on the words "Part C enabled" in a dispatch or in the README row, and asks the
  orchestrator to decide (Maintenance notes). Plan 135 records it as "off by default".
- Nobody is assigned to answer.

### C5. `bb68eeca3` item 3 is assigned to a plan that never mentions it (MED; unowned hunk)

- Plan 123 defers the lord-template roster XSLT to "WP134 notes, D13" (lines 24, 1257).
- `bb68eeca3` appears 0 times in plan 134 and in `packs/134-*.md`. No disposition row exists anywhere, so the brief's
  "nothing silently dropped" rule fails for this hunk.

### C6. Graphify wiring is routed to WP143, which declines it (MED; hand-off loop)

- Plan 141's disposition sends the wiring and ADR-012 "to WP143", and its hand-offs list them, including agent lines for
  `refactoring-specialist` and `9cfcadd07`'s comment.
- Plan 138's Maintenance notes also say WP143 takes it "(its Part D)".
- Plan 143 Part G says "Not taken here, report as owed", deletes `refactoring-specialist.md`, skips `9cfcadd07`'s graphify
  hunks and lists ADRs 011 to 013 as 1.5.x-only.
- So WP141's `check-graphify-usage.sh` lands with its workflow wiring owned by nobody. What that gate does with no wiring
  was not checked (UNVERIFIED).

### C7. WP133 calls a WP112 file "1.5.x-only" and leaves a stale test row (LOW; wrong creator claim)

- Plan 112 creates `TAOM.Tests/Core/MainPartyNullCheckScanTests.cs` from `4f27b3bde`, and its notes say "WP133's
  `1888b6f28` drops the `PlayerEquipmentAdapter` row from `AliasedSites`".
- Plan 133 (lines 390, 393, 2223) labels that file 1.5.x-only and drops the hunk.
- The test still passes: it asserts the file does not mention `PartyBase.MainParty`, and A1 deletes that use. The row
  then pins a site that no longer exists, and two plans contradict each other.

### C8. WP142 Step E2 tells the executor to write feature rows against D2 (LOW; D2 inconsistent)

Plan 142 Step E2 says to rewrite each 1.5.x row with "`IoC.cs`/`SubModule.cs` wiring instead of a module". Under D2 the
ported features keep their modules on 1.4.x: BattleCorpses, StuckBattle, MissionStartGuard, SkeletonBuffer, TavernSpecialities,
AlignmentRecruitment, MessengerSessionRules and others. The instruction would write false rows for exactly those.

### C9. WP144 assumes WP142 recomputes lesson counts; WP142 removes them (LOW; stale assumption)

Plan 144 C1 item 7 and its Maintenance notes say WP142 "recomputes" `LESSONS-LEARNED.md`, and its Scope lists the file
"(counts only)". Plan 142 F1 drops the counts, and its Verify requires `grep -c ", [0-9]+ lessons"` to print 0. Item 7 is
guarded on a log word that will not exist, so it is dead text, but it invites re-adding the counts.

### C10. Disposition text names the wrong owner of `6ae924638`'s other hunks (LOW)

Plan 112's split is the accurate one: WP112, WP119, WP123, WP129, WP137. Plan 129 says "WP111, WP112, WP114 and WP123",
and plan 123 says "WP112, WP126, WP114". Plans 111 and 114 never mention the commit; plan 126 mentions it once, as WP119's.

### C11. Smaller text defects (LOW)

| Plan | Text | Fact |
|---|---|---|
| 133 Maintenance | "BannerBearers tests and `banner-bearers.md` (WP129's race gate)" | The race gate is WP130's (A1, A2); plan 129 does not name it |
| 128, 132 Current state | "no `.gitattributes`" | The file exists at `1b00d1acd`. Also `std_taom_enlistment_strings_*` reads `i/lf w/crlf` while `std_taom_module_strings_*` and `std_taom_xslt_strings_*` read `-text` |
| 144 prerequisites | `tools/tpac_clone_metamesh.py`: "WP141 (optional there)" | Plan 141 skips it unconditionally, so B5 always takes the whole file |
| 141 C2 and disposition | formulas "without the tool", tool "Skipped" | True at WP141's merge, but WP144 brings the tool later, so the ledger text goes stale |
| 130 line 728 | Writes `dotnet test TAOM.Tests --filter ...` into `docs/features/dread-aura.md` with no `-p:` flags | The brief says an unflagged command deploys into the game install on this line |
| 143 Disposition | No row for `30abf550b` | Plans 108, 130 and 137 route its lint regex hunk there. It is moot: `35ff005dc`, which WP143 ports, removes that regex. The route still needs recording |
| 142 Step E0 | The starting list has no 136 or 137 hits | Plan 136 alone hands over about eight lessons. The grep is authoritative, so nothing is lost if the executor runs it |
| 135 lines 215, 685, 714, 1232 | Keeps a `RequiresGame` test (read in part) | Plans 123 and 129 delete every such tag under D9. Likely the same rule should apply; confirm when fixing |

## Programme-level issues

1. **The README table is stale for two rows.** Plan 130's real edges include 110 and 127, the table shows 105, 123. Plan 133's
   include 104 and 101, the table shows 105. The 136 row still shows `?`. Plan 141's title still promises "creature fit" and
   "ranged ladders", which the plan skips. `PROGRESS.md` already owes the regeneration; regenerate from the plans, not `wp_map`.
2. **There is no register of owed work.** These items end with "the integrator decides", "owed" or "a later sync", with no
   owner: graphify wiring (C6); WP108's CLAUDE.md Armory trap row, `/armory-audit` and session-start drift line; the
   `audit_mbproj_registration.py` half of `09f3126a7` (WP136, WP138 and WP144 each defer it, each naming another);
   the strict Mixed Formations hotkey rule (WP123 waits for WP133's `EnumNames`, and no plan picks it up); the
   `module_sounds.xml` elf horn path (WP121 to WP134 to the integrator); `lord_1_48_1/2/3` (WP126 to WP134 to the maintainer);
   the `Patch87_ReturnToArmy.cs` comment; `44d4dba46`; the 1.5.x commits newer than `e45f6df2c` named by plans 124, 135, 136,
   140, 141 and 143. Put one "Owed after the programme" section in `plans/README.md`, each row with an owner or an explicit
   "not porting".
3. **Table-order merging hides the missing edges.** README step 5 merges in table order, which rescues most same-wave
   hand-offs: WP142's lessons from 136 to 140, WP143's Part G from 139 and 140, WP138's expected-absent K13. It does not rescue a
   lane that STARTS before its sibling merges, which is why C1 and C3 need real edges. Everything else was found safe under
   table order, so no other new edge is proposed.
4. **WP118 cites D9 for skipping `binding-gate.runsettings`.** D9 is about CI, and that file is not CI (plan 122 flags this too).
   Under table order, WP122's A1 variant takes the hunk and WP143 row 5 is conditional, so it works. Plan 118's reason
   should still be corrected.
5. **The style check must exempt verbatim match strings.** The long dashes in plan 130 (1 line) and plan 144 (9 lines), and
   those the script counted in plans 102 and 115 (5 each, not inspected), are in scope for a check. The 130 and 144 lines are
   old text an executor must match, or a grep pattern; removing them would break the plans.
6. **Dead branches are harmless but cost reading time.** Plan 135 A9 handles "WP132 absent" though 132 is a hard edge. Plan 129
   handles "WP118 absent" though wave 2 precedes wave 4.

## Checks that came back clean

- **Edges.** Every Depends on line in 128 to 144 matches the reviewed list given to this critic. No cycle; every edge points to a
  lower id; none contradicts the wave order.
- **Same commit in two plans.** Every hash in two or more disposition tables was reviewed and splits cleanly into different hunks:
  `30abf550b`, `25995dc91`, `113d3483b`, `a9f2d7eda`, `71fea53c8`, `2992763f6`, `452796db4`, `4fbfb7adc`, `434d1d795`,
  `709649c3f`, `f1675c058`, `09f3126a7`, `e0ad0db76`, `a27c6b338`, `4520af802`, `e1a940ac9`, `9dd2f594f`, `2c7f7fae9`,
  `f48ef74dc`, `ecaa67d26`. WP143's G9 honours WP138's S2 to S4. WP141's Case L matches WP135's A7. WP129's `e0ad0db76` adoption
  matches WP123, whose `Ported-from` carries the full hash that plan 129 Step 0 greps.
- **Pack coverage.** Every commit classified PORT or PORT-PARTIAL in a 128-to-144 group has a row in its owner's table, or is
  named in the owner's plan where that table uses long hashes. The one PORT-PARTIAL with no owning WP is `7f3636b2e`,
  issue-966, out under D7.
- **Relied-on symbols checked against the creator.** `slot_rejections` (WP117 takes the tool from a blob that has it),
  `PatchShieldFinalizerTests` (107), `MissionTickProfilerWiringTests` (117), `CareerPerkOffThreadTests` (106),
  `StaleCharacterParentRepairTests` (112), `randomRoll` (127), `CampaignHotPathSettingsProvidersTests` (123),
  `McmRenamedDefaultsTests` (110), `test_perf_runs` (117), and WP138's H3 pin (`_pybin.sh` at `88e805450` already holds WP103's three
  helpers). `tools/reviewctl.py` and `tools/tests/test_model_registry.py`, which plan 143 uses, are in the base tree.
- **Deleted prefabs.** No other plan names the prefabs WP140 deletes.
- **Commit and command template.** Subjects in 128 to 144 carry `v2.0.28` (other versions the scan hit are 1.5.x subjects quoted in
  disposition tables), and none of the plan's own subjects exceeds 72 characters. Trailers are 40-hex or carry an explicit
  `git rev-parse` instruction. `-p:ModuleId=` is on every runnable build and test except C11's doc line. No plan's own step
  edits `plans/README.md`. Branch names follow the brief.
- **Patch numbers.** No two different features share a number at 89 or above.
- **Decisions spot-checked.** D1, D2 (module list at `e45f6df2c` against plans 131 and 133; WP128's "no Enlistment module" holds),
  D3, D7, D8, D9 (no workflow added), D15, D18.

D4 to D6, D10 to D12, D14, D16, D17 and D19 to D24 were not checked one by one here; they mostly bind earlier waves.

## Per-plan fixes

| Plan | Fix | Finding |
|---|---|---|
| 139 | Add 136 to Depends on. In Scope add `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` and `EntryPointLineCountGateTests.cs`. In C1, after the view grows: run `TAOM_WRITE_ENTRY_POINT_BASELINE=1` with `Baseline_Regenerate_WhenAsked`, expect the new row `...BattleActionBarMissionView 160`, and raise `MaxBaselineEntries` by one. Repeat after E1 if a listed class moved. Cite ADR-002 in the commit body | C1 |
| 137 | Add a Part D step: take `6ae924638`'s `.ai/verification.md` `api-snapshot` row and `.claude/skills/verify/SKILL.md` Step 2d (with `0002cb561`'s wording if it differs), after `4ab005dd1` makes `-Check` pass. Add both files to Scope and a disposition row for the shared hunk | C2 |
| 112 | Replace "plan 137 did not list them ... the integrator must place them" with the WP137 step reference | C2 |
| 140 | Add 136 to Depends on. Add `tools/data_gates.py` to Scope and a Part C step adding its gate class names to the `gui-tests` row. Verify with the existing `test_data_gates.py` | C3 |
| 134 | Decide under D13: make Part C ON by default, remove the "Part C enabled" gate and the "For the orchestrator" note, and mark the disposition row as ported. If the maintainer rejects it, state that as decided with the reason | C4 |
| 135 | Follow 134's outcome: replace "off by default" and count the 280 rows in Part C's expectations | C4 |
| 134 | Add a disposition row for `bb68eeca3` item 3: test it against D13 on 1.4.x data, then port or skip with the reason | C5 |
| 123 | Point the deferred roster XSLT line at that row; name the hotkey rule's owner or say "not porting" | C5, P2 |
| 141 | Reword the `b8833474e` and `e6745a492` rows and the WP143 hand-off: wiring, ADR-012 and ADR-010 are owed, not WP143's. Remove the `refactoring-specialist` and `9cfcadd07` lines. State what the gate does with no wiring | C6 |
| 138 | Maintenance notes: replace "hands the workflow wiring to WP143 (its Part D)" with the same owed wording | C6 |
| 133 | Remove `MainPartyNullCheckScanTests.cs` from the A1 `drop` list and from "1.5.x-only"; keep the one-line row removal, stage it, and update the disposition row | C7 |
| 142 | In Step E2 replace "`IoC.cs`/`SubModule.cs` wiring instead of a module" with: "wiring as this tree has it: the module where the WP ported one (D2), `IoC.cs`/`SubModule.cs` otherwise" | C8 |
| 144 | Delete C1 item 7, remove `LESSONS-LEARNED.md` from Part C's Scope, and reword the Maintenance line to "WP142 removes the counts (F1)" | C9 |
| 129 | Disposition of `6ae924638`: "its other hunks belong to WP112, WP119, WP123 and WP137" | C10 |
| 123 | Disposition of `6ae924638`: "Other hunks: WP112, WP119, WP129, WP137" | C10 |
| 133 | Maintenance: "WP129's race gate" becomes "WP130's race gate" | C11 |
| 128 | "no `.gitattributes`" becomes "`.gitattributes` covers scripts only; these files are CRLF in the worktree" | C11 |
| 132 | Same wording as 128 | C11 |
| 144 | Prerequisite row: "WP141 skips it; B5 always takes the whole file" | C11 |
| 130 | Line 728: add `-p:DisableModuleCopy=true -p:ModuleId=` to the documented command | C11 |
| 143 | Add a disposition row: `30abf550b`, lint regex hunk, superseded by `35ff005dc` (F1) | C11 |
| 142 | Step E0: add plans 136 and 137 to the starting list | C11 |
| 135 | Drop the `RequiresGame` tag as 123 and 129 do (D9), after confirming the test's shape | C11 |
| 118 | Correct the D9 citation: `binding-gate.runsettings` is WP122's hunk, not a CI artefact | P4 |

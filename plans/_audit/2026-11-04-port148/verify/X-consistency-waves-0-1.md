# X-consistency: plans 101 to 116 (waves 0 and 1)

Cross-plan consistency critic, run 2026-11-04. No plan was edited. Line numbers are the plan files as they stood at this read.

## Coverage, stated plainly

The assignment asked for each of the 16 plans to be read in full. They total about 1.6 MB, and I did not read every line. What was actually read:

- In full: `data/brief-workflow3-decisions.md`, `data/wp_map.json`, the writer summaries for 101 to 116, the integrator table and decisions in `plans/README.md`.
- From all 16 plans: the Status lines, every line naming another WP (each cut at 420 characters), every disposition row keyed by a SHA, and targeted excerpts around each suspect below.
- From plans 117 to 144: the Depends, Wave and Decisions lines, SHA disposition rows, and greps for specific symbols. Not their full Status, Scope and disposition sections.
- Scripted across 101 to 116: pack SHA coverage, commit subjects, trailers, worktree names, dash characters, `git checkout --`, `git add`, dotnet flags.

So the findings are real, but the list is not proven complete. A defect that lives only in step prose and never names another WP or SHA could be missed. Engine claims were not re-checked; this pass is about the plans against each other.

Repository evidence came from local `HEAD` `c5b84fb41` (not `1b00d1acd`) and 1.5.x `e45f6df2c`.

## Findings

### F1 (HIGH). Five plans here use a git form that WP103 turns into a stalling prompt

`.claude/hooks/block-dangerous-git.sh` asks for confirmation on any `git checkout` whose arguments contain a bare ` -- ` (regex read from the script; the 1.5.x end state at `88e805450` that WP116 ports keeps the same arm). WP103 makes that output effective, and WP103:1157-1159 lists this form as one that stalls an unattended run, adding that the brief's procedure avoids it. WP106:300 and WP112:262 explicitly tell the executor to avoid it. Others prescribe it:

| Plan | Lines |
|---|---|
| WP110 | 293, 295, 385, 387, 587, 666, 784 |
| WP111 | 138, 309, 312, 313, 316, 378, 380, 382, 386, 620, 622, 623, 773, 784, 789, 793, 796, 839, 848, 855, 869 |
| WP113 | 219, 274 |
| WP114 | 254, 256, 291, 292, 312, 313 |
| WP104 | 1139 (only bites if WP103 merged first; the lanes are parallel) |

The brief writes these forms without `--` (`git checkout --ours <path>`, `git checkout HEAD <path>`). Later plans carry the same form; raw grep counts, which may include a prohibition line: 117 (17), 118 (3), 119 (6), 122 (2), 123 (11), 124 (1), 126 (4), 127 (3), 128 (6), 129 (9), 130 (4), 131 (13), 132 (3), 133 (16), 134 (11), 135 (6), 136 (5), 137 (2), 139 (5).

**Fix:** delete the ` --` in each. `git checkout <sha> -- <path>` becomes `git checkout <sha> <path>`, and the same for `--ours` and `HEAD`. Add one line to the brief's per-port procedure naming the banned form.

### F2 (MED). The ModuleData gate needs zero errors, while the programme's rule is "no new ERROR vs baseline"

The brief's Data row and every plan compare against the WP101 baseline. The hook does not: `check-moduledata-validation.sh:97-105` runs 19 `--code` flags and any exit 1 blocks (WP103:89-91 says so for the 3,423 case). WP101:587-590 expects `BROKEN_ITEM_REF` near 0 but only records counts, and accepts `N_MERGED` up to `N_ORIGIN`. A non-zero count in any of the 19 codes after the Armory refresh satisfies WP101, then denies every later ModuleData commit once WP103 is live: WP104 Step 4, WP109, WP111, WP113 and others commit string XML. WP108's two added codes widen this; WP108 already flags its own half.

**Fix (WP101 Part B, after the merged validator run):** run the hook's exact `--code` list, record its exit code in the baseline block, and treat a non-zero exit as a STOP routed to the maintainer, naming the consequence.

### F3 (MED). WP111 and WP112 disagree about `.claude/rules/adapters.md`

WP111 Step C1.5 (:801-806) ports `bb85e9284`'s "campaign statics" paragraph after the computed-getter paragraph. WP112:106 says 1.4.x has no such paragraph, WP112:1094 calls `bb85e9284` 1.5.x-only, and WP112:424 inserts at the same anchor (`:25`). WP111 merges first in README order, and WP112 does not depend on it. The contents differ, so the result is a textual conflict plus a false statement in WP112's disposition table.

**Fix (WP112):** at :106 and :1094 say WP111 brings that paragraph when merged first; at :424 and at the `PartyBase.MainParty` insert (:987) anchor after whichever paragraph ends the block, keeping both.

### F4 (MED). Hand-offs nobody accepts

1. **WP112 to WP137.** WP112:243 and :1071 assign the `.ai/verification.md` api-snapshot row and the `.claude/skills/verify/SKILL.md` step to WP137. A grep of plan 137 finds neither path. WP112 itself says the integrator must place them, which is not an owner. **Fix:** add a step to WP137 Part D, add both paths to its scope, and reduce WP112:1071 to a pointer.
2. **WP108 to WP113.** WP108:99-102 and :1569-1573 say WP113 owns correcting the "`polls=1` means #352" wording in `MissionState_TickLoading_BattleLoad_Patch.cs:12-16`, `battle-load-diagnostics.md:94`, the registry and the triage tool. Plan 113 has no match for `TickLoading`, `WaitForMeshesToBeLoaded` or `#352`, and lists `MissionState_TickLoading_BattleLoad_Patch.cs` nowhere. The 1.5.x tip still carries the same comment, and `1581d4d0a`'s triage tool still has the phrase at about line 1309, so WP113's A1 transplant does not cure it. **Fix:** add the corrections to WP113 Part A3, re-reading each location after A1, because WP108's tool line numbers will have moved. I did not check WP108's `registry:316` citation against `1b00d1acd`.
3. **`06252ae2c`'s `FeatureModulesTests` hunk.** WP104 cannot take it (file absent). WP105:1100 and :1217-1219 defer it to "any later WP that touches that test". No plan claims it. **Fix:** WP105 applies it when it creates the file, because `MentionsOutsideComments` exists from WP104. Otherwise record it as skipped with a reason.
4. **`ae1a6b553`'s `adapters.md` sentence.** WP114:1285 defers it to WP112. The SHA appears nowhere in WP112. It may be covered by WP112's fresh `PartyBase.MainParty` paragraph, but I did not compare the text. **Fix:** WP112 adds a disposition row saying so.

### F5 (LOW to MED). Missing edge: WP115 needs WP108

WP115:1074-1076 writes `/release` text that runs `tools/audit_armory_refs.py` "(from WP108)" and relies on WP108's two validator codes. WP115's Depends are 107 and 109, and its preconditions (:315-320) do not check it. README number order hides this unless WP108 Part B blocks, which WP108 names as a real outcome. **Fix:** add 108 to WP115's Depends, the README row and `wp_map.json`, and add `test -f tools/audit_armory_refs.py` to step 0.

### F6 (LOW). Wrong owners and stale statements in plan text

| Plan | Line | Says | Should say | Proof |
|---|---|---|---|---|
| WP104 | 1226, 1337 | `35007dbb8`'s field-commission fix stays with WP126 | WP129 | WP129:1071, :1688; WP126:239 |
| WP108 | 1591 | `/verify-bindings` by "WP122 or the integrator" regenerates `patch-targets.md` | WP137 Part D | WP122:238, WP137:186, WP112:1201 |
| WP113 | 7, 977 | Update the WP113 row in `plans/README.md` | Report in the hand-off; integrator edits (the brief's rule; all 15 others comply) | brief "Executor environment" |
| WP113 | 12-16 | Earlier-WP drift list stops at WP106 | Add WP107 (`SubModule.cs`, `battle-load-diagnostics.md`, `BattleLoadStallWatchdog`), WP112 (`OnBeforeInitialModuleScreenSetAsRoot`, MainMenuStep) | WP107:313-314, WP112:889-899; both merge earlier |
| WP107 | 317 | "only if WP115 merged first" | Delete: WP115 depends on WP107 | Status lines |
| WP112 | 866-867 | The `RecordingLogger` helper "arrives only with WP118" | WP109 creates the identical file; the local nested helper still compiles | WP109:1151 |

### F7 (LOW). Practice differs from the brief's bookkeeping rule

The brief's rule 2 drops `docs/reviews/lessons/*` and `.claude/` in code WPs, excepting only `.claude/rules/*.md` lines documenting the ported mechanism, and the brief says it overrides the packs. These plans go beyond that:

- Lessons: WP108:215 and :540-551 (two entries, citing orchestrator note K2); WP114:216, :346, :356, :477, :501, :518 (four files, from its pack's port notes). WP106's single entry is covered by D7.
- `.claude/skills` or hooks: WP104 (deep-review check 9), WP108 (`check-moduledata-validation.sh`, needed for its gate), WP109 (engine-bump Phase 4 item 7), WP110 (`/release` line).

Each has a reason, and WP142's "only if its heading is absent" rule (:564) stops duplicates of whole entries. WP114's in-place 32,763 edit relies on its own hand-off at :1404. **Fix (brief):** list these exceptions; no plan change needed. Otherwise WP114's lessons should move to WP142.

### F8 (LOW). D7 is slightly exceeded by WP106

D7 says take "only" `bd1ac8292`'s tickCompleted lesson. WP106:1339 also takes the `SaveShieldPolicy` and Patch69 comments and a rule sentence, staged at :1270 (`Dependencies/Foundation/SaveShieldPolicy.cs`). These are comment-only and no co-op code comes along, but WP106:284 reserves the rest of `Dependencies/` for WP107, and WP112:706 relies on the Patch69 comment. **Fix (brief):** widen D7 to "the lesson and its comments". Dropping them would break WP112's merge expectation.

## Programme-level issues

1. **Dependency records disagree.** The plan headers are the most complete; they and `wp_map.json`, called authoritative by the brief, differ:
   - `wp_map.json` lacks WP118's 105 and 112, WP130's 110 and 127, WP133's 101 and 104, WP134's 104, WP140's 104 and 115.
   - The README row for WP130 says `105, 123`; plan 130 and the reviewed list add 110 and 127. **Fix the row.**
   - The reviewed list given to this critic has no entries for 105 and 113. Their plans and README say 104 and 106, so nothing is wrong except the list.
   - The README row for WP136 shows `?` for priority, effort and risk; plan 136 states P2, L, MED.
2. **D19's count is stale.** It says "the 10 git gates". WP102 removes one, so WP103:49-50 and WP116 P3 (:76) correctly say nine. Fix the brief's wording.
3. **`wp_map.json` and pack 107 title** say "six allowlisted shims". The plan's 16 is right: `Native2ManagedTargets.All` at `e45f6df2c` has 16 entries, counted by hand.
4. **`wp_map.json` still gives WP106 pri 0.** D21 makes it P1, which the plan and README follow.
5. **README status reads two ways.** WP104:384 and :1278 read the committed `bannerlord-1.4.5:plans/README.md`; WP109:322 and WP114:275 read the main checkout's working copy. The brief does not say the integrator commits after each merge. It should, or the two readers can differ.

## Checked, no defect found

- Dependency order: for all 44 plans every dependency has a lower id and no higher wave, so no cycle and no reversal. README number order satisfies every edge.
- Double ports: of the 92 SHAs in more than one plan's disposition rows, those involving 101 to 116 were read. The splits agree (`c79a58521`, `30abf550b`, `6ae924638`, `ecaa67d26`, `6eb2e1b90`, `a9f10688f`, `d99398f12`, `ab78564e0`, `1165a00de`, `ea8f2ad52`). `RecordingLogger.cs`, `EngineDataStore.cs` and the `TickProfiler` files are declared add/adds.
- Pack coverage: pack SHAs absent from a plan were read one by one. All are context mentions, not owned commits, in packs 102 to 116.
- Shared groups in `wp_map.json` (104, 107, 118, 120): each side's disposition names the hunk.
- D2: 1.5.x module list at `e45f6df2c` read. BattleCorpses, StuckBattle, MissionStartGuard, SkeletonBuffer and BardTownGuard arrive as modules. Patch90, the shader notice and HideoutClanGuard have no 1.5.x module and stay hand-wired.
- D5: WP109:185 carries the exact row. D1: the only `CHANGELOG.md` staging is WP101 and WP102's own re-cut. D21: WP106 is P1.
- Symbols checked across plans: `PreserveForRethrow(Exception?, MethodBase?)` and the `PatchShieldPolicy` exclusion methods against 1.5.x source; the `FeatureModuleHooks` calls WP109 quotes against WP105; `MissionStartGuardCalls.cs` exists at `e45f6df2c`.
- Patch91b passes WP122's gate: its regex `^Patch(\d+[a-z]?)_` keeps `91b` distinct from `91`. WP114's three `Patch61_*` categories get registry sections.
- WP143's pre-commit code list is defined as equal to the hook's, so WP108's codes carry over.
- Baseline 9154/9151/3 and the commands agree where quoted. Every worktree and branch name matches its slug. No dotnet command in an executable step was found lacking `-p:ModuleId=`. The subject scan found no malformed label and none over 72 characters; the v2.0.29 to v2.0.34 hits are citations of 1.5.x subjects.
- The long dashes in WP102 and WP115 are quoted anchors and existing code strings, which must match byte for byte.

# Addendum to brief.md: facts established after the classification pass (read after brief.md)

Everything here was verified by the orchestrator on 2026-11-04 unless marked as a lead.

## Outputs of the classification and deep-dive pass (Workflow 1)
- Per-commit verdicts: SP\agg\classified.jsonl (1114 rows: h, sha, subj, date, verdict, track, group, priority,
  symptom, prereqs, why, port_notes, superseded_by, pick, conflicts, real_conflicts (non-bookkeeping), files).
- Port groups (verdict PORT, PORT-PARTIAL or UNSURE): SP\agg\groups.json (331 groups, 1.7 MB: query it with Python,
  do not read it whole). One-line view: SP\agg\groups_compact.txt.
- Classifier cross-batch notes (prerequisites, series, traps): SP\agg\batch_notes.md, one "## batch NN" section each.
- Nine deep-dive lens reports: Repo\plans\_audit\2026-11-04-port148\deepdive\{save-time, memory, freeze-threading,
  loading-hang, xml-data, load-time, runtime-perf, wotr, release-integrity}.md. Finding ids look like SAVE-TIME-01.
- Draft work-package map (every port group assigned to exactly one WP 101-136): SP\agg\wp_map.json.

## Refs moved
- origin/bannerlord-1.5.x is now e45f6df2c (2026-11-04 10:48). The 6 commits newer than the manifest are 5 features
  (Fords of War, Long Defeat, hero-race Patch132 icons) and fe20eddff (docs: the taom-src cache is per game build).
  Nothing else is unclassified.
- The 1.4.x tip is still 1b00d1acd. origin/bannerlord-1.4.5 removed its GitHub Actions workflow in ccef172ad: the 1.4.x
  line has NO CI gate, so every gate a plan relies on must run locally.

## Baseline builds (orchestrator, scratch exports made with git archive, Debug configuration)
Commands: `dotnet restore TAOM.sln`, `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=`,
`dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=`.
- origin/bannerlord-1.4.5 at 1b00d1acd: build OK (the CS1620 risk from rca-pr1064-port did not materialise);
  9153 tests: 9150 passed, 3 skipped, 0 failed.
- 1b00d1acd merged with origin/fix/public-armory-ids-145 (the merge conflicts only in
  docs/reviews/lessons/data-content-cultures.md): build OK; 9154 tests: 9151 passed, 3 skipped, 0 failed.
- Local checkout c5b84fb41: 9124 tests, 9121 passed, 3 skipped (earlier this session).
- Four tests (EnlistmentEquipmentCultureTests, FieldDutyRuntimeTests, TaomCulturalFeatsDefinitionTests,
  ExitStallDisarmTests) find the repo root by walking up to a `.git` directory, so they fail in an export without one.
- Every build prints analyzer warning BHA0001: Patch88_InitializeLordPartyPropertiesScope.cs(22,2), member
  'InitializeLordPartyProperties' does not exist in type TaleWorlds.CampaignSystem.Party.PartyComponents.InitializationArgs.
  Not yet checked against the installed v1.4.8 DLLs.

## Tooling traps
- On 1.4.x, Main/TAOM.csproj:133-136: DisableModuleCopy does NOT gate the CopyModule target. Every build or test command
  in a plan passes BOTH `-p:DisableModuleCopy=true -p:ModuleId=` (as .ai/verification.md does). Never ./build.ps1.
- Git Bash mangles `git show origin/<ref>:.claude/...` (any `ref:.<dir>` argument) through MSYS path conversion and
  reports the file absent. Set MSYS_NO_PATHCONV=1 first.
- 1.4.x .claude/settings.json registers every hook with a relative path (no CLAUDE_PROJECT_DIR), so a persistent cd
  silences the v2.0.28 subject gate and the CHANGELOG gates. 7b342ed59 fixed this on 1.5.x.

## The #612 lesson (decisive for any battle-freeze conclusion)
1.5.x docs/reviews/lessons/state-lifecycle-save.md, entry "A two-sample falling `shaders=` count is not 'draining'
when the 1 Hz stream went silent (2026-10-18)": on the PUBLIC 1.4.8 build a battle load logged two WaitingForRender
samples with a falling count (3128 then 3066), then nothing, and the watchdog fired about 290 s later with the count
frozen. It was a wedge, not a slow compile: every large battle loaded again once the player swapped their elf hero's
Warg mount for a horse (2026-10-19). E3 has exactly this shape (2720 then 2697, then silence), and the E3 player
killed the game at about 92 s, before the 1.4.x watchdog (300 s plus 60 s unchanged) could speak. So "E3 is a slow
shader compile" is NOT established. Related: 0b74cc2dd, 89374fc4f (triage spots a frozen count).

## Porting rules learned in Workflow 1 (from the batch notes)
- Plans 006, 007, 009, 013 and 018 reached 1.5.x through merge commits that carry their own conflict resolutions
  (998ae7e95 009, 1efdf2985 018, d90b4be31 007, 5f6a96d2e 006, 88408869f, f8d40c0ed 013). Port these from their
  post-merge END STATE, not by replaying the individual commits (merges are not in the 1114-commit list).
- c79a58521 (subject "Add scripts for generating and validating animal animation clips") is really a 208-file harness
  and rules rewrite. It also introduced Dependencies/Foundation/RethrowStackPreserver.cs, which 9cfb91b15 and 42624b95b
  need. Several other "Add unit tests ..." subjects (30abf550b, 719074072, 0934f65d2, 6ae924638) hide mixed payloads.
- Anything that changes Dependencies/ (TAOM.Dependencies) reaches players only when the release ships both modules
  from one build (#371 pairing). E3's BuildStamp shows the public build pairs a 2026-09-30 TAOM.dll with a 2026-09-14
  TAOM.Dependencies.dll.
- Main/Composition (plan 018, the feature-module runner) is absent on 1.4.x. A 1.5.x feature registers through its
  *Module.cs; on 1.4.x the same registration goes into Main/IoC.cs and Main/SubModule.cs (single-owner files).
- Plans 001 and 002 are half-landed on 1.4.x (f4273639 CareerSystem reset, bee07b48 TroopWeight NaN guard). The
  SpecialResources half (4263535a1 series) and the mutation-config half (78889a85c series) are missing.
- Recorded maintainer decisions on the 1.5.x ref: plans/_audit/2026-09-23-opus/DECISIONS.md #28 (Mike chose 1.4.5
  ports for plans 006, 007, 009, 012, 014 and 015; not done yet) and plans/README.md D59 (plan 017's 1.4.5 port is
  Mike's call).
- The desktop's live TAOM_Map exported after 2026-10-29 carries 1.5.x culture and kingdom ids that 1.4.x TAOM does not
  define (b30 note 3). A 1.4.x release must pin its TAOM_Map package.
- Do not carry 1.5.x claims that MBGlobals.GetActionSet "returns the default set" to 1.4.x: in v1.4.8 it throws on an
  invalid set (MBGlobals.cs:33-45; b30 note 5).
- 1b00d1acd (1.4.x tip) is PR #1064, the 1.4.x twin of 1.5.x ab78564e0; diff the two before porting the 1.5.x side.

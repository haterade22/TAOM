# For Mike: what is waiting on you (2026-09-24 night)

## 1. Protected-file edits for plans 011, 016 and 017 (your D1 approval)

You approved these edits on the plans' own branches. When I tried to make them, the auto-mode
classifier refused to let me create the config-protection override marker
(`/tmp/claude-config-override-<session id>`) as self-modification. I did not try another way
around it. Two ways forward, your choice:

- **A. You make the edits in an editor** (each is small and exact; the plan section has the JSON):
  - **016:** `E:\repos\taom-improve\wt-016\.claude\settings.json` (branch
    `improve/016-repo-hygiene-pins-readme`, already created at `a39a9c86`): the `permissions.deny`
    block from `plans/016-repo-hygiene-pins-readme.md`, "Step 0" item 2 (lines 431-464), then its
    check prints `9 True False`.
  - **011:** `E:\repos\taom-improve\wt-011\.claude\settings.json` (I create this worktree on 013's
    tip once the 013 follow-up lands): the two PowerShell groups from
    `plans/011-stop-reminders-and-trunk-guard.md`, "Step 0" item 2 (lines 444-484), plus your D42:
    remove the `suggest-compact.sh` registration (PreToolUse) and the `notify-test-results.sh`
    registration (PostToolUse, Bash group).
  - **017:** `E:\repos\taom-improve\wt-017\Directory.Build.props`: the `TaomStampWorkingTreeState`
    target from `plans/017-build-identity-dirty-flag.md`, "Step 5" (about lines 648-700). Its
    executor runs the C# steps tonight and stops at Step 5 as the plan says, so its RED probe is
    ready for you.
- **B. Let me make them:** add a Bash permission rule that allows creating the marker, for
  example `Bash(touch /tmp/claude-config-override-*)` in your `settings.local.json`, and tell me
  "go". While the marker exists, this session's Edit and Write tools may change any protected file
  (settings, `Directory.Build.props`, ADRs) anywhere, so I would create it, make one plan's edits
  in its worktree only, and delete it straight away, per plan.

Either way the branches are then executed, deep-reviewed and Codex-reviewed like the others.

**Two new plans need the same kind of edit (written tonight, committed in `1091f3b6`):**
  - **020** (CHANGELOG generated at `/release`, your D18): its Step 0 removes the two CHANGELOG hook
    registrations from `.claude/settings.json` in its worktree (the plan quotes the exact lines).
  - **021** (the three rule amendments, your D19): its Step 0 is your edit to three ADRs (ADR-002,
    ADR-007, ADR-008), with the exact text in the plan. **One wording choice for you there:** the
    seam conditions you decided ("ids and value types only", "each seam body is a single engine
    call or query") do not fit every existing seam: `RefugeService.SpawnRefugeParty` runs about 15
    lines of engine calls and `IsCampReady` takes a `CampState`. The plan gives the evidence and a
    looser variant; the executor's checks accept either wording.

## 1b. C: drive (report only; nothing deleted)

C: free space fell from about 12 GB to 9.3 GB during the night. This session's own C: footprint is
now 31 MB (task logs) after moving its scratchpad (329 MB) to
`E:\repos\taom-improve\scratch\moved-from-c-scratchpad`. The largest items in
`C:\Users\mikew\AppData\Local\Temp`, none of them this session's: `2yteklx1` 2.4 GB (Visual Studio
installer caches), `claude\e--repos-TAOM\` other sessions' folders (734, 206 and 134 MB),
`3pbnl2u2` 831 MB, `Roslyn` 532 MB, `DiagOutputDir` 418 MB, `SymbolCache` 342 MB, and a 341 MB
`4b9f8d55-0400-479b-abef-42b55c1859e7.tmp` created at 20:12. Your call what to clear; the
`move-claude-and-codex-data-to-E.ps1` script is still ready for when Claude Code and Codex are closed.

## 2. Everything else

Decisions 1 to 48 are in `DECISIONS.md`. New decisions from tonight's reviews are appended there
as `PENDING` rows and listed below as they arrive; the queue does not wait on them.

### Pending decisions

- **P1 (010, found by the D46 measurement):** `Patch71FillTests` and `TeamCombatantSelectorTests`
  carry no `RequiresGame` tag and pass CI today only because they end Inconclusive without the
  game. Pointed at the reference-assembly stubs they throw `NullReferenceException` in
  `TaleWorlds.Core` constructors (10 methods). **Recommended:** tag both classes `RequiresGame`, so
  CI skips them honestly instead of passing them as Inconclusive; it would also let the unit step
  later use `refasm-game` and recover the other 14 skipped tests. A one-commit follow-up on 010.

- **018 (#662), two open items from its review:** (a) review row 12, applied as a defect fix:
  a parked module that owns save data no longer fails closed in service registration (cannot fire
  today; the only module is neither parked nor a save owner); confirm or reverse. (b) Design
  proposal P6: merge patch-failure and module-fault reports into one startup inquiry (needs plan
  009's `ReportPatchFailures` to take the runner's summary); until then both show as two queued
  inquiries. Recommended: confirm (a); defer (b) until a second module exists.
- **013 (#661), two filing questions from its review:** (a) file the pre-existing follow-ups plan
  011 does not cover (commit gates judge the main tree's index for a worktree or `git -C` commit;
  a handful of stale hook comments); (b) add the payload-serializer premise as a row in
  `.claude/rules/harness-facts.md` once that file's pending edits from your other session land.

- **015 (#659), from its second review:** (a) a NaN bite progress: keep today's behaviour (the
  check idles until the action changes; pinned by a test) or write `!(progress < max)` so NaN ends
  the bite. Recommended: end the bite (a stuck check is the worse failure). (b) Inject `LogTask`'s
  logger from `BuildTree` like the other four nodes (a shared `BaseBehaviorTree` constructor
  change). Recommended: yes, in the same pattern.
- **019 (#660), from its second review:** (a) treat a value of only spaces as missing too
  (`string.IsNullOrWhiteSpace`), since it renders blank. Recommended: yes. (b) log one warning at
  load per incomplete kingdom entry, as `csharp-architecture.md` "Config Providers MUST Validate"
  asks. Recommended: yes.
- **014 (#656), from its second review:** (a) `EnlistmentMenuBehavior.cs` is 162 lines, over the
  ADR-002 ceiling: file a split issue. Recommended: yes. (b) Keep `_lossAnnouncedFor` (your D12
  said clear it; a lens proposed deleting it): recommended keep. (c) Keep the Enlistment reset
  inside the shared game-end `try` (your D16 placement): recommended keep. (d) Pre-existing: drop
  the last `CampaignGameStarter` held by three singleton behaviors at game end, and skip the
  Enlistment resolve at game end outside a campaign. Recommended: a follow-up issue, with a heap
  snapshot before any memory claim.

- **025 (scope note, no action unless you disagree):** your D21 "delete the reserved config
  fields" is read as the two with no reader (`EnablePathReuse`, `EnablePersistentPathCache`); the
  other six `CacheRebuildConfig` fields have readers and stay. Say so if you meant all eight.
- **022 (Auto-Assign):** ready to execute; its riskiest assumption is that calling vanilla's
  `OrderOfBattleHeroItemVM.OnHeroSelection` then `OrderOfBattleFormationItemVM.ExecuteAcceptCaptain`
  equals a manual captain drag on the live screen, which only an in-game check proves. Its three new
  strings are seeded in all 12 languages as English; the paid translator run is yours to approve.

- **Tool defect found tonight (recommend a small fix plan):** `tools/translate_with_claude.py`
  `sync_missing_ids` puts newly seeded rows AFTER `</strings>` (inside `<base>`) in language files
  whose last lines mix line endings (rows end LF, the `</strings>` line CR CR LF). The game reads
  only `<string>` rows inside `<strings>`, so those rows, and any translation later written into
  them, are silently ignored. `LanguageFileCoverageTests` counts rows without checking their
  parent, so it stays green. It hit both seedings tonight: 022's executor caught and moved its 36
  rows; **I missed it on my own 009 translation run** (my checks counted rows and placeholders but
  never parsed placement) and fixed it in `7eae4704` (all 60 rows moved inside `<strings>`,
  checked by parsing). Trunk has no stranded rows today (scanned every language file).
  Recommended: fix the anchor in `sync_missing_ids`, and make the coverage test require rows
  inside `<strings>`.

#### From review batch 3 (006, 007, 009, 002, 003, 005, 013, 010; all READY FOR COMMIT)

- **005, security correction (my error):** I recorded BUTR's vendored credential as "already gone
  from disk". That was **false**: `Dependencies\.vendor-source` holds six `.tar.gz` drops, which my
  plain grep could not read; the review streamed them (values never printed) and found the
  credential block in three: `butterlib-v2.10.4`, `mcm-v5.11.4`, `uiextenderex-v2.13.2`. They are
  gitignored and never in TAOM's history; the token is BUTR's own, from their public source. The
  branch's CHANGELOG is corrected and its checklist grep now streams archives; commit `6b34fd00`'s
  body still has my false line (the branch is unpushed, so a squash at merge can drop it).
  **Your call:** rebuild those three tarballs without the `nuget.config` credential block, or delete
  the vendor drops if nothing needs them.
- **006 (#650):** (1) with Enable Crash Capture OFF, D26's stack preservation clears the live frames
  ButterLib's own crash window reads on four Patch37 targets. Options: (a) hand back the raw
  exception on that capture-off path only, (b) accept and document, (c) preserve the text without
  clearing frames. Recommended: (a). (2) Main-thread reference: keep the boot-time id (now logged)
  unless one launch shows it disagrees with `TWParallel.IsMainThread()`. (3) Merge 007 no later than
  006, or PatchShield wraps all 16 callbacks per call until it lands. (4) Follow-ups: the battle-load
  watchdog captures from a pool thread without the off-thread flag; `Mission_OnPreTick` and
  `Mission_TickAgentsAndTeams` reach TAOM code but are not on the allowlist.
- **007 (#651):** its 1.4.5 port is already in your D28 list (check the `ManagedCallbacks`
  namespace on 1.4.8 first); file issues for its four deferred follow-ups, or let them live in #651.
- **009 (#653):** (a) a category that lost a class to the index still reports success, so the
  preview loop logs "applied OK" beside the SKIPPED line; fixing it also changes Patch77's switcher
  disable. Recommended: report it as partial. (b) Risk acceptance: the index build runs unguarded,
  so a pre-2.3 Harmony at runtime would stop the module load (every installed Harmony is 2.4.2).
- **002:** a non-finite mutation parameter falls back silently; the config rule wants a warning but
  `MutationParams` has no logger. Recommended: accept (the plan's choice).
- **013 (#661):** (R8) confirm `suggest-compact.sh` stays on its old filter until 011 deletes it;
  (R14) the escape rule also sends payloads with literal `\u` text (for example a path segment like
  `\uncapturable_heroes`) through Python: cost only. Recommended: accept both.
- **010 (#421):** tag `PatchClasses_AreRegisteredInAllThreePlaces` `BindingVerification`, so the
  registration check runs in the strict gate (after D45 it runs in neither CI step). Recommended: yes.
- **Issues for the June ports and the new plans:** filed #663 (002) and #664 (003); 001, 022 and 025
  follow after review batch 4. **005's issue is drafted but HELD for your OK**
  (`E:\repos\taom-improve\issues\005.md`): it is a security plan and says publicly that BUTR's
  credential block sits in three local vendor tarballs (no value printed), and the `/improve` skill
  asks for your confirmation before a security-sensitive issue goes public. Say "file 005" and I
  will, or edit the draft first.

### Also ready for you

- **017** stopped at Step 5 by design (item 1 above); its commit A (`743ce2e1`, the build stamp
  in crash bundles) is done and green.
- **024** (`improve/024-managed-debug-profile`, `37ff41e4`) is config only: merging it makes your
  next Play the test of the slow-patching theory (`diag.log` shield pass should drop from about
  69 s to a few seconds).

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

### Also ready for you

- **017** stopped at Step 5 by design (item 1 above); its commit A (`743ce2e1`, the build stamp
  in crash bundles) is done and green.
- **024** (`improve/024-managed-debug-profile`, `37ff41e4`) is config only: merging it makes your
  next Play the test of the slow-patching theory (`diag.log` shield pass should drop from about
  69 s to a few seconds).

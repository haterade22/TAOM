# Plan review 050, round 1 (cold)

Plan: `plans/050-messenger-arrival-classification.md`. Code read from the baseline worktree at
`7087fa633` (HEAD confirmed, tree clean). No earlier review file for this plan exists in the audit
folder, so there are no earlier blocking items to re-check.

## What I checked, and how

- Every "Current state" excerpt against the file at the base: `MessengerCampaignBehavior.cs`
  (lines 20 to 28, 214 to 234, 265 to 327, 348 to 358, 612 to 680), `MessengerService.cs:26-74`,
  `IMessengerService.cs`, `MessengerTargetReader.cs:15-38`, `Domain/HeroSnapshot.cs`, the source
  pins in `MessengerCampaignBehaviorSourceTests.cs`, `CoopAuthorityGateTests.cs:190-206`,
  `MessengerCampaignBehaviorTests.cs:25-134`, `MessengerConversationStateTests.cs:20-46`,
  `MessengerEntryPolicyTests.cs:26-36`, `EntryPointLineCountGateTests.cs` (whole file) and
  `EntryPointLineCountBaseline.txt` (header bytes, CRLF, row 15).
- Engine facts against the v1.5.7 decompile that `pwsh tools/taom-src.ps1 path Hero` names:
  `Hero.cs:28-38, 233-243, 303, 305, 307, 311, 315, 319, 330, 490, 670, 958`,
  `MobileParty.cs:1102`, `PartyBase.cs:288`, `Army.cs:267-274`. All match the plan's quotes.
- The behavior equivalence, by simulation (`scratch/review-050-r1/sim.py`): the plan's oracle
  `OldArrivalRule` and the Step 4 `ClassifyArrival`, over all 256 cells: 0 mismatches. Every data
  row and named test holds for both. Mutation M1 breaks 32 cells, both Prisoner rows and all three
  prisoner asserts; M2 breaks 21 cells and `ConfusedBeforeWait`. So Step 6's expected failures are
  right.
- Line arithmetic for Step 7: 5a replaces 14 lines with 13, 5c replaces 4 with 2, 5d deletes 6, so
  the class goes from 668 to 659 (the gate counts every line from `class` to the closing brace,
  comments included, `EntryPointLineCountGateTests.cs:304`). The regenerator writes the same five
  header lines, ordinal order and CRLF (`File.WriteAllLines`), so a one-row diff is the right
  expectation.
- Single implementer: `git grep -n "class .*: *IMessengerService"` gives only `MessengerService.cs:7`.
  `new HeroSnapshot(` sites: exactly the reader and the 13 listed test lines (plus the unrelated
  CrashReport record at `CampaignStateCollector.cs:82`).
- Hooks: `check_commit_msg.py` accepts the planned subject (64 characters, `v2.0.38` matches
  `SubModule.xml:6`); pre-commit runs only `lint_docs --drift-only --staged` for this change.
- `python -B tools/lint_docs.py --fail-on-drift` at the base: exit 0 (warnings only), so Step 8's
  gate starts green. `graphify_taom.py status`: MISSING, exit 2, as the plan says.
- Nullable: `Directory.Build.props:6` enables it and `.editorconfig:39-46` sets the CS86xx ids to
  none for `Main`, so the `ReadSnapshot(target)!` and the new code add no error.
- Dashes: four em dashes in the plan, all inside quoted code (lines 95, 112, 121, 144). No prose
  dash. No secret values.
- NOT checked (read-only role, no dotnet): the base totals `39250 / 9 / 39259`, the 11-warning count,
  and whether any base warning names a Messengers file (Step 5's grep assumes none). UNVERIFIED.

## Blocking

1. **Step 2's RED expectation names a diagnostic the compiler probably suppresses (plan lines
   713 to 716).** The Verify requires the errors to name "the unknown named argument `heroState`".
   In `Snap`, other arguments of the same `new HeroSnapshot(...)` call already carry errors
   (`MessengerHeroState.Dead` cannot bind), and Roslyn does not report an overload-resolution
   failure (CS1739, no parameter named `heroState`) when an argument already has errors. So the
   build will most likely show CS0246 for `MessengerHeroState` and `MessengerArrivalVerdict` and
   CS1061 for `ClassifyArrival`, but no `heroState` error. A literal executor then sees a failed
   verification, "fixes" something, fails again and STOPs. PLAUSIBLE (I could not compile; the
   suppression is Roslyn's rule for arguments with errors). Fix: require CS0246 for the two enums and
   CS1061 for `ClassifyArrival`, all in `MessengerArrivalClassificationTests.cs`; say the `heroState`
   error may be absent as a cascade; keep "any error in another file is a STOP".

## Non-blocking

1. **Done criterion at plan line 997 to 998 will show two lines in the behavior, not one.**
   `git grep -n "ClassifyArrival" -- Main` also hits the Step 5a comment
   `// The verdict is the service's (MessengerService.ClassifyArrival); ...` (plan line 835). State
   the exact expected output: one line in `IMessengerService.cs`, one in `MessengerService.cs`, and
   two in `MessengerCampaignBehavior.cs` (the comment and `_service.ClassifyArrival(`), or grep for
   `_service.ClassifyArrival(` with an exact count of 1.
2. **Step 7 (plan lines 913 to 916) could name the exact number.** The edits make the row
   `TAOM.Features.Messengers.MessengerCampaignBehavior 659`. A range of 150 to 667 accepts a wrong
   edit (a lost or duplicated line); "659, or report the difference" is a sharper gate.
3. **Step 8, line 30 insertion point is ambiguous (plan lines 925 to 927).** The `GetOffer` item is
   followed by its parenthetical `(quote, verdict against that quote, days),`. Say "insert after
   `(quote, verdict against that quote, days),`" so the new item does not split them.
4. **Step 8 table order (plan lines 931 to 934).** Rows 121 to 123 of `docs/features/messengers.md`
   are alphabetical (`HeroSnapshot`, `MessengerValidationResult`, `PendingMessenger`). Put the two
   new rows before `MessengerValidationResult` (after `HeroSnapshot`), not after it.
5. **Two supplied sentences overstate the send-check gap.** Plan line 40 ("a wanderer in any state
   but `NotSpawned` or `Active` is lost although the send check let it through") and the test class
   summary at plan lines 525 to 526 ("a wanderer that is neither active nor unspawned is lost
   although the send check accepts it"). `CanSendMessenger` refuses a dead, prisoner or fugitive
   wanderer before the reach test (`MessengerService.cs:34-44`). Only Released, Traveling and
   Disabled pass the send check and are lost on arrival, which lines 216 and 325 already state.
   Use that wording in the test summary, since the executor ships it as a comment.
6. **Drift check (plan line 10) omits a file the plan quotes.** Step 7 cites
   `TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs:77-78` and `:100-125`; add it to the
   drift paths (it is read, not edited).
7. **Evaluation-order note (plan lines 334 to 340) misses one read.** Calling `ReadSnapshot` up
   front now runs, for every arrived messenger, `Army.DoesLeaderPartyAndAttachedPartiesContain`,
   which dereferences `LeaderParty.AttachedParties` (`Army.cs:267-274`), and `IsChild`, which reads
   `Campaign.Current.Models.AgeModel` (`Hero.cs:490`). Old arrival code read neither for a lost
   target and never read the army. Both are side-effect free and the dialog path already runs them,
   so this is not a STOP, but name it in "Review probes" so `/deep-review` looks at a possible new
   throw in the hourly tick.
8. **Small citation looseness.** Plan line 378 "the authority gate at `:201`" means
   `MessengerCampaignBehavior.cs:201`, inside a bullet about `CoopAuthorityGateTests.cs`; name the
   file. Plan line 383 `:34` and `:45` are the method declarations; the `ReadSource` calls are at
   `:36` and `:47`. Plan line 59 cites `:214-234` but the excerpt stops at `:225`. None changes what
   the executor does.
9. **Step 5 warning grep (plan line 875)** needs the build output captured first (for example
   `> "<scratch>/build.log" 2>&1`, then grep the log). Say so, and say that a Messengers warning
   already present in Step 1's log is not a failure.

## Template checklist

| Item | Result |
|---|---|
| TDD order for C# | Holds: Step 2 RED by compile, Step 3 RED by assertion, Step 4 GREEN, Step 6 mutations |
| Issue line | "filed by the orchestrator before execution", repeated in Orchestrator steps |
| Binding ADRs named | ADR-002, ADR-007, ADR-008, one line each, with their gates |
| Protected files | None changed; listed out of scope; no Step 0 needed |
| Single-owner files | `IoC.cs`, `SubModule.cs`, `TAOM.csproj` out of scope with a STOP; the empty-diff done criterion checks it |
| STOP conditions | Specific to this plan (oracle edits, prisoner wait, wanderer alignment, getters that write, baseline whole-file diff) |
| Done criteria | Machine-checkable, except non-blocking item 1 |
| Planned-at and drift paths | `7087fa633` matches the worktree HEAD; drift paths cover Scope (see item 6) |
| Non-deploying commands | Every dotnet command carries `-p:DisableModuleCopy=true -p:ModuleId=`; no `./build.ps1` |
| Worktree path or branch name | None; "a branch made for this plan, to be merged later" only |
| CHANGELOG step | None; `CHANGELOG.md` is out of scope |
| Dashes, secrets | None in prose; none |

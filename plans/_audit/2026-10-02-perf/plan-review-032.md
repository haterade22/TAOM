# Plan review 032, round 1 (cold)

Plan: `plans/032-worker-thread-formation-patch.md` (1229 lines). Code read from the worktree at
`e9cd8b39`; `git diff --stat dffdf879..HEAD` over every drift-check path printed nothing (the two
commits past `dffdf879` touch docs and plans only), so the code equals the planned-at commit.
No earlier `plan-review-032*.md` exists; nothing to re-check.

## Verdict

Two blocking items. The plan is otherwise unusually complete: every C# excerpt matches the code,
every engine fact I checked matches the v1.5.3 decompile, the RED/GREEN order is sound, and the
stale-claim sweep prints exactly the six lines the plan names.

## Blocking

**B1. Step 10 Verify (line 1063) and Done criterion (line 1163) contradict the plan's own Step 6
text.** Step 6 (lines 858-859) prescribes the `IFormationAdapter.cs` doc comment
`(<c>FormationQuerySystem.IsCavalryFormationReadOnly</c>)`. After Step 6,
`git grep -n "IsCavalryFormationReadOnly" -- Main` prints two lines (`Main/Adapters/IFormationAdapter.cs`
and `Main/Adapters/FormationAdapter.cs`), plus a third if the executor's Step 7.4 comment names the
engine member. The plan says "prints exactly one line". A weak executor either STOPs ("verification
fails twice") or deletes the doc comment the plan told it to write. Fix: grep the code form only,
for example `git grep -n "QuerySystem.IsCavalryFormationReadOnly;" -- Main` gives exactly
`Main/Adapters/FormationAdapter.cs`, or state the expected two files.

**B2. The snapshot misses an existing writer path: `SetLayout(f, Vanilla)` followed by
`CycleLayouts`.** Line 216-217 claims "`NextLayout` never returns `Vanilla`, so a cycle never changes
WHICH formations have a layout", and Step 7.5 (line 926-927) has the executor write that as a code
comment with no code change. False: `SetLayout(f, Vanilla)` stores `Vanilla` in `_layoutByFormation`
(`FormationLayoutService.cs:59`); `CycleLayouts` then finds it (`:114`) and `NextLayout(Vanilla)`
falls to `_ => InfantryFrontRangedBack` (`:242`), so the formation gains a layout. On the base,
`ComputeUnitPlanePosition` then returns a position; after this plan, `SetLayout` removed the key from
`_laidOut` (Step 7.3) and `CycleLayouts` never re-adds it, so it returns `null`. No production caller
reaches it today (`SetLayout` has none; `ApplyDefaultsToFormations` never writes `Vanilla`, `:134`),
but it is a behaviour change on the public interface, it contradicts "Positions ... stay the same"
(line 47) and the commit body, and it violates the plan's own rule that a snapshot missing a writer
is a correctness bug (line 1182). Fix (keeps parity): in `CycleLayouts`, when `current ==
FormationLayoutType.Vanilla`, add the formation to a copy of `_laidOut` published once per pass
(the Step 7.6 pattern), and add a test `FindLaidOutFormation_SetVanillaThenCycle_ReturnsTheFormation`
(plus a position not null). Correct line 216-217 and the Step 7.5 comment.

## Non-blocking

1. Line 75-77: the line-ending claim is wrong. `file` reports CRLF for all 13 in-scope existing
   files in the worktree, including `IFormationAdapter.cs`, `FormationAdapter.cs`, `Patch30_*.cs`,
   `Patch93_CreatureBandits.cs`, `IFormationLayoutService.cs`, `FormationLayoutServiceTests.cs`,
   `mixed-formations.md`, `harmony-il.md`, `csharp-architecture.md`. The instruction ("keep each
   file's existing endings", Edit tool, no `sed -i`) still works; fix the list.
2. `docs/features/mixed-formations.md:150` ("Per-position-query work: lock acquire + dictionary
   lookup ...") and `:152` ("Reads on the hot path lock briefly (~25ns uncontended)") become stale
   after this plan, which drops the same "~25ns" sentence from the code comment (Step 7.2). Scope
   lists only lines 30, 32, 34. Add both lines to Scope and Step 10. The Tests section (`:132-135`)
   also omits the new `Patch30ThreadSafetyIlTests.cs`; optional line.
3. Line 427-428: `.claude/rules/harmony-patches.md` names `GetOrderPositionOfUnit` at line 139, not
   140. Lines 148-149 for the edit are correct.
4. Step 8.3 (line 983-984): "the compiler warns" about an unused `using` is not true here (CS8019 is
   hidden; no `.editorconfig` or `EnforceCodeStyleInBuild` raises it). After the edit nothing in the
   file names a `TAOM.Adapters` type (`var formation = service.FindLaidOutFormation(...)`), so say
   plainly: remove `using TAOM.Adapters;`.
5. Step 5 tests 11 and 12 (lines 802-806) give no setup: test 12 uses `f` without defining it and
   test 11 has no body. Spell them out (`var f = MakeFormation(8, 6);`, apply or set, `OnMissionEnd()`,
   assert null).
6. Done criterion line 1167 / Step 11 line 1079: `git status --porcelain` "lists only in-scope files"
   fails if the executor's worktree carries the run's own untracked plan files (this worktree does).
   Compare against a Step 1 `git status --porcelain` snapshot instead.
7. Line 562: the inline code span `'TaleWorlds.MountAndBlade.QueryData`1'` contains a backtick, so
   rendered Markdown breaks the span. The raw command works (I ran it); consider a fenced block.
8. Blast radius (lines 445-468) was not re-run by me (the tool writes graph files; read-only role):
   UNVERIFIED. The grep-based parts match (`new FormationAdapter` sites, `IFormationLayoutService`
   users, sole implementer `FormationAdapter.cs:11`). The test count is 37 attributes, not 36
   (immaterial).
9. Baseline totals (line 27) UNVERIFIED by me (no dotnet run in a read-only role); the same line
   appears in 15 run files.

## Excerpt check (item 3)

All match at `dffdf879`: `Patch30_FormationGetOrderPositionOfUnit.cs:9-80` (elisions marked),
`FormationLayoutService.cs:19-31, 54-63, 65-102, 104-128, 130-158, 180-191, 193-220, 236-243`,
`FormationAdapter.cs:38-41`, `IFormationAdapter.cs:37-45`, `CreatureBanditAgents.cs:8-23`,
`CreatureBanditRules.cs:20, 54-55`, `Patch93_CreatureBandits.cs:95-146` (the replaced phrase spans
101-102), `SmartCavalryAIMissionBehavior.cs:27-30, 84, 90`, `MixedFormationsMissionBehavior.cs:88, 96`,
`SpatialGridRemovalTests.cs:23`, `TAOM.csproj:112-117`, `SubModule.cs:593`,
`CreatureBanditDiag.cs:252-277` (also calls `RecordOf`, a `ConcurrentDictionary` lookup; no setup
needed, as claimed). Doc lines: registry 214-218 and 222, mixed-formations 30/32/34, harmony-il 80
and 125, csharp-architecture 270-271: phrases present as quoted.

Engine (v1.5.3 cache from `taom-src`): `FormationQuerySystem.cs:197, 199, 444, 446, 744`;
`QueryData`1.cs:18-37, 73-76`; `Formation.cs:253, 1374, 1983, 1998, 2595-2598` (no `Equals`
override); `Agent.cs:520, 522, 538, 642, 744, 1483, 2086, 2091, 4718, 4757-4760, 5312-5314, 5575-5577`
(`1626` holds the timer creation); `AgentHelper.cs:33-37`; `AgentFlag` values
(`Mountable=1`, `CanAttack=8`, `CanDefend=0x10`, `IsHumanoid=0x800`, `CanWieldWeapon=0x4000`);
`MBObjectBase.StringId { get; set; }`; `HumanAIComponent.cs:608, 666`; `OrderController.cs:1452`.
RefAsm package is `1.5.3.122374-beta`, the same engine, so the new member compiles on CI.

## Checklist (item 4, 5)

- TDD order: Step 2 RED (IL order) before Step 3; Steps 4-5 RED before 6-8. Each RED names its
  filter and the expected failing assertion. OK.
- Issue line, ADR-002/007/008 named, no protected file (Step 0 "None" is right; both rules are
  path-scoped and not protected), single-owner files out of scope with a STOP. OK.
- STOP conditions are specific to this plan's risks. OK.
- Planned-at `dffdf879` and the drift paths cover every Scope entry. OK.
- Non-deploying commands with `-p:ModuleId=`; no worktree path, no branch name, no CHANGELOG step.
  Both commit subjects are 69 characters. OK.
- No em or en dash outside verbatim code excerpts (lines 86, 270, 276 quote existing source). No
  secrets, no local absolute paths.

# Plan review 030, round 1 (cold)

Plan: `plans/030-mission-diagnostics-diet.md` (1719 lines), read as a zero-context executor. Code read at
`dffdf879` with `git show dffdf879:<path>` from the program worktree. The worktree HEAD is `e9cd8b39`
(two `docs(engine)` commits past `dffdf879`); the plan's own drift-check command prints nothing there,
and `git diff --stat dffdf879..HEAD -- Main TAOM.Tests docs/features docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot`
also prints nothing, so every in-scope file equals the planned-at commit. No earlier
`plan-review-030*.md` exists in this folder, so there is no earlier blocking item to re-check.

Verdict: one blocking item (the branch base). The six designs hold up against the code: every
call-site list, line number, doc line and test oracle I checked is right except the small
mismatches listed under "Excerpt mismatches". Not run (read-only role): dotnet, the hook suite,
graphify. The baseline totals and the blast-radius output are UNVERIFIED by me; the baseline matches
`plans/_audit/2026-10-02-perf/baseline.md` lines 7-8.

## Blocking

**B1. Done criteria and one STOP condition assume the executor's branch starts at `dffdf879`, and it
very likely will not (plan lines 6-7, 1595, 1650, 1660).** `DECISIONS.md` D3 puts the work on
`perf/NNN-*` branches beside the program branch, and the program branch already carries
`761b20fe` and `e9cd8b39` past `dffdf879` (they touch `docs/INDEX.md`,
`docs/reference/bannerlord-engine-and-toolchain.md`,
`docs/reference/engine/mission-frame-threads-and-native-costs.md`,
`docs/reference/provenance-register.md` and `plans/_audit/**`; checked with
`git diff --stat dffdf879..e9cd8b39`), plus probably a commit adding the plan files themselves. A
branch cut from that tip makes:
- line 1650 `git diff dffdf879 --stat lists only in-scope files` fail (it lists those docs and audit
  files);
- line 1595 `git log --oneline dffdf879..HEAD shows the six commits` fail (eight or more);
- lines 6-7 and 1660 ("carries another plan's commits past `dffdf879`, STOP") ambiguous: a weak
  executor cannot tell whether `docs(engine): ... hitch analysis` or a plans commit is "another plan's
  commits", so it either STOPs at the start or ignores a real collision.

Fix: in Step 1, record `START=$(git rev-parse HEAD)` and require `git diff --stat dffdf879..$START -- <the drift-check paths>`
to print nothing (that is the real precondition); replace the STOP with "a commit between `dffdf879`
and your start touches any drift-check path"; and write lines 1555, 1595 and 1650 against `$START`
(`git diff $START --stat`, `git log --oneline $START..HEAD`). `git diff dffdf879 -- Main/SubModule.cs`
at line 1555 stays correct either way, since no later commit touches it, but using `$START` there too
keeps one rule.

## Non-blocking

**N1. Stale doc claims the plan leaves behind (quality bar "stale-claim grep is a floor").**
- `docs/features/mission-diagnostic.md:70`: "No service-level unit tests yet." becomes false when
  Step 2 adds `MissionDiagnosticServiceTests`. Step 4's doc list (lines 842-856) does not touch it.
  `:86` "the dedup `HashSet` resets per-mission" is now two sets (still true, but Step 4 already
  rewords :58 for that).
- `docs/features/banner-color-persistence.md:140`: "6 files, 33 `[TestMethod]` as of 2026-09-02."
  Step 13.7 (lines 1531-1534) updates the :117 row to 5 files / 29 but not this sentence. I counted
  the base: AgentColorStoreTests 4, BannerColorConfigProviderTests 4, BannerColorServiceTests 15,
  BannerTripletOrderingTests 5, Clan_UpdateBannerColor_PatchTests 3,
  Clan_UpdateBannerColorsAccordingToKingdom_PatchTests 2 = 33, so 29 after the deletion is right.
- `docs/features/troll-brute-force.md:112`: the behavior row says it "ticks the spacing tracker and
  the clip trace" unconditionally; add "once the mission has built a troll".
- `docs/features/creature-bandits.md` Tests section (from :218, the ledger tests named at :239) does
  not mention the new `CreatureBanditDiagTests` or `AnyRegistered`.

**N2. Step 4 code block carries a placeholder comment line (plan line 833):**
`// (keep the existing four-line comment on character and monster ids here, unchanged)`. A literal
executor pastes it. Inline the four real lines (base `MissionDiagnosticBehavior.cs:84-87`) or say
"replace this line with base lines 84-87".

**N3. `ResetDiagnostics_ClearsTheHitDedupe_SoTheNextBattleLogsAgain` never fails (plan lines 935-945,
963).** It passes at the base, and rule 8 says a test is not done until it has failed against the code
it guards. Cheap fix: in Step 6 add the `_loggedHits` dedupe first, run that one test and quote its
"Expected 2, received 1" failure, then add `_loggedHits.Clear();` in step 6.2.

**N4. Step 11's "reads the budget" STOP rule (line 1411-1413) needs one named exception besides
snap.** `CreatureBanditDiagTicker.cs:520` prints `"lines", I(_formationLines) + "/" + I(FormationLineCap)`;
`_formationLines` is the ticker's own formation counter (incremented at :515, before the call), not
the ledger budget, so it is safe to move inside the `if`. A cautious executor may read "lines" as a
budget read and STOP. One sentence fixes it.

**N5. Issue number for commit bodies (lines 33, 1679-1680).** `DECISIONS.md` D4 drafts issues now and
files them later, so the executor may get no number. Say "if no issue number was given, omit it".

**N6. `ICareerAgentStatService.cs:73-74` replacement (lines 1053-1056) is given in backticks; the file
uses XML doc `<c>...</c>`.** Give the exact `/// <summary>` lines.

**N7. Rules to read (line 75-77).** `.claude/rules/provenance.md` also matches `Main/**/*.cs` and
`docs/features/**/*.md`; "at least" covers it, but naming it costs nothing.

## Checks that passed (evidence)

- Engine facts (plan 79-109) match the v1.5.3 cache under the taom-src cache root:
  `MBActionSet.cs` `internal readonly int Index` (:10), `GetHashCode` returns `Index` (:31-33),
  `GetName` (:36-42); `Agent.cs` `ActionSet` :722, `Name` :782, `Character` :1426, `SetActionSet` :2608;
  `BasicCharacterObject.cs` `Race` :76, `IsFemale` :78.
- Item A: `MissionDiagnosticBehavior.cs` :19, :41-45, :76-92 and the loop text match; the service
  :15 and :169-188 match; the characterization string matches :180-182 exactly. Only implementer of
  `IMissionDiagnosticService` is the service (no fake to update).
- Item B: `CareerAgentStatService.cs` :26-34, :72-79, :175-211, :213-253 and the term lines :183,
  :188, :203, :221, :227, :236, :245, :249-250 match; `Pct`/`Num` are static (:93, :95);
  `AttackTypeMask` has `Melee, Ranged, Cut, Pierce, Blunt`; `CareerAbilityBuffTracker.SetAllyBuff(int, ActiveBuffs)`
  at :36; test file `Setup` :26-34, `RequiresGame` tag, tests at :296-324. I traced each of the six
  new tests against the base and the GREEN code: the RED counts at lines 958-962 (2/1, 50/1, 2/1,
  2/1, 4/3) and the GREEN results are right. `CareerPerkMissionBehavior.cs:209`,
  `TaomAgentApplyDamageModel.cs:30/:40`, `career-system.md:461`, `dev-console.md:307` match.
- Item C: behavior lines 33-41, 73-96, 98-116 match (file is 117 lines; the edits add about 9);
  `TrollBruteForceService.cs:13-14`, `:41`; `TrollFormationSpacingStore.cs:68-70`; spacing tracker
  and clip trace scan `mission.Agents` with the same `IsBruteForceTroll` predicate, so the latch is
  exact; `TrollBruteForceServiceTests.cs:17-37` pins the keys.
- Item D: the 22 `Write(` callers are exactly the listed lines (git grep at `dffdf879`); every one is
  a standalone statement (no braceless `if`/`else` that a two-line rewrite would capture); only the
  snap line reads the ledger budget. Ledger lines :94-160 match; `Register` is the only serial source
  and `Reserve` (:65) is its only caller, so the `AnyRegistered` early return is exact. RED
  diagnostics (CS1061 on the instance, CS0117 on the static type) are the right codes. The Step 11
  count gate (17 and 5) cannot be inflated by `TryTakeLine` or the doc `cref`s.
- Item E: `git grep Patch35_Mission_OnTick dffdf879 -- Main TAOM.Tests docs/features docs/reference`
  gives exactly the class plus companion-tactics.md :149/:195, registry :1057, patch-targets :94; six
  other classes carry the category; registry :271/:273 phrases match.
- Item F: every excerpt matches (`Mission_SpawnAgent_Patch.cs` :15, :17-22, :54-66, :68-78;
  EquipItems prefix :23-36; IoC :13; `SubModule.cs` :635-637, :2113-2121; `FeatureModulesTests.cs`
  :169-170 and `AssertOnceBetween` uses first-occurrence `IndexOf`, and
  `BattleActionBarMissionView()` occurs once in `SubModule.cs`). Patch-targets has 268 `| \`TAOM` rows.
  The done-criterion grep (line 1633) at `dffdf879` lists only in-scope files plus the kept
  2026-04-05 history line. `CoopVetoClassificationTests` scans `static bool Prefix(` only; the
  deleted EquipItems prefix is `void`, and `Mission_SpawnAgent_Patch` keeps its bool prefix and its
  registry key. Registry :118 (Patch16 `Mission.Initialize`), :172, :325 match.
- Both csproj files are SDK style with no explicit `Compile Include`, so adds and deletes need no
  csproj edit. No hook guards `SubModule.cs`; the commit-subject hook accepts `perf`; suggested
  subjects are 63 to 68 characters.
- Template items: TDD order for each C# item; ADR-002/007/008 and the rules named; no protected file;
  `SubModule.cs` edits exact and limited to Step 13.5; every dotnet command carries both `-p:` flags;
  no worktree path or branch name; no CHANGELOG step; prose has no em or en dash (the one U+2014 is
  inside the quoted base comment at plan line 175); no secrets or local absolute paths.
- Test arithmetic (line 1569-1572): 12348 - 4 + 19 = 12363, passed 12345 - 4 + 19 = 12360. Correct.

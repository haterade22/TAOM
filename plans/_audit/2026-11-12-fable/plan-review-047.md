# Plan review 047, round 1 (cold)

Plan: `plans/047-fold-safe-twins.md` (1062 lines). Code read from the baseline worktree at `7087fa633`
(HEAD confirmed `7087fa633`, tree clean). No earlier review files exist for this plan.

Verdict: **not executable as written by a weak executor**. Four blocking items, all in verification
gates whose expected results are wrong for a correct change. The design, the excerpts and the engine
facts hold up.

## Blocking

### B1. Three `git grep` gates expect untracked files in their output (plan 676-677, 793-794, 937-939)

`git grep` searches tracked files only. At each of these checks the new file has not been staged yet
(staging happens at the commit sub-step afterwards), so the output does not match:

- Step 4 (676-677): `git grep -n -E "IssueFrequencyTier\.(VeryCommon|Rare)|\?\? -10" -- Main Client`
  is expected to list `LotrIssueTemplateRules.cs`. After a correct Step 4 it prints nothing (the four
  old sites are gone, the helper is untracked).
- Step 7 (793-794): `git grep ... "PossibleTypeNames|FindCultureStageViewType" -- Main Client TAOM.Tests`
  is expected to list `CultureStageViewType.cs` and `CultureStageViewTypeTests.cs`. Both untracked:
  prints nothing.
- Step 10 (937-939): the two `{=taom_sl_*_row}` greps are expected to list `SupplyRowVM.cs` plus the
  strings row. `SupplyRowVM.cs` is untracked: only `taom_module_strings.xml:2812` / `:2830` print.

Proof: a scratch repository with one staged file and one untracked file holding the same token;
`git grep` printed only the staged file, `git grep --untracked` printed both.

Fix: add `--untracked` to those three greps (or `git add` the new files first), or rewrite each
expectation. The Done-criteria copies (990-992) run after the commits and are fine.

### B2. Step 4's `git diff` check fails on a correct change (plan 678-679)

"`git diff 7087fa633 -- Main/Features/LotrIssues` shows no line containing `SaveableField`, `class `".
The repo sets no diff driver for `.cs` (`git check-attr diff` gives `unspecified`), so the default
hunk-header heuristic picks the last column-0 line, which is the class declaration. A simulated
relation-line edit on `CombatLotrIssue.cs` gives `@@ -129,7 +129,7 @@ public class CombatLotrIssue : IssueBase`.
The behavior file's hunks carry `public ... class LotrIssuesCampaignBehavior` the same way. A weak
executor reads this as a failed gate and STOPs, or ignores the gate.

Fix: check only added and removed lines, for example
`git diff 7087fa633 -- Main/Features/LotrIssues | grep -E '^[-+]' | grep -v -E '^(\+\+\+|---) ' | grep -E 'SaveableField|class |LotrIssueSaveableTypeDefiner'`
returns nothing (exit 1).

### B3. "six Part A paths" is seven (plan 693, 703-704)

Scope (454-459) lists seven Part A paths: `LotrIssueTemplateRules.cs`, `LotrIssueTemplateRulesTests.cs`,
the three templates, `LotrIssuesCampaignBehavior.cs`, `docs/features/lotr-issues.md`. Step 5 says
"stage the six Part A paths" and its Verify says `git log -1 --stat` "lists only the six Part A paths".
An executor that trusts the count drops one (most likely the doc), and Done criterion 998
(`git status --porcelain` empty) then fails; one that stages all seven sees a gate mismatch.

Fix: say seven and list them in the step.

### B4. Two gates ask to see named test classes in output that does not print them (plan 683-684, 933-935)

- Step 5.1: "The new `LotrIssueTemplateRulesTests` results must appear in the unit step and pass."
- Step 10: "the output shows `SupplyRowVMTests` (4), `SupplyOrderPrefabBindingTests` and
  `SupplyOrderScreenVMTests` results among the passes."

`dotnet test` at default console verbosity prints failures and the totals line only; neither
`TAOM.Tests/TAOM.Tests.csproj` nor `Directory.Build.props` sets a logger or verbosity, and
`binding-gate.runsettings` sets none either. The Step 5.1 check is the only proof that the untagged
Part A tests really run off the game, so it matters.

Fix: add `--logger "console;verbosity=normal"` to those runs, or give each a class filter with an exact
count (for example the RefAsm unit step plus `&FullyQualifiedName~LotrIssueTemplateRulesTests` reports
`Passed: 11`).

## Non-blocking

- **N1. A stale count the plan leaves behind.** `docs/features/lotr-issues.md:401` says
  "`TAOM.Tests/Features/LotrIssues/*` | config-provider + service + suppression tests (84 `[TestMethod]`
  methods, plus two data-driven ones)". Today `git grep -c` gives 26 + 38 + 20 = 84 `[TestMethod]` and
  2 `[DataTestMethod]`. After Part A that is 87 and 5, and the row still omits the invariants tests and
  now the rules tests. Step 5.2 edits the same table; it should update or drop that count (the
  "stale-claim" quality bar).
- **N2. Excerpt wording (see mismatches).** "Constructor shown in full" (162) stops at file line 40;
  lines 41-48 (comment block, `[HarmonyPostfix] static void Postfix(object __instance) => _hook?.OnCreated(__instance);`)
  are omitted. "The other two differ only after `:32`" (207) is not exact: lines 10, 12 and 14 (class
  name, `_hook` type, `Initialize` parameter) differ too; 211 mentions the hook types, so no harm.
- **N3. Drift check coverage (line 9).** It omits files the plan depends on without editing:
  `Main/_Module/ModuleData/taom_module_strings.xml` (Step 10 byte check), `Main/Features/SupplyLines/ISupplySourceService.cs`,
  `TAOM.Tests/Features/SupplyLines/SupplyOrderScreenVMTests.cs`, `TAOM.Tests/Infrastructure/ServerLoadGateTests.cs`,
  `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`, `TAOM.Tests/Composition/ClientFrontEndCompositionTests.cs`.
  The Scope paths are all covered.
- **N4. Trailers longer than 72 characters** (699-700, 800, 948) against "wrapped at 72" (505-506).
  No hook enforces body width (`tools/check_commit_msg.py` checks the subject only), so this is style.
- **N5. Part B's new untagged test first meets RefAsm at Step 11**, with no fallback rule like Step 5.1.
  It reads only a static string array, so the risk is low; one sentence pointing at Step 5.1's rule
  would close it.
- **N6. Name the existing byte-identity gate.** `TAOM.Tests/Infrastructure/Localization/LocalizationKeyConsistencyTests.cs:258`
  (`EveryCSharpDefault_OfARegisteredTaomKey_MatchesTheRegisteredEnglish`, regex at `:172-173`) matches
  any whole `"{=taom_*}..."` literal, the factory arguments included, and fails on a drifted default.
  Step 10 and the STOP list could cite it next to the manual comparison at 919-920.
- **N7.** 411-412 paraphrases `csharp-architecture.md` "File Layout" as "patch helpers live in the
  feature's `Hooks/` folder"; the layout shows only Harmony patches there. Harmless.
- **N8.** `docs/features/lotr-issues.html` is a one-off export with no generator
  (`docs/reviews/rca-lotr-issues-deliver-personnel-2026-07-30.md:22`); the plan rightly leaves it, and a
  line saying so would stop an executor from "syncing" it.
- **N9. UNVERIFIED:** the baseline totals (21-26). This review ran no dotnet command.

## Excerpt check (plan "Current state" against `7087fa633`)

Matched: `GetFrequency` and `CanPlayerTakeQuestConditions` token-identical across the three templates
at the cited ranges (diffed); `EnsureDef` lines; `_defId` `[SaveableField(1)]` at 30/30/28; behavior
`:111-112`, `:143-152`; `IssueFrequencyTier.cs:8-13`; `LotrIssueDefinition.cs:49`, `:86` and the
16-argument constructor (the test builder's argument order is correct); `LotrIssueServiceTests.cs:37-40`;
`LotrIssueSaveableTypeDefiner.cs:30-36`; `LotrIssueService.cs:39`; patch files lines 16-30, `:32`,
`:34-38`, Finalize `:37`, `:43-44`; `ServerLoadGateTests.cs:19`, `:91`, `:121-122`;
`HarmonyPatchBindingTests` discovery and by-name `TargetMethod` invocation; `ClientFrontEndCompositionTests`
`_hook` reflection; `patch-targets.md:170-172`; both row VMs (79 lines, diff limited to summary, names,
label lines); `SupplyGoodRowVM.cs:32-79`; `ISupplySourceService.cs:4`, `:29` (fields, so the object
initializer compiles); `SupplyOrderScreenVM.cs` seven lines; prefab binding lines; `SupplyOrderPrefabBindingTests.cs`
`:21`, `:39-40`, `:187-188`; `SupplyGoodsSearchTests.cs:160`; `taom_module_strings.xml:2812`, `:2830`;
`.editorconfig` nullable block; `ClientAssemblies.cs` names; no in-scope class in `EntryPointLineCountBaseline.txt`.
Engine (taom-src v1.5.7): `IssueBase.cs:41` protected `PreconditionFlags`, `:76-81`, `:527`, `:529`;
`TypeDefinition.cs:176-177`; `TypeDefinitionBase.cs:20-33`; `ViewModel` constructor cache,
`GetPropertiesOfType` (instance members only), `OnPropertyChanged` raising `PropertyChanged`;
`GauntletView.cs:166-168` `IMBBindingList`.

Mismatches: N2 only.

## Checklist

- TDD order: Part A RED (throwing skeleton, 11 failures) then GREEN; Part B compile RED then GREEN;
  Part C characterization on the old classes, compile RED, then GREEN. Correct for a refactor.
- Issue line: "filed by the orchestrator before execution", with an orchestrator step. OK.
- ADRs named with one line each (002, 003, 004, 007, 008, 013); all exist under `docs/adrs/`.
- Protected files: none edited, stated (490-491). Single-owner files: out of scope with a STOP. OK.
- STOP conditions: specific to this plan's risks (save ids, protected enum, `[ClientOnly]`, loc keys).
- Planned-at `7087fa633` matches the drift check; Scope paths are inside the drift paths.
- Every dotnet command carries `-p:DisableModuleCopy=true -p:ModuleId=`; no `./build.ps1`.
- No worktree path, no branch name, no CHANGELOG step, no local absolute path, no secret.
- No em or en dash in the plan (scanned for U+2013 and U+2014: none).

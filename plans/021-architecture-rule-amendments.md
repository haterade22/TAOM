# Plan 021: Amend three architecture rules to match the code: virtual boundary seams, when a service gets an interface, when a patch gets a hook

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> maintains that index.
>
> **This is a documentation and rules change. No C#, XML or tool code changes.**
> You never edit any file under `docs/adrs/`: those three edits are Mike's, done
> by hand in Step 0 before you are dispatched (a PreToolUse hook blocks the Edit
> and Write tools on `docs/adrs/*.md`, and you must not get around it with a
> shell command either).
>
> **Where you work: `E:/repos/wt-plan-021` only.** Your session is probably
> rooted at `E:\repos\TAOM`, whose working tree holds another session's
> uncommitted edits, and the Bash tool resets its cwd on every call. So:
> - every Edit, Write or Read `file_path` is absolute, `E:/repos/wt-plan-021/<path>`
>   (a step that says "In `AGENTS.md`" means `E:/repos/wt-plan-021/AGENTS.md`);
> - every command in this plan is written to be cwd-independent: `git -C E:/repos/wt-plan-021`,
>   absolute file paths, or a leading `cd E:/repos/wt-plan-021 && `. Copy each one whole; never
>   drop that prefix or shorten a path to a relative one.
>
> **Shell**: every command below is for the **Bash tool** (Git Bash). Write
> `<scratchpad>` as your session scratchpad in Windows form with forward slashes
> (`C:/Users/.../scratchpad`), never `/c/Users/...` or `/tmp`. Never type the
> literal word pair "git" + "commit" inside a read-only Bash command (for
> example inside a grep pattern): a PreToolUse hook matches that text and may
> deny the command.
>
> **Line numbers** in this plan are those at `a39a9c86` before any of your
> edits. An earlier insertion in the same file shifts later lines; where a step
> knows the shifted number it gives it, but always match the Edit on the quoted
> old text, never on the number. The text inside each fenced block is the exact
> new text; strip only the block's own list indentation, and keep any leading
> spaces the step names explicitly.
>
> **Drift check (run first, Step 1)**:
> `git -C E:/repos/TAOM diff --stat a39a9c86 bannerlord-1.5.x -- AGENTS.md .ai/review-reference.md .claude/skills/deep-review/lenses/1-standards.md .claude/rules/think-before-coding.md .claude/agents/feature-builder.md .claude/skills/new-feature/SKILL.md .claude/rules/csharp-patterns.md .claude/rules/harmony-patches.md .claude/rules/csharp-architecture.md docs/ai-includes/architecture.md docs/ai-includes/decompiled-code-analysis.md docs/ai-includes/patterns.md docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md docs/adrs/008-testability-requirements.md`
> Expected: no output. If any in-scope file changed on the trunk since this plan
> was written, compare every "Current state" excerpt for that file against
> `git -C E:/repos/TAOM show bannerlord-1.5.x:<path>`; if a quoted line changed,
> treat it as a STOP condition. A change elsewhere in the file is fine: note it
> in your report.

## Status

- **Priority**: P2
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: tech-debt
- **Planned at**: commit `a39a9c86`, 2026-09-24 (branch `bannerlord-1.5.x`)
- **Issue**: create before implementation lands (orchestrator)
- **Maintainer decision**: sprint decision 19 (2026-09-24), recorded in
  `plans/_audit/2026-09-23-opus/DECISIONS.md` row 19: "Three rules that disagree with the code:
  **Amend all three**". Findings ARCH-01, ARCH-03 and ARCH-05 in
  `plans/_audit/2026-09-23-opus/lane-2.findings.md`, checked in `verify-a-batch-02.md` and
  `verify-a-batch-03.md`.

## Why this matters

Three written architecture rules describe a codebase TAOM does not have, so reviewers flag correct
code and demand files that hide nothing. (A) ADR-007 and the review reference grade any TaleWorlds
use in a service CRITICAL, yet four reviewed, well-tested campaign services keep their engine access
in `protected virtual` "seam" members overridden by a test subclass, and nothing records that
pattern as allowed. (B) The `/deep-review` Standards lens and ADR-002 demand an interface for every
service while `.claude/rules/think-before-coding.md` forbids single-implementation interfaces; the
two rules contradict each other and the feature scaffolds keep producing interfaces nothing fakes.
(C) AGENTS.md states "patch → hook interface → service → adapter" as mandatory, but only 24 of the
217 Harmony patch files use a hook interface and none of the 19 hook interfaces is faked in a test.
When this lands, every rule source says the same thing and matches the code: seams are allowed under
four conditions, an interface is written when a test fakes it or a second class implements it
(adapters always), and a hook interface is written only when a patch needs a narrow seam or a test
fake.

## Current state

All excerpts below were read at `a39a9c86` with `git show a39a9c86:<path>`. Line numbers are at
that commit.

### The code the rules must describe (context only; you change none of it)

- `Main/Features/Refuge/RefugeService.cs:48-50` names the seam pattern in a doc comment:
  ```csharp
  /// <para>Campaign statics (parties, gold, distances, militia rosters, raids, messages) sit behind
  /// protected virtual members, the CampService/SupplyOrderService pattern, so every decision path
  /// is unit-testable; the virtual bodies are the honest untested boundary sliver.</para>
  ```
  and `:787-791`:
  ```csharp
      // --- campaign-static seams (the untested boundary sliver; overridden in tests) ---

      protected virtual string MainPartyId() => MobileParty.MainParty?.StringId;

      protected virtual int PlayerGold => Hero.MainHero?.Gold ?? 0;
  ```
  The test side is `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs:26`,
  `private sealed class TestableRefugeService : RefugeService`, with overrides such as `:72`
  `protected override string MainPartyId() => MainParty;`. The same idiom is in
  `Main/Features/FieldCamp/CampService.cs` (seams from `:659`), `Main/Features/SupplyLines/SupplyOrderService.cs`
  and `Main/Features/Refuge/WardenService.cs`. The audit counted 94 seam members over those four
  services (`verify-a-batch-02.md`, "ARCH-05", correction 1).
- Hook interfaces: 217 files under `Main/` carry `[HarmonyPatch` or `HarmonyPatchCategory`; 24 of
  them reference an `IOn*` interface; `Main/` declares 19 distinct `interface IOn*`; `TAOM.Tests`
  has 0 `Substitute.For<IOn` (measured at `a39a9c86` with `git grep`). One hook that earns its file:
  `Main/Features/SpecialResources/Hooks/PartyUpgradeResourceCheckHook.cs` narrows the wide
  `ISpecialResourceService` to the party-screen API for three patches
  (`PartyCharacterVM_InitializeUpgrades_Patch.cs`, `PartyScreenLogic_AddCommand_Patch.cs`,
  `PartyScreenLogic_UpgradeTroop_Patch.cs`). One that does not:
  `Main/Features/SpecialResources/Hooks/RecruitmentResourceGateHook.cs:14-15` is a single forward,
  `=> _service.CanAffordRecruit(heroId, kingdomId, cultureId, cart);`.
- Interfaces: the audit measured 495 interfaces declared in `Main/` at `b2e387db`, 477 with exactly
  one implementation, 154 of those neither an adapter nor faked by any test
  (`lane-2.findings.md`, "Measurement method"; totals reproduced in `verify-a-batch-02.md`).
- An exemplar of registering a concrete class with no interface, used by the new feature-builder
  template: `Main/Features/ArmyTargeting/ArmyTargetingIoC.cs:19`,
  `container.Register<ArmyTargetingLifecycleBehavior>(Reuse.Singleton);`.
- Engine fact used in the ADR text: `CampaignTime` is a struct in the installed v1.5.3 engine
  (`pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.CampaignTime` gives
  `C:\Users\mikew\.taom-src\v1.5.3\TaleWorlds.CampaignSystem.CampaignTime.cs`, line 11:
  `public struct CampaignTime : IComparable<CampaignTime>`).

### The rule text to change (every line you edit, quoted exactly)

1. `AGENTS.md:36`
   ```
   | **Architecture** | Patch, model or behavior → hook interface → service → adapter. Services take adapters, never sealed TaleWorlds types (ADR-007); entry points under 150 lines (ADR-002). |
   ```
2. `.ai/review-reference.md` (613 lines)
   - `:21` `- **CRITICAL**: ADR-007 (sealed type in service), ADR-002 (fat entry point), Harmony target method does not exist in the installed engine (v1.5.2)`
   - `:215` ``**One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]` -> `IHookInterface` -> `Service` -> `IAdapter` (sealed types)``
   - `:249` ``| No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero` |`` (followed at `:250` by `| Constructor injection only | No service locator in services |`)
   - `:274` `    IMyFeatureService.cs` (inside the "Feature File Layout" code block)
   - `:328` ``- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface` ``
   - `:389-400`:
     ````
     ### 1. Hook Pattern (Harmony -> Hook Interface -> Service)

     ```
     HarmonyPatch (thin)
         -> IOnXxx hook interface
             -> XxxHook implementation
                 -> IXxxService (business logic)
     ```

     - Harmony patch resolves `IOnXxx` hooks via `IoC.ResolveAll<IOnXxx>()`, iterates, delegates
     - Hook implementation builds context, calls service
     - Service contains all logic — uses adapters, fully testable
     ````
3. `.claude/skills/deep-review/lenses/1-standards.md`
   - `:8` `1. **Adapter Pattern (ADR-007):** Services NEVER reference TaleWorlds types directly (Hero, Clan, Kingdom, etc.). They use IXxxAdapter interfaces. Flag ANY direct TaleWorlds type usage in service classes.`
   - `:13` `6. **Interface Segregation:** Every service has an interface. Every adapter has an interface.`
4. `.claude/rules/think-before-coding.md:33-34` (an unscoped rule: it loads in every session)
   ```
   4. Only then write the minimum: no single-implementation interface, no plumbing "for later"
      (`simplicity-criterion.md`).
   ```
5. `.claude/agents/feature-builder.md`
   - `:21-24`:
     ````
     ## Architecture (MANDATORY)
     ```
     Entry Points (thin, <150 lines) → IHookInterface → Service → IAdapter (sealed types)
     ```
     ````
   - `:40` `├── I{Name}Service.cs            # Service interface`
   - `:55` `        container.Register<I{Name}Service, {Name}Service>(Reuse.Singleton);`
6. `.claude/skills/new-feature/SKILL.md:23` ``- `I{FeatureName}Service.cs` — Interface defining the feature's public API``
7. `.claude/rules/csharp-patterns.md:11-13`:
   ````
   ## 1. Hook Pattern (Harmony → Hook Interface → Service)

   ```
   ````
8. `.claude/rules/harmony-patches.md:53` ``- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface` ``
9. `.claude/rules/csharp-architecture.md`
   - `:28` ``| No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero` |`` (followed at `:29` by `| Constructor injection only | No service locator in services |`)
   - `:223` `├── IMyFeatureService.cs` (inside the "File Layout" code block)
10. `docs/ai-includes/architecture.md`
    - `:106-110`:
      ```
      **Rules for Services:**
      - All dependencies injected via constructor
      - Only use adapter interfaces, never sealed types (ADR-007)
      - Single responsibility
      - Fully unit testable with mocked adapters
      ```
    - `:246-250`:
      ````
      ```
      Entry Point → Hook Interface → Service → Engine → Adapter
           ↓            ↓              ↓         ↓         ↓
         Thin      Orchestrate      Logic     Algorithm   Wrap
      ```
      ````
11. `docs/ai-includes/decompiled-code-analysis.md`
    - `:99` `    → Hook Interface (IOn[EventName])`
    - `:279` `7. Does it fit TAOM's architecture (patches -> hooks -> services -> adapters)?`
12. `docs/ai-includes/patterns.md:9` `Separation between game integration and business logic.` (under `### 1. Hook Pattern + Harmony Integration`)

### The protected ADR text (Mike changes it in Step 0; quoted so Step 0 is checkable)

- `docs/adrs/002-thin-entry-points.md:247` `2. **Interface-Based**: Always define `IServiceName` interface`;
  `:249` `4. **No Game Dependencies**: Services should not depend on game lifecycle or sealed game types`;
  `:252` `7. **Testable**: All services must have unit tests using mocked adapters`; `:253` blank;
  `:254` `## Adapter Pattern Implementation`; `:291` `- [ ] Service has corresponding interface`.
  ADR-002 never mentions hooks (no `hook` match), so it needs no hook edit.
- `docs/adrs/007-adapter-pattern.md:552` `## Exceptions` holds only `### Value Types Don't Need Adapters`
  (`:554`) and `### Entry Points May Use Sealed Types` (`:571`), ending at `:582`
  `- Pass only adapters to services`, then `:583` blank, `:584` `## Migration Strategy (TDD Approach)`.
  Checklist `:753` `- [ ] Service implementations use adapters throughout` and `:759`
  `- [ ] Tests use mocked adapters (NSubstitute)`.
- `docs/adrs/008-testability-requirements.md:11` `### Rule 1: No Static TaleWorlds Calls in Services`,
  `:13` `Services MUST NOT call static methods or properties from TaleWorlds game framework:` (its
  FORBIDDEN example lists `CampaignTime.Now` and `Hero.MainHero`, exactly what the seams read);
  `:85` `All services MUST use these providers instead of static calls:`. The audit brief named only
  ADR-007 for part (A); ADR-002 guideline 4 and ADR-008 Rule 1 also forbid what the seam does, so
  Step 0 amends them too, or the rules would still contradict each other.

### Binding rules and conventions

- **ADR-002 (thin entry points)**: entry points (patches, models, behaviors) stay under 150 lines and
  delegate to services. This plan changes its service-interface guideline, not the 150-line rule.
- **ADR-007 (adapters)**: services take adapter interfaces, never sealed TaleWorlds types; adapters
  are decided and stay. This plan adds one recorded exception; it does not weaken the adapter rule.
- **ADR-008 (testability)**: services are unit-testable without the game. The seam exception keeps
  that property (a test subclass overrides every seam).
- **ADR-011 (knowledge tiers)**: AGENTS.md and the unscoped rules are paid by every session; add no
  counts nothing computes and no copies of another tier's text there. Budgets are enforced by
  `python tools/lint_docs.py --fail-on-drift`: entry docs (CLAUDE.md + AGENTS.md + orientation.md)
  at most 24,576 B (22,189 B at `a39a9c86`), each AGENTS.md table row at most 400 characters (the
  new row is 255), unscoped rules at most 16,384 B (12,420 B at `a39a9c86`). `tools/reviewctl.py`
  and its test cap AGENTS.md at 8,192 B (7,114 B at `a39a9c86`).
- **`.claude/rules/output-style.md` Part 2**: no em dash (U+2014) or en dash (U+2013) in any line you
  add or rewrite; code spans are exempt. Several old lines you replace contain an em dash; the new
  text must not.
- **`.claude/rules/simplicity-criterion.md`**: no bulk deletion of interfaces or hooks (the decision
  says deletions happen when a file is next touched).
- **Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
  `Directory.Build.props`: not touched by this plan. If you think one needs a change, STOP.

## Commands you will need

All in the Bash tool, copied whole (`lint_docs.py`, `test_hooks.sh` and the proof script find the
worktree from their own path, so they need no `cd`; the rest carry one).

| Purpose | Command | Expected on success |
|---|---|---|
| Docs lint (gated checks) | `python E:/repos/wt-plan-021/tools/lint_docs.py --fail-on-drift` | exit 0 |
| Docs lint (full report) | `python E:/repos/wt-plan-021/tools/lint_docs.py > <scratchpad>/lint-after.txt` | compared with the Step 1 report in Step 8.2 (numbers stripped) |
| Doc and bootstrap tests | `cd E:/repos/wt-plan-021 && python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation` | `OK` (47 tests when planned) |
| Hook contract | `bash E:/repos/wt-plan-021/tools/test_hooks.sh` | exit 0, or the same failures as the Step 1 baseline |
| Rule-text proof | `python <scratchpad>/plan021_check.py rules` | `OK` and exit 0 |
| ADR proof | `python <scratchpad>/plan021_check.py adr` | `OK` and exit 0 |
| No new dash | `python <scratchpad>/plan021_check.py dash` | `OK` and exit 0 |
| Build (only if asked) | `cd E:/repos/wt-plan-021 && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0 |
| Tests (only if asked) | `cd E:/repos/wt-plan-021 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | at `a39a9c86`: 10,313+ passed, 2 skipped, 0 failed |
| Filtered tests (only if asked) | `cd E:/repos/wt-plan-021 && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | the named class passes |
| Data (only if asked) | `cd E:/repos/wt-plan-021 && python tools/validate_moduledata.py` | 0 ERRORs |

No `.cs`, `.xml` or `.csproj` file changes in this plan, so the build, the test suite and
`python tools/validate_moduledata.py` are not required gates. The baseline at `a39a9c86` is 10,313+
tests passed, 2 skipped, 0 failed; if the orchestrator asks you to run the suite and anything fails,
explaining the failure is yours. Never run `./build.ps1`.

## Scope

**In scope** (the only files you modify; each path is relative to `E:/repos/wt-plan-021/`):
- `AGENTS.md` (line 36 only)
- `.ai/review-reference.md`
- `.claude/skills/deep-review/lenses/1-standards.md`
- `.claude/rules/think-before-coding.md`
- `.claude/agents/feature-builder.md`
- `.claude/skills/new-feature/SKILL.md`
- `.claude/rules/csharp-patterns.md`
- `.claude/rules/harmony-patches.md`
- `.claude/rules/csharp-architecture.md`
- `docs/ai-includes/architecture.md`
- `docs/ai-includes/decompiled-code-analysis.md`
- `docs/ai-includes/patterns.md`
- `CHANGELOG.md` (one new entry; required by the commit hook for any `.claude/` or `AGENTS.md` change)
- Scratch files in your session scratchpad (the check script, the commit message file)

**Mike's, in Step 0, never yours**: `docs/adrs/002-thin-entry-points.md`,
`docs/adrs/007-adapter-pattern.md`, `docs/adrs/008-testability-requirements.md`.

**Out of scope** (do NOT touch, even though they look related):
- Any `.cs` file. No interface is deleted, no hook is folded, no service is seamed. In particular:
  not the 154 unfaked interfaces, not `RecruitmentResourceGateHook` or
  `CultureStageViewFinalizeHook` (they fold when next touched), not `SupplySourceService` or
  `SupplyCaravanService` or any other raw-read service (decision: do not plan seaming here).
- `Main/IoC.cs`, `Main/SubModule.cs` (single-owner; nothing here needs them).
- `docs/ai-includes/architecture.md:149` "One adapter interface per sealed type" (a separate,
  undecided wording question; leave it).
- `docs/ai-includes/architecture.md:252-260` "Example Flow: Damage Calculation" and the code examples
  in `docs/ai-includes/patterns.md` and `docs/ai-includes/architecture.md:440-454`: they illustrate
  a hook where one is used; leave them.
- `plans/README.md`, `docs/INDEX.md`, `docs/ai-includes/orientation.md`, `CLAUDE.md`.
- Two known leftovers that still show the hook layer as the architecture, accepted for this plan
  (neither is a rule source an agent loads as policy; both go to the orchestrator as follow-ups):
  `README.md:96` (`[HarmonyPatch / GameModel / CampaignBehavior] → IHookInterface → Service → IAdapter`,
  the public repo overview) and `.serena/memories/project_overview.md:16` (Serena's onboarding
  memory). Step 8.6 expects exactly these, plus three in-scope matches it names.
- Anything in `E:\repos\TAOM`'s own working tree: another session's uncommitted edits live there
  (including `CHANGELOG.md`, `Main/IoC.cs`, `Main/SubModule.cs`, `docs/INDEX.md`). You work only in
  `E:/repos/wt-plan-021`.

## Git workflow

- Mike creates the branch and worktree in Step 0:
  `git -C E:/repos/TAOM worktree add E:/repos/wt-plan-021 -b improve/021-architecture-rule-amendments a39a9c86`.
  Run every command in this plan from `E:/repos/wt-plan-021`. If the worktree or branch is missing,
  STOP (Step 0 did not happen). Never create a second worktree, never delete, reset or re-point
  this one.
- The worktree checks out with CRLF line endings (`core.autocrlf=true`). Use the Edit tool for every
  change; never `sed -i`. Write scripts and the commit message with the Write tool into your session
  scratchpad, not with heredocs.
- Commit subject format: `<type>(<scope>): v<version> - <description>`, where the version is the
  `<Version value="..."/>` in `Main/_Module/SubModule.xml` (`v2.0.30` at planning time). At most 72
  characters, body wrapped at 72, **no AI attribution trailer** (no `Co-Authored-By`).
- **Which SubModule.xml the subject hook reads**: `check-commit-subject-version.sh` ignores
  `git -C`; it runs `cd "$CLAUDE_PROJECT_DIR"` (the directory your session started in, normally
  `E:\repos\TAOM`) and reads `Main/_Module/SubModule.xml` there, the staged copy if staged, else the
  working-tree copy. So before committing (Step 9.2) read both
  `E:/repos/wt-plan-021/Main/_Module/SubModule.xml` and `E:/repos/TAOM/Main/_Module/SubModule.xml`.
  Both read `v2.0.30` at planning time. If they differ, STOP: the hook would deny a subject that
  matches your branch, or pass one that mislabels it.
- One commit from you, on top of Mike's Step 0 commit:
  `docs(rules): v2.0.30 - align three architecture rules with the code` (67 characters).
- Stage explicit paths only, never `git add -A` or `git commit -a`. Commit with the pathspec form so
  the CHANGELOG hook sees `CHANGELOG.md` in the commit (the hook reads `E:\repos\TAOM`'s index, not
  your worktree's, and also parses the paths after ` -- `):
  `git add <the 13 in-scope paths>` then
  `git commit -F <scratchpad>/plan021-msg.txt -- <the same 13 paths>`.
- If any hook denies a commit, STOP and report its message verbatim. Never bypass a hook, never use
  `--no-verify`, never edit an out-of-scope file to satisfy one.
- Never push, never open a PR, never merge.
- **Merge note for the orchestrator**: the branch starts at `a39a9c86`. The main tree's
  uncommitted `CHANGELOG.md` edits (another session) will meet this branch's CHANGELOG entry at
  merge time; resolve by keeping both entries.

## Steps

### Step 0: Before dispatch (Mike, in an editor)

The executor cannot do this step. Mike, three ADR edits, then one commit. Work in the new worktree.

**0.1** Create the worktree:
`git -C E:/repos/TAOM worktree add E:/repos/wt-plan-021 -b improve/021-architecture-rule-amendments a39a9c86`

**0.2** `E:/repos/wt-plan-021/docs/adrs/007-adapter-pattern.md`: insert this block after line 582
(`- Pass only adapters to services`) and its blank line, before `## Migration Strategy (TDD Approach)`:

```markdown
### Protected-Virtual Boundary Seams (amendment, 2026-09-24)

A service may keep its engine access in `protected virtual` members of its own class, instead of
behind an adapter, when all four conditions hold:

1. **Seam signatures use ids and value types only**: string ids, primitives, enums and engine
   structs such as `Vec2` and `CampaignTime`; never a sealed TaleWorlds class.
2. **Each seam body is a single engine call or query**, with no TAOM decision logic inside it.
3. **The service's interface stays engine-free**: callers still pass ids or adapters.
4. **A test subclass overrides every seam the tests reach**, so every decision path runs in unit
   tests without the game.

Why: `RefugeService`, `CampService`, `SupplyOrderService` and `WardenService` use this pattern, each
tested through one subclass (for example `RefugeService.MainPartyId()` and `TestableRefugeService`
in `RefugeServiceTests.cs`); the 2026-09-23 audit counted 94 seam members
(`plans/_audit/2026-09-23-opus/verify-a-batch-02.md`, ARCH-05). The adapter route for the same
coverage would need several new adapter interfaces per service for no behaviour gain. The cost is
that seam bodies are untested engine code inside a service, so they stay thin.

Reviewers: a seam that meets these conditions is not an ADR-007 violation. A seam that breaks one,
or engine use in a service outside a seam, still is. This exception also qualifies ADR-002 Service
Design Guideline 4 and ADR-008 Rule 1, which point here.
```

In the same file, change checklist lines 753 and 759 to these two lines respectively:

```markdown
- [ ] Service implementations use adapters throughout, or protected-virtual boundary seams that meet the Exceptions conditions
- [ ] Tests use mocked adapters (NSubstitute), or a test subclass that overrides the service's boundary seams
```

**Before you paste conditions 1 and 2, one fact to weigh.** As decided, they do not describe every
seam that exists today, so the next Standards review would flag reviewed code:
- Condition 2: `RefugeService.SpawnRefugeParty(string, string)` (`RefugeService.cs:860-874`) spans
  fifteen lines with several engine calls (`MobileParty.CreateParty`,
  `InitializeMobilePartyAtPosition`, `ActualClan`, `PinPartyAi`), and `AttachWarden` (`:876-895`)
  holds a try/catch.
- Condition 1: `RefugeService.IsCampReady(CampState camp)` (`:805`) and
  `BuildProgressOf(RefugeData data)` (`:808`) take TAOM classes (`CampState`, `RefugeData` are
  `sealed class`es), which are neither ids nor value types; `ShowMessage(TextObject text, bool error)`
  (`:1214`) takes a `TextObject`, which ADR-007 already exempts as a value-like type.

If you want the ADR to describe today's seams, use this variant of conditions 1 and 2 instead (the
executor's checks accept either; conditions 3 and 4 and every other line stay as above):

```markdown
1. **Seam signatures use ids, value types and TAOM types only**: string ids, primitives, enums,
   TAOM data classes, engine structs such as `Vec2` and `CampaignTime`, and the value-like types
   under "Value Types Don't Need Adapters"; never a sealed TaleWorlds class.
2. **Each seam body is one engine operation**: the engine calls or queries for one action, with
   their null guards and id lookups, and no TAOM decision logic.
```

**0.3** `E:/repos/wt-plan-021/docs/adrs/002-thin-entry-points.md`: lines 247, 249 and 252 become,
respectively, these three lines (lines 246, 248, 250 and 251 stay):

```markdown
2. **Interface When Needed**: Define an `IServiceName` interface only when a test fakes the service or a second implementation exists; every adapter always has one (ADR-007)
4. **No Game Dependencies**: Services should not depend on game lifecycle or sealed game types, except through protected-virtual boundary seams (ADR-007 "Exceptions")
7. **Testable**: All services must have unit tests using mocked adapters, or a test subclass that overrides the service's boundary seams (ADR-007 "Exceptions")
```

After line 252 insert a blank line and this block (it lands before the blank line that precedes
`## Adapter Pattern Implementation`):

```markdown
### Amendment (2026-09-24): service interfaces and boundary seams

Guideline 2 used to read "Always define `IServiceName` interface". The 2026-09-23 audit found most
service interfaces had exactly one implementation and many were faked by no test
(`plans/_audit/2026-09-23-opus/lane-2.findings.md`, ARCH-01), while `.claude/rules/think-before-coding.md`
forbade single-implementation interfaces. An interface now earns its file for one of two reasons: a
test fakes it, or a second class implements it. Adapters keep their interfaces unconditionally
(ADR-007). Existing interfaces are not deleted in bulk; one goes when its file is next touched and
nothing fakes it. Guidelines 4 and 7 now name the protected-virtual boundary seam that ADR-007
records.
```

Line 291 (checklist) becomes:

```markdown
- [ ] Service has an interface if a test fakes it or a second implementation exists (not otherwise)
```

**0.4** `E:/repos/wt-plan-021/docs/adrs/008-testability-requirements.md`: after line 13
(`Services MUST NOT call static methods or properties from TaleWorlds game framework:`) insert a
blank line and the first line below; line 85
(`All services MUST use these providers instead of static calls:`) becomes the second:

```markdown
**Exception (2026-09-24):** a static read inside a protected-virtual boundary seam that meets ADR-007's "Protected-Virtual Boundary Seams" conditions is allowed; the service's test subclass overrides the seam, so no test touches the static.
All services MUST use these providers instead of static calls (or a protected-virtual boundary seam, per the Rule 1 exception):
```

Do not touch the auto-generated `## Referenced by` blocks at the end of any ADR.

**0.5** Commit (from any shell; this commit touches no `.claude/` path):
`git -C E:/repos/wt-plan-021 add docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md docs/adrs/008-testability-requirements.md`
then
`git -C E:/repos/wt-plan-021 commit -m "docs(adr): v2.0.30 - record boundary seams, conditional interfaces" -- docs/adrs/002-thin-entry-points.md docs/adrs/007-adapter-pattern.md docs/adrs/008-testability-requirements.md`
(subject 66 characters, no AI trailer; Step 1.2 checks for the `docs(adr): v2.0.30 - ` prefix).
Then dispatch the executor.

### Step 1: Confirm Step 0, run the drift check, record baselines

1. `git -C E:/repos/wt-plan-021 rev-parse --abbrev-ref HEAD` → `improve/021-architecture-rule-amendments`.
2. `git -C E:/repos/wt-plan-021 log --format=%h\ %s a39a9c86..HEAD` → exactly one line, a
   `docs(adr): v2.0.30 - ...` subject. `git -C E:/repos/wt-plan-021 diff --name-only a39a9c86 HEAD`
   → exactly the three `docs/adrs/` files above. `git -C E:/repos/wt-plan-021 status --short` →
   empty.
3. Run the drift check at the top of this file.
4. Write `<scratchpad>/plan021_check.py` with the Write tool, exactly this content:

```python
"""Plan 021 proof: old rule text gone, new rule text present. Works from any cwd.

Modes: `adr` (Step 0 landed), `rules` (Steps 2-7 landed), `dash [FILE ...]` (no U+2013/U+2014
on any line added since HEAD in the worktree, CHANGELOG.md included, nor on any line of each
FILE given)."""
import pathlib
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8")
ROOT = pathlib.Path("E:/repos/wt-plan-021")

DIAGRAM = (
    "Entry Point → [Hook Interface] → Service → Engine → Adapter\n"
    "     ↓               ↓              ↓        ↓         ↓\n"
    "   Thin     Narrow seam (opt.)    Logic  Algorithm   Wrap\n"
)

ADR = [
    ("docs/adrs/007-adapter-pattern.md", None, "### Protected-Virtual Boundary Seams"),
    ("docs/adrs/007-adapter-pattern.md", None, "A test subclass overrides every seam the tests reach"),
    ("docs/adrs/007-adapter-pattern.md", None, "or protected-virtual boundary seams that meet the Exceptions conditions"),
    ("docs/adrs/002-thin-entry-points.md", "**Interface-Based**", "### Amendment (2026-09-24): service interfaces and boundary seams"),
    ("docs/adrs/002-thin-entry-points.md", "- [ ] Service has corresponding interface", "except through protected-virtual boundary seams"),
    ("docs/adrs/008-testability-requirements.md", None, "**Exception (2026-09-24):**"),
]

RULES = [
    ("AGENTS.md", "behavior → hook interface → service", "→ service (through a hook interface only when the patch needs a narrow seam or a test fake) → adapter"),
    (".ai/review-reference.md", "ADR-007 (sealed type in service), ADR-002", 'a protected-virtual boundary seam that meets ADR-007 "Exceptions" is not one'),
    (".ai/review-reference.md", "-> `IHookInterface` -> `Service`", "an `IOnXxx` hook interface sits between patch and service only when"),
    (".ai/review-reference.md", "delegate ALL logic to services via `IHookInterface`", "directly or through an `IOnXxx` hook interface when the patch needs a narrow seam or a test fake"),
    (".ai/review-reference.md", None, "| Interfaces that earn their file |"),
    (".ai/review-reference.md", None, "the one exception is a protected-virtual boundary seam"),
    (".ai/review-reference.md", None, "<-- only if a test fakes it or a second class implements it"),
    (".ai/review-reference.md", None, "Optional layer. Most patches resolve their service at the boundary and call it directly"),
    (".claude/skills/deep-review/lenses/1-standards.md", "Every service has an interface.", "A service gets one only when a test fakes it or a second implementation exists"),
    (".claude/skills/deep-review/lenses/1-standards.md", None, 'ADR-007 "Exceptions: Protected-Virtual Boundary Seams"'),
    (".claude/rules/think-before-coding.md", "no single-implementation interface", "no interface unless it wraps an engine type (an adapter), a test"),
    (".claude/agents/feature-builder.md", "→ IHookInterface → Service", "A hook interface (`IOnXxx`) goes between an entry point and its service only when"),
    (".claude/agents/feature-builder.md", "# Service interface", "container.Register<{Name}Service>(Reuse.Singleton);"),
    (".claude/skills/new-feature/SKILL.md", "Interface defining the feature's public API", "only when a test fakes the service or a second implementation exists"),
    (".claude/rules/csharp-patterns.md", "## 1. Hook Pattern (Harmony → Hook Interface → Service)", "## 1. Hook Pattern (optional: Harmony → Hook Interface → Service)"),
    (".claude/rules/csharp-patterns.md", None, "Optional layer. Most patches resolve their service at the boundary and call it directly"),
    (".claude/rules/harmony-patches.md", "delegate ALL logic to services via `IHookInterface`", "directly or through an `IOnXxx` hook interface when the patch needs a narrow seam or a test fake"),
    (".claude/rules/csharp-architecture.md", None, "the one exception is a protected-virtual boundary seam"),
    (".claude/rules/csharp-architecture.md", None, "| Interfaces that earn their file |"),
    (".claude/rules/csharp-architecture.md", None, "← only if a test fakes it or a second class implements it"),
    ("docs/ai-includes/architecture.md", "Entry Point → Hook Interface → Service → Engine → Adapter", "Entry Point → [Hook Interface] → Service → Engine → Adapter"),
    ("docs/ai-includes/architecture.md", None, DIAGRAM),
    ("docs/ai-includes/architecture.md", None, "- An `IXxxService` interface only when a test fakes it or a second implementation exists (ADR-002)"),
    ("docs/ai-includes/decompiled-code-analysis.md", "(patches -> hooks -> services -> adapters)", "patches -> services -> adapters, with a hook interface only where"),
    ("docs/ai-includes/decompiled-code-analysis.md", None, "only when the patch needs a narrow seam or a test fake"),
    ("docs/ai-includes/patterns.md", None, "This layer is optional."),
]


def run(checks):
    problems = []
    for path, gone, present in checks:
        text = (ROOT / path).read_text(encoding="utf-8")  # text mode: CRLF reads as \n
        if gone is not None and gone in text:
            problems.append(f"STILL PRESENT in {path}: {gone}")
        if present is not None and present not in text:
            problems.append(f"MISSING in {path}: {present.splitlines()[0]}")
    return problems


def has_dash(line):
    return "\u2013" in line or "\u2014" in line


def added_dash_lines():
    diff = subprocess.run(["git", "-C", str(ROOT), "diff", "HEAD", "-U0"],
                          capture_output=True, check=True).stdout
    problems = []
    for raw in diff.decode("utf-8", errors="replace").splitlines():
        if raw.startswith("+") and not raw.startswith("+++") and has_dash(raw):
            problems.append(f"DASH on an added line: {raw[:120]}")
    return problems


def file_dash_lines(paths):
    problems = []
    for p in paths:
        for n, line in enumerate(pathlib.Path(p).read_text(encoding="utf-8").splitlines(), 1):
            if has_dash(line):
                problems.append(f"DASH in {p}:{n}: {line[:120]}")
    return problems


mode = sys.argv[1] if len(sys.argv) > 1 else "rules"
if mode == "dash":
    found = added_dash_lines() + file_dash_lines(sys.argv[2:])
else:
    found = run(ADR if mode == "adr" else RULES)
for line in found:
    print(line)
print("OK" if not found else f"{len(found)} problem(s)")
sys.exit(1 if found else 0)
```

**Verify**:
- `python <scratchpad>/plan021_check.py adr` → `OK`, exit 0. If it reports any problem, STOP:
  Step 0 is missing or incomplete; report the lines it printed.
- `python <scratchpad>/plan021_check.py rules` → exit 1, last line `39 problem(s)` (every old
  string still present, every new string missing; the planner ran this exact script against the
  `a39a9c86` copies of these files and got 39). This is the RED state. If the count differs, some
  rule text drifted from the excerpts: STOP.
- Baselines (write each result down; later steps compare against them):
  - `python E:/repos/wt-plan-021/tools/lint_docs.py --fail-on-drift` → expected exit 0 (7
    report-only `size-warn` lines for path-scoped rules over 12,288 B, including
    `csharp-architecture.md` and `harmony-patches.md`);
  - `python E:/repos/wt-plan-021/tools/lint_docs.py > <scratchpad>/lint-before.txt` (the full
    report, kept for Step 8.2);
  - `cd E:/repos/wt-plan-021 && python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation`
    → expected `OK`;
  - `bash E:/repos/wt-plan-021/tools/test_hooks.sh` → record the final summary line and exit code.

### Step 2: AGENTS.md, the hook-interface rule (part C)

In `AGENTS.md`, replace line 36 (quoted in "Current state" item 1) with exactly:

```
| **Architecture** | Patch, model or behavior → service (through a hook interface only when the patch needs a narrow seam or a test fake) → adapter. Services take adapters, never sealed TaleWorlds types (ADR-007); entry points under 150 lines (ADR-002). |
```

Change nothing else in AGENTS.md.

**Verify**: `grep -c "through a hook interface only when the patch needs a narrow seam" E:/repos/wt-plan-021/AGENTS.md` → `1`;
`grep -c "hook interface → service" E:/repos/wt-plan-021/AGENTS.md` → `0`.

### Step 3: `.ai/review-reference.md` (parts A, B, C)

Six edits to `E:/repos/wt-plan-021/.ai/review-reference.md`, each by the Edit tool with the exact
old text from "Current state" item 2 (see "Line numbers" at the top: after edit 3 inserts a line,
old lines 274, 328 and 389 sit at 275, 329 and 390).

1. Line 21 becomes:
   `- **CRITICAL**: ADR-007 (sealed type in service; a protected-virtual boundary seam that meets ADR-007 "Exceptions" is not one), ADR-002 (fat entry point), Harmony target method does not exist in the installed engine (v1.5.2)`
2. Line 215 becomes:
   ```
   **One-liner:** `[HarmonyPatch/GameModel/CampaignBehavior]` -> `Service` -> `IAdapter` (sealed types); an `IOnXxx` hook interface sits between patch and service only when the patch needs a narrow seam or a test fake
   ```
3. Line 249 becomes the first line below, and the second line is inserted directly after it (before
   `| Constructor injection only | ...`):
   ```
   | No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero`; the one exception is a protected-virtual boundary seam (ADR-007 "Exceptions") |
   | Interfaces that earn their file | Every adapter has one (ADR-007); a service gets one only when a test fakes it or a second class implements it (ADR-002) |
   ```
4. Line 274 (`    IMyFeatureService.cs`, four leading spaces, inside the layout block) becomes:
   `    IMyFeatureService.cs     <-- only if a test fakes it or a second class implements it`
5. Line 328 becomes:
   ```
   - Patches are **thin entry points**: delegate ALL logic to a service, directly or through an `IOnXxx` hook interface when the patch needs a narrow seam or a test fake
   ```
6. Line 389 (`### 1. Hook Pattern (Harmony -> Hook Interface -> Service)`) becomes
   `### 1. Hook Pattern (Harmony -> Hook Interface -> Service), optional`, and after it and its blank
   line (before the opening fence of the diagram) insert this paragraph followed by one blank line:
   ```
   Optional layer. Most patches resolve their service at the boundary and call it directly (`patch -> service -> adapter`). Add an `IOnXxx` hook interface only when the patch needs a narrow seam over a wide service or a test fakes the hook; a hook that only forwards one call is a deletion candidate when its files are next touched.
   ```

Leave line 400 (`- Service contains all logic — uses adapters, fully testable`) as it is: it is untouched existing
prose.

**Verify**: `grep -c "IHookInterface" E:/repos/wt-plan-021/.ai/review-reference.md` → `0`;
`grep -c "protected-virtual boundary seam" E:/repos/wt-plan-021/.ai/review-reference.md` → `2`;
`grep -c "Interfaces that earn their file" E:/repos/wt-plan-021/.ai/review-reference.md` → `1`.

### Step 4: The `/deep-review` Standards lens (parts A, B)

In `.claude/skills/deep-review/lenses/1-standards.md`:

1. Line 8 becomes (the old sentence kept, one sentence appended):
   ```
   1. **Adapter Pattern (ADR-007):** Services NEVER reference TaleWorlds types directly (Hero, Clan, Kingdom, etc.). They use IXxxAdapter interfaces. Flag ANY direct TaleWorlds type usage in service classes. One exception: a `protected virtual` member of the service that meets the four conditions in ADR-007 "Exceptions: Protected-Virtual Boundary Seams" (a test subclass overrides it) is not a violation; flag a seam that breaks a condition, and any engine use outside a seam.
   ```
2. Line 13 becomes:
   ```
   6. **Interface Segregation:** Every adapter has an interface. A service gets one only when a test fakes it or a second implementation exists (ADR-002 guideline 2): flag a new service interface that no test fakes and no second class implements, and do not demand one that nothing would fake.
   ```

**Verify**: `grep -c "Every service has an interface" E:/repos/wt-plan-021/.claude/skills/deep-review/lenses/1-standards.md` → `0`;
`grep -c "Protected-Virtual Boundary Seams" E:/repos/wt-plan-021/.claude/skills/deep-review/lenses/1-standards.md` → `1`.

### Step 5: Builder guidance: think-before-coding, feature-builder, new-feature (parts B, C)

1. `.claude/rules/think-before-coding.md` lines 33-34 become exactly:
   ```
   4. Only then write the minimum: no interface unless it wraps an engine type (an adapter), a test
      fakes it or a second class implements it; no plumbing "for later" (`simplicity-criterion.md`).
   ```
2. `.claude/agents/feature-builder.md`:
   - line 23 becomes `Entry Points (thin, <150 lines) → Service → IAdapter (sealed types)`, and
     after line 24 (the closing fence) insert one blank line and:
     ```
     A hook interface (`IOnXxx`) goes between an entry point and its service only when the patch needs a narrow seam or a test fake. A service gets an `I{Name}Service` interface only when a test fakes it or a second implementation exists; every adapter has one (ADR-002, ADR-007).
     ```
   - old line 40 (now line 42, after the two inserted lines) becomes
     `├── I{Name}Service.cs            # Only if a test fakes it or a 2nd impl exists`
   - old line 55 (now line 57) is replaced by these two lines (same eight-space indent):
     ```
             container.Register<{Name}Service>(Reuse.Singleton);
             // when a test fakes it: container.Register<I{Name}Service, {Name}Service>(Reuse.Singleton);
     ```
3. `.claude/skills/new-feature/SKILL.md` line 23 becomes:
   ```
   - `I{FeatureName}Service.cs`: only when a test fakes the service or a second implementation exists (ADR-002); otherwise register the concrete class, `container.Register<{FeatureName}Service>(Reuse.Singleton)`
   ```

**Verify**: `grep -c "single-implementation interface" E:/repos/wt-plan-021/.claude/rules/think-before-coding.md` → `0`;
`grep -c "IHookInterface" E:/repos/wt-plan-021/.claude/agents/feature-builder.md` → `0`;
`grep -c "Interface defining the feature" E:/repos/wt-plan-021/.claude/skills/new-feature/SKILL.md` → `0`.

### Step 6: The C# path rules: csharp-patterns, harmony-patches, csharp-architecture (parts A, B, C)

1. `.claude/rules/csharp-patterns.md`: line 11 becomes
   `## 1. Hook Pattern (optional: Harmony → Hook Interface → Service)`, and after it and its blank
   line (before the opening fence at line 13) insert this paragraph and one blank line:
   ```
   Optional layer. Most patches resolve their service at the boundary and call it directly (patch → service → adapter, AGENTS.md "Architecture"). Add an `IOnXxx` hook only when the patch needs a narrow seam over a wide service (`PartyUpgradeResourceCheckHook` narrows `ISpecialResourceService` for three patches) or a test fakes the hook; a hook that forwards one call adds a file and hides nothing.
   ```
2. `.claude/rules/harmony-patches.md` line 53 becomes:
   ```
   - Patches are **thin entry points**: delegate ALL logic to a service, directly or through an `IOnXxx` hook interface when the patch needs a narrow seam or a test fake
   ```
3. `.claude/rules/csharp-architecture.md`:
   - line 28 becomes the first line below, and the second is inserted directly after it:
     ```
     | No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero`; the one exception is a protected-virtual boundary seam (ADR-007 "Exceptions") |
     | Interfaces that earn their file | Every adapter has one (ADR-007); a service gets one only when a test fakes it or a second class implements it (ADR-002) |
     ```
   - line 223 (`├── IMyFeatureService.cs`, now line 224 after the insertion) becomes
     `├── IMyFeatureService.cs     ← only if a test fakes it or a second class implements it`

**Verify**: `grep -c "IHookInterface" E:/repos/wt-plan-021/.claude/rules/harmony-patches.md` → `0`;
`grep -c "Optional layer" E:/repos/wt-plan-021/.claude/rules/csharp-patterns.md` → `1`;
`grep -c "Interfaces that earn their file" E:/repos/wt-plan-021/.claude/rules/csharp-architecture.md` → `1`.

### Step 7: The `docs/ai-includes` guides (parts A, B, C)

1. `docs/ai-includes/architecture.md`:
   - after line 108 (`- Only use adapter interfaces, never sealed types (ADR-007)`) insert two lines:
     ```
     - Engine access outside an adapter only through protected-virtual boundary seams (ADR-007 "Exceptions")
     - An `IXxxService` interface only when a test fakes it or a second implementation exists (ADR-002)
     ```
   - the three diagram lines inside the dependency-flow block (old lines 247-249, now 249-251,
     between the two fence lines at old 246 and 250) become the three lines of the unindented
     block below, copied byte for byte. Line 1 starts at column 0, line 2 with 5 spaces, line 3 with
     3 spaces; the arrows on line 2 sit under the middle of each box on line 1, and the proof
     script checks all three lines exactly:

```
Entry Point → [Hook Interface] → Service → Engine → Adapter
     ↓               ↓              ↓        ↓         ↓
   Thin     Narrow seam (opt.)    Logic  Algorithm   Wrap
```

Then, after that block's closing fence in the file (old line 250, now 252), insert one blank line
and this line:
`The hook interface is optional: a patch uses one only when it needs a narrow seam or a test fake; most patches call their service directly.`

2. `docs/ai-includes/decompiled-code-analysis.md`:
   - line 99 becomes `    → Hook Interface (IOn[EventName]), only when the patch needs a narrow seam or a test fake`
   - line 279 becomes `7. Does it fit TAOM's architecture (patches -> services -> adapters, with a hook interface only where a narrow seam or a test fake needs one)?`
3. `docs/ai-includes/patterns.md` line 9 becomes:
   `Separation between game integration and business logic. This layer is optional. Use it only when a patch needs a narrow seam over a wide service or a test fakes the hook; most patches call their service directly (AGENTS.md "Architecture").`

**Verify**: `python <scratchpad>/plan021_check.py rules` → `OK`, exit 0 (GREEN). Any line it prints
names the edit that is missing or wrong: fix that edit only, then re-run.

### Step 8: Prove nothing else broke

1. `python E:/repos/wt-plan-021/tools/lint_docs.py --fail-on-drift` → exit 0. Its Context budget
   section shows the same 7 report-only `size-warn` file names as the Step 1 baseline (the byte
   counts of `csharp-architecture.md` and `harmony-patches.md` grow; that is expected) and no new
   finding kind (no `table-row`, no entry-docs or unscoped-rules budget finding).
2. `python E:/repos/wt-plan-021/tools/lint_docs.py > <scratchpad>/lint-after.txt`, then compare the
   two full reports with every number stripped, because the report prints byte counts and line
   numbers that your edits legitimately move (for example `csharp-architecture.md` 26,239 B grows,
   and its finding at `:205` moves to `:206`):
   `diff <(sed -E 's/[0-9][0-9,]*//g' <scratchpad>/lint-before.txt) <(sed -E 's/[0-9][0-9,]*//g' <scratchpad>/lint-after.txt)`
   → no output, exit 0. If it prints a `>` line that names one of your edited files, your edit added
   a finding: STOP and report it. Report any other difference and continue. Never use `git stash`
   to rebuild a baseline.
3. No new dash: `python <scratchpad>/plan021_check.py dash` → `OK`, exit 0 (it scans every line
   added since `HEAD` in the worktree for U+2013 and U+2014; Step 9 runs it again to cover the
   CHANGELOG entry and the commit message).
4. `cd E:/repos/wt-plan-021 && python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation` → `OK`.
5. `bash E:/repos/wt-plan-021/tools/test_hooks.sh` → same exit code and summary as the Step 1
   baseline (AGENTS.md is read by `check-doc-config-drift.sh`; its "Target:" line is untouched).
6. Old rule text left anywhere tracked:
   `git -C E:/repos/wt-plan-021 grep -c -e "hook interface → service" -e "Every service has an interface" -e "Always define .IServiceName. interface" -e "IHookInterface" -- . ":(exclude)plans"`
   → exactly these five lines, and no other path:
   ```
   .serena/memories/project_overview.md:1
   README.md:1
   docs/adrs/002-thin-entry-points.md:1
   docs/ai-includes/architecture.md:1
   docs/ai-includes/patterns.md:1
   ```
   The first two are the accepted leftovers in "Out of scope". The ADR-002 match is Step 0's
   amendment quoting the old guideline (a count of 0 there is also fine). The two `docs/ai-includes`
   matches are code examples left alone by decision (`architecture.md` line 452,
   `foreach (var hook in IoC.ResolveAll<IHookInterface>())`, and `patterns.md` line 461,
   `[HarmonyPatch] → IHookInterface → IStrategy → Fluent Context`). Any other path is the STOP
   condition "another tracked file states the old rule".
7. `git -C E:/repos/wt-plan-021 status --short` → exactly the 12 rule and doc files of the Scope list
   (CHANGELOG.md comes in Step 9), each ` M`.
8. `git -C E:/repos/wt-plan-021 diff --numstat HEAD -- AGENTS.md` → one row: `1`, `1`,
   `AGENTS.md`, tab-separated (one line added, one removed).

### Step 9: CHANGELOG entry and commit

1. In `E:/repos/wt-plan-021/CHANGELOG.md` (never the main tree's copy, which holds another
   session's edits), directly below the `> **Archive:** ...` paragraph and its blank line, add a
   `## <today's date, YYYY-MM-DD>` heading (skip the heading if the first `## ` heading already has
   today's date) and this entry. Write the last paragraph only from the outputs you read in Step 8,
   with the real numbers:

```markdown
### docs(rules): v2.0.30 - align three architecture rules with the code

Three written rules disagreed with the code and with each other; sprint decision 19 (2026-09-24)
amends all three. ADR-007 now records the protected-virtual boundary seam that `RefugeService`,
`CampService`, `SupplyOrderService` and `WardenService` use, with four conditions, and ADR-002 and
ADR-008 point to it (Mike's commit on this branch). A service now gets an interface only when a
test fakes it or a second class implements it, while every adapter keeps one: the `/deep-review`
Standards lens, `think-before-coding.md`, the `feature-builder` agent and `/new-feature` say so. The
hook interface between a patch and its service is now conditional (a narrow seam or a test fake) in
AGENTS.md, `.ai/review-reference.md`, `csharp-patterns.md`, `harmony-patches.md`,
`csharp-architecture.md` and three `docs/ai-includes` guides (at `a39a9c86`, 24 of the 217 patch
files used one).

No code changed and nothing was deleted: single-implementation interfaces and one-call hooks go when
their files are next touched. Verified: <the lint, unittest and test_hooks results from Step 8>.
```

   Then `python <scratchpad>/plan021_check.py dash` → `OK` (the entry has no em or en dash).
2. Read the `<Version value="..."/>` line in both `E:/repos/wt-plan-021/Main/_Module/SubModule.xml`
   and `E:/repos/TAOM/Main/_Module/SubModule.xml` (see "Git workflow": the subject hook reads the
   second). Both must say `v2.0.30`. If either differs, STOP and report both values. Then write
   `<scratchpad>/plan021-msg.txt` with the Write tool, exactly this content (every line is at most
   72 characters; no `Co-Authored-By` or any other AI trailer):

```text
docs(rules): v2.0.30 - align three architecture rules with the code

Sprint decision 19 amends three written rules that disagreed with the
code and with each other. The ADR text (ADR-002, ADR-007, ADR-008) is
the previous commit on this branch; this one brings the rule files,
the review reference and the docs/ai-includes guides in line with it.

A, seams: the review reference, csharp-architecture.md and the
/deep-review Standards lens accept a protected-virtual boundary seam
that meets the four ADR-007 "Exceptions" conditions.

B, interfaces: a service gets an interface only when a test fakes it
or a second class implements it; every adapter keeps one. The
Standards lens, think-before-coding.md, the feature-builder agent and
/new-feature now say so.

C, hooks: the hook interface between a patch and its service is
optional (a narrow seam or a test fake) in AGENTS.md, the review
reference, csharp-patterns.md, harmony-patches.md and three
docs/ai-includes guides.

No code changed and no interface or hook was deleted; those go when
their files are next touched.

Not-tested: docs and rules only; no code changed
```

   Then `python <scratchpad>/plan021_check.py dash <scratchpad>/plan021-msg.txt` → `OK`, exit 0
   (it scans the worktree diff, CHANGELOG entry included, and every line of the message file).
3. Stage and commit with the pathspec form:
   `git -C E:/repos/wt-plan-021 add AGENTS.md .ai/review-reference.md .claude/skills/deep-review/lenses/1-standards.md .claude/rules/think-before-coding.md .claude/agents/feature-builder.md .claude/skills/new-feature/SKILL.md .claude/rules/csharp-patterns.md .claude/rules/harmony-patches.md .claude/rules/csharp-architecture.md docs/ai-includes/architecture.md docs/ai-includes/decompiled-code-analysis.md docs/ai-includes/patterns.md CHANGELOG.md`
   then the same list after `git -C E:/repos/wt-plan-021 commit -F <scratchpad>/plan021-msg.txt -- `.
   Write the `-F` path in Windows form with forward slashes (`C:/Users/.../plan021-msg.txt`), not
   `/c/Users/...`: the subject-version hook opens that file from Windows Python, and if it cannot
   read it the subject goes unchecked.

**Verify**:
- `git -C E:/repos/wt-plan-021 log -1 --format=%s | awk '{print length}'` → at most 72.
- `git -C E:/repos/wt-plan-021 log -1 --format=%B | grep -c -i "co-authored-by"` → `0`.
- `git -C E:/repos/wt-plan-021 show --name-only --format= HEAD | sort` → exactly the 13 paths above.
- `git -C E:/repos/wt-plan-021 status --short` → empty.
- `python <scratchpad>/plan021_check.py rules` and `... adr` → both `OK`.

## Test plan

- No C# changes, so no new MSTest tests; the TDD analogue is the proof script: RED in Step 1 (39
  problems), GREEN in Step 7 (`OK`). It pins every old sentence as gone and every new one as
  present, in all 12 rule and doc files plus the three ADRs, and the three new diagram lines in
  `architecture.md` byte for byte.
- Existing gates that read these files: `tools/tests/test_reviewctl.py` (AGENTS.md under 8,192 B),
  `tools/tests/test_ai_documentation.py` (links resolve inside the repo),
  `tools/lint_docs.py --fail-on-drift` (entry-doc and unscoped-rule budgets, AGENTS.md table rows at
  most 400 characters), `tools/test_hooks.sh` (hook contracts; `check-doc-config-drift.sh` reads
  AGENTS.md).
- Structurally untestable: whether a future `/deep-review` Standards pass applies the new lens text
  as intended. Name it in the commit's `Not-tested:` trailer if the orchestrator asks for detail.

## Done criteria

ALL must hold:

- [ ] `python <scratchpad>/plan021_check.py adr` exits 0 (Step 0 landed)
- [ ] `python <scratchpad>/plan021_check.py dash <scratchpad>/plan021-msg.txt` exited 0 before the commit (no em or en dash on an added line or in the message)
- [ ] `python <scratchpad>/plan021_check.py rules` exits 0 (every old sentence gone, every new one present)
- [ ] `git -C E:/repos/wt-plan-021 grep -n "IHookInterface" HEAD -- AGENTS.md .ai .claude/rules .claude/agents` returns no match
- [ ] `git -C E:/repos/wt-plan-021 grep -n "Every service has an interface" HEAD -- .claude .ai docs/adrs` returns no match
- [ ] Step 8.6's leftover grep printed exactly its five expected lines
- [ ] `python E:/repos/wt-plan-021/tools/lint_docs.py --fail-on-drift` exits 0
- [ ] `cd E:/repos/wt-plan-021 && python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation` prints `OK`
- [ ] `bash E:/repos/wt-plan-021/tools/test_hooks.sh` matches its Step 1 baseline
- [ ] `git -C E:/repos/wt-plan-021 diff --name-only a39a9c86 HEAD` lists exactly the 3 ADRs, the 12 rule and doc files and `CHANGELOG.md` (16 paths), and no `.cs`, `.xml` or `.csproj` file
- [ ] Two commits on `improve/021-architecture-rule-amendments` above `a39a9c86`, subjects at most 72 characters, no AI trailer; nothing pushed

## STOP conditions

Stop and report back (do not improvise) if:

- The worktree `E:/repos/wt-plan-021` or the branch is missing, or `plan021_check.py adr` fails:
  Step 0 has not been done. Never write the ADR text yourself, by any tool or shell command.
- An Edit or Write call is denied by the config-protection hook (it means you touched a protected
  path such as `docs/adrs/*.md`, `settings.json` or `Directory.Build.props`).
- The drift check shows a quoted line changed on `bannerlord-1.5.x`, or the Step 1 RED run does not
  end with exactly `39 problem(s)`.
- `lint_docs.py --fail-on-drift` fails with a finding your edit caused (for example a `table-row`
  over 400 characters in AGENTS.md, or an entry-docs or unscoped-rules budget over its cap) and the
  fix would mean rewording the decided rule text rather than trimming your own added words.
- Step 8.6's grep prints a path beyond its five expected lines (another tracked file states the
  old rule as mandatory): report its path and line; do not edit it.
- Step 8.2's normalized lint diff prints a `>` line naming one of your edited files.
- The two `SubModule.xml` copies read in Step 9.2 disagree, or either is not `v2.0.30`.
- Any step seems to need a code change: deleting an interface, folding a hook
  (`RecruitmentResourceGateHook`, `CultureStageViewFinalizeHook`), seaming a service, or touching
  `Main/IoC.cs` or `Main/SubModule.cs`. All of that is out of scope by decision.
- Any hook denies a commit, or the commit would include a path outside the 13 listed.
- You find you would have to work in `E:\repos\TAOM`'s own working tree.

## Maintenance notes

- **What changed in meaning**: reviewers stop demanding a service interface or a hook layer by
  default, and stop grading a recorded seam as CRITICAL. Watch the next few `/deep-review` Standards
  reports for the opposite failure: a lens flagging every existing single-implementation interface.
  The lens text asks only about *new* ones.
- **What a reviewer should scrutinize**: that the lens line 8 exception and the review-reference
  line 21 wording cannot be read as permission for engine use anywhere in a service (the conditions
  live only in ADR-007; the lens points there rather than restating them, per ADR-011); that the
  four ADR-007 conditions Mike chose match the seams that exist (see the Step 0 note on
  `SpawnRefugeParty` and `IsCampReady`); that no new line carries an em or en dash.
- **Deferred, not in this plan**:
  - Deleting unfaked single-implementation interfaces: opportunistic, when a file is next touched
    (decision; a bulk PR would touch every feature IoC file and the single-owner `Main/IoC.cs`).
  - Folding the one-call hooks `RecruitmentResourceGateHook` and `CultureStageViewFinalizeHook` into
    their patches: when those files are next touched.
  - Seaming the services that read engine statics with no seam: not planned here by decision. The
    checker found the two lead candidates (`SupplySourceService`, `SupplyCaravanService`) are
    self-declared engine boundaries already faked through their interfaces, so they are not the
    debt the audit first described (`verify-a-batch-02.md`, ARCH-05, correction 2).
  - Extracting the nine seams copied between `CampService` and `RefugeService` into a shared adapter:
    rejected under `simplicity-criterion.md` until a fourth service copies them.
  - `README.md:96` and `.serena/memories/project_overview.md:16` still draw the hook layer as the
    architecture: a one-line follow-up each, outside this plan's rule-source scope.
  - `docs/ai-includes/architecture.md:149` "One adapter interface per sealed type" (the audit
    suggested "per sealed type per feature need"): undecided wording, left alone.
  - ADR-007 "Architecture Tests (Recommended)" cites `TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs`,
    which does not exist at `a39a9c86` (`git ls-tree -r a39a9c86 TAOM.Tests` has no `Architecture`
    path): a stale reference for whoever next edits ADR-007.

# Deep review: plan 021, architecture rule amendments (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 021, amend three architecture rules to match the code (boundary seams, when a
         service gets an interface, when a patch gets a hook)
Branch:  improve/021-architecture-rule-amendments, worktree E:\repos\taom-improve\wt-021
Diff:    bec0389d..7d1b7a54 (1cdf8eb0 ADRs, 9c29d732 rules and CHANGELOG, 7d1b7a54 Codex-round
         ADR fixes); 16 Markdown files, no C#, XML, XSLT or hook file
Date:    2026-09-25 (plan dated 2026-09-24)

Scope:   harness and docs (rules, lens, agent, skills, review reference, ADRs, ai-includes guides)
Waves:   one wave: Agent 1 Standards, Agent 4 Completeness, Agent 5 Data flow, Agent 6 Design;
         Codex adversarial (gpt-6-astra, ultra) on bec0389d..9c29d732

STANDARDS:     FAIL, 2 MEDIUM + 8 LOW (0 CRITICAL, 0 HIGH); the MEDIUM rule conflict is fixed,
               the other MEDIUM needs ADR wording from Mike
COMPATIBILITY: NOT IN SCOPE (no engine-facing code changed)
EFFICIENCY:    NOT IN SCOPE (no code changed)
COMPLETENESS:  INCOMPLETE: GitHub issue missing (needs Mike, public); Codex prompt file untracked
               (orchestrator's file); the rest fixed here or listed for the orchestrator
DATA FLOW:     FAIL, 3 gaps + 5 inconsistencies, none HIGH; 5 fixed here, 3 need ADR wording
DESIGN:        6 KEEP proposals (2 apply, 4 follow-up); both APPLY proposals applied
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE (no script or hook changed)
```

## Verification of every finding

Each finding was re-read against the worktree before it was classified. Findings raised by more
than one lens are merged; the source column names every lens that raised it.

### CONFIRMED and fixed on this branch (review follow-up commit)

| # | Sev | Finding | Source | Fix |
|---|---|---|---|---|
| C1 | MEDIUM | `.claude/rules/think-before-coding.md:33-34` (unscoped, always loaded) allowed an interface only for an adapter, a test fake or a second implementation, so it forbade the narrow hook interface that `AGENTS.md:36`, `csharp-patterns.md:13`, `harmony-patches.md:53` and the review reference allow. Proof: `IOnPartyUpgradeResourceCheck`, the rule's own example, has one implementer (`PartyUpgradeResourceCheckHook.cs:3`), and `git grep IOnPartyUpgradeResourceCheck -- Main TAOM.Tests` finds no test reference | Codex P2 1; A1 M1; A5 A; A4 F1; A6 KEEP 1 | Item 4 now adds "or it narrows a wide service for a patch (a hook, AGENTS.md "Architecture")". Unscoped rule 2,027 to 2,106 B |
| C2 | LOW | `1-standards.md:8` said "One exception" (the seam) while ADR-007 "Exceptions" also exempts the value types (`Vec2`, `TextObject`, `ExplainedNumber`, ADR-007:554-560), so the lens would flag `new TextObject(...)` in `RefugeService` and a seam's own `Vec2` output | A1 L1; A5 D | Check 1 names both exceptions |
| C3 | LOW | `docs/reference/engine/submodule-lifecycle-and-harmony.md:45` and `:50` still stated the mandatory `→ IHook →` one-liner; the plan's Step 8.6 grep searched only `IHookInterface`. AGENTS.md "Research first" sends agents to this folder first | A1 follow-up; A5 C; A4 F2 | Both lines now read `→ Service → IAdapter`, with the conditional hook noted on `:50` |
| C4 | LOW | `docs/ai-includes/decompiled-code-analysis.md:107-108` registered `IFeatureService` and `IOnSomeEvent` unconditionally, and Phase 4 (`:121-139`) told an agent to always create `IOn[EventName].cs`, `I[Feature]Service.cs` and `[Feature]HookTests.cs`, contradicting line `:99` of the same file, which this diff made conditional | A5 E | Concrete registration by default, the two interface registrations commented with their conditions, the tree and test rows annotated |
| C5 | LOW | The seam exception (part A) never reached two prompts that grade ADR-007: `.claude/skills/improve/references/audit-playbook.md:68` (the audit that raised ARCH-05 would flag the 94 seams again) and `.claude/skills/codex-verify/SKILL.md:50` | A5 F; A4 F3 | Each now excepts a seam that meets ADR-007 "Exceptions" |
| C6 | LOW | `.ai/review-reference.md:21`, a line this diff rewrote, still named the installed engine "(v1.5.2)"; the pin is v1.5.3 (`.claude/pinned-game-version.txt`) | A1 L5; A4 follow-up; A6 note | Points to the pin file instead of a version number, as `harmony-patches.md:39` does |
| C7 | LOW | `docs/ai-includes/architecture.md:112` "Fully unit testable with mocked adapters" sat three lines under the new seam bullet and did not match ADR-002 guideline 7 | A1 L4; A5 E; A4 F5 | Adds "or a test subclass that overrides its boundary seams" |
| C8 | LOW | `CHANGELOG.md:12` credited the ADR text to "Mike's commit"; `1cdf8eb0` says the orchestrator made it on Mike's approval, and `7d1b7a54` had no line | A1 L6; A5 H; A4 CHANGELOG | Provenance corrected, `7d1b7a54` described, review follow-ups paragraph added |
| C9 | LOW | `csharp-patterns.md:13` (a path-scoped rule, tier 3) added the uncomputed count "for three patches", which CLAUDE.md "Where new knowledge goes" forbids | A6 note | "for the party-upgrade patches" |

### CONFIRMED and fixed by the orchestrator (inside the reviewed diff)

| # | Finding | Verification |
|---|---|---|
| Codex P2 2 | ADR-008's exception allowed only "a static read", while ADR-007 condition 2 allows the engine calls for one action (`RefugeService.ChargePlayer`/`RefundPlayer` mutate gold through `GiveGoldAction`) | FIXED (orchestrator, 7d1b7a54). `docs/adrs/008-testability-requirements.md:15` now reads "a static method call or property access inside a protected-virtual boundary seam" |
| Codex P2 3 | ADR-002 migration step 4 always created a service interface | FIXED (orchestrator, 7d1b7a54). `docs/adrs/002-thin-entry-points.md:314` now creates one "only when a test fakes the service or a second implementation exists (Service Design Guideline 2)" |

### ADR changes for the orchestrator

The ADRs are protected; none of these was edited here. O1 and O2 are decisions (NEEDS MIKE);
O3 to O6 are one-line contradictions the amendment introduced or left beside its new text.

| # | Sev | File:line | Old text | New text | Real contradiction? |
|---|---|---|---|---|---|
| O1 | MEDIUM | `docs/adrs/007-adapter-pattern.md`, after condition 4 (`:596`) | (none) | "A private helper that only seams call, such as an id lookup (`FindParty`, `FindHero`), is part of the seam body: condition 1 applies to the seam that calls it, not to the helper's own signature." Mirror in `1-standards.md:8`: "...and any other TaleWorlds use outside a seam or a private helper only seams call." | NEEDS MIKE. Verified: `RefugeService.cs:1218-1225` (`FindParty` returns `MobileParty`, `FindHero` returns `Hero`, `FindTroop` returns `CharacterObject`), `:1008` `RemoveRegularRows(TroopRoster)`, `:1108` `PinPartyAi(MobileParty)`, `SupplyOrderService.cs:518` and `WardenService.cs:293` `FindHero`; each is called from seam bodies (29 call sites in Refuge, per Agent 1's map; spot-checked `:821-1208`). As written the ADR flags the pattern's own exemplars |
| O2 | MEDIUM | `007-adapter-pattern.md:598-599` ("Why:") | "`RefugeService`, `CampService`, `SupplyOrderService` and `WardenService` use this pattern, each tested through one subclass" | Either "...use this pattern, tested through test subclasses. Seams written before 2026-09-24 that carry TAOM decision logic (`SupplyOrderService.ChargePlayer`, `RefugeService.FindNearestHostile`, `WardenService.MintCompanionFromTroop`, `WardenService.CompanionsInMainParty`) break condition 2 and are known debt, moved into the service when their file is next touched." or refactor those seams (Agent 6 KEEP 6, a code follow-up) | NEEDS MIKE. Verified: `ChargePlayer` (`SupplyOrderService.cs:477-514`) splits goods plus troops from fees and routes or destroys the payee's share; `FindNearestHostile` (`RefugeService.cs:1168-1199`) applies six filters and picks the nearest; `MintCompanionFromTroop` (`WardenService.cs:200-235`) applies an age policy and a culture-matched template fallback; `CompanionsInMainParty` (`:128-151`) filters eligibility. Also: `RefugeServiceTests.cs` has two subclasses (`:27`, `:1713`), so "one subclass" is wrong |
| O3 | LOW | `007-adapter-pattern.md:28` | "- **Singletons**: `MBObjectManager`, `CampaignTime`" | "- **Singletons**: `MBObjectManager`" | Yes: condition 1 (`:590`) allows `CampaignTime` as an engine struct in seam signatures, while `:28` lists it among "Sealed Classes Requiring Adapters". Codex quoted `public struct CampaignTime : IComparable<CampaignTime>` from the v1.5.3 dump; Agents 1 and 5 read the same from the installed-DLL decompile |
| O4 | LOW | `007-adapter-pattern.md:591` | "under "Value Types Don't Need Adapters"; never a sealed TaleWorlds class." | "under "Value Types Don't Need Adapters"; never any other TaleWorlds reference type." | Tightening, not a contradiction: `TroopRoster`, `ItemRoster`, `Equipment`, `Town`, `Village` are unsealed classes that `:23-25` says need adapters (Agent 1, v1.5.3). No current seam signature uses one |
| O5 | LOW | `008-testability-requirements.md:232` and `:263` | "- [ ] No direct `CampaignTime.X` calls in services" and "Entry points may use static calls, but services cannot" | "- [ ] No direct `CampaignTime.X` calls in services outside a boundary seam (Rule 1 exception)" and "Entry points may use static calls; services only inside a boundary seam (ADR-007 "Exceptions")" | Yes: the same file's `:15` allows exactly these calls; seams at `RefugeService.cs:810-814`, `CampService.cs:868-870`, `SupplyOrderService.cs:463-465` call `CampaignTime.Now` |
| O6 | LOW | `002-thin-entry-points.md:303` | "- [ ] Service has unit tests using mocked adapters" | "- [ ] Service has unit tests using mocked adapters, or a test subclass that overrides its boundary seams" | Yes, narrower than guideline 7 (`:252`) in the same file; ADR-007's matching checklist line (`:784`) was updated |

Smaller ADR items, not contradictions (follow-up): ADR-008 Rule 3 (`:87` table) requires four
provider interfaces that exist nowhere (`git grep -w ICampaignTimeProvider -- Main TAOM.Tests` finds
nothing, per Agent 6); ADR-008 revision history (`:331-332`) lists only 2025-01-22; `docs/adrs/README.md:33-34`
has no pointer to the exception; ADR-002 `:27` diagram and `:331` assume a service interface; ADR-007
condition 3 and `:777` say "the service's interface"; ADR-007 `:789` cites
`TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs`, which does not exist.

### NEEDS MIKE (not ADR text)

- **GitHub issue missing** (A4): plan 021 `:54` requires one; issue creation is public and needs
  consent (and the GitHub MCP is unauthenticated in this session).
- **"Seam" has three meanings** (A1 L8, A5 G): the hook case ("narrow seam"), the ADR-007 case
  ("boundary seam") and a patch target. Renaming the hook wording ("a narrow interface over a wide
  service") touches about 12 plan-dictated lines; optional.

### FALSE POSITIVE (with reason)

- **A4 F3, `.claude/skills/deep-review/lenses/adversarial.md`**: the adversarial lens runs only on
  files Agent 1 flagged CRITICAL, and Agent 1 now applies the seam exception before flagging; the
  prove-it framing is the lens's design, so it needs no exception of its own.
- **A4 F3, `.ai/review-reference.md:174-182` "Intentional Patterns" list**: the exception is already
  stated where severity is assigned (`:21`) and in the rules table (`:249`); a third copy adds text,
  not coverage.

## DETAILS

### Agent 1, Standards
CRITICAL 0, HIGH 0, MEDIUM 2 (M1 = C1, M2 = O1), LOW 8 (L1 = C2, L2 = O3, L3 = O4 plus the O2 nit,
L4 = C7 and O5/O6, L5 = C6, L6 = C8, L7 and L8 not applied, see below). H4 prose and H5 budgets
passed. Follow-ups listed under FOLLOW-UP.

### Agent 4, Completeness
INCOMPLETE. F1 = C1; F2 = C3; F3 = C5 (two prompts fixed, two parts false positive); F4 = O3, O5, O6
and the ADR follow-ups; F5 = C7 (in-scope line), the coverage tables are follow-up; F6: the REVIEW-LOG
entry is added by this commit, the prompt file
`docs/reviews/codex-adversarial-021-architecture-rule-amendments-2026-09-24.prompt.md` is untracked
and left for the orchestrator (not this session's file). GitHub issue: NEEDS MIKE. CHANGELOG
partial = C8.

### Agent 5, Data flow
19 flows traced. A = C1; B = O1 and O2; C = C3; D = C2; E = C4, C7 and O3, O5, O6 plus ADR follow-ups;
F = C5; G not applied; H = C8.

### Agent 6, Design
KEEP 1 (think-before-coding hook clause, APPLY, CHANGING): applied as the C1 fix on the
orchestrator's instruction to fix Codex P2 1. KEEP 2 ("engine" to "TaleWorlds", APPLY, PRESERVING):
applied. KEEPs 3 to 6: FOLLOW-UP.

### Codex adversarial
Complete ("END OF CODEX REVIEW" present). See CODEX REVIEW below.

## ACTION ITEMS

1. Orchestrator: apply O3, O5 and O6 (one-line ADR contradictions) under the ADR bypass. DONE
   `b22edd47` (2026-09-25).
2. Mike: decide O1 (private helpers in the seam body) and O2 (known-debt note or refactor). DONE:
   decision 55 (O1, applied) and decision 56 (O2, refactor in plan 026). O4 went to Mike as well,
   because it rewords decision 49; still open.
3. Mike: consent to a GitHub issue for plan 021, or waive it. Covered by Mike's standing request to
   file an issue for every sprint plan; the orchestrator files it at merge.
4. Orchestrator: commit or drop the untracked Codex prompt file. DONE `16e45bf8`.

## IMPROVEMENTS (Step 4)

APPLIED:
- `.claude/rules/think-before-coding.md:33-35`: the hook clause (Agent 6 KEEP 1, the C1 fix).
  Proof: before, `git show 7d1b7a54:.claude/rules/think-before-coding.md` has no hook case; after,
  item 4 lists it. `lint_docs.py --fail-on-drift` exit 0, `tools.tests.test_reviewctl` and
  `tools.tests.test_ai_documentation` 47 OK.
- `.claude/skills/deep-review/lenses/1-standards.md:8` and `docs/ai-includes/architecture.md:109`:
  "engine use" / "Engine access" to "TaleWorlds use" / "TaleWorlds access" (Agent 6 KEEP 2,
  PRESERVING), so TAOM's own `Engine` layer (`ISupplyOrderEngine`, `ICultureBonusEngine`) is not
  read as banned. Same proof commands.

NOT APPLIED:
- `CHANGELOG.md:5` date heading (A1 L6): the entry sits under `## 2026-09-24` though the commits are
  2026-09-25; the file already has a `## 2026-09-25` section lower down, and every sprint branch adds
  under the top heading, so the orchestrator orders headings at merge.
- `feature-builder.md:26` restating AGENTS.md (A1 L7): plan-dictated text, optional.
- Renaming the hook-case "seam" (A1 L8, A5 G): needs Mike (see above).
- Private-helper clause in `1-standards.md:8` (A1 M2 second half): held until Mike decides O1, so the
  lens and ADR-007 do not diverge.
- `refactoring-specialist.md:69` and `.claude/rules/adapters.md:11` (A4 follow-up): builder-side
  absolutes the plan left by design; follow-up.

FOLLOW-UP (pre-existing, not changed by this diff; no issue filed: issue creation is public and
needs consent):
- Hook wiring documented as `IoC.ResolveAll<IOnXxx>()` per call (`csharp-patterns.md:22-37`,
  `review-reference.md:401`, `patterns.md:61`, `architecture.md:76`); the code resolves once and
  passes the hook to a static `Initialize` (Agent 6 KEEP 3, A5).
- Layer labels `Service (IXxxService)` (`csharp-architecture.md:16`, `review-reference.md:208`),
  `docs/features/TEMPLATE.md:58`, `docs/INDEX.md:235`, `README.md:96`, `.serena/memories/*`
  (Agent 6 KEEP 5, A1, A4, A5).
- Coverage tables at 100% with no seam-body note (`tests.md:47`, `csharp-architecture.md:45`,
  `review-reference.md:266`, `architecture.md:322`, `:439`) (A4 F5).
- Seams carrying decision logic moved into their services (Agent 6 KEEP 6), tied to O2.
- ADR-008 Rule 3 providers that were never built (Agent 6 KEEP 4).
- No Standards check for a new hook that only forwards one call (A1, A5).
- `PartyUpgradeResourceCheckHook` has no tests (A5).
- Stale v1.5.2 at `feature-builder.md:36`, `review-reference.md:321`, `:559`,
  `.claude/skills/codex-verify/SKILL.md:52` (added by the convergence pass); `feature-builder.md:87`
  names `./build.ps1 -RunTests` for a subagent; `new-feature/SKILL.md:3` description form;
  `IoC.Resolve` inside GameModel override examples (`csharp-patterns.md:64`,
  `review-reference.md:364`); ADR-007 `:789` missing test file (A1).

VERDICT: NEEDS FIXES. Every finding in an unprotected file is fixed and the full suite is green;
three one-line ADR contradictions (O3, O5, O6) remain for the orchestrator, and O1 and O2 need
Mike's wording.

Update 2026-09-25 (orchestrator): O3, O5 and O6 are applied, O1 and O2 are decided and handled (see
"Orchestrator follow-ups" at the end); the NEEDS FIXES items are resolved, subject to the final
convergence pass recorded there.

## Verification run for this report

- Full suite in the worktree, before and after the edits (docs only, no C# changed):
  `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`, both runs:
  `Passed!  - Failed:     0, Passed: 10629, Skipped:     2, Total: 10631` (the branch contains
  `a39a9c86`, so no failure was allowed).
- `python tools/lint_docs.py --fail-on-drift --summary --dash-base bec0389d`: exit 0, 8 findings
  (7 report-only context-budget warnings, 1 missing TrollBruteForce feature doc, both pre-existing),
  ai_dashes 0, dead_links 0.
- `python -m unittest tools.tests.test_reviewctl tools.tests.test_ai_documentation`: 47 tests OK.
- No U+2013 or U+2014 on any added line (checked with a Python scan of `git diff -U0`).
- `bash tools/test_hooks.sh` not run: no hook or skill frontmatter changed.

## CODEX REVIEW

Codex (gpt-6-astra, reasoning ultra, 233,952 tokens) reviewed `bec0389d..9c29d732` through git refs.
Complete: the file ends "END OF CODEX REVIEW". It quoted v1.5.3 declarations for `CampaignTime`,
`Vec2`, `MobileParty.MainParty` and `GiveGoldAction`, the three `PartyUpgradeResourceCheckHook`
patch targets, and cross-referenced every changed reference.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | MEDIUM | Yes | `think-before-coding.md:33-34` forbade the one-implementation hook that `csharp-patterns.md:13` names; confirmed by `git grep`. Fixed here (C1) |
| 2 | P2 | MEDIUM | Yes | ADR-008 `:15` allowed only a static read; `RefugeService.cs:793-799` seams mutate gold. FIXED (orchestrator, 7d1b7a54) |
| 3 | P2 | LOW | Yes | ADR-002 `:314` migration step 4 always created a service interface. FIXED (orchestrator, 7d1b7a54) |

Known suspects: 1 (plan's hardcoded worktree path absent) confirmed as stated, no defect in the
diff; 2 and 6 UNVERIFIED by Codex (no transcripts supplied), agreed; 3, 4, 5, 7, 8 and 10 disputed,
agreed (lint and budget later confirmed by Agents 1, 4 and 5 running `lint_docs.py`); 9 (the proof
script checks substrings, not agreement between sentences) confirmed, and findings 1 to 3 plus C2 to
C5 show the gap.

**Confirmed bugs:** 1, 2, 3 above. **False positives:** none. **Design questions:** none from Codex.
**Things Codex missed:** O1 and O2 (the ADR's own exemplars break its conditions), O3 (`CampaignTime`
listed as a sealed class), O5 and O6 (checklists left beside the amended rules), C2 (value-type
exception), C3 (`→ IHook →` leftover), C4 (Phase 4 procedure), C5 (audit prompts). Codex checked the
sentences the plan prescribed against each other; it did not sweep the rest of the repo for other
statements of the three rules.

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | General interface rule forbids the permitted hook case | Convention inconsistency | The plan amended the specific rules and the general rule in separate steps and never read the two sentences side by side | Lesson in `lessons/misc.md` (a new permitted case amends every general rule it is an exception to) |
| 2 | ADR-008 exception narrower than ADR-007 | Convention inconsistency | Step 0 wording written before decision 49 chose the looser seam variant | Same lesson |
| 3 | ADR-002 migration step still always creates an interface | Other: incomplete sweep | The plan's ADR-002 edits listed the guideline and checklist, not the procedure | Recurrence note on `lessons/misc.md` "A plan's stale-claim grep is a floor" |

## AGENTS.md lessons (pending)

For the consolidated Phase 3h edit, not made here:
- **Bugs Codex typically misses:** other statements of a rule outside the files a plan names (the
  `→ IHook →` one-liner, audit prompts, procedure steps); an ADR's cited exemplars checked against
  the ADR's own conditions.
- **What Codex does well:** reading a documentation-only diff as behaviour (who follows which
  sentence and what they build), and quoting the engine declarations a rule relies on.

RCA: `docs/reviews/rca-architecture-rule-amendments-2026-09-24.md`.

## Convergence

Convergence pass on `d4e6273a` (`git diff 7d1b7a54..HEAD`, 14 Markdown files, no C#, XML, XSLT or
hook file). Four LOW defects reported; each was re-read against the worktree and all four are
CONFIRMED. No false positives.

| # | Sev | Finding | Verification | Fix |
|---|---|---|---|---|
| V1 | LOW | `docs/ai-includes/architecture.md:109` (Agent 6 KEEP 2) allowed TaleWorlds access outside an adapter only through seams, so it forbade the value types ADR-007 "Exceptions" lists (`Vec2`, `Vec3`, `TextObject`, `ExplainedNumber`, ADR-007:554-560), which the Standards lens (`1-standards.md:8`) accepts | `RefugeService.Dismantle` (`RefugeService.cs:283`) is public, not a seam, and builds `new TextObject(...)` at `:305` | Mirrors the lens: "other than the value types, only through protected-virtual boundary seams" |
| V2 | LOW | `docs/ai-includes/decompiled-code-analysis.md:155`, the Phase 4 patch sample, still resolved `IFeatureService`, while `:107` now registers only `FeatureService` by default and `:131` makes the interface optional | `IoC.Resolve<T>` is `_container.Resolve<T>()` (`Main/IoC.cs:257-259`) on a plain `new Container()` (`:92`); an unregistered interface throws and the sample's empty `catch` (`:158`) swallows it | The sample resolves `FeatureService`; the `:108` comment says the interface registration replaces `:107` rather than sitting beside it |
| V3 | LOW | `CHANGELOG.md:32` and `REVIEW-LOG.md:4687` said the ADR wording was listed for Mike; this report assigns O3, O5 and O6 to the orchestrator (ACTION ITEMS 1) and only O1 and O2 to Mike (item 2) | Report "ADR changes for the orchestrator" table and ACTION ITEMS read this pass | Both now say orchestrator (O3, O5, O6) and Mike (O1, O2) |
| V4 | LOW | `rca-architecture-rule-amendments-2026-09-24.md`: Agent 4's missed list omitted C9 (Source "A6 note" only) and Agent 6's found list omitted C6 (Source includes "A6 note") | C6 and C9 rows of the CONFIRMED table above | C9 added to Agent 4's missed list, C6 to Agent 6's found list |

**Not changed (open for the orchestrator):**

- `CHANGELOG.md:11` says both orchestrator commits were "made on Mike's approval". The `1cdf8eb0`
  body says so; the `7d1b7a54` body cites only the protected-file bypass. The approval for
  `7d1b7a54` is not in any file this pass read, so the line was left as is: confirm or narrow it.
- `.claude/skills/codex-verify/SKILL.md:52` still says "installed v1.5.2 DLLs" (pre-existing, outside
  the fix lines); add it to the FOLLOW-UP list of stale v1.5.2 references.

No test pins this text; all four fixes are documentation, so there was no failing test to write
first.

## Orchestrator follow-ups (2026-09-25)

The ADRs are protected, so their edits are the orchestrator's, under the bypass Mike granted for
plan 021.

| Commit | What |
|---|---|
| `b22edd47` | O3, O5 and O6 applied as the "ADR changes for the orchestrator" table gives them; the CHANGELOG now states the approval behind each ADR commit (open item 1 of the Convergence section). O4 not applied: it rewords condition 1 as Mike chose it in decision 49, so it is his call |
| `9dcf1a4d` | CHANGELOG entry rewrapped at 100 columns, and "a third" named `b22edd47` |
| `16e45bf8` | The Codex prompt file committed (ACTION ITEM 4) |
| `6a2ce8cb` | Decisions 55 and 56 plus the three defects of the convergence pass below |
| the record commit after `6a2ce8cb` | The three record defects of the final convergence pass below, and the ADR-007 wording "the plan 021 review found four seams" |

**Mike's decisions.** Decision 55 (O1): a private helper that only seams call is part of the seam
body; one sentence in ADR-007 after condition 4, mirrored in the Standards lens check 1. Decision 56
(O2): refactor the four seams that carry decision logic; that is new plan 026
(`improve/026-seam-decision-logic`), and ADR-007's "Why" now names the four seams and the plan, and
says "test subclasses" (RefugeServiceTests has two) instead of "one subclass".

**Convergence pass on `e809f258..16e45bf8`** (one deep-reviewer): CONVERGENCE: DEFECTS 3, all LOW,
all confirmed and fixed in `6a2ce8cb`.

| # | Defect | Fix |
|---|---|---|
| C-D1 | O5 left ADR-008's recommended CI step grepping `CampaignTime.Now` in services with no seam exemption, contradicting the edited checklist line; no workflow runs it | Step removed; one sentence says static calls in services are a review check, because a grep cannot tell a seam from a violation |
| C-D2 | CHANGELOG listed `csharp-architecture.md` among the files that made the hook interface conditional; it changed only interface and seam rows (from the plan's CHANGELOG template) | Moved into the interface sentence |
| C-D3 | This report's ACTION ITEMS and VERDICT, and the REVIEW-LOG entry, still showed O3, O5 and O6 as owed and O4 with the orchestrator | ACTION ITEMS and VERDICT annotated, REVIEW-LOG entry updated, this section added |

**Final convergence pass on `16e45bf8..6a2ce8cb`** (one deep-reviewer): the ADR-007, lens and
ADR-008 edits match the commit and add no contradiction; CONVERGENCE: DEFECTS 3, all LOW and all in
the review records, confirmed and fixed in the record commit after `6a2ce8cb`.

| # | Defect | Fix |
|---|---|---|
| F-D1 | The table above credited `9dcf1a4d` with the approval wording that `b22edd47` wrote, and named `6a2ce8cb` "last commit" | Rows corrected, hashes named |
| F-D2 | REVIEW-LOG's update did not say how the GitHub issue is settled | Update sentence names it |
| F-D3 | The first pass's open item 2 (`codex-verify/SKILL.md:52` still says v1.5.2) was dropped without a record | Added to the FOLLOW-UP bullet of stale v1.5.2 references |

The same pass noted that ADR-007 presented "four seams" as a complete list, while three more seams
hold similar filter or routing logic (`SupplyOrderService.RefundConsumption`,
`RefugeService.ReleasePeacePrisoners`, `CampService.DistanceToNearestFortification`). ADR-007 now
says the review found four and that a seam breaking condition 2 is a finding wherever it is; whether
plan 026 takes the other three is a question for Mike. These last edits are records and one ADR
sentence, checked by the orchestrator directly (text, hashes, dashes, line endings); no further
reviewer pass was run, because each pass over review records produces new records to review.

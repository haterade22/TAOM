# Deep review: plan 003, the BattleBalanceSettingsProvider cache (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 003 (PERF-04 only, decision 17): BattleBalanceSettingsProvider takes the MCM
         settings reference once instead of on every read
Branch:  improve/003-battlebalance-settings-cache, diff a39a9c86..7feca96b (3 files)
Date:    2026-09-24

Scope:   C# (one provider, one test file), CHANGELOG. No XML, XSLT, scripts or harness.
Waves:   wave 1 lenses 1, 2, 5; wave 2 lenses 3, 4, 6. Codex gpt-6-astra (ultra) in parallel.

STANDARDS:     PASS (code); 5 process/doc items (1 MEDIUM: no GitHub issue; 4 LOW)
COMPATIBILITY: PASS, 0 incompatible, 1 unverified (suite count, now verified below)
EFFICIENCY:    PASS, 0 issues in the changed code; 1 LOW comment accuracy fix (APPLY), 2 FOLLOW-UP
COMPLETENESS:  INCOMPLETE at 7feca96b: no issue, stale feature doc, no read-through test
DATA FLOW:     PASS, 10 flows, 0 gaps, 1 inconsistency (stale docs), 1 latent risk (ctor null pin)
DESIGN:        3 KEEP proposals (3 apply, 0 follow-up)
XML:           NOT IN SCOPE (no ModuleData, XSLT or prefab in the diff)
TOOLING:       NOT IN SCOPE (no scripts or hooks in the diff)
```

## Details

Every finding below was re-read against the worktree before it was classified. The engine and MCM
facts the lenses relied on were spot-checked: the only resolve of `IBattleBalanceSettingsProvider`
is `Main/SubModule.cs:1144` (grep of `Main`), the registration is `Reuse.Singleton` at
`BattleBalanceIoC.cs:10`, and MCM 5.12.3's `DefaultSettingsProvider.GetSettings` walks the
containers and logs `LogWarning("GetSettings " + id + " returned null")` on every miss
(`E:\Decompiled_Bannerlord\_modules_build\TAOM.Dependencies__MCMv5.cs:2214-2233`).

### Agent 1: Standards

C# checks 1 to 10 pass (no banned construct, no `IoC.Resolve`, no TaleWorlds type, registration
unchanged, provider byte-identical to `6eb5955c`).

| # | Sev | Finding | Verdict |
|---|---|---|---|
| S1 | MEDIUM | No GitHub issue for plan 003; the plan's Status block requires one before implementation lands | CONFIRMED, NEEDS MIKE (`/issue` is public and never auto-invoked; decision 17 is silent on issues for ports) |
| S2 | LOW | `docs/features/battle-balance.md:29,103` and `docs/modding/file-catalogue.md:269` describe the removed per-access proxy and old line numbers | CONFIRMED, fixed |
| S3 | LOW | Heading and commit subject say "read the MCM settings once"; only the reference is taken once | CONFIRMED; the commit subject cannot change without rewriting history (forbidden here), and the CHANGELOG heading mirrors it. NOT APPLIED, recorded |
| S4 | LOW | CHANGELOG "checked in code" (nothing enforces it) and "that branch's warg half" (impl-003 has four commits) | CONFIRMED, fixed in place; the precondition itself is gone with the lazy accessor |
| S5 | LOW | Commit `7feca96b` body line 12 is 76 characters | CONFIRMED (awk); NOT APPLIED, needs a history rewrite |
| S6 | LOW (better way) | `CollectionAssert.AreEqual` on a count mismatch never prints the offending getter names | CONFIRMED, applied (`Assert.AreEqual(0, offenders.Count, ... string.Join(...))`) |
| S7 | LOW (better way) | The constructor-read precondition is written only in the CHANGELOG | CONFIRMED, removed by the lazy accessor (see D3) |
| S8 | LOW (better way) | Field should be `TaomSettings?` | CONFIRMED, applied with the lazy accessor |
| S9 | INFO | Comment says "ConcurrentDictionary walk ... was the leak" | CONFIRMED (two lookups plus a container walk; nothing leaked), comment rewritten |

### Agent 2: Engine compatibility

12 claims verified, 0 incompatible. The one real risk is lifecycle order, not signatures:
`BaseSettingsProvider.Instance` is set only in `MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot`,
so a constructor read during `OnSubModuleLoad` would cache null for the process.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| C1 | LOW | Constructor caching adds an unenforced ordering precondition; a future eager resolve silently pins the defaults | CONFIRMED (latent). Agent 2 preferred keeping the constructor form plus a comment; the lazy accessor was applied instead (D3), see the trade-off there |
| C2 | INFO | Comment wording "a ConcurrentDictionary walk" | CONFIRMED, rewritten |
| C3 | INFO | Test comment "(MCM v5 not loaded)": the DLL is loaded, MCM is never initialised | CONFIRMED, rewritten |
| C4 | UNVERIFIED | Suite count | Now verified: see the final run below |

### Agent 3: Efficiency

No performance issue in the changed code.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| E1 | LOW, APPLY | Comment and CHANGELOG say "per troop per simulation round"; the engine also calls `GetDefaultTroopPower` twice per XP-scored hit in live battles (`DefaultCombatXpModel.GetXpFromHit`), per casualty and per roster row in `GetPowerOfParty` | CONFIRMED (the four lenses and Codex agree on the call sites), applied to the comment and the CHANGELOG |
| E2 | LOW, FOLLOW-UP | `BattleBalanceConfig.GetTierPower` boxes and formats `"T{0}"` per call | Pre-existing, outside the diff. Listed |
| E3 | MEDIUM, FOLLOW-UP | About 100 other `=> TaomSettings.Instance?.` getters in 40 files, `CombatMechanicsSettingsProvider` the hottest | Pre-existing, outside the diff. Listed |

### Agent 4: Completeness

| # | Sev | Finding | Verdict |
|---|---|---|---|
| P1 | MEDIUM | Nothing tests that getters read THROUGH the cached reference: a constructor snapshot to fields, or `Tier8Power` wired to `Tier9Power`, passes all seven tests | CONFIRMED (same as Codex F1 and Agent 6 D1), fixed with a behavioural read-through test |
| P2 | LOW | Six of twelve fallbacks unpinned | CONFIRMED, fixed (D2) |
| P3 | LOW | Precondition only in the CHANGELOG | CONFIRMED, removed by D3 |
| P4 | LOW | Feature doc stale: line 103, line 29, Tests section, doc Changelog | CONFIRMED, all four fixed |
| P5 | LOW | `file-catalogue.md:269` line anchors | CONFIRMED, now `:20-32` |
| P6 | LOW | Commit body 76 characters; the June `Not-tested:` trailer dropped | CONFIRMED; the in-game check is now recorded in the CHANGELOG ("Not verified in game") and this commit carries a `Not-tested:` line |
| P7 | missing | GitHub issue | = S1, NEEDS MIKE |

### Agent 5: Data flow

T1 to T10 traced; the cached object is the one MCM edits in place (register returns early for a known
id; `OverrideSettings` and `ResetSettings` write through `PropertyReference.Value`), all twelve toggles
reach a runtime read, the fallbacks equal the MCM defaults, and there is no static state.

| # | Sev | Finding | Verdict |
|---|---|---|---|
| F1 | LOW latent | Start-up order precondition that nothing enforces | CONFIRMED, fixed by D3 (option b, the lazy form) |
| F2 | LOW | Stale `battle-balance.md` and `file-catalogue.md` | CONFIRMED, fixed |
| N1 | nit | "one ctor read serves the whole process" is one read per container | CONFIRMED, the sentence is gone |

### Agent 6: Design and elegance

| # | Proposal | Verdict |
|---|---|---|
| D1 | IL rule: each getter calls the `TaomSettings` getter of its own name; count from the interface | Count from the interface APPLIED. The same-name IL check NOT APPLIED: superseded by the behavioural read-through test, which catches everything it would (snapshot, wrong mapping) and also Codex's counterexample (a constructor that discards the reference), which an IL check cannot see. The convergence pass disproved this reason for the first version of the test; it holds for the rewritten test (see "Convergence") |
| D2 | `EveryFallback_EqualsTheMcmCompiledDefault` over all twelve | APPLIED; the six literal pins stay (they pin balance numbers, not only agreement) |
| D3 | Lazy `_settings ??= TaomSettings.Instance` behind a private accessor | APPLIED as behaviour-PRESERVING: on the only current path (resolve at `OnGameStart`, after MCM initialises) the first read returns the same object the constructor did. Trade-off, recorded for Agent 2's objection: if MCM ran but never registered `TaomSettings`, every read would repeat the lookup and its per-miss warning, which is the pre-`7feca96b` behaviour and the same for the other ~100 per-read getters |

### Codex

See "Codex review" below: 0 P1, 0 P2, one P3 test gap (confirmed, fixed), one P3 plan observation
(confirmed, pre-existing, follow-up).

## Action items

1. (NEEDS MIKE) File a GitHub issue for plan 003, or one umbrella issue for the decision-17 ports, and
   put its number in the CHANGELOG heading.
2. (OWED, in game) Change a Battle Balance slider (Tier 7 Base Power) mid-campaign and confirm the
   next auto-resolve uses it without a restart.
3. (Orchestrator) `plans/README.md:41` still lists PERF-04 as open; record the PERF-01 decision for
   `bdf18039` before decision 17 deletes `impl-003`.

## Improvements (Step 4)

APPLIED:
- `Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs:14-18`: lazy `Settings` accessor
  (`_settings ??= TaomSettings.Instance`), field `TaomSettings?`, empty public constructor, internal
  test constructor. Proof: `Getters_NeverReadTaomSettingsInstance_TheLazyAccessorDoes` RED against the
  constructor form ("a constructor read would pin a null..."), GREEN after; the six literal pins and
  `EveryFallback_EqualsTheMcmCompiledDefault` and `Getters_ReadThroughTheSettings_SoLiveMcmEditsApply`
  green before and after (characterisation).
- `BattleBalanceSettingsProvider.cs:5-13`: hot-path comment (true call frequency, "cost" not "leak",
  the lazy rationale). Comment only.
- `TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs`: read-through test (all
  twelve edited after construction, each to a distinct value; a Tier8-reads-Tier9 mutation fails it
  with "Expected:<4.635>. Actual:<5.11>. Tier8Power"), all-twelve fallback pin, real DryIoc container
  resolve (guards the new internal constructor against
  `UnableToSelectSinglePublicConstructorFromMultiple`), IL rule on the lazy accessor with an
  interface-derived count and a failure message that names the getters, corrected harness comment.
- `docs/features/battle-balance.md:29,103`, Tests section, doc Changelog; `docs/modding/file-catalogue.md:269`.
- `CHANGELOG.md`: new follow-up entry; the plan 003 entry's "checked in code", "per troop per
  simulation round" and "warg half" corrected.

NOT APPLIED:
- Agent 6 D1 same-name IL check: superseded by the behavioural read-through test. The first version
  was not strictly stronger (see "Convergence"); the rewritten one is.
- Agent 1 S3 and S5 (commit subject wording, 76-character body line on `7feca96b`): need a history
  rewrite, which this assignment forbids.
- Agent 2's alternative (keep the constructor form, comment only): the lazy form removes the
  precondition instead of documenting it, at no cost on current paths; see D3.

FOLLOW-UP (pre-existing code outside the change; no issue filed, because `/issue` is public and needs
Mike's word):
- The provider's eight MCM floats are not validated (`csharp-architecture.md` "Config Providers MUST
  Validate", category 2); a hand-edited NaN in `TAOM.json` would reach `GetTroopPower`. Whether
  MCM's loader passes NaN through is UNVERIFIED for this group.
- `BattleBalanceConfig.GetTierPower` boxes and formats a key string per call (Agent 3 E2).
- About 100 per-read `TaomSettings.Instance?.` getters in other providers, `CombatMechanicsSettingsProvider`
  first (Agent 3 E3; PERF-02 names `MixedFormationsSettingsProvider`). A plan of its own.
- ADR-002 inline logic in `TaomMilitaryPowerModel.cs:21-35`, `TaomCombatSimulationModel.cs:53-58`,
  `TaomPartyHealingModel.cs:45-83`.
- `battle-balance.md:119` cites `SubModule.cs:291-295` (the `AddModel` calls are at `:1146-1148`);
  `:127` and the Tests note say `ICareerPassiveService` is resolved lazily with `IoC.Resolve`, but it
  is constructor-injected.
- `plans/003-hot-path-resolve-and-grid-caching.md:127`: "0.1f rebuilds ~6x more often than 2f" is
  20x (2 / 0.1). Codex observation 2; correct it before PERF-01 is scheduled.
- A one-line rule for `.claude/rules/csharp-architecture.md` saying when an MCM provider may cache in
  its constructor (never, unless first resolved after MCM initialises); proposed as a lesson below.

## Final verification

`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` in the worktree, after all edits:
`Passed! - Failed: 0, Passed: 10323, Skipped: 2, Total: 10325`. (10320 at `7feca96b` plus the three
new tests.) `python tools/lint_docs.py` reported nothing for the touched docs. No convergence
`deep-reviewer` pass was launched: this delegate cannot spawn agents, so the orchestrator owns it.

```
VERDICT: READY FOR COMMIT (the GitHub issue and the in-game smoke are owed to Mike, not to code)
```

## Codex review

Codex gpt-6-astra at ultra, `docs/reviews/raw/codex-adversarial-003-hot-path-resolve-and-grid-caching-2026-09-24.md`
(complete: ends with "END OF CODEX REVIEW"). It quoted the vanilla military power, combat simulation
and healing methods and the MCM accessor, container and `OverrideValues` code, cross-referenced all
twelve settings, and dispositioned all ten suspects.

### Phase 3d assessment

| # | Codex severity | My severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | MEDIUM (test gap) | Yes | The seven tests only ran the null path; a constructor that reads `Instance` then discards it, or a snapshot, passes. Fixed with the read-through test via an internal constructor, as Codex proposed |
| 2 | P3 (plan observation) | LOW, pre-existing | Yes | `plans/003-...md:127` says ~6x; 2 s to 0.1 s is 20x. Plan text, not the diff: FOLLOW-UP |
| S1 | Plan drift CONFIRMED | n/a | Yes | The provider-only port avoids the stale TroopWeight and grid edits |
| S2 | TroopWeight sites mismatch CONFIRMED | n/a | Yes | Out of this port's scope (decision 17) |
| S3, S5, S6, S7, S10 | DISPUTED | n/a | Yes | Re-read: defaults unchanged, no protected file touched, warg order unchanged at `SubModule.cs:1962-1964` |
| S4 | UNVERIFIED | n/a | Resolved | The suite passes (10323 after follow-ups) |
| S8 | DISPUTED, null-then-available UNVERIFIED | LOW latent | Partly | Codex rated the ctor null pin a contingency; the three lenses that raised it and I treat it as a confirmed latent trap, now removed by the lazy accessor |
| S9 | CONFIRMED (= finding 1) | MEDIUM | Yes | Same gap |

**Confirmed bugs:** none in runtime code. Confirmed test gap: finding 1, fixed.
**False positives:** none.
**Design questions:** none raised by Codex.
**Things Codex missed:** the stale feature doc and file catalogue lines; the undercounted hot path
(live-battle XP hits); the missing GitHub issue.

### Phase 3e root cause table

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| 1 | No read-through test | Other: test proves where a call happens, not the value that reaches the getter | The June commit pinned only null-path defaults, and the port added an IL location rule, which reads as full coverage | Lesson in `docs/reviews/lessons/testing-qa.md`; the read-through test |
| 2 | Plan cadence 6x vs 20x | Logic error (plan arithmetic) | The plan writer reasoned from frames, not from the two intervals | FOLLOW-UP for the orchestrator; no rule |

## AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Proposed entries:
- **What Codex does well:** built a concrete counterexample (a constructor that reads `Instance` and
  discards it) to show a test suite cannot distinguish a broken implementation.
- **Bugs Codex typically misses:** docs the change made stale when they are outside the diff
  (`battle-balance.md`, `file-catalogue.md`); call-frequency claims in comments.
- **Calibration:** Codex rated a constructor read of an MCM singleton that only works because of
  start-up order as an "UNVERIFIED lifecycle contingency"; TAOM treats a silent session-long pin of
  defaults as a confirmed latent defect when the class of resolve (eager, in `OnSubModuleLoad`) is a
  common pattern in the codebase.

## Convergence

A convergence `deep-reviewer` pass on `02157b18` (diff `7feca96b..02157b18`, 10 files) found no
runtime defect: the only resolve is still `SubModule.cs:1144` under `OnGameStart`, so the lazy first
read returns the object the constructor read did. It raised three LOW defects in the tests and the
docs. All three were re-checked against the code before any fix, and all three are CONFIRMED.

| # | Sev | Finding | Verification | Fix |
|---|---|---|---|---|
| V1 | LOW | The read-through test cannot catch a getter wired to the wrong bool setting: `EnableCustomTroopPower`, `EnableCustomCasualtyRatios` and `EnableCulturalSurvivalBonuses` all default to `true` (`TaomSettings.cs:271,313,328`) and the test flipped all three to `false` together. Five docs claimed that coverage, and D1 was rejected on it | Mutant `EnableCustomCasualtyRatios => Settings?.EnableCustomTroopPower ?? true` run against a temporary copy of the old test: the old test PASSED, the new one FAILED with "Expected:<True>. Actual:<False>. EnableCustomCasualtyRatios after editing EnableCustomTroopPower" | The test now edits ONE setting per pass on a fresh `TaomSettings` and asserts all twelve getters after each edit. Claims corrected in `CHANGELOG.md`, `battle-balance.md` (Tests), `lessons/testing-qa.md`, the RCA row 2 and D1 above |
| V2 | LOW | The test never read a getter before the edit, so a getter that caches its first read passed | Mutant `private float? _t7; public float Tier7Power => _t7 ??= Settings?.Tier7Power ?? 2.91f;`: the old test copy PASSED, the new one FAILED with "Expected:<3.91>. Actual:<2.91>. Tier7Power after editing Tier7Power" | Each pass reads and asserts all twelve getters before its edit. The lesson now says "read every getter once, then mutate one property per pass" |
| V3 | LOW | The DryIoc comment said the two-public-constructor error fires "at resolve time in game" | A temporary test registering a type with two public constructors caught `ContainerException` "Error.UnableToSelectSinglePublicConstructorFromMultiple" from `container.Register`, before any resolve | Comment now says "at registration (IoC.cs:127, the container build), before any resolve". `IoC.cs:127` is the `RegisterBattleBalanceFeature` call |

**D1 re-accounted.** The NOT APPLIED reason ("strictly stronger") was false for the first version of
the read-through test, which V1's mutant proves: a same-name IL check would have failed it. With the
one-setting-per-pass rewrite, any getter that reads another setting fails the pass that edits that
other setting (its value moves while its own setting does not), which covers everything the IL check
would, plus a snapshot, a first-read cache and a discarded reference. D1 stays NOT APPLIED on that
reason.

**False positives:** none.

**Not in scope:** `docs/reviews/codex-adversarial-003-hot-path-resolve-and-grid-caching-2026-09-24.prompt.md`
is untracked in this worktree; it is outside the reviewed diff and left for the orchestrator.

**Final verification.** Both mutants and the probe were reverted before the run (the provider is
byte-identical to `02157b18`). `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Passed! - Failed: 0, Passed: 10323, Skipped: 2, Total: 10325`. The count is unchanged because the
read-through test was rewritten in place.

# Deep review: plan 019, nullable ratchet (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 019, stop discarding the nullable warnings and graduate Main/Features/Siege to errors
         (branch improve/019-nullable-ratchet, 7f02fc8d..19ca72d3, 3 commits, 13 files)
Date: 2026-09-24

Scope:   C# (1 Harmony prefix, 2 DTOs, 1 service method, 2 test files), build config
         (2 csproj, 2 .editorconfig), docs (CHANGELOG, code-quality.md, siege.md)
Waves:   lenses 1-6 ran before this lead pass; the review lead verified, fixed and wrote this
         report; Codex gpt-6-astra (ultra) complete

STANDARDS:     FAIL - 7 findings (1 MED process, 6 LOW), all docs or process; no code-standards
               violation in the changed C#
COMPATIBILITY: PASS - 0 incompatible, 0 unverified (15 API usages); 1 LOW doc finding
EFFICIENCY:    PASS - 0 issues in the changed hunks (1 LOW simplicity, 1 FOLLOW-UP)
COMPLETENESS:  INCOMPLETE - no GitHub issue; /build-fix advice; procedure only in the plan;
               defer outcome undocumented; 2 of 5 paths tested; doc inaccuracies
DATA FLOW:     PASS - 13 flows, 1 gap (LOW), 4 inconsistencies (1 MED, 3 LOW); no runtime gap
DESIGN:        6 KEEP proposals (6 apply, 0 follow-up only)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE (harness file touched only by the fix for finding 1)
```

## Verification of findings

Every finding below was re-read against the worktree at `19ca72d3` before it was classified.
Engine facts were spot-checked in the v1.5.3 taom-src cache: the two callers of
`GetSiegeCampPartyPosition` (`MobileParty.cs:3248-3249` reads `SiegeEvent.BesiegedSettlement.Town`
first; `BesiegerCamp.cs:566` passes the settlement into `GetSiegeCampFrames` before `:595`),
`Settlement.GatePosition`/`Party` private setters, `Settlement.Name` reading `Party.CustomName`,
and `CampaignVec2.X`/`Y`/`IsOnLand`.

| # | Finding (lenses) | Sev | Verdict |
|---|---|---|---|
| 1 | `/build-fix` recommends `!` for CS8602, now a live error in Siege (A4 F2, A5 #11) | MED | CONFIRMED, fixed |
| 2 | No-settlement defer described as "same outcome ... without throwing"; vanilla throws, and the path is unreachable from vanilla (A2, A4 F4, A5 #5, A6 #2, Codex obs.) | LOW | CONFIRMED, fixed |
| 3 | `KingdomSiegeMessages` comment wrong about `AcceptButton` (A1, A4 F6.1, A5 #7, A6 #3, Codex P3 #2) | LOW | CONFIRMED, fixed |
| 4 | CHANGELOG points at a procedure `code-quality.md` did not contain (A1, A3 c, A4 F3, A5 #12, A6 #1) | LOW | CONFIRMED, fixed |
| 5 | `siege.md:21,35` say `__result = GatePosition`; the code uses a ring (A1, A4 F6.3, Codex obs.) | LOW | CONFIRMED, fixed |
| 6 | `siege.md:56` misattributes `SiegeEngineAvailabilityServiceTests` (A1, A2, A4 F6.2, A5 #13) | LOW | CONFIRMED, fixed |
| 7 | Camp-2 test checks lengths only (Codex P3 #1) | LOW | CONFIRMED, fixed |
| 8 | "Never re-add to `<NoWarn>`" has no gate (A1, A4 FU2) | LOW | CONFIRMED, fixed |
| 9 | Tests cover 2 of 5 prefix paths; settlement path called untestable (A4 F5) | LOW | CONFIRMED, fixed |
| 10 | `<NoWarn>$(NoWarn)</NoWarn>` is a no-op (A1, A3 #1, A5 F7, A6 #4) | NIT | CONFIRMED, fixed |
| 11 | No Arrange/Act/Assert comments in the new test class (A4 F7) | NIT | CONFIRMED, fixed |
| 12 | No GitHub issue (A1, A4 F1) | MED | CONFIRMED, NEEDS MIKE |
| 13 | New test file named after the patch category, not the method (A4 F7) | NIT | FALSE POSITIVE: both styles exist (`Patch62MovieReleaseAvGuardTests.cs`, `RecruitGatePatchTests.cs`, `Clan_UpdateBannerColorsAccordingToKingdom_PatchTests.cs`) |

No HIGH finding was reported by any lens or by Codex. Root causes, the repeat of the harmony-il
defer lesson and why each agent missed what it missed: `docs/reviews/rca-nullable-ratchet-2026-09-24.md`.

## Details (condensed from the lens reports)

**Agent 1, standards.** ADR-002/003/004/005/007 pass on the changed C#; naming, test isolation,
encoding, commit subjects and lint pass; executor build and test claims confirmed from its logs.
Findings 3 to 6, 8, 10, 12. Follow-ups: the ring algorithm inside the entry point (ADR-002),
`SiegeDefenseService` engine statics (ADR-007), the Patch8 registry stub, "sealed" in `siege.md`.

**Agent 2, engine.** All 15 API usages verified against the installed v1.5.3 DLLs, including the
Harmony target's signature and parameter names, `Debug.Print`'s filter mask, and the engine DLLs
carrying no nullable annotations. `/nowarn` beating `.editorconfig` verified in the SDK 10.0.401
compiler. Finding 2 (with the unreachability proof) and 6. Follow-ups: the `siege_camp_1` fix recipe
is wrong (the engine matches the `map_camp_area_1` tag, not a name), "sealed" is wrong, the registry
gap, and the forward risk that future engine annotations become errors in graduated folders.

**Agent 3, efficiency.** No performance issue in the changed hunks; build and test times show no
regression. Finding 10. Follow-up: `OnHourlyTickShared`/`OnHourlyTickLocalPlayer` allocate every
campaign hour with no active events (an early return would fix it).

**Agent 4, completeness.** Findings 1 to 6, 9, 11, 12 and the false positive 13. Follow-ups: the
harmony-il lesson's Patch8 example and registry entry (fixed here as part of the RCA, see below),
the NoWarn gate (fixed as finding 8), `/new-feature` could start new folders graduated,
`SiegeDefenseConfig` null dictionary values, `siege-defense.md` staleness, the `AcceptButton`
fallback, and a `CHANGELOG.md` conflict with improve/010 at merge.

**Agent 5, data flow.** 13 flows; the suppression, its scoping away from `TAOM.Tests`, the Siege
override, the id lists, the test's `DebugManager` swap, the initializers and Harmony registration
are all connected. Findings 1 to 4, 6, 10. Follow-ups F1 to F8 (listed below).

**Agent 6, design.** Six KEEP proposals, handled in Step 4 below. Already optimal: the root `none`
plus per-folder `error` design, the guard's placement after the camp-2 swap, `string?` on the DTO,
`= ""` on the event, the uninitialized-object harness.

## Action items

1. File the GitHub issue for plan 019 (Mike), then add `(#N)` to the CHANGELOG heading and the
   `siege.md` "GitHub Issue" lines, and `Refs #N` on the merge.
2. Run the convergence pass on the follow-up commit (the lead cannot spawn agents).
3. Decide the NEEDS MIKE items below.

## Improvements (Step 4)

The suite was green before (98/98 Siege tests at `19ca72d3`), and each change below was proven by
the named test or command.

```
APPLIED:
- Main/TAOM.csproj:9, deleted the no-op <NoWarn>$(NoWarn)</NoWarn> (A6 #4, A3 #1). Proof:
  dotnet msbuild -getProperty:NoWarn prints 1701;1702 before and after; Main --no-incremental
  build 2 Warning(s), 0 Error(s), 0 CS86xx.
- docs/ai-includes/code-quality.md "How nullable is enforced", the graduation procedure, fix rules
  and hotfix escape moved in from the plan; the hand-kept graduated list became a git grep; both
  .editorconfig comments point there (A6 #1). Proof: lint_docs --dash-base 19ca72d3, 0 dashes,
  0 dead links.
- BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:46-48, comment states the path is unreachable
  from vanilla and that vanilla then throws (A6 #2). Proof: comment only;
  SiegeCampGuardPatchTests green.
- KingdomSiegeMessages.cs:5, dropped the false consumer clause (A6 #3). Comment only.
- SiegeDefenseServiceTests.cs:373, StringAssert.Contains(msgs.Title, "{attacker}") (A6 #5).
  Proof: SiegeDefenseServiceTests 26/26 before and after; TAOM.Tests CS86xx 2,256 before and
  after (executor's count_cs86.py).

NOT APPLIED:
- SiegeDefenseService.cs:87-89, Resolve simplified to `template is null` (A6 #6): behaviour
  preserving by reasoning (String.Replace on "" returns ""), but nothing observes the private
  Resolve, and exposing it for a characterisation test costs more than the one clause it removes
  (simplicity criterion). The explicit check also matches the net472 rule now in code-quality.md.
- Any behaviour-changing proposal: none were applied; each is listed under NEEDS MIKE.

FOLLOW-UP (pre-existing code; no issues filed, because filing is public and needs Mike):
- siege.md:4,7,8,61,65,71 and docs/modding/settlements.md:398, and the patch's log text: the
  "add an entity named siege_camp_1" recipe is wrong; the engine takes children tagged
  map_camp_area_1/2 (SandBox.MapScene.GetSiegeCampFrames) (A2).
- siege.md:14,51 call BesiegerCamp sealed; it is `public class` (A1, A2).
- The ring algorithm inside the patch (ADR-002) and SiegeDefenseService's engine statics (ADR-007) (A1).
- SiegeDefenseConfig: a `"kingdom": null` entry returns null from GetMessages and throws in
  GrantReward after influence is granted; KingdomSiegeMessages? would let the error tier enforce
  it (A5 F1, A3 b, A4 FU4).
- ResponseWindowDays is dead config; siege-defense.md's config table omits KingdomMessages and says
  "17 unit tests" (now 26); siege-trebuchets.md says "No direct unit tests" (A5 F3-F5, A4 FU5).
- OnHourlyTickShared/LocalPlayer allocate every hour with no active events (A3 #2).
- The ratchet covers seven ids; CS8619/8620/8629/8655 stay warnings and `#nullable disable` would
  bypass it; the deferred downgrade gate could check both (A5 F8).
- /new-feature could copy Siege's .editorconfig into every new feature folder (A4 FU3).
- Engine annotations in a future bump would surface as errors in graduated folders; the hotfix
  escape covers it (A2 FU4).
- Merge order: CHANGELOG.md conflicts with improve/010 (both add a 2026-09-24 block) (A4 FU7).
```

**Scope extension, stated for Mike:** the fix for finding 1 edits `.claude/skills/build-fix/SKILL.md`,
which is outside plan 019's file list. It changes one table row and adds one DON'T line.

**Also fixed as part of the RCA (pre-existing text the change made wrong or required):** the Patch8
entry in `docs/reference/harmony-patch-registry.md` ("Target: Various") now names the target and
records both throwing defers, as `lessons/harmony-il.md`'s Prevent line requires for a defer into a
vanilla that throws; that lesson's example, which named Patch8 as a safe defer, is corrected.

**Convergence pass:** not run. The lead cannot spawn agents; the orchestrator owes one
`deep-reviewer` pass on the follow-up commit's diff.

## NEEDS MIKE

1. File the GitHub issue for plan 019 (finding 12).
2. The no-settlement path's fallback (plan 019 "Deferred gameplay decision"): recommend closing it as
   unreachable from vanilla in v1.5.3 rather than designing a fallback; if kept open, the fix is a
   behaviour change (skip vanilla with a position) and needs a smoke.
3. A partial JSON entry: `AcceptButton ?? DefaultMessages.AcceptButton` (or per-field fallback to
   `DefaultMessages`) would give the button a label; behaviour-changing, outside plan 019.
4. Approve the scope extension into `/build-fix` (already applied; revert is one file).

## Final verification

- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental`:
  `2 Warning(s)`, `0 Error(s)`, 0 CS86xx.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: `Failed: 2, Passed: 10242,
  Skipped: 2, Total: 10246`. The two failures are the known live-Armory tests
  (`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`,
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`). Total is the executor's 10,241 plus
  3 new guard tests and 2 gate rows.
- `NullableRatchetGateTests` shown RED with `CS8602` temporarily added to the Dependencies
  `<NoWarn>` (`Expected:<0>. Actual:<1>`), then the csproj restored byte for byte.

```
VERDICT: READY FOR COMMIT (the missing GitHub issue blocks the merge, not the commit; the
convergence pass is owed)
```

## Codex review

Codex gpt-6-astra at ultra, read-only, on `7f02fc8d..19ca72d3` through git refs. Output:
`docs/reviews/raw/codex-adversarial-019-nullable-ratchet-2026-09-24.md` (gitignored, complete:
"END OF CODEX REVIEW"); prompt: `docs/reviews/codex-adversarial-019-nullable-ratchet-2026-09-24.prompt.md`.
Quality: engine excerpts from the installed DLLs, a five-row scenario table for the prefix, a full
config cross-reference (ids, JSON keys, tokens, kingdom ids), and all ten Known Suspects answered.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3: camp-2 test checks lengths, not the handed-over frames | LOW | Yes | Verified: the patch assigns the camp-2 array itself (`:41`); a `new MatrixFrame[1]` would pass the old assertions. Fixed with `Assert.AreSame` |
| 2 | P3: DTO comment promises a "" fallback for every message | LOW | Yes | Verified: `AcceptButton` goes straight to `InquiryData` (`SiegeDefenseService.cs:148`). Fixed |
| obs. | `siege.md:21,35` GatePosition vs ring | LOW | Yes | Same as deep-review finding 5. Fixed |
| obs. | "Without throwing" describes the prefix, not the outcome | LOW | Yes | Same as finding 2. Fixed |
| obs. | Partial-JSON behaviour untested through deserialization | none | Partly | True, and unchanged by this diff; listed as follow-up with the null-entry item |

- **Confirmed bugs:** the two P3 findings (both fixed).
- **False positives:** none.
- **Design questions:** none raised by Codex beyond the plan's own deferred fallback (NEEDS MIKE 2).
- **Things Codex missed:** findings 1 (`/build-fix` advice, in a file its git-ref scope did not
  include), 4 (the CHANGELOG pointer), 6, 8, 9 (path coverage beyond the camp-2 oracle), 10 and 12.

### Phase 3e root cause for Codex's confirmed findings

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Camp-2 test's length-only oracle | Other: weak test oracle | The plan prescribed the oracle (`:648-657`) and the executor copied it | `Assert.AreSame`; lesson in `lessons/testing-qa.md`; plan-text lesson in `lessons/build-tooling-workflow.md` |
| 2 | DTO comment overstates the fallback | Convention inconsistency (comment vs code) | Plan-supplied comment contradicted the plan's own pass-through step | Comment fixed; plan-text lesson |

## AGENTS.md lessons (pending)

Not edited here; for the consolidated Phase 3h update:

- **What Codex does well:** answered every Known Suspect with evidence and separated "DISPUTED
  statically" from "UNVERIFIED historically" (build counts, RED runs it could not see) instead of
  guessing either way; produced a per-input scenario table for the patched method.
- **Bugs Codex typically misses:** advice in unchanged harness files (skills, rules) that a change
  makes live, because a git-ref-scoped prompt never opens them; pointers whose target text it does
  not open (a CHANGELOG line promising a procedure).
- **False positives Codex has produced:** none new in this review.

## Convergence

Convergence pass on `19ca72d3..155e3ea5` (the review-fix commit). No runtime or code-standards
violation in the changed C#; three LOW defects in the applied fixes, all verified against the code
and fixed. No false positives.

| # | Defect | Verified by | Fix |
|---|---|---|---|
| 1 | `NullableRatchetGateTests` split `<NoWarn>` on `;` only and matched numeric ids, so `1701,8602`, `1701 8602` or the `nullable` alias (which the compiler expands to every nullable warning) switched the ratchet off with the gate green | New `LeakedRatchetIds_CatchesEveryCompilerSpelling` rows: 4 of 6 RED on the old parsing (comma, space, `nullable`, `Nullable`). A temporary `;nullable` in the Dependencies `<NoWarn>` left the Dependencies gate row green | Split on `;`, `,` and space; expand `nullable` (any case) to the seven ids. The same temporary `;nullable` now fails the Dependencies row (reverted). `code-quality.md` names the alias |
| 2 | `siege.md` item 5 and the diagram's catch line still called the catch's hand-back to vanilla protective, against the registry and `lessons/harmony-il.md` | The patch returns at `:24-25` when camp-1 has frames, and its only write to camp-1 (`:41`) is followed by a statement that cannot throw, so the catch only fires with camp-1 null or empty | Item 5 and the diagram now say the original runs and throws, and link the registry |
| 3 | The graduation procedure in `code-quality.md` built `Main/TAOM.csproj` without `-p:DisableModuleCopy=true -p:ModuleId=`, which deploys into the game install (`ModuleId` defaults to the project name; `DisableModuleCopy` does not gate CopyModule, per the csproj's own comment) | `Main/TAOM.csproj:7` and the `FailOnIdeStateInModule` comment | Steps 1 and 3 use `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= --no-incremental` |

The CHANGELOG entry's gate test count is now 9 (2 csproj rows, 6 spelling rows, 1 negative row).

**Verification:** `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: Failed 2,
Passed 10249, Skipped 2, Total 10253. The two failures are the known live-Armory ones
(`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`,
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`).

# Deep review: plan 022, Order of Battle Auto-Assign (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: Plan 022, OOB "Assign Heroes" places captains through HeroAutoAssigner
         branch improve/022-order-of-battle-auto-assign, diff 1091f3b6..66e3fd59
         (d1b2c44c feat, 66e3fd59 docs), worktree E:\repos\taom-improve\wt-022
Date: 2026-09-24 (compiled 2026-09-25)

Scope:   C# (12 files), XML (13 string files), docs (CHANGELOG, feature doc, feature map)
Waves:   Wave 1: Standards, Engine, Data flow, XML. Wave 2: Efficiency, Completeness, Design.
         Codex adversarial review complete ("END OF CODEX REVIEW" present).

STANDARDS:     PASS - 0 violations of checks 1 to 10; 2 LOW, 2 NIT (all fixed)
COMPATIBILITY: PASS - 0 incompatible, 0 unverified (33 API usages verified);
               1 MEDIUM behaviour finding (siege), 3 wrong text claims (fixed)
EFFICIENCY:    PASS - 0 high, 0 medium, 1 low (applied); 3 follow-ups
COMPLETENESS:  INCOMPLETE - GitHub issue missing (needs Mike: /issue is public);
               tests and doc sections added on this branch
DATA FLOW:     PASS - 0 gaps, 6 inconsistencies (1 MED, 5 LOW): 4 fixed, 2 to Mike
DESIGN:        6 KEEP proposals (3 apply, 3 follow-up): 2 applied as fixes, 1 to Mike
XML:           PASS - 4 gates (1 failed, not caused by this diff), 2 LOW findings (fixed)
TOOLING:       NOT IN SCOPE
```

## Verification of every finding

Each finding below was re-read against the worktree source and, for engine claims, the v1.5.3 `taom-src` cache
(`C:\Users\mikew\.taom-src\v1.5.3\`) before it was classified. Engine lines I re-read myself this pass:
`Mission.cs:4111-4118, 4285-4309` (hero agents get `FixedEquipment`, the spawn equipment is a clone with the Horse
slot cleared under `AgentNoHorses`, stored by `InitializeSpawnEquipment`), `SandBoxSiegeMissionSpawnHandler.cs:16-17`
(`SetSpawnHorses(false)` for both sides), `Agent.cs:746, 893` (`HasMount`, public `SpawnEquipment`),
`OrderOfBattleVM.cs:17, 75, 254` (`public class`, `_isPlayerGeneral` field), `Equipment.cs:56-79, 145-149, 445-460`
(standalone constructor; the indexer setter ignores `IsItemFitsToSlot`'s result), `ItemObject.cs:291`.

| # | Finding (lenses) | Class | Evidence and action |
|---|---|---|---|
| 1 | MED: siege assault, horse owners never placed (Agent 2 M1, Agent 5 T7, Agent 6 P2, Codex 1 P2) | CONFIRMED | `OOBCaptainAutoAssigner.cs:98` built `new HeroCombatAdapter(hero)` (campaign `BattleEquipment`); `CompanionRoleService.cs:110-115` turns any mount into Cavalry/HorseArcher; `HeroAutoAssigner.cs` scores both 0 on classes 1, 2, 5. **Fixed:** `HeroCombatAdapter(Hero, Equipment)` overload; boundary passes `item.Agent.SpawnEquipment ?? hero.BattleEquipment`. RED observed (CS1729, the overload did not exist), then green |
| 2 | LOW: doc says nothing campaign-side or save-backed (Agent 2 W1, Agent 5 T12, Codex 2) | CONFIRMED | `companion-tactics.md:108-109`. **Fixed:** Co-op and Save data bullets rewritten (vanilla persists the layout via `SPOrderOfBattleVM.SaveConfiguration`; co-op sync unverified) |
| 3 | LOW: "does not move hero-troops" contradicts code (Agent 2 W3, Agent 6 P3, Agent 5 T13 doc half) | CONFIRMED | `OOBCaptainAutoAssigner.cs:57-61` makes hero-troops candidates. **Fixed:** limitation sentence rewritten per Agent 6 P3 |
| 4 | LOW: "sealed TaleWorlds VM type" (Agent 1 L1, Agent 2 W2, Agent 6 nit) | CONFIRMED | Decompile `:17` `public class OrderOfBattleVM : ViewModel`. **Fixed** in `IOOBCaptainAutoAssigner.cs`; the same word in `IOrderOfBattleVMTracker.cs:8` predates this change (follow-up) |
| 5 | LOW: boundary early returns untested (Agent 1 L2, Agent 4 F2) | CONFIRMED | **Fixed:** `OOBCaptainAutoAssignerTests` (null VM; not general, planner never called) |
| 6 | NIT/LOW: stale reflection labels `OOBOverlayService.cs:57/58` (Agent 1 N3, Agent 2 L3, Agent 5 T14) | CONFIRMED | Fields now at `:60/61`. **Fixed** in `ReflectionSiteBindingTests.cs:40-41` and `reflection-sites.md:33-34` |
| 7 | NIT: tree line omits Assign Heroes (Agent 1 N4) | CONFIRMED | **Fixed** |
| 8 | LOW: feature doc Key Files, Dependencies, Changelog not updated (Agent 4 F6) | CONFIRMED | **Fixed** (new rows, 4 TaleWorlds types, dated Changelog line) |
| 9 | LOW: `score > 0` threshold unpinned (Agent 4 F3) and "Unknown scores 0 everywhere" wording | CONFIRMED | **Fixed:** two planner tests (Archer to class 5, HorseArcher to class 6); wording now "on every class Auto-Assign fills (1 to 6)" |
| 10 | LOW: message arms untested, delegation-only oracle (Agent 4 F4, Codex 3) | CONFIRMED | `TextObject.ToString` catches and returns an error string. **Fixed:** one DataRow test per status asserting the shown text via `InformationManager.DisplayMessageInternal` |
| 11 | LOW: new DI edge unpinned, resolved inside an empty catch every frame (Agent 3 #4, Agent 4 F5) | CONFIRMED | Registration line 30 is changed code. **Fixed:** `CompanionTacticsWiringTests` (DryIoc `Validate`) |
| 12 | LOW: seeded language rows end in LF inside `\r\r\n` files (Agent 7 #1) | CONFIRMED | Byte count before: 2663 `\r\r\n` + 15 LF-only per file. **Fixed** by binary round-trip: now 2666 `\r\r\n` + 12 LF-only (the 12 older #608 rows, follow-up); all three ids still direct children of `<strings>` in all 12 files |
| 13 | LOW: extra blank line before `</strings>` (Agent 7 #2) | CONFIRMED | **Fixed** (one line removed) |
| 14 | LOW: "companions" wording narrower than the candidate set (Agent 2 nit, Agent 3 aside, Agent 5 FU4, Codex scenario table) | CONFIRMED (wording) | **Fixed** in CHANGELOG and feature doc. Whether to restrict the set is N4 below |
| 15 | LOW: `NoneAssigned` covers "empty plan" and "vanilla rejected every pick" (Agent 5 T9) | NEEDS MIKE | Accurate. Simplicity criterion: tiny win (engine drift only, already logged as a warning) against a fourth status and a new string in 12 languages: Reject unless Mike wants it |
| 16 | LOW: greedy tie-break loses a placeable captain, e.g. heroes [ShieldInfantry, Archer] and classes [5, 1] (Agent 5 T8, Agent 6 P1) | NEEDS MIKE | Traced: pairs (100,s0,h0), (100,s1,h0), (50,s0,h1) give plan [(0,0)] only. Behaviour-CHANGING and the plan mandated this tie-break (Codex: conforms to contract). Recommend Agent 6 P1 (slot-scarcity key, about 4 lines plus 2 tests) |
| 17 | LOW: formations with a class but no troops count as open slots (Agent 2 L1; Agent 6 UNVERIFIED) | NEEDS MIKE | `HasFormation` means "class set" (decompile `:959`). Behaviour-CHANGING; Codex argues `HasFormation` is the right test. Options: filter `TroopCount > 0`, or break ties by troop count |
| 18 | "manual-drag path" is strictly the click path (Agent 2 claims list) | FALSE POSITIVE | Agent 2 itself notes the click and drop both run `OnFormationAcceptCaptain`; the doc wording is accurate in effect. No change |

Codex's ten Known Suspect dispositions (disputes and UNVERIFIED items about the build history) are not findings;
none contradicts the code.

## Details

The seven lens reports and the Codex review are the inputs to this report and are summarised in the table above;
the Codex raw output is `docs/reviews/raw/codex-adversarial-022-order-of-battle-auto-assign-2026-09-24.md`.

**OWED in-game checks** (from Agents 2, 6, 7 and the plan):
1. Field battle as general, Formation Presets on: press Assign Heroes; open infantry, ranged and cavalry formations
   get matching captains; message "Captains assigned: N.".
2. **Siege assault** as general: a companion who owns a horse and carries a one-handed weapon becomes captain of an
   infantry formation (before this fix the button reported "No hero suits an open captain slot.").
3. Banner bearers still stand in auto-captained formations (Agent 2 F1: `HandleCaptainAssignment` sets the formation
   banner from the captain's banner item).
4. Non-English language (German): the message shows the English seed, not a raw key or "Error at id".

## Action items

1. Mike: the NEEDS MIKE list below (N1 is the one with a concrete recommended change).
2. Orchestrator: file the GitHub issue (public, `/issue`), label `triage-needs-ingame`, cite it in the CHANGELOG and
   feature doc, and comment on #117 that captain Auto-Assign landed (D21) while Save/Load stays open.
3. Orchestrator: run the Step 4.6 convergence `deep-reviewer` on this fix diff (this delegate cannot spawn agents).
4. Paid translator run for the 3 keys in 11 languages (Mike).

## Improvements (Step 4)

APPLIED:
- `Main/Features/CompanionTactics/FormationPresets/HeroAutoAssigner.cs` `PlanCaptains`: used-hero and used-slot
  `HashSet<int>` replaced by `bool[]`, slot class read once per slot (Agent 3 #1, PRESERVING).
  `HeroAutoAssignerTests` 19 passed before and after.
- `docs/features/companion-tactics.md` known-limitation sentence (Agent 6 P3, PRESERVING, doc).
- Agent 6 P2 (spawn equipment) was applied as the fix for finding 1, not as an improvement.

NOT APPLIED:
- `HeroAutoAssigner.cs:73-75` Agent 6 P1 slot-scarcity tie-break: behaviour-CHANGING, needs Mike (N1).
- `OOBCaptainAutoAssigner.cs:48` empty classed formations (Agent 2 L1): behaviour-CHANGING, needs Mike (N2).
- `OOBButtonsVM.cs:88-95` distinct engine-rejection message (Agent 5 T9): simplicity Reject, needs Mike to overrule.

FOLLOW-UP (pre-existing code; no issue filed from here because `/issue` is public and never auto-invoked):
- `RoleTooltipDecorator.cs:79, 94` OOB badge should read spawn equipment too (Agent 6 P4; behaviour-changing, file out
  of plan scope). Documented as a known limitation.
- `IOrderOfBattleVMTracker.cs:8` "sealed" wording (Agents 1, 2).
- `CompanionRoleService` cache never helps; delete `_cache` and `ComputeSignature` (Agent 3 #2).
- `OOBOverlayService.cs:82` per-frame `FieldInfo.GetValue` boxing; use a `FieldRef` (Agent 3 #3).
- `int` formation class to `DeploymentFormationClass` enum across `IHeroAutoAssigner` (Agent 1 F4, Agent 6 P5).
- Break equal fits with vanilla `BattleCaptainModel` ratings (Agent 6 P6; Mike's call, D21 chose equipment scoring).
- `translate_with_claude.py sync_missing_ids` misplaces rows in mixed-ending files; `LanguageFileCoverageTests` should
  read `base/strings/string` only (Agent 1 F1, Agent 7 FU1-FU2, Agent 4 FU1).
- The 12 older `taom_behavior_f6.*` LF rows (#608) in the same language files (Agent 7 FU3).
- `harvest_literal_loc_keys.py insert_rows` adds a blank line every run (Agent 7 FU4).
- `check_external_loc_coverage.py` FAIL on the live Armory and TAOM_Map installs, not caused by this diff (Agent 7 FU5).
- `OOBButtonsVM.cs` 226 lines against the ADR-002 ceiling; extract the inquiry chain when #117 Load/Save lands
  (Agent 1 F2).
- Unlocalized overlay strings: "Assign Heroes", "No Order of Battle screen detected.", "Presets", the inquiry texts
  (Agent 1 F3, Agent 7 FU6; plan-deferred).
- Skirmisher counts as ranged in the scorer while vanilla and `IsRangedRole` treat it as infantry (Agent 5 FU1).
- MCM hint for `EnableFormationPresets` (`TaomSettings.cs:835`) does not mention Assign Heroes (Agent 5 FU3).
- `Patch35_OOBUIHandler_Tick.cs:25` swallows every exception with no log (Agent 4 FU3).
- `companion-tactics.md` "GitHub Issue" section says "Not yet created" though #115 and #117 exist (Agent 4 FU2).
- Test name `ScoreRoleForFormation_HeavyInfantryClass_PrefersMelee` keeps the wrong class name (Agent 4 FU4; plan kept
  it on purpose).
- `plans/README.md` has no 022 row; 18 `improve/*` branches each add a `## 2026-09-24` CHANGELOG heading and will
  conflict at `CHANGELOG.md:5` (Agent 4 Info).

## NEEDS MIKE

- N1: At equal score, prefer the slot fewer heroes can lead, so a mixed formation stops taking the only hero a pure
  one could use (Agent 6 P1; recommended).
- N2: Should formations with a class set but no troops be skipped (`TroopCount > 0`), or only lose ties?
- N3: Should heroes the player already placed as hero-troops stay Auto-Assign candidates?
- N4: Candidates are every non-player hero on the player's team; restrict to the player's clan or companions?
- N5: File the plan 022 GitHub issue (public) with `triage-needs-ingame`, and update #117.
- N6: Make the OOB role badge read spawn equipment too, so it agrees with Auto-Assign in sieges (Agent 6 P4).
- N7: A distinct message when vanilla rejects every pick (Agent 5 T9); recommendation: no.

## Verification

- Baseline before any edit: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
  Passed 10325, Skipped 2, Failed 0.
- After all fixes, same command: **Passed 10335, Skipped 2, Failed 0, Total 10337** (10 new results: 2 adapter,
  2 planner, 3 message rows, 2 boundary, 1 wiring). The branch is based after `a39a9c86`, so no known failure applies.
- `python tools/validate_xml_schemas.py` on the 13 string files: PASS (12 NOT REGISTERED, expected).
- `python tools/harvest_literal_loc_keys.py --dry-run`: 0 unregistered.
- `python tools/lint_docs.py --quick --summary --fail-on-dead`: 0 findings.
- ElementTree placement parse: 3 new ids under `<strings>` in all 12 language files.
- Step 4.6 convergence pass: NOT RUN (this delegate cannot spawn agents; owed by the orchestrator).

## Codex review

Codex adversarial review, complete, 128,428 tokens; raw output
`docs/reviews/raw/codex-adversarial-022-order-of-battle-auto-assign-2026-09-24.md`. Verdict ISSUES FOUND:
**P1 0, P2 1, P3 2.** It quoted installed-DLL code for every vanilla member it relied on, traced the siege spawn chain
and vanilla's deployment-end persistence, and cross-referenced every string key, setting and registration.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | MEDIUM | Yes | Siege horse owners never placed. Re-read `Mission.cs:4111-4118, 4285-4309` and `SandBoxSiegeMissionSpawnHandler.cs:16-17`; confirmed and fixed (finding 1) |
| 2 | P3 | LOW | Yes | Doc's "nothing save-backed" is false; vanilla `SaveConfiguration` persists captains. Doc fixed (finding 2) |
| 3 | P3 | LOW | Yes | VM test asserted dispatch only. Message assertions added; they pass, so the current text renders (finding 10) |

- **Confirmed bugs:** 1 (fixed in `Main/Adapters/HeroCombatAdapter.cs` and `OOBCaptainAutoAssigner.cs:98`).
- **Confirmed quality findings:** 2 and 3 (doc and test, fixed).
- **False positives:** none.
- **Design questions:** Codex's scenario table raised the allied-lord candidates (N4) and the greedy tie-break (N1),
  both correctly classed as conforming to the plan.
- **Things Codex missed:** the LF seeded rows and the `sync_missing_ids` hazard (Agent 7), the untested early returns
  and threshold (Agents 1, 4), the unpinned DI edge (Agents 3, 4), the stale reflection labels (Agents 1, 2, 5), the
  hero-troop limitation sentence (Agents 2, 5, 6), the sealed wording (Agents 1, 2, 6), and empty classed formations
  (Agent 2).

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Siege horse owners never placed | Missing vanilla gate / assumed an API worked a certain way | The plan assumed campaign gear describes the mission agent and named only a field battle to test | Adapter overload, boundary fix, `HeroCombatAdapterTests`; lesson in adapters-taleworlds-api (repeat of #627's lesson, broadened) |
| 2 | Doc denies vanilla persistence | Other: unverified plan premise | Didn't trace the full lifecycle to deployment end | Doc fixed; lesson in state-lifecycle-save |
| 3 | Delegation-only VM test | Other: test oracle | The plan's gate named delegation; `TextObject.ToString` swallows errors | Message tests; lesson in testing-qa |

### AGENTS.md lessons (pending; Phase 3h consolidated later)

- What Codex does well: traced a mission-type-specific path end to end (siege spawn handler, `DecideAgentSpawnEquipment`,
  the OOB class selector) and followed a vanilla handler to its save-time persistence.
- Bugs Codex typically misses: byte-level data hazards in seeded language files (line terminators that break a
  line-based tool), missing tests for guard clauses it judged "structurally untestable", unpinned DI edges, and
  stale line-number labels in the reflection catalogue.
- False positives: none new this review.

VERDICT: READY FOR COMMIT (review follow-ups committed on the branch; the Step 4.6 convergence pass and Mike's
answers to N1 to N7 are still owed before merge)

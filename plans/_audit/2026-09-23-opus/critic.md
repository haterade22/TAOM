# Completeness critic: run `2026-09-23-opus`, baseline `b2e387db`

Read-only critic. Inputs read in full this run: `BRIEF.md`, `PROGRESS.md`, `baseline.md`, `reconcile.md`,
`triage-A.md`, `triage-B.md`, `triage-C.md`, `triage-check.md`, `lane-1..6.findings.md`, all 13
`verify-{a,b}-batch-*.md`, the 8 `verify-<ID>-{design,repro}.md`, `followup-patchshield.md`,
`jscpd-digest.md`, and `.claude/skills/improve/references/audit-playbook.md`. Measurements below are
read-only greps and `git ls-tree` at `b2e387db`; every script was piped to `python -` from PowerShell
(no file written outside this one). No build, no test.

## Progress notes (appended as I go)

- All inputs read. Starting the coverage measurements.
- Feature-folder, tools and category measurements done (section 1). Security scan and five game-data
  auditors run read-only. GitHub issue backlog read (`gh issue list`, `gh issue view`, read-only).

## 1. Coverage: what got no real coverage (measured)

### 1a. `Main/Features` folders

Method: `git ls-tree -r -l b2e387db Main/Features/`; a folder counts as cited when any findings file
holds `<file>.cs:<line>` for a `.cs` file in it (basename matched, folder used to break ties). A cite
detached from its file name (for example lane 3's "Patch58_SkipCampaignIntro.cs ... (`:67-68`)") is
missed, so these counts are a floor.

| Measure | Value |
|---|---|
| Feature folders (plus `TaomSettings.cs` at that level) | 109 |
| With at least one `path:line` cite in a lane file | 89 |
| With one anywhere (lanes, verify, triage, reconcile) | 95 |
| Source bytes in the 14 folders with no cite anywhere | 283 KB of 6,469 KB (4%) |

No `path:line` cite anywhere: DevConsole (26 files, 138 KB), AiPartySize (37 KB), MapEventGuard (22 KB,
a crash-guard feature on the trap index), InitialChildGeneration, PlayerPossession, StartupResources,
NazgulFamily, ReturnToArmy, LocalizationOverride, AdvancedStartOptions, SkipCampaignIntro (floor
artifact, see above), AtmospherePersistence, WeatherBoundsGuard, Encyclopedia.

Thin by weight (large gameplay folders whose lane cites are mostly table rows, with no finding whose
subject is the folder): CareerSystem 368 KB (7 lane cites; `CareerAgentStatService` is skip-listed),
CultureDoctrine 271 KB (5; ships toggled off), BattleLoadDiagnostics 204 KB (3), CulturalFeats 153 KB
(3), WarOfTheRingMomentum 114 KB (7, all ARCH-01 table rows), Diplomacy 94 KB (5, Harmony compat only),
CombatMechanics 91 KB (5, interface rows), SignatureStrikes 85 KB (2), CoopInterop 78 KB (1),
CultureConversion 76 KB (1). Co-op correctness (peer authority, dedicated server) had no lane: lane 4
says so ("Did not audit the co-op ... paths"), lane 1 covered co-op only as a design question.

### 1b. The playbook's ten categories

| # | Category | Coverage this sprint | Unaudited sub-bullets (playbook) |
|---|---|---|---|
| 1 | Correctness | Lane 3 (7) plus June re-triage | Off-thread writes (see 1d); boundary conditions (0 hits for `DivideByZero`, no per-culture dispatch-cell count); Python tools correctness (none) |
| 2 | Security | June items via triage-A, seeds F5/F6 | `/security-scan` never run by a lane (I ran it, 1c); `eval`/`exec` in 6 tools unreviewed; prompt-injection surface unchecked by any lane (I swept it, 1c) |
| 3 | Performance | Lane 4 (4) plus June PERF | Memory footprint (no lane; 1d); Python tools on large XML (lane 4 "out of scope") |
| 4 | Test coverage | Lane 5 audited test *effectiveness* only | The category's core question, "which untested code is dangerous" (critical paths with zero tests, churn times no tests), got only June's re-triage (L205 to L291); no fresh churn map |
| 5 | Tech debt | Lanes 1 and 2, extensive | Python tools duplication (lane 2: "not examined") |
| 6 | Deps | triage-B, DEPS-L6-05 | adequate |
| 7 | DX | Lane 6, extensive | "features that fail silently in-game": only CORRECTNESS-02's counts |
| 8 | Docs | `lint_docs.py` in baseline, DOCS-L6-07/08 | adequate |
| 9 | Game data | `validate_moduledata.py` (baseline), `audit_scene_names.py` (reconcile), June GAMEDATA re-triage | Not run by the sprint: `audit_battle_scenes`, `audit_action_set_parity`, `audit_polearm_shield_parity`, `check_prefab_budget`, `check_rdc_entries`, ranged-ladder `--verify`, `/xslt-check`; sprite refs; localization coverage (I ran or measured most, 1c) |
| 10 | Direction | June DIR re-triage only (triage-C) | No fresh direction pass; the GitHub backlog (the label query AGENTS.md names as the backlog) was never read (section 3) |

### 1c. Gaps I closed with a measurement (so they need no follow-up)

- **Security scan** (`python tools/audit_claude_config.py --json`, working tree): 9 findings: 1 HIGH
  `secret-generic` at `tools/tests/test_audit_repo_secrets.py:178` (the fixture BRIEF already rejects),
  1 MED `mcp-npx-unpinned` (seed F6), 7 INFO. Nothing new.
- **Prompt-injection sweep** (`git grep -l -i` for "ignore previous instructions" and similar phrasings
  over the tracked tree): 2 files, both the rule texts themselves (`.claude/skills/improve/SKILL.md`,
  `tools/audit_claude_config.py`). Nothing to report.
- **Python risk constructs in `tools/`** (329 non-test scripts, 5.1 MB; only 12 files carry a
  `path:line` cite anywhere): `shell=True` 2 sites, both the auditor's own pattern strings; `pickle.load`
  0; archive `extractall` 0; `eval`/`exec` 7 sites in 6 Blender or animation tools (`arp_retarget.py`,
  `creature_anim_ops.py`, `harness.py`, `rebuild_anim_from_json.py`, `skeleton_spec.py`,
  `spider_cfg.py`), unreviewed; `except Exception: pass` 43 sites in 15 files. Operator-run tools, so no
  top-10 impact.
- **Game-data auditors** (read-only, no write path: grep of each source for `open(...'w')`, `--apply`,
  `unlink`): `audit_action_set_parity.py` exit 0; `audit_battle_scenes.py` exit 0, "no crash
  suspects"; `check_prefab_budget.py` 93,830 of 131,072 entities, OK; `check_rdc_entries.py` **exit 1**:
  7 live-Armory packages with no `.rdc` (engine skips them silently: troll clips, `elephant_harad_armor_01`,
  `ani_dg_spi_idle_01`, `SK_Northern_Cape_A`) and 280 animation masters without an entry;
  `audit_polearm_shield_parity.py` **exit 1** on 4 stale `KNOWN_FAILURES` entries (the rosters it reads
  include another session's dirty equipment files), plus a WARN of 77 rosters and 35 troops carrying a
  two-handed weapon a shield troop never draws, which the tool labels "tracked separately" (not checked
  where). All live-install or roster-backlog state, owned by `/armory-audit` and #526; not a code
  finding for this sprint.
- **Sprite refs**: 97 distinct literal `*Sprite="..."` values in 49 tracked prefabs; 14 resolve against
  neither `Main/_Module/GUI/TAOMSpriteData.xml` (1,027 names) nor the 5 installed module SpriteData
  files (5,570 names in total). All 14 sit in 4 prefabs, 10 of them in `ROTsprite.xml`, whose names are
  another mod's (`bolton`, `dragonstone`, `nightswatch`), so it looks like a foreign orphan prefab.
  Whether any of the 4 is loaded is UNVERIFIED. LOW.
- **Localization structure**: 12 language folders, 17 string files each, `language_data.xml` rows
  match the files exactly in all 12. Unkeyed C# literals are few: 6 `new TextObject("...")` and 19
  `new InformationMessage("...")` sites without `{=`. XSLT-injected text was not checked. LOW.

### 1d. Gaps measured but not closed

- **Off-thread engine callbacks** (BRIEF: "violations are findings"): no lane audited them. The field
  signal is live: the only `[ERROR]` lines in the 30 local `taom_debug_*.log` files (68 lines, 7
  signatures) are `BehaviorTreeMissionLogic.<callback> ran off the main mission thread`, in 24 of 30
  logs. 18 `Main` files override agent or object callbacks (`git grep -c "override void (OnAgent...|OnObject...)"`);
  4 of those 18 call `RunOrDefer` (`BehaviorTreeMissionLogic`, `CareerPerkMissionBehavior`,
  `MountDespawnMissionBehavior`, `WargMissionBehavior`). I spot-read 5 of the other 14
  (`SignatureStrikesMissionLogic.cs:55` over a `ConcurrentDictionary` in `SignatureAgentRoster.cs`,
  `EnlistmentMeritMissionBehavior.cs:110-118` `Interlocked`, `ElephantMissionBehavior.cs:231-235`
  origin-local counter, `FieldCommissionMissionLogic.cs:32`, `MixedFormationsMissionBehavior.cs:65`):
  each is deliberately thread-aware (#634 sweep, `f75288eb`). Open issue #607 still says "five TAOM
  overrides are unguarded". No evidence of an unguarded writer, so no top-10 change; the gap is
  unmeasured completeness, not a found defect.
- **Memory footprint**: no lane measured it. Local `[MemSample]` lines (`taom_debug_2026-09-23_13-43-40.log`)
  reach `privMB=13688 ... heapMB=914` at 14:18. Managed heap is under 1 GB of it, so the bulk is native
  (assets), which the texture-downsizing work owns. Tracked elsewhere; not ranked here.

## 2. Load-bearing claims still unverified

### 2a. Lane findings no refuter saw: 6 of 42

Method: every `### [ID]` heading in `lane-*.findings.md` against `^##+ .*<ID>` in `verify-*.md`
(and a plain mention count, 0 for each of the six).

| ID | Unchecked load-bearing claim |
|---|---|
| CORRECTNESS-06 | Catch-to-vanilla after a committed side effect (`SupplyCaravanEncounterPatch.cs:43-50`, `Patch58_SkipCampaignIntro.cs:67-74`, the Patch71 RCA class) and the Diplomacy veto compat claim. MED confidence, the only unchecked correctness item with a crash-adjacent shape |
| ARCH-08 | Side observation: Custom Battle has no creature mount-lock (`TaomCustomBattleAgentStatCalculateModel.cs:25,36,43`); the lane marks it UNVERIFIED |
| COMP-05 | "The engine never reloads TAOM in process" (managed call graph only; native UNVERIFIED) |
| COMP-04 | "1 of 19 self-resolving mission behaviors is constructed by any test" |
| COMP-06 | FieldCamp ordering comment stale; Patch25 "must be first" |
| CORRECTNESS-03 | `Patch35_Mission_OnTick` is an empty per-frame postfix (low stakes) |

### 2b. PLAUSIBLE, or refuted in part, and the lane text was never corrected

The REPORT must take these from the verify files, not the lane files.

- CORRECTNESS-07 position pin: PLAUSIBLE (`MobileParty.IsActive` after abandoning a campaign untraced).
- DX-L6-01 and DX-L6-03 PowerShell-tool bypass extensions: PLAUSIBLE (matcher list read, not exercised).
- TEST-L5-08: REFUTED (BUTR builds from Steam, same day as the install).
- DX-L6-02 Step 1 prefilter: REFUTED as designed (a newline-led `git` arrives as `\ngit` and skips every
  gate); the corrected substring form is itself unproven live.
- DX-L6-04: the "bundle lacks the SHA" leg REFUTED (the zip carries the whole session log); "built at
  the parent" overstated.
- PERF-L4-01 fix sketch wrong: MCM `Instance` is null in `OnSubModuleLoad`, so a renamed default does
  nothing (verify-PERF-L4-01-repro link 2).
- TEST-L5-01 fix sketch conflicts with a same-day recorded decision (lessons `data-content-cultures.md`
  at `b2e387db` lines 1588-1591).
- ARCH-01 rule origin is ADR-002 (Accepted), so the fix is an ADR change; ARCH-02 binding row is a TAOM
  self-reflection, not an engine binding; ARCH-05 lead targets are self-declared engine boundaries;
  ARCH-06 gate gap is 11, not 14; ARCH-07 LotrIssue base class is save-safe if `_defId` moves up, and the
  Elephant/Mumakil ratios counted comments.

### 2c. Numbers that carry the impact but were never measured directly

- **Every boot and load cost is from one machine in one mode.** The PatchShield follow-up
  (`followup-patchshield.md`, "Measurements" and "Verdict") shows the per-`Harmony.Patch` cost on this
  desktop is bimodal: 154 fast first-pass-2 runs (median 2.3 s, 5 to 10 ms per attach) and 259 slow
  (median 66.7 s, about 186 ms per attach), slow since 2026-06-12 14:08 except 2026-09-01 to 03, same
  modlist and Harmony 2.4.2; root cause "not found". PERF-L4-01's "about 30 s of every boot, every
  player" and the 69 s pass 2 both assume the slow mode is universal; both PERF-L4-01 verifiers
  accepted that. No player-machine per-attach rate exists in any file. `crashz/` is not a counter-sample:
  its manifest names `Logs\taom_debug_2026-08-08_21-36-44.log`, and verify-PERF-L4-01-repro matched
  it to this desktop's 34 s boot gap.
- PERF-L4-02, PERF-L4-03, PERF-L4-04 frame cost: UNMEASURED (volume measured for L4-02 only; scale
  numbers for L4-03/04 are densities the finder assumed).
- COMP-01 after the first throw: "the save never finishes loading" is UNVERIFIED (repro).
- TEST-L5-07 hosted compile with `Bannerlord.BuildResources` and an empty `GameFolder`: UNVERIFIED by
  the lane and both verifiers.
- DX-L6-04 SDK target names; DX-L6-06 Stop decision-control JSON (the fetched page was truncated).

### 2d. Claims raised only by a verifier or the follow-up, never checked by a second agent

`TaomStateCollector` registered with no providers, so every bundle's TAOM state fields are null
(`CrashReportIoC.cs:34-35`, verify-a-batch-04); `com.taom.mod` is not a PatchShield-protected owner, so
an engine-drift exception would silently `Unpatch` TAOM's own patches (`PatchShieldPolicy.cs:23-95`,
follow-up); the crash-loop marker flags 40 of 453 launches as a previous crash loop, 35 naming a
"likely culprit" mod (follow-up); both CrashReport MCM toggles are dead and their hints false
(verify-PERF-L4-01-repro); `settings.local.json` ships `Bash(node:*)`, `Read(//c/Users/mikew/**)` and
`Bash(git checkout *)` pre-approvals to every clone (verify-b-batch-06);
`TroopCountDiagnosticsBehavior` static event subscription per campaign (verify-a-batch-01);
`Clan_UpdateBannerColor_Patch` postfix with no catch over a throwing reflection call
(verify-a-batch-02); a vacuous stub pass in `PlayerClanLeadershipServiceTests` (verify-b-batch-02);
discovery-floor Inconclusive in the binding gate (verify-TEST-L5-01-repro); ADR-003 and ADR-008
promise a pre-commit installer never committed (verify-b-batch-03).

### 2e. June STILL_VALID verdicts

71 entries; `triage-check.md` re-read code for L21, L69/L93 and L140 only ("I accepted them without
re-deriving call frequencies"). The other MED verdicts (L256, L273, L282, L404/L412, L533/L586, L624,
L775/L790) rest on one triage agent each. They are known and calibrated, so they do not move a ranking.

## 3. Sources that should have been read and were not

1. **The GitHub backlog.** AGENTS.md:77-78 makes the label query the backlog. Read-only
   `gh issue list --state open`: 99 open, 49 `bug`, 7 `triage-needs-ingame`, 3
   `triage-blocked-decision`. Of 16 sampled bug issues only #634 appears in any audit file (lane 3, in
   passing). Overlaps the REPORT must dedupe: #491 (Arena `OnTournamentEnd` null paths) against
   CORRECTNESS-01's Arena lines; #593 (BuildStamp MISMATCH on every release, visible on line 2 of the
   newest local log) against DX-L6-04; #623 against DX-L6-01; #573 and #578 against CORRECTNESS-07 and F3;
   #607 against section 1d; #501 (release lines diverged) against the branch findings in DX-L6-03 and
   DOCS-L6-08. #482 (vanilla `SpawnCaravan` null-template deref, no guard: `git grep -n SpawnCaravan b2e387db -- Main`
   returns nothing) is filed as a latent hazard, "not a live bug", so it does not outrank the list.
2. **Player-machine runtime evidence** (2c): a diag.log from any machine other than this desktop.
3. **`Modules/TAOM.Dependencies`** beyond PatchShield: only the follow-up read `diag.log`; SaveShield
   (the 6 swallow lines in diag.log are its own) was never read by anyone.
4. **The RCA and lessons corpus as a source of open items**: 252 RCAs used by filename slug and for
   by-design checks; never swept for "still open" follow-ups (two verifiers hit such lines by chance:
   `rca-lord-identity-2026-08-29.md:72-80`, `rca-race-fertility-2026-09-19.md` row 2).
5. **Category 9 auditors and `/security-scan`**: not run by the sprint; I ran them (1c).
6. `.ai/`, `.agents/`, `.codex/` beyond command greps, and `Main/_Module/GUI/` beyond three prefabs.

## 4. Verdict: one gap can change the top of the ranking

**Yes, one.** The two largest measured player costs, PERF-L4-01 (31 s at boot) and PatchShield pass 2
(69 s at the first game start; the follow-up puts PERF-L4-01's true per-process cost at about 77 s
because the same 247 shims are shielded again), are counts times a per-`Harmony.Patch` tax that is
186 ms on this desktop in its slow mode and 5 to 10 ms in its fast mode. If players run fast, both
shrink to about 1 to 3 s and fall out of the top 10; if slow is universal, together they are the
largest player-facing item in the sprint and belong first. Nothing in the run can tell which.

**Follow-up task (one agent, one question):** "Does a machine other than this desktop pay the
slow-mode tax of about 186 ms per `Harmony.Patch`?" Read-only. Compute ms per attach from the
`PatchShield.Install (pass N)` and `shield pass: +N new` line pairs (the follow-up's pairing method) in
at least one `diag.log` not written on this desktop: a player crash bundle attached to or linked from a
GitHub issue (bundles carry `diag.log`, `CrashBundleWriter.cs:19,48`), or the laptop's install. If none
exists, say so, and read what is readable without admin on this desktop for the 2026-06-12 13:38 to
14:08 flip and the 2026-09-01 to 03 fast window (Windows Update and install history, Application and
Defender Operational event logs, file times of `0Harmony.dll`, the MonoMod DLLs and the
`Bannerlord.Harmony` module). Output: fast, slow or unknown for players, with the evidence line.
Independent of the answer, the follow-up's option 1 (exclude `ManagedCallbacks` from PatchShield) and
PERF-L4-01's code fallback flip stay S-sized and help in both modes; only their rank depends on it.

Not a follow-up, but an orchestrator step before the REPORT: map each top-10 item to the open issues
in section 3 so no ranked item duplicates a filed one without saying so.

## What I did not cover

- No build or test. The security scan and the five auditors ran on the working tree, which carries
  another session's edits (the polearm auditor's stale entries may be theirs).
- The feature-folder measure counts `file.cs:line` cites only; a cite split from its file name is
  missed, and a cite is not proof of review (many are table rows).
- I did not read the off-thread overrides beyond 5 of 14, did not trace `ROTsprite.xml` or the other 3
  prefabs to a loader, did not check XSLT-injected text for harvesting, and did not look for where the
  polearm tool's "tracked separately" WARN is tracked.
- Of the 99 open issues I read five bodies (#482, #491, #501, #511, #607) and titles only for the rest.
- I did not re-verify any lane or verify claim beyond the counts and spot reads cited above.

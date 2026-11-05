# X: cross-plan consistency, waves 2 and 3 (plans 117 to 127)

Critic run, 2026-11-04. No plan was edited. Line numbers are in the plan files as they stand on disk today.

## What this report covers, and what it does not

The brief asked for plans 117 to 127 to be read in full. They total 1.23 MB, which did not fit in this session, so
**coverage is partial and this report says so**:

- **Read directly for all 11:** Status, Scope, prerequisites tables, Maintenance notes, and heading outlines.
- **Checked by script across all 44 plans:** every Disposition table against `classified.jsonl` and `wp_map.json`;
  every sha mentioned beside a WP number, tested for presence in that WP's plan; patch category numbers; `dotnet`
  flags; subject length and label; dash characters; `README.md` dependencies against the reviewed list.
- **Read by targeted grep and excerpt:** the step text behind each finding below.
- **Not read:** most Step bodies of plans 117 to 127 line by line. A defect that shows only inside a Step body and
  names no other plan could be missed.
- **Other plans:** Status and Scope read for 128 to 134. For 135 to 144 only excerpts and greps were read; the
  remaining Status and Scope text was too large for what was left of the session.

Every finding below was confirmed against plan text or git this session. Claims about runtime behaviour are marked.

## Summary

| Check | Result |
|---|---|
| Owned commits missing from their plan's Disposition section | None (all 42 plans with owned groups) |
| Cycles or reversed dependency edges | None; every edge in the reviewed list points to a lower id |
| Same hunk ported twice | None found; every shared sha is a declared split or a declared reference |
| Bad commit subjects in plans 117 to 127 | None; the only two script hits are prose lines, not subjects |
| 1.5.x modules hand-wired against D2 | None found for the features in these plans |
| Patch number collisions | None; 1.4.x stops at Patch88 |
| Defects needing a plan edit | 16 per-plan fixes, 5 programme issues |

## Per-plan fixes, most severe first

### 1. WP122 and WP118: the runsettings hunk can fall between them (dependency gap, shared hunk)

- `118:37`, `:235`, `:781` skip `a27c6b338`'s `binding-gate.runsettings` hunk and cite D9.
- `122:325` takes the hunk only if `TAOM_RUN_BENCHMARKS` is already in its own base.
- `122:1795-1798` says that if WP118 merges after WP122, WP118 must take the hunk. Plan 118 has no such step.
- Both are wave 2 with no edge. If WP122 cuts its branch before WP118 merges, neither side takes the hunk, whatever
  the merge order. By WP122's own account the strict gate then fails `XmlMergeLiveEquivalenceTests`.
- **Fix:** add WP118 to WP122's "Depends on". Make the A1 variant the only path and delete its condition. Delete the
  "otherwise WP118 takes it" wording at `122:1679` and `:1795-1798`.
- **Also:** D9 covers CI and plan 010, not plan 008, so D9 is the wrong citation. In `118:37`, `:235`, `:781`, say the
  file arrives with WP122 (which is the plan that takes this hunk).
- **Also:** `118:938` writes "1.4.x has no `binding-gate.runsettings` (D9)" into the shipped feature doc. WP122 A1 must
  rewrite that sentence in `docs/features/xml-merge-fast-path.md`, and needs it added to its Scope.

### 2. WP118 Step D2: anchor and new text both collide with WP103's command gate

- `118:1250-1252` replaces the line `` Check: `dotnet test TAOM.Tests --filter DependenciesPairing` `` in
  `docs/modding/module-dependencies.md` by exact text, quoting the `1b00d1acd` form, and writes an unflagged command.
- WP103 creates `tools/tests/test_dotnet_commands_flagged.py`, which scans `docs/modding/**/*.md`, and fixes every
  listed line by adding both flags (`103:358-430`). That line is one WP103 must rewrite.
- So in wave 2 the old text no longer matches, and if the executor forces the edit the new line fails the tool test.
- **Fix:** anchor on `--filter DependenciesPairing`, and write the replacement with
  `-p:DisableModuleCopy=true -p:ModuleId=`.

### 3. WP133 has no step for WP125's #993 re-check (shared hunk neither side ports)

- `125:1694` requires that, once WP133 ports #993 (Patch23 rebuilds the code), the party cache is re-checked, the
  comment from `963218f7f` is taken, and `PartyFastOpenBindingTests` is confirmed.
- `Shadowfax` appears zero times in plan 133. `963218f7f` is a merge commit with no row in `classified.jsonl`.
- **Fix:** add a step to WP133 Part B after the Patch23 retarget. Take the `963218f7f` comment for
  `PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch.cs`, add `FullyQualifiedName~PartyFastOpenBindingTests` to
  the filter, add the file to the Stage list, and add a Disposition row for `963218f7f`.

### 4. `af990ca76`'s inventory corrections have no owner (shared hunk neither side ports)

- `117:1681`, `:1708` and `:1799` leave them for "the integrator to assign". Plan 120 does not mention the commit.
- **Fix:** give `docs/features/battle-load-diagnostics.md` to WP120 Part A3 (already in its Scope), using the v1.4.8
  citations at `117:1799` (`GameStateManager.cs:257`, `:277-279`, `:285`). Give `rca-memory-instruments-2026-09-12.md`
  and `native-commit-audit-2026-08.md` to WP142. Add a Disposition row to both. Change plan 117's wording to name them.

### 5. WP117 and WP118: the "until WP118" doc sentences have no remover (orphan)

- `117:939`, `:979`, `:1316`, `:1321`, `:1341` write "arrives with WP118" and "until WP118" sentences into feature docs.
- `117:1798` says WP118 "should then drop" them. Plan 118 mentions none of those docs (grep count 0).
- Left alone, those docs state something false from the moment WP118 merges.
- **Fix:** add a conditional edit to WP118 Part C: if `docs/features/mission-perf-heartbeat.md` or
  `docs/features/map-perf-profiler.md` exists, remove the listed sentences and convert the link. Add those docs and
  `docs/features/time-acceleration.md` to WP118's Scope. If WP118 merges before WP117, plan 117 must not write the
  sentences: add a `test -f docs/features/map-load-diagnostics.md` branch to plan 117.

### 6. WP117 and WP120: `map-perf-profiler.md` rows are deflected both ways (shared hunk)

- `120:1809` says that if WP117 merges after WP120, WP117 "owes the three rows".
- `117:1726` and `:1799` say the rows belong to WP120. Plan 117 has no step for them.
- If WP120's base lacks the doc, it lists them as owed in a hand-off that has no named reader.
- **Fix:** name the integrator explicitly in both plans: after both merge, the integrator applies the rows at
  `120:1433-1436` and `:1601-1604`. Or add a conditional step to plan 117's B1, as WP120 already has in reverse.

### 7. WP136's ADR text has no maintainer step (decision routing)

- `136:791` leaves `8caac7da5`'s ADR edits "for the maintainer to apply with WP127's ADR edits".
- `8caac7da5` does not appear in plan 127, and WP127's Part M runs two waves earlier, so it cannot carry them.
- The brief requires a plan to give the exact diff for `docs/adrs/*.md` and route it as an explicit step.
- **Fix:** add to WP136 a maintainer step with the exact diff for `002-thin-entry-points.md` and
  `007-adapter-pattern.md`. Plan 143's conditional at `143:653` then has something real to test.

### 8. WP123: MCM facts are stale once WP118 Part D merges

- `123:38`, `:121`, `:190`, `:684`, `:772`, `:964` state that 1.4.x ships MCM 5.12.1. The `:684` drop reason says so
  outright, and the Research trailers repeat it.
- WP118 Step D1 moves `Main/TAOM.csproj` and the vendored stack to 5.12.3 in wave 2, and WP118 also edits
  `docs/modding/module-dependencies.md`.
- **Fix:** read the version from `Main/TAOM.csproj` at A0 and write trailers from that. At `:684`, change the reason
  to "WP118 owns that doc". Re-verify the `GlobalSettings<T>.Instance` and `BaseSettingsProvider.Instance` facts at
  `123:190-198` against 5.12.3, since `CountingMcm` depends on them. [Likely] they are unchanged because the 1.5.x
  tests ran on 5.12.3, but this session did not read 5.12.3.

### 9. WP122: the "every transpiler" rule is false for later and same-wave transpilers

- WP122 B1's rule line says every TAOM transpiler gets a test in `TranspilerSiteBindingTests`, and its commit subject
  says "every transpiler". It has nine tests.
- `TranspilerSiteBinding` appears in no plan but 122, 131 and 143. Plans 117, 120 and 125 add transpilers. Plan 125
  (wave 3, after WP122) relies on its own JIT-acceptance tests (`125:1580`).
- `122:1781` names the wrong plans: WP124 has no transpiler (grep count 0), and `f83a63e57` belongs to WP131, not WP128.
- **Fix:** change the rule to "a test in `TranspilerSiteBindingTests`, or a feature binding test that runs it over the
  installed IL", and correct `122:1781` to WP117, WP120, WP125 and WP131. Reword the commit subject to match.

### 10. WP123: wrong owners for `6ae924638`'s hunks

- Scope line "career quest owner: WP126; HeroRace flag: WP114" and `123:1152` "Other hunks: WP112, WP126, WP114" are
  wrong. The career quest hunk is WP129 (`129` Disposition) and the HeroRace hunk is WP119 (`119` Scope).
- **Fix:** rewrite both to "WP112, WP119, WP129".

### 11. WP123: the Maintenance note names plans that do not edit its test

- The note says WP122, WP125, WP131 and WP133 edit `CampaignHotPathSettingsProvidersTests.cs`.
- WP122 is wave 2 and never names the file. WP133 skips that hunk (`133:2238`).
- **Fix:** reduce it to WP125 and WP131. No dependency edge from WP133 to WP123 is needed.

### 12. WP120 and WP123: NameplateCull provider rows are offered, never taken

- `120:1817-1818` says WP123 "can add" the `NameplateCullSettingsProvider` rows from `ecaa67d26`. Plan 123 never
  mentions NameplateCull. WP125 solved the same problem with a conditional step.
- **Fix:** add a Step C2 item to WP123: add the two DataRows and the `BattleLoadDiagnosticsSettings` row, then fold
  `NameplateCullSettingsProviderTests`. Or record it as declined in both plans.

### 13. WP118: `1325c36ea`'s Yotthani provenance note has no owner

- `118:1255` and `:1705` defer it "with the WP that ports NameplateCull", which is WP120. `120:1679` says "Not here",
  and WP120's provenance edits touch only the VanillaTuning row.
- The ButterLib attribution ("Fourth pass on 2026-10-06") therefore lands nowhere.
- **Fix:** add the sentence to WP144's provenance step at `144:964`, adapted, and change `118:1705` to name WP144.

### 14. WP124 and WP126: stale `RequiresGame` pointers, D9 misapplied

- `124:1152` says the tags "are WP122's under D9". `124:1254` says "WP122, if it brings `RequiresGame`". `126:295`
  says D9 "makes that optional". But WP122 skips those tags (`122:232` and its Disposition), and D9's only optional
  item is plan 019.
- **Fix:** all three should say the tags are not ported (D9).

### 15. WP117, WP119 and others: "only editor while it runs" is false in a parallel wave

- WP117's and WP119's Scope call `Main/SubModule.cs` single-owner with this WP as its only editor while it runs.
  WP118 also edits it, and all three are parallel lanes of wave 2.
- WP117 (`:1287`) and WP119 (`:164`) both insert into the `OnGameEnd` teardown block.
- **Fix:** reword to "edits only these anchors; the integrator resolves". Add an integrator note that both `OnGameEnd`
  lines survive.

### 16. Pointers in earlier plans

- `104:1226` and `:1337` name "WP 126" for `35007dbb8`. The final map gives it to WP129, as `126` Scope states.
- `108:1591` says "/verify-bindings (WP122 or the integrator) regenerates" `patch-targets.md`. `122` Scope forbids
  that and WP137 owns it.
- `121` Part E adds a `tools/README.md` row, though the brief reserves that file for the sync WPs. Route it through
  the hand-off (`142:552` already reads hand-offs).
- `124` Status omits D16, which its body uses, and D10, which the other ten plans in this range all cite. Add both.
- **Optional:** add WP123 to WP127's "Depends on". `127:1418` and `123` Maintenance already agree that shape B is
  the correct result, and README table order already puts WP123 first. The edge removes shape A and its Step B0
  detection, and the conflict if the branches are cut in parallel.

## Programme-level issues

1. **`plans/README.md` is stale against the reviewed dependencies.** WP117 shows 104, 105, 106, 107, 109, 111 (reviewed:
   106, 107, 109, 111). WP130 shows 105, 123 (reviewed: 105, 123, 110, 127). WP133 shows 105 (reviewed: 105, 104, 101).
   WP136 still shows `?` for priority, effort and risk. Regenerate the table, as `PROGRESS.md` already plans.
2. **No single rule for `patch-targets.md`.** WP117, WP118 and WP133 leave it to WP137. WP107, WP120, WP123 and WP125
   edit or regenerate it. WP120's `SNAP0 = 0` branch and WP107's `S0` branch look dead, because `-Check` cannot pass
   before WP137 (`122` Scope, `137:79`). Nothing breaks, since WP125 Step 10 tolerates missing rows and WP137
   regenerates. Decide one rule: hand-kept nowhere until WP137, or hand-added everywhere.
3. **Wave 2 shared-file load.** WP117, WP118 and WP120 all edit `BattleLoadDiagnosticsSettings.cs`,
   `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`, the two co-op docs and, for 117 and 118,
   `PatchShieldPolicy.cs`. Every plan says the integrator recounts, but no plan gives the recount procedure. Add one
   recipe to `plans/README.md`'s integrator section.
4. **Merge table order versus branch base.** The README merges in table order, but lanes cut branches in parallel. Any
   step conditional on "if WP x merged first" reads the executor's base, not the eventual merge order. Findings 1, 5
   and 6 all come from this. Rule: a conditional on a same-wave WP needs either a dependency edge or a named integrator
   action.
5. **Dash characters outside this scope.** The script counted em or en dashes in plans 102 (5), 115 (5), 130 (1) and
   144 (10). Plans 117 to 127 have none. These may be quoted text or script literals; this session did not inspect them.

## Checks that found nothing

- D2: every module named by plans 117 to 127 (AnimMemoryProbe, LostFooting, LoadTimeStamps, XmlMerge,
  ButterLibDistanceMatrix, NameplateCull, MapViewRelease, ShadowfaxScreens) is in the `e45f6df2c` `FeatureModules.cs`.
  MapLoadDiagnostics, CombatMechanics, Enlistment, SpecialResources and HeroRace are not, so hand wiring is right.
- D5, D6, D14, D22, D23, D24: the Status and Scope text of the plans that carry them states them as the brief does.
  The D5 row with `0x88681` appears in plan 109 as the brief gives it.
- WP110's allowlist entry is deleted by WP123 (`123:411`). WP105's mission anchor is moved by WP123 (`123:458`) and
  again by WP130 (`130:1544`), with no gap.
- WP127's `PathReuseCache` deletion handles both the DataRow and the catalogue row, and WP137 checks for it.
- WP122's `ALLOWLIST_ROW` note matches `143:918`. WP124's `ReferenceIdentity` is not re-added by WP130, whose port is
  by hand and never names that type.
- Baseline references (`119:426`, `124:346`) match 9154, 9151 and 3. All plans use the template's worktree form.

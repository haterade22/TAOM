# Adopting yotthani's delta of 2026-10-29: engine findings, campaign rules, head authoring and two TAOM fixes

Written for: TAOM maintainers deciding what to take from yotthani's two repositories, and for yotthani (the
"For yotthani" section is meant to be forwarded). Procedure: `/upstream-review`, the ninth review of these sources
(the eighth of MithrilForge, the sixth of `yotthani/bannerlord`), after
[adopt-yotthani-2026-10-16.md](adopt-yotthani-2026-10-16.md). The request: review the latest changes, find what TAOM
can use in its own code (performance first), and take all the documentation and knowledge into TAOM's processes.
TAOM is on Bannerlord v1.5.6 and v2.0.36.

**Summary.** No upstream code was taken. Every measured performance fix in yotthani's engine-findings document is
already in TAOM, so the performance value of this delta is knowledge: what not to optimise, and how to measure a
GPU-bound case. The review produced two TAOM fixes, built beside this record: four creature behaviours that never
detected a missing action set (#1065), and a stale party popup prefab that hid the horse-use tooltip (#1051, step 4
for that file). The knowledge went into the owning TAOM docs (the batch diff names them) and one new reference, each fact tagged with its source,
evidence kind and engine version. One correction runs the other way: TAOM's own face docs had wrongly called deform
keys 1 to 58 sliders.

## Sources, and what was read

| Source | Commit range | Read |
|---|---|---|
| `yotthani/bannerlord` (private, shared with Mike by its author; MIT by the maintainer's statement of 2026-10-09) | `2ff002be` to `6265f4a2cce3d85364ba8aa7f803f32c6270178a`, 817 non-merge commits | `docs/taom/side-boundary-report.md`, the Armory auto-resolve and party-positions specs and plans, `docs/balance-sim` (facts only), `HoN/VanillaTuning_TAOM` side rules, `HoN/CampaignProbe` README and readers, `HoN/PerfProbe` (MapGfx), `bn faces/` (232 commits: HeadExtract notes, groom specs, LiveDump), the RealmUI research docs and the engine-carrying commits, `HoN/Armory_TAOM`, `HoN/ElbinTest`, `HoN/RingSystem.*` build fixes |
| `yotthani/MithrilForge` (private, MIT) | `ae7834aa` to `d09f44a4ab5f0468c880ffa39306d8dcb1a817eb`, 8 non-merge commits | `docs/engine/for-taleworlds/bannerlord-engine-findings.html`, the new `docs/engine/perf.md` section (map graphics, four runs), `docs/engine/battle-behaviour-measured.md` (the deploy-test retraction), one map-graphics run's CSV and log, `tools/perf-audit/run_scenario.ps1` |

Four Sonnet readers went through the areas (performance; campaign and side rules; faces; UI and Armory). One Opus
verifier then re-read 15 load-bearing claims against the pinned files, the v1.5.6 managed decompile, the v1.5.6 native
client (Ghidra) and TAOM at `0c5d81408`, and tried to refute each. The main session spot-checked three of its verdicts.

## Security

- **Safe to learn from:** yes. Both repositories were read through blobless clones, read-only; nothing was built,
  installed or run, and upstream text was treated as data.
- **Licences:** MithrilForge is MIT. `yotthani/bannerlord` is MIT by the maintainer's statement of 2026-10-09 and still
  has no root `LICENSE` file (asked for again below).
- **Comparison only:** no code, data or asset was taken, so the provenance rows stay `comparison-only`.

## What TAOM verified (2026-10-29)

| Claim | Verdict and evidence |
|---|---|
| A behaviour that throws during mission start makes the load retry for ever | Engine fact confirmed on v1.5.6 (`MissionState.cs:333-345`, `Mission.cs:3845`, `TickLoading` :221-233). **Not a TAOM gap:** Patch103 `MissionStartGuard` wraps the six start calls since 2026-10-08 |
| `GameStateManager` forces `GC.Collect()` on every push and pop | Confirmed (`GameStateManager.cs:288, :316`, `Common.cs:244`); TAOM already records it (`battle-load-diagnostics.md`). The 60 to 160 ms figure is yotthani's |
| A formation order during deployment moves nobody | Confirmed, with a qualifier: only when `HasDeploymentBoundaries(team)` (`Mission.cs:6671`, fallback `Formation.cs:1331-1364`); agent teleports are not affected |
| The agent limit and a hang with soldiers in reserve near it | Limit confirmed (`GetMaximumNumberOfAgents` at `0x6E5EE0` on v1.5.6 returns 2040); the hang is yotthani's observation only |
| "Kopf als Morph": the always-on `eyebump` channel carries a base head to its shape, and vanilla groom follows | Formula confirmed on v1.5.6 (`0x56F8C0` adds `((key_max-key_min)*slider+key_min)*delta[frame][bound point]`); one gate (key `+0x74`) unresolved |
| Channel 51 is `hide_ears`, not a slider | Confirmed in Native `skins.xml`; keys 46, 51, 57 and 58 are not user sliders. `EarsAreHidden` is also true for cover type `All` |
| QTangent handedness sign | A real conflict between MithrilForge `TangentBasis.cs` and FaceLearner after `73b6fe29`; which is right is UNVERIFIED |
| `cover_type4` is the hair variant with no helmet and no body armour | Confirmed (`Equipment.cs:83-99`) |
| A bool, int or float view-model setter raises only its typed change event | Confirmed; TAOM's three listeners are safe |
| Armory_TAOM and RealmUI reach into named TAOM members | Confirmed, and the readers' list was incomplete; the full list is in `external-mod-couplings.md` |
| The inventory's apply-changes switch skips `DoneLogicExtrasDelegate` | Confirmed; TAOM's `ProvenderSaleAdapter` is safe because Trade mode locks that path |
| The three Free/Shadow gaps | Real in TAOM code; the measured peace counts likely predate `2410aec51` (no start wars, no AI wars before day 30) |
| `CompanionsCampaignBehavior.DailyTick` swaps wanderers daily | Confirmed on v1.5.6, so the side report's "wanderers never move" is wrong |
| `IsAtConstantWar(clan, kingdom)` is asked before a minor clan considers a contract | Confirmed (`DiplomaticBartersBehavior.cs:117`); its side effects for a side rule are UNVERIFIED |
| Armory's auto-resolve hook patches the most-derived `SimulateHit` | Confirmed statically: `TaomCombatSimulationModel` is that override at HEAD |
| `MBActionSet.GetActionSet` returns set 0 on a missing id | Re-confirmed on v1.5.6 by the main session on 2026-10-29 (`native_decompile.py --engine-method MBActionSet.GetIndexWithID`: `0x6DFF50`, which calls `0x5907E0`; `--rva 0x5907E0` shows the "could not be found, using default action set!" log and `return 0`) |

## Decisions

| Item | Decision | Where |
|---|---|---|
| C0, the action set check | Built and reviewed beside this record: `ActionSetLookup.Found` compares the looked-up name with the id, used at the four call sites, test first; two docs corrected; the doc review then corrected the same "a missing set throws" claim in the other live docs and in the unsent community guide update | #1065 |
| C1, the party popup | Built and reviewed beside this record: `PartyTroopManagerPopUp.xml` re-based on v1.5.6, TAOM's close button kept (its intent stays unverified); `PrefabCloneWidgetReferenceTests` now also requires a vanilla `DataSource` to survive in a clone | #1051 |
| C2, the Free/Shadow boundary | No code now (Mike's choice of the recommendation, 2026-10-29): the gaps are real, but the measured counts likely predate `2410aec51`. A change needs a fresh measurement on 2.0.36 | `diplomacy.md`, `wanderer-allegiance.md` |
| C3, the troll ring's knockdown on every unblocked hit | No change now: the pin-down loop is yotthani's observation; an in-game check decides | `troll-brute-force.md` "Known limitations" |
| C4, a repeatable map-graphics benchmark | Not built: the measured runs already answer the question for now | `map-perf-profiler.md` |
| Knowledge items | Taken, as restated and credited facts in the owning docs | the docs this batch touches |

## Not taken

| Item | Reason |
|---|---|
| RealmUI, Armory, Armory.Sim and the sim app | yotthani's products; ArmorySim stays standalone. Only their engine facts are restated |
| CampaignProbe as a tool | it carries yotthani's paths and module list; its measured facts are restated |
| FaceLearner's native head swap | gated to 1.5.3 to 1.5.5 and off on 1.5.6 |
| RingSystem API drift | TAOM's models derive from `Default*` classes and inherit the new members |
| The balance-sim tuning proposals | simulator predictions for Mike's balance work, not facts about TAOM |

## Findings for Mike (no code here)

1. **In-game checks owed:** the party popup after the re-sync (layout and the horse-use tooltip), and #1065's "not
   found" line with a renamed set in a test copy of the Armory.
2. **The Free/Shadow measurement is worth repeating on 2.0.36** before any side rule is built.
3. **The troll ring's knockdown:** watch a troll fight for a soldier who never gets up between low-damage hits.
4. **Renames of the members in `external-mod-couplings.md`** silently switch off yotthani's mods; tell yotthani before
   such a rename.

## The doc review

Three finders read the knowledge batch, one each for fidelity to the source (22 findings), consistency with TAOM's
code and docs (6) and routing and form (12); an Opus verifier then re-read every finding, and this record as a 41st
item, against the pinned upstream files, the v1.5.6 engine and the worktree. Verdicts: 27 confirmed, 13 partly, 1
refuted. Every confirmed and partly confirmed fix is applied. The ones that changed a conclusion:

- **The retracted deploy battles:** yotthani retracted 30 battles run with the deploy switch on 15 and 16 October
  (`battle-behaviour-measured.md`@d09f44a4 line 125, "mostly not moved"); that they are the 19 and 11 battles TAOM had
  restated comes from the dates alone, so `formations-and-team-ai.md` now says it is TAOM's reading.
- **The campaign band's build:** two rows mix the 8 runs of 18 October with 14 runs of 21 October; that the runs
  predate `2410aec51` is TAOM's [Likely] inference from a start war declared at a median day 17 in the same runs
  (`campaign.md` line 1097), since that commit allows no AI war before day 30. A counting window ("wars from day 7")
  had been read as build evidence and is no longer.
- **The one-entry tattoo list:** yotthani's own `KopfMessung.cs` lists it among four guesses "alle im Spiel
  widerlegt" (all refuted in game); the head doc no longer states it as the mechanism.
- **The "throws on a miss" sweep:** the refuted claim still stood in nine live docs and four community guides beyond
  the two #1065 named; all are corrected (the snapshot of the live Armory and the review history stay as written).
  The community guides are corrected in the repo only; rebuilding and sending them stays Mike's step.
- **Two routing moves:** the troll knockdown decision went to `troll-brute-force.md` "Known limitations", and the
  garrison recruitment bullet to its owner, `settlement-economy-food-prosperity.md`; facts written twice now have one
  owner and a link.
- **Refuted:** two trap-index lines for the couplings and the late `DailyTick` listener. Both were over 180 characters,
  the index is at its 45-row cap and the entry docs are 3 bytes under budget; adding either means merging an existing
  row, which is Mike's call.

The code review of #1065 and #1051 is in [rca-actionset-guard-popup-2026-10-29.md](rca-actionset-guard-popup-2026-10-29.md).

## For yotthani

Thank you for the engine findings document, the map-graphics runs and the side-boundary report, and for retracting
the deploy tests in the open; that retraction is the more useful result. TAOM restated what it could check, credited to
you in each doc. What we would add, in case it helps:

1. **QTangent handedness:** MithrilForge `TangentBasis.cs` (lines 120 to 123) and FaceLearner's `Program.cs` after
   `73b6fe29` (lines 4147 to 4151) use opposite signs for the same tangent w. FaceLearner's comment says it measured vanilla's sign ("VORZEICHEN GEMESSEN": `head_male_a` 1389 of 1389, `body_male_a` 1584 of 1586); if that holds, MithrilForge's `TangentBasis.cs` still carries the old sign. TAOM has not read a vanilla QTangent itself.
2. **The side report's wanderers:** `CompanionsCampaignBehavior.DailyTick` calls `SwapCompanions()` every day (v1.5.6
   lines 145 to 150), so wanderers do move. Your code sweeps daily, which is right, but a listener added later runs
   before the game's (newest first), so a wanderer can sit on the wrong side for up to a day.
3. **Peace by barter:** the AI builds a `PeaceBarterable` only for kingdomless clans; realm peace goes through
   `MakePeaceKingdomDecision` and `MakePeaceAction`.
4. **The 134 Free/Shadow peaces** likely come from a TAOM build before `2410aec51`, which removed the start wars and
   AI wars before day 30.
5. **`campaign.md` line 817** names a TAOM file `AiBehaviorHook.cs`, which does not exist at `0c5d81408`.
6. **The asks of 2026-10-15 stand:** a root `LICENSE` file in `yotthani/bannerlord`.

Thank you again.

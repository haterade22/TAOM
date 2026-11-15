# RCA: review of the scenario autorun, #1075 slice 1

The scenario autorun (02bd1e3d2) starts a new campaign with nobody at the keyboard, runs it to day N on fast forward and quits;
`tools/run_scenario.py` launches and judges it. The review ran at tier 1 (campaign start, the client application tick, DI
composition, engine calls): lens 5 (data flow), lens 2 (engine compatibility) and the tooling lens, each at the agent definition's
`max`. Codex did not run: its usage limit blocks it until 2026-11-20, so that pass is owed before the slice is called reviewed.
Mike asked for the work in progress to land on trunk before the review ended, so 02bd1e3d2 carries a "Known limitation" paragraph,
and every fix below is on the branch `feat/1075-scenario-autorun-review`. No game run has happened yet.

| # | Sev | Finding (source) | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The default culture `empire_w` is the Gondor kingdom id, not a `cultures.json` `culture_id`, so every default run would end `creation_failed` (lens 2) | Wrong id kind | The id came from the #1075 plan text ("empire_w (Gondor)"). The culture check moved to run time when `CultureExists` left the host (no campaign objects at the main menu), and no test tied the default to the data | Fixed: default `gondor`; `DefaultCulture_IsACharacterCreationCultureId` (C#) and a Python test hold both defaults to `cultures.json` and to each other. Repeat of `data-content-cultures.md` "Verify every referenced troop/item/clan ID against the canonical XML"; new lesson there |
| 2 | MED | `StopGameOnFocusLost = false` "in memory" reached the player's config: `Module.FinalizeModule` destroys the game and then calls `BannerlordConfig.Save()` (Module.cs 1348 to 1362), so a manual exit mid-run kept it (lens 5; read again by the session) | Persisted side effect | Nobody listed the writers of the config file to disk. The Fords and Long Defeat views avoid this on purpose (`FordsCommandView.cs:43-47`), and the copy did not carry that reason | Fixed: off in `StartNewCampaign`, restored in a new `ClientSubModule.OnGameEnd` that every game end reaches before the save. Repeat of `adapters-taleworlds-api.md` "A mode that copies a sibling's views must copy what they refuse"; new lesson there |
| 3 | MED | Hiding a scene notification in the frame it turns active throws on the null scene (`GauntletSceneNotification` sets `_isActive` a layer tick before `_scene`), ending the run as `driver_error` (lens 2) | Flag leads object | "Active" read as "ready" | Fixed: the driver hides a notice only on its second sighting in a row; two tests, both fail under a mutation of the guard. New lesson in `adapters-taleworlds-api.md` |
| 4 | MED | The judge skipped the load window in the crash scan, never checked that the done file echoes the armed plan, and passed a stale deploy (tooling lens) | Sibling checks dropped | `run_long_defeat.py` has all three; the wrapper copied the launch, not the judge | Fixed with tests. Repeats of `build-tooling-workflow.md` "An unattended wrapper binds its PASS to the build under test" and "Port the sibling tool's refusal tests before writing its code" |
| 5 | MED | Trunk went red after 02bd1e3d2: `TaomClientComposition_RegisterServices_RegistersExactlyTheExpectedServices` lists every client registration (peer session `taom-1b`) | Test outside the filter | The pre-push run used a filter (`Autorun`, `GateTests`, `ServerJit`, `ClientWiring`) and skipped `/verify` because the commit was rushed | Fixed on trunk in 7a0cd9311; full suite 41,035 passed after it. New lesson in `testing-qa.md` |
| 6 | LOW | `TAOM_COMMAND_TRIAL` did not unarm the plan, so the field trial and the scenario could both start a game (lens 5) | Arm exclusion | The list named only the two siblings in `Main` | Fixed with a plan test |
| 7 | LOW | The exit wait (120 s) equalled the launcher's kill grace (lens 5, tooling lens) | Timing | The two were set in different files | Fixed: 60 s and an `exit_stalled` warning |
| 8 | LOW | The done text was built outside its guard (lens 5) | Guard scope | The argument of a guarded call is evaluated before the guard | Fixed with a test |
| 9 | LOW | `ScenarioPlan.ArmedInThisProcess` had no reader (session, lens 5) | Dead member | Copied from the siblings | Deleted |
| 10 | LOW | The host did not set the hero's culture before `SetSelectedCulture`, so the clan name came from another culture (lens 2) | Skipped vanilla step | The culture screen's VM does it, not the content | Fixed |
| 11 | LOW | Doc and comment errors: the start reason, the cheat's effect, the inquiry titles, the wall seconds in the done file (lens 2, lens 5) | Engine claim from intent | Written from the research summary, not the lines | Fixed in the doc and the comments |
| 12 | LOW | Seven tooling items: `--help` printed the Fords help, `ledger.txt` was written on a failed ledger, a pass-through handler, an unused stall override, lossy number text, the child encoding, a test that could not fail (tooling lens) | Tool polish | The wrapper was written fast from `run_long_defeat.py` | Fixed with tests |
| 13 | LOW | Seven driver test gaps (lens 5) | Test gap | The builder saw RED only for the plan and driver tests | Fixed: seven tests |

FOLLOW-UP no issue: the inquiry title can be a queued inquiry's, and multi-selection inquiries log an empty title (lens 2, lens 5).
The titles feed the log only; the doc now says so, and a FIFO would add state for a log line (`simplicity-criterion.md`).

## Root-cause pattern

Findings 2, 4 and 6 share one cause: the slice copied its siblings' mechanics (launch, tick, host) and not the reasons behind
their refusals and checks. Each sibling held the answer in a comment or a judge check. Findings 1, 3 and 11 share a second cause:
an engine or data statement taken from a summary (the plan text, a research report) and not from the line that produces it.

## Why each lens caught or missed these

- Lens 2 alone found 1, 3, 10 and most of 11: each needs the engine source or the data file the engine reads.
- Lens 5 found 2, 6, 7, 8, 9 and 13 by tracing each value to its last writer and each guard to a test.
- The tooling lens found 4 and 12 by comparing the judge with `run_long_defeat.py` and by probing the CLI.
- No lens found 5: it is in a test file outside the diff, and the lenses read the diff's consumers, not the composition tests.

## In-game checks owed

The first unattended run (`python tools/run_scenario.py --days 3`) needs Mike's word. Its log settles the UNVERIFIED points in
`docs/features/scenario-autorun.md`: the clean unload line after the native quit, an `inquiry hidden ... phase=Running` line, and
the days per minute at speed 15.

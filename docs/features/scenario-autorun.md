# Scenario Autorun (unattended campaign runs)

## Overview

The scenario autorun starts a new sandbox campaign and lets the AI play it with nobody at the keyboard. It skips character creation,
keeps the player's party out of the world, holds time on fast forward until the wanted number of days has passed, then writes a done
file, ends the campaign and quits the game. `tools/run_scenario.py` launches the game, waits for the done file and judges the run.

A second kind, `story-mission` (#1368), starts the same campaign, opens one story mission through the dev menu's own click path, waits
for the mission to end and the map to come back, and quits. It runs no campaign days of its own. A first run of it is recorded below.

A third kind, `story-offer` (#1105 slices 3 to 5), drives the real story offer of the War Chronicle instead of the dev menu: a console
command opens the event's window, the driver presses the offer popup's accept button, picks one option of the menu `taom_story_offer`, plays
the mission with the bot (or lets fate decide), clicks Continue and reads the chronicle. Four runs of it passed on 2026-11-21 (the
Breaking as defence, hunt and fate, and the Hornburg by date); they are listed in
[story-missions.md](story-missions.md#how-to-verify-in-game).

## Why This Exists

- **Vanilla behavior:** a campaign needs a person at every step: the menu, character creation, the map, every popup.
- **TAOM requirement:** balance work (#1283) needs an AI campaign run to day 150 and read through `analyze_war_ledger.py`. A person
  playing that run spends hours and keeps the party out of the wars by hand.
- **Without this feature:** every balance check is a manual playthrough, so few are done.

The design is #1075's plan (Mike's decisions of 2026-11-01). It is built clean room from the TaleWorlds decompile and the TAOM repo
only: ArmorySim stays a standalone tool (the 2026-10-18 rule in `.claude/skills/upstream-review/SKILL.md`).

## Architecture

### Design Challenge

The run crosses four game states (main menu, intro video, character creation, map), each with its own screen code, and the engine stops
time for popups, focus loss and some menus. The driver must also never arm in a player's session and never run on a dedicated server.

### Solution Approach

- `ScenarioPlan` reads the arm file named by the environment variable `TAOM_AUTORUN`. No file fallback: a stray file never arms a
  session. A second autorun variable (`TAOM_FORDS_AUTORUN`, `TAOM_LONGDEFEAT_AUTORUN`, `TAOM_COMMAND_TRIAL`) leaves the plan unarmed.
- After the done file, the driver waits up to 60 s for the main menu and then quits; a wait that runs out logs `exit_stalled`. The
  launcher kills the game 120 s after it sees the done file, so the game quits first.
- `ScenarioAutorunDriver` is a pure state machine ticked with wall seconds from the client's application tick. Phases: `WaitMenu`,
  `Creation`, `WaitMap`, `Running`, `Exiting`, `Done`; the story kind adds `MissionMenu`, `MissionOpen`, `InMission` and
  `MissionReturn` and has no `Running`; the offer kind adds `OfferWait`, `OfferMenu` and `OfferReturn` (it shares `InMission`) and has no `Running`. The decisions are in `ScenarioRules`; every wall and day gate fails on NaN.
- `ICampaignAutorunHost` holds every engine call. Core registers `NullCampaignAutorunHost`; TAOM.Client replaces it with
  `CampaignAutorunHost` (ADR-013), so a dedicated server never runs the driver.
- Every exit path writes `autorun_done.txt` once and one `[Check]` line, then ends the campaign. A throw finishes the run as
  `driver_error`.

Two choices differ from the #1075 plan. The Fords pieces are not extracted into a shared base, and there is no `_game_launch.py`:
`run_scenario.py` reuses `run_fords_autorun.py` by swapping names, as `run_long_defeat.py` does. The extraction is a follow-up on #1075.

### Component Diagram

```
tools/run_scenario.py  --(arm file, TAOM_AUTORUN)-->  ScenarioPlan
        ^                                                 |
        | autorun_done.txt, autorun_status.txt    ScenarioAutorunDriver (Main, pure)
        |                                                 |
        +---------------------------------------  ICampaignAutorunHost
                                                         |
                                     CampaignAutorunHost (TAOM.Client, engine calls)
```

## Configuration

### Arm file (written by `run_scenario.py`)

UTF-8 `key=value` lines. An unknown key or a bad value is a warning and keeps the default.

| Key | Type | Description |
|-----|------|-------------|
| `kind` | string | Must be `campaign-new`, `story-mission` or `story-offer`, or the plan is unarmed |
| `out` | path | Absolute folder for the done and status files; required |
| `scenario` | string | A free label, echoed in the log and the done file (default `-`) |
| `mission` | string | `story-mission` only, required: the story mission id. A story kind with no mission, or `-`, is unarmed. Ignored under `campaign-new` |
| `mission_limit_seconds` | number | `story-mission` only: wall seconds from the mission screen until the run stops as `mission_limit` (default 1200) |
| `event` | string | `story-offer` only, required: the War Chronicle event id the run waits to be offered (a missing one, or `-`, is unarmed). Ignored under the other kinds |
| `choice` | string | `story-offer` only, required: `defend`, `attack` or `fate` (any case; another value is unarmed) |
| `open_by_date` | 0 or 1 | `story-offer` only (default 0): the run arms no open command and waits, on fast forward at `speed`, for the event's window to open on its own day; the wait for the popup is then the whole `wall_limit_seconds`, not 120 s |
| `after` | string, repeatable | `story-offer` only: a console command line run once when the offer ended, like `command=` and appended to `commands.txt` |
| `culture` | string | The player's culture id (default `gondor`; a kingdom id such as `empire_w` fails); checked at the culture stage |
| `days` | int | Campaign days to run, 1 to 1000 (default 3) |
| `speed` | float | Fast forward multiplier, above 0, at most 30 (default 15). `story-offer` holds this speed only while it waits for the popup |
| `stall_seconds` | number | Wall seconds with no new day before the run stops as `stalled` (default 600) |
| `wall_limit_seconds` | number | Wall seconds from the campaign start before the run stops as `wall_limit` (default 3600). A by-date `story-offer` run also takes its popup wait from it: this limit less `mission_limit_seconds`, never under 120 s, counted from the start of `OfferWait`, and that wait ends as `offer_not_shown` |
| `quit` | flag | Quit the game after the run |
| `command` | string, repeatable | A console command line run once when the map is ready, in order (#1434). Each output goes to the TAOM log and to `commands.txt` in `out`; a command that throws is reported there and the run goes on. The frame that runs them is not counted against the stall or wall clock. A `taom.*` command needs `cheat_mode = 1` |

### Done reasons

`ok`, `start_refused`, `creation_failed`, `creation_timeout`, `map_timeout`, `stalled`, `wall_limit`, `driver_error`.

The story kind adds `mission_refused`, `mission_not_opened`, `mission_limit`, `mission_undecided`, `mission_no_verdict` and
`return_failed`. `days`, `speed` and `stall_seconds` are not used by it, and its done file has no `days_run` or `days_wanted`.

The offer kind uses `mission_not_opened`, `mission_limit`, `mission_undecided`, `mission_no_verdict` and `return_failed` as the story kind does,
and adds `offer_refused` (the open command did not open the window, the popup's accept button could not be pressed, the menu never settled,
the wanted option is not offered) and `offer_not_shown` (no popup for the armed event in 120 s, or in the by-date wait). Each ends with one warning that names the
reason, its cause and `open_menu=`.

### The story-mission kind

The driver (`ScenarioAutorunDriver`) goes on from `WaitMap` into four phases. Each step waits at most 120 s
(`ScenarioRules.StoryStepWaitSeconds`) and reuses `ScenarioRules.MapSettled` as its settle time.

1. `MissionMenu`: with the free map held, it calls `ICampaignAutorunHost.OpenStoryMenu`, which is `StoryDevMenuBehavior.Open` with the
   armed id, the auto flag and a verdict callback. A refusal finishes as `mission_refused`; the warning carries the dev menu's message
   and the id of the menu that is open (`open_menu=`).
2. `MissionOpen`: with the story menu shown and settled, and no event window open or waiting, it calls `ChooseOpenMission`.
3. `InMission`: hides inquiries and closes event windows every tick, never fast forwards. It reads the verdict when the map is back
   (`StoryVerdict`): `Undecided` is `mission_undecided`, no verdict is `mission_no_verdict`, no mission screen in 120 s is
   `mission_not_opened`, and a mission still running after `mission_limit_seconds` is `mission_limit`.
4. `MissionReturn`: finishes `ok` when the free map has settled, after "Back to the map" was chosen or with no menu open (the log says
   which). A map that does not settle in 120 s is `return_failed`.

The click path: the host calls `MenuContext.InvokeConsequence` after `GetMenuOptionConditionsHold` and `IsEnabled`, from the application
tick, never from the console and never inside the campaign tick. It does not pass the cheat-mode gate of the `taom.story_open` console
command; only the arm file starts it. The host reads the menu of the active `MapState`, because during a mission
`Campaign.CurrentMenuContext` falls back to the map state's menu.

The done file for the kind has `reason`, `kind`, `mission`, `verdict` (`Won`, `Lost`, `Undecided`, or `-`), `mission_seconds`,
`day_start`, `day_end` and the shared keys. The check id is `taom.autorun.story-mission.done`; only `reason=ok` passes.

Not covered: the defence mission only (`helms_deep_defend` is the one the manifest names), no War Chronicle offer path, no save, no
co-op, no speed above 1x, no forced loss. The wall limit of the Running phase does not apply; the step waits, the mission limit and
the launcher's wall limit bound the run.

The judge in `run_scenario.py` and `tools/ingame_checks/1368.toml` both require three log lines: the done line with
`verdict=(Won|Lost)` for the armed mission, the `hosted.boot` pass line and the strict `taom.story-missions.ended` pass line. The ended
pattern ties the verdict to the reason of its side, as `StoryVerdict.Of` maps them, and the judge also requires the ended line to carry
the done line's verdict. The mission's field and `mustLive` traits are read once, at launch, with its side. The
judge searches the whole log and requires exactly one launch line. The manifest scopes the boot and ended rows to the launch of the
armed mission; its done row has no scope, because the driver writes one done line per process and a failure before the launch
(`mission_refused`, a launcher refusal) writes no launching line. Its fail pattern names the mission, and the ended row also fails a
`reason=Abandoned` end. Read the manifest from one session (the default `--sessions 1`, or `--session <the run's log>`), because its
three rows must come from the same run.
The driver writes `reason=ok` with `verdict=Lost` for an abandoned mission (the player's agent left the boundary), so the done line
alone cannot pass it; the strict `ended` pattern names only the three ways a mission ends by itself. The judge also requires exactly one
`[StoryMissions] launching` line, with `autoplay True` and the armed id.

### The story-offer kind

`python tools/run_scenario.py --kind story-offer --event <id> --choice <defend|attack|fate> [--culture <id>]` (#1105 slices 3 to 5). The
runner arms `command=taom.chronicle_open <event> confirm` (first, then any `--command`), `after=taom.war_effects` and `after=taom.chronicle_status`, and `speed=1`.
With `--by-date` it arms `open_by_date=1` in place of the open command, and `speed=15` (or `--speed`). The by-date wait has no stall check:
a locked clock, or a window the map resolves with no offer (an attacker already besieges the target, or its owner changed), costs the whole
wait before `offer_not_shown`.
The phases, from the settled map (`WaitMap` runs the command rows):

1. After the commands: a command output with no `[WarChronicle] Opened:` (a cheat mode off, an event that is not Pending, a window the
   chronicle refuses) finishes `offer_refused` at once and the warning quotes the output. A by-date run (`open_by_date=1`) skips this check.
2. `OfferWait` (120 s, `ScenarioRules.OfferPopupWaitSeconds`; by date, `ScenarioRules.OfferPopupLimit`): keeps fast forward at the armed speed, hides scene notifications, closes event
   windows, and hides inquiries only while `StoryOfferBehavior.Pending` is not the armed event (a hidden popup is a snooze until tomorrow). When
   the behavior holds the armed event and the popup has stayed open 2 s, the host presses its affirmative button; the driver logs
   `accepted the offer popup '<title>'`. No popup in that time is `offer_not_shown`, and the warning quotes the campaign day and the command output.
3. `OfferMenu`: with the menu `taom_story_offer` shown for 5 s and no event window open or waiting, the host picks the first offered option of
   the choice: `defend` is `taom_story_offer_defend` or `taom_story_offer_stand`, `attack` is `taom_story_offer_attack` or
   `taom_story_offer_hunt`, `fate` is `taom_story_offer_decline`. For a side the host first sets `StoryOfferBehavior.BotPlaysNextMission`, so the
   bot plays the lord, and clears it after the click. None offered is `offer_refused` and the warning names `choices=` (what the offer allows).
4. `InMission` (shared with the story kind): the verdict is `StoryOfferBehavior.Verdict` when the map is back.
5. `OfferReturn`: after a mission, the menu's init applies the verdict, and the driver clicks `taom_story_offer_continue` once the menu has
   settled; after `fate` the menu left in the click, so no Continue follows. Then the free map must settle (5 s, no event window open or
   waiting), the `after=` rows run, and the run finishes `ok`. A Continue that is not offered, or a map that does not settle in 120 s, is
   `return_failed`.

The done file has `reason`, `kind`, `event`, `choice`, `verdict` (`Won`, `Lost`, `Undecided`, or `-`), `mission_seconds` and the shared keys; the
check id is `taom.autorun.story-offer.done` (`reason=`, `event=`, `choice=`, `verdict=`, `mission_seconds=`, `culture=`), and only `reason=ok`
passes. The judge in `run_scenario.py` requires, besides the done line and the common log conditions: `taom.story-missions.offer.shown`
with the event's story mission; for `defend` and `attack` the `hosted.boot` line (siege or field by the mission's row), the `ended` line of
the side with the done line's verdict, the `taom.story-missions.offer.result` pass line with that verdict and the result it gives
(`offer_result_pass`: Held or Fell for a fortress, Averted, Occurred or DeathOwed for a hero) and exactly one `[StoryMissions] launching`
line (the chosen mission, autoplay True);
for `fate` the `[WarChronicle]` line of the decline (`'<event>' Occurred on day N (the player let fate decide` for a hero story,
`'<event>': the player let fate decide the story offer.` for a fortress window) and no launching line.

What decides the sides: `--culture` (default `gondor`, a free people), read through `alignment.json` as `StorySideGate.Choose` reads it: a side
shows when its mission ships and the player's side is not opposed to the event's own defender (defend) or first attacker (attack); neutral is
opposed to nobody. A culture the table does not list is refused at parse, and so is a choice the culture is never offered (`fate` needs one
side). `breaking` offers defend to a free or neutral culture and attack to an evil or neutral one (`isengard` is both in `cultures.json` and
`alignment.json`); `hornburg` offers defend to a free or neutral culture and, since slice 5, attack (`helms_deep_attack`) to an evil or
neutral one. The runner cannot know the tie rule (a player already in the defender's or an attacker's kingdom): a new campaign has none. The default of the setting "Offer Story Missions" (`TaomSettings.WarChronicleStoryMissions`) is on; with it off the
event opens as text and the run ends `offer_not_shown`. A `breaking` window opens a hero story only while `lord_1_75` lives, which a new campaign
has at the start.

Not covered: the popup's look, a save between the popup and the result, co-op (the offer is single player only), the text after the Continue
click beyond the closing of its event window, and any event with no story block.

## Engine facts

All from the v1.5.7 decompile (`pwsh tools/taom-src.ps1 path <Type>`).

- **Start:** the menu button opens the start options screen whenever the options list is not empty (`SandBoxViewSubModule.StartGame`,
  lines 278 to 287), and vanilla's provider fills it. The host makes the call the screen's Confirm makes when nothing changed
  (`GauntletCampaignStartingOptionsView.OnConfirm`): `MBGameManager.StartNewGame(new SandBoxGameManager(...))` with
  `AdvancedStartOptionsManager.CreateCampaignStartOptions().GetChangedOptions()`.
- **Intro video:** Patch58 already skips it; `VideoPlaybackState.OnVideoFinished()` is the fallback.
- **Character creation:** `CharacterCreationManager.RemoveStage<T>()` is public. The host sets the culture with `SetSelectedCulture`,
  removes the six later stages and calls `NextStage()`; at the end of the stage list `NextStage` runs `ApplyFinalEffects()` and
  `FinalizeCharacterCreationState()`, so TAOM's finalize handlers still run.
- **Main party:** `IgnoreByOtherPartiesTill(CampaignTime.Never)`, as the `set_main_party_attackable 0` cheat does: an ignored party
  refuses interaction and no encounter forms around it. The host also holds the main party itself (`SetMoveModeHold()`).
- **Culture:** the host sets `Hero.MainHero.Culture` before `SetSelectedCulture`, as the culture screen does, because the clan name
  comes from the hero's culture. The hero's name is never set (the view's `NextStage`, which sets it, is bypassed).
- **Scene notifications:** the engine marks one active a layer tick before its scene exists, and closing it then throws on the null
  scene (`GauntletSceneNotification.CloseNotification`). So the driver hides a notification only on its second sighting in a row.
- **Non-wait game menus:** an open game menu that is not a wait menu stops time (`Campaign` sets `_dt = 0`). The driver closes no
  menu, so one shows as `stalled screen=MapMenu`.
- **Time:** `CampaignTimeControlMode.UnstoppableFastForward` (the stoppable mode stops on Hold). `SpeedUpMultiplier` has no setter cap.
- **What stops time:** `TimeControlModeLock`, and the escape menu that focus loss opens (`MapScreen` asks its map views on focus
  loss). The host sets `BannerlordConfig.StopGameOnFocusLost = false` in memory when it starts the campaign. The engine saves that
  config on shutdown: `Module.FinalizeModule` calls `Game.Current.OnFinalize()` (which ends in `Game.Destroy` and every submodule's
  `OnGameEnd`) and then `BannerlordConfig.Save()` (Module.cs lines 1348 to 1362). So `ClientSubModule.OnGameEnd` puts the player's
  value back at every game end, before that save, whoever ended the game. A crash or a kill does not save. MCM is never written.
- **Autosave:** the host replaces `SandBoxManager.SandBoxSaveManager` with an `ISaveManager` that disables autosave.
- **Popups:** `InformationManager.HideInquiry()` and `MBInformationManager.HideSceneNotification()`. No API gives an open inquiry's title,
  so the host listens to `OnShowInquiry` and `OnShowTextInquiry` and logs, for the first 20 hides, the newest title shown. With two
  inquiries queued, that title can be the queued one's, and a multi-selection inquiry logs an empty or old title. The titles are for
  the log only.
- **Day count:** `CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow`.
- **Exit:** `MBGameManager.EndGame()`, then `TaleWorlds.Engine.Utilities.QuitGame()`.

The story kind (#1368) also relies on these, read in the v1.5.7 source:

- **The click:** `MenuContext.InvokeConsequence(int)` runs only when `Campaign.Current.CurrentMenuContext == this`
  (`MenuContext.cs` lines 154 to 160). With a `MapState` active, `Campaign.CurrentMenuContext` returns that state's `MenuContext`
  (`Campaign.cs` lines 423 to 425); during a mission it falls back to the predecessor `MapState`'s menu (lines 427 to 429). So the
  host reads the menu of the active `MapState` on every call and passes that object.
- **The option index:** `GameMenuManager.RunConsequenceOfVirtualMenuOption` (`GameMenuManager.cs` lines 385 to 407) counts one virtual
  item when the menu has no repeat objects (`MenuRepeatObjects.Count <= 0`), so the index `k` of a menu with no repeat objects reaches
  `RunConsequencesOfMenuOption(menuContext, k)`. The host refuses a menu that has repeat objects.
- **After the mission:** `MapState.OnActivate` calls `MenuContext?.Refresh()` unless a conversation flow is active (`MapState.cs`
  lines 237 to 245), so the menu context is the same object, refreshed, when the map is back.
- **No escape menu on focus loss in a mission:** `MissionGauntletSingleplayerEscapeMenu.OnFocusChangeOnGameWindow` opens the menu only
  when `BannerlordConfig.StopGameOnFocusLost` is true (line 54), and the host sets that to false.
- **The end order:** `Mission.EndMissionInternal` calls every listener's `OnEndMission` (`Mission.cs` line 4645) and every behavior's
  `OnEndMissionInternal` (line 4650, which calls the behavior's `OnEndMission`, `MissionBehavior.cs` lines 125 to 127) before it sets
  `CurrentState = State.Over` (line 4667). `MissionState` pops its state only when the mission is `Over` (`MissionState.cs` lines 120
  to 128), so the story's verdict callback has fired before the map is back.

Read in the v1.5.7 source by the 2026-11-15 review:

- TAOM's career handler runs from `ApplyFinalEffects` and, with no pick, assigns the first eligible career. Patch78 never fires: it
  is a postfix on `CharacterCreationManager.StartNarrativeStage`, whose only caller is the narrative stage view, never built here.
- `NextStage` with the culture view open is safe: `FinalizeCharacterCreationState` pushes `MapState` with `CleanAndPushState`, which
  finalizes every screen at once, as the view's own Next button does.
- In a default run `KingdomJoinOfferService` opens nothing (it needs a takeover), and `CampStartInCamp` is off by default. A run
  inherits the machine's MCM settings.

- **Story event windows:** TAOM's War Chronicle and coronation windows (`EventWindows`, in the Realms popup slot) pause the campaign
  with `GameStateManager.RegisterActiveStateDisableRequest` while they show. Time stops, but the time mode stays unlocked and the
  screen still reads `Map`. The host closes an open one with `RealmsPopups.Close()`, which runs the same `RealmsPopupLayer.Close`
  as the Continue button, and the driver counts it in `event_windows_closed`. A window that opens in Running opens and closes
  in the same application tick, so it never pauses the run. One that opens in WaitMap stays open until the first Running tick.

The story-offer kind (#1105 slices 3 to 5) relies on these, read in the v1.5.7 source:

- **The popup shows only while the campaign ticks with time moving:** `Campaign.Tick` calls `CampaignEventDispatcher.Instance.Tick(_dt)` only
  under `if (_dt > 0f || CurrentTickCount < 3)` (`Campaign.cs` lines 974 to 981), whose receiver runs `Instance._tickEvent.Invoke(dt)` (`CampaignEvents.cs` line 2085), and
  `StoryOfferBehavior.OnTick` is a `TickEvent` listener that waits 30 ticks. So the driver keeps fast forward on in `OfferWait`, unlike the story kind, which never needs a campaign tick.
- **The accept button:** `SingleQueryPopUpVM.ExecuteAffirmativeAction` is `_data.AffirmativeAction?.Invoke(); CloseQuery();` (`SingleQueryPopUpVM.cs`
  lines 105 to 109). The view model is a private field of `GauntletQueryManager`, which subscribes its own `CloseQuery` to
  `InformationManager.OnHideInquiry` (`Initialize`, line 50), so the host calls the captured `InquiryData.AffirmativeAction`, then
  `InformationManager.HideInquiry()`: the same two steps, in the same order. The button is enabled by
  `GetIsAffirmativeOptionEnabled` (`UpdateButtonEnabledStates`), which the host honours. A second `ShowInquiry` in the same campaign tick is
  queued by the engine (`GauntletQueryManager.CreateQuery` enqueues while one is active) but still raises `OnShowInquiry`. So the host keeps the
  newest inquiry only for the log and the hide, and keeps a separate offer inquiry: the one shown while `StoryOfferBehavior.Pending` is set (the
  behavior sets it before `StoryOfferPopup.Show`) and whose title is the popup's own. The accept presses that one only and clears it.
- **The clock stops when the menu opens:** `GameMenu.ActivateGameMenu` begins with `Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;`
  (`GameMenu.cs` line 347), and it then creates the menu context in the same call (`SetNextMenu`, `MapState.EnterMenuMode`, lines 350 to 352), so a
  popup accept that finds the menu not shown ended with a busy map. No offer phase after `OfferWait` calls `KeepFastForward`, so the clock stays
  stopped: the 10 day window of `breaking` cannot close under the driver.
- **A hidden popup is a snooze:** the behavior's next check with the map free and an offer still pending calls `Later()`, which snoozes the event
  until tomorrow (`StoryOfferBehavior.OnTick`), so the driver hides an inquiry only when the pending offer is not the armed event.
- **The open command:** `taom.chronicle_open <id> confirm` goes through `TaomConsole.RunInCampaign`, which runs `DevConsoleGuard.CheckCampaignCheatUsage`
  (`CampaignCheats.CheckCheatUsage`: a campaign and cheat mode). The host's `RunConsoleCommand` calls `CommandLineFunctionality.CallFunction` and
  passes that gate like a typed command, so cheat mode must be on. Its answer starts `[WarChronicle] Opened: ` when it acted
  (`ChronicleCheatReport.FormatOpen`).
- **The hero window:** `ChronicleService.HeroStoryCanOpen` needs the setting `StoryOffersEnabled` (default true: `TaomSettings.WarChronicleStoryMissions`),
  a single-player authority, a hero that is not the player and is alive, and the event's kingdoms.

The first run (`E:\Temp\scenario-runs\20261115-142426`, build cb470ab4e) settled three points. The native quit runs the managed
shutdown (`[Shutdown] clean module unload`). The driver ticks while an inquiry pauses the game (`inquiry hidden n=2 phase=Running`).
At speed 15 a campaign day took about 6 wall seconds, between 5 and 7 (day 1 at 14:26:16, day 2 at 14:26:22; the log has 1-second resolution). The War Chronicle's `breaking`
window then stopped time on day 2 for 9 minutes, until a person closed it.

The second run (`E:\Temp\scenario-runs\20261115-154350`, build a3fc6dc88) proved the fix: `[WarChronicle] 'breaking' Occurred on day
2` and `event window closed n=1 phase=Running` share the second 15:45:18, the done file reads `event_windows_closed=1` and
`notices_hidden=0`, and the 3 days took 37.4 wall seconds, with a clean unload.

UNVERIFIED: that a layer tick runs between two application ticks, which the second-sighting rule for scene notifications needs (a
`notices_hidden` above 0 with no `driver_error` settles it; the first run met no scene notification).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Autorun/AutorunModule.cs` | Feature module: registers the plan, the null host and the driver |
| `Main/Features/Autorun/Scenario/ScenarioPlan.cs`, `ScenarioArmFile.cs` | Arm variable and arm file parse |
| `Main/Features/Autorun/Scenario/ScenarioAutorunDriver.cs`, `ScenarioAutorunDriver.Offer.cs` | The state machine; the partial file holds the story-offer phases |
| `Main/Features/Autorun/Scenario/ScenarioRules.cs` | Pure decisions and limits |
| `Main/Features/Autorun/Scenario/ScenarioAutorunDone.cs`, `ScenarioOutcomes.cs` | Done and status file text, the `[Check]` line |
| `Main/Features/Autorun/Scenario/ICampaignAutorunHost.cs`, `NullCampaignAutorunHost.cs` | Engine seam and its core null |
| `Client/Features/Autorun/CampaignAutorunHost.cs` | The engine calls (TAOM.Client) |
| `Client/ClientSubModule.cs`, `Client/TaomClientComposition.cs` | Tick and registration |
| `tools/run_scenario.py` | Launcher and judge |
| `tools/ingame_checks/1075.toml` | Manifest for `taom.autorun.campaign-new.done` |
| `tools/ingame_checks/1368.toml` | Manifest for the story kind: the done line, `hosted.boot` and the strict `ended` line |
| `Main/Features/StoryMissions/Hooks/StoryDevMenuBehavior.cs` | The dev menu the story kind opens; `Open` has an optional verdict callback (the console path passes none) |
| `Main/Features/StoryMissions/Hooks/StoryOfferBehavior.cs` | The real offer the offer kind drives; the internal `Pending`, `Verdict` and `BotPlaysNextMission` are the driver's seams |

## Tests

- `TAOM.Tests/Features/Autorun/Scenario/`: the plan and arm file, the rules, the driver against `FakeCampaignAutorunHost`, the done
  text and outcomes, and the client wiring (`ScenarioClientWiringTests`, `RequiresGame`).
- `TAOM.Tests/Features/Autorun/Scenario/ScenarioOfferDriverTests.cs`: the story-offer phases against the fake host.
- `tools/tests/test_run_scenario.py`: the launcher's arguments, arm file and judge, the story-offer kind included.
- Co-op: the feature adds no campaign behavior; a dedicated server keeps the null host.

## How to verify in game

**Preconditions:** no game running, the idle gate passes, and this checkout is deployed with `pwsh ./build.ps1` (the launcher does
not build, and it judges the build against the checkout as read at launch: HEAD, the `.dirty` mark against the uncommitted files, a `MISMATCH` stamp and a watched file edited after the build all fail the run, see `tools/autorun_build.py`). A run starts the game unattended,
so it needs Mike's word.

1. Run `python tools/run_scenario.py --days 3`. The TAOM log shows
   `[Check] id=taom.autorun.campaign-new.done result=pass reason=ok`, and the run folder under `E:\Temp\scenario-runs\` holds
   `autorun_done.txt` with `reason=ok`.
2. Measure the days per minute from the log: the `[Autorun][Scenario] phase WaitMap -> Running wall=` line and the `phase Running ->
   Exiting wall=` line give the running seconds, and `days_run` in the done file gives the days. (`wall_seconds` in the done file also
   counts boot, the menu, creation and the map load.) Then set a wall limit for the long run.
3. Run `python tools/run_scenario.py --days 150`. The run folder holds `ledger.txt` from `analyze_war_ledger.py`.
4. Story kind (owed, never run): `python tools/run_scenario.py --kind story-mission --mission helms_deep_defend`. `--days` is refused
   with it, as are `--speed` and `--stall-seconds`. The default wall limit is the campaign-new boot allowance plus the 1200 s mission
   limit (3000 s); the driver reads none of it for this kind, and `--wall-limit` only moves the runner's give-up time. An unarmed
   build (one without this kind) ends as the runner's 10 minute stall, and the printed results then quote its `not armed:` line. Then
   `python tools/ingame_verdict.py --issue 1368` reads the three rows (one session: its default).

5. Story-offer kind (it needs `cheat_mode = 1`, Mike's word and the idle gate; first runs 2026-11-21, run folders `20261121-215614`,
   `-220814`, `-221113` and `-223617`):
   `python tools/run_scenario.py --kind story-offer --event breaking --choice defend`, then `--choice attack --culture isengard`,
   `--choice fate`, and `--event hornburg --choice defend --by-date`. `--days`, `--stall-seconds` and `--mission` are refused with it,
   and `--speed` unless `--by-date` is given. A siege window opens only in the Full War (`taom.chronicle_open` answers "a siege opens only
   in the Full War"), so `hornburg` needs `--by-date`: the run then arms no open command, holds fast forward (speed 15 by default) and
   waits for the popup as long as the wall limit (4200 s by default).

The first story run (`E:\Temp\scenario-runs\20261116-075342`, build 4baa2f53a) settled what only a game can prove. The click opened
the scene: `phase MissionOpen -> InMission` came 32 s after the game started, with `[StoryMissions] launching ... for
'helms_deep_defend' (story1, autoplay True)`. The autoplayed lord and the hosted run ended the mission with no input in a campaign:
`taom.story-missions.ended result=pass verdict=Won side=Defending reason=TargetReached waves=1`, then `mission ended verdict=Won
seconds=390.3`. The map came back: `chose Back to the map`, `back on the free map after Back to the map`, `done reason=ok`. The whole
run took 432 s of game time, hid three inquiries, closed no event window, and unloaded clean. The runner printed PASS, and
`python tools/ingame_verdict.py` read PASS for #1368 and #1366 and for the boot, ended and music rows of #1105.

## Changelog

- 2026-11-15: first slice, `campaign-new` only (#1075, for #1283).
- 2026-11-16: the `story-mission` kind (#1368). Proved by the unattended run 20261116-075342.
- 2026-11-21: the `story-offer` kind (#1105 slices 3 to 5) and its `--by-date` mode. Proved by four unattended runs the same day. The
  first Hornburg run without `--by-date` ended `offer_refused` in 22 s with the chronicle's own reason, as designed (`20261121-221235`).

## GitHub Issue

- **Issue:** #1075: Unattended scenario runs: start a sandbox campaign, run days and save with nobody at the keyboard
- **Status:** Open

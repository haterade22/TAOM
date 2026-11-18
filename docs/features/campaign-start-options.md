# Campaign start options

**Status:** phases 1 and 2 built on branch `feat/campaign-start-options` (2026-11-18, design `output/taom-mod/campaign-start-options/DESIGN.md`
in the owner's workspace). The `SubModule.OnGameStart` call and the `IoC.cs` registration are in place. Not yet played in
game (see "Owed").

## Overview

The Advanced Start Options screen (shown before character creation) now carries choices that belong to one campaign and
are kept in its save: **War of the Ring or Sandbox**, the **War of the Ring start date** (Early day 10 and 20, Standard =
the MCM sliders, Late day 60 and 90, or Custom with two sliders), **Troop Weight**, **War can be won**,
**Eye of Sauron hunters**, **Large AI armies** and an **Alignment rules** preset (Standard, Strict, Relaxed).

**The rule for MCM:** the TAOM mod options (MCM) are the default for NEW campaigns. The start screen shows what MCM says and
the campaign keeps what the player leaves or picks there; a later MCM change does not touch it. Saves from before this
feature, and campaigns started some other way, carry no pinned values and follow MCM exactly as before. After loading a
campaign whose pinned values differ from MCM now, one plain message says so (see "Mismatch message").

## Why This Exists

- **Vanilla behavior:** the options screen has no TAOM rules. TAOM's War of the Ring, Troop Weight and the other
  rules below were global MCM switches, so changing MCM changed every save at once, and a co-op client could differ from the host.
- **TAOM requirement:** the player picks these once, at the start, and the campaign keeps them.
- **Without this feature:** a campaign cannot be pinned to a start date, and a mid-campaign MCM edit rewrites the rules of
  a game already underway.

## Architecture

### Design Challenge

`AdvancedStartOptions.GetChangedOptions` keeps only options whose value differs from their default and that are not
hidden, so an option the player leaves alone is **absent** from the campaign. The campaign's option data is saved by
the engine (`CampaignOptions._advancedStartOptionsData`, `[SaveableField(26)]`) with the generic
`AdvancedStartData<bool|int|string>` types, so TAOM needs no save definer and a save still loads with TAOM removed.

### Solution Approach

1. **The screen** (`Main/Features/AdvancedStartOptions/CampaignStartOptionsScreen.cs`, called from
   `TaomStartOptionsProvider`) replaces vanilla's `Scenario` list with `taom_war_of_the_ring` and `none` (relabelled
   "Sandbox (no scripted war)"), adds the start-date list and two Int sliders in category `worldscenarios`, and a
   `taom_rules` category with four Boolean toggles (Troop Weight, War can be won, Eye of Sauron hunters, Large AI armies) and the Alignment rules list. The vanilla war scenarios and United Empire are no longer offered
   (owner decision 2026-11-18); another mod's scenario item is kept. United Empire merges the factions that hold the
   three imperial StringIds (Dunland, Gondor and Mordor here) into a kingdom named "Calradian Empire", and its code branches
   on `Culture.StringId == "empire"`, which is true only for Dunland in TAOM, so a King, Vassal or Mercenary start in Gondor or
   Mordor leaves the player in an empty, fief-less kingdom beside the merged one; the unifier pick does not change this. Each default is what MCM, else the JSON, says.
2. **The pin** (`CampaignStartSession.Begin`, called from `SubModule.OnGameStart`): on a **new** campaign it resolves the
   screen's keys plus the defaults (`CampaignStartOptionsResolver`, pure) and adds the resolved keys to the campaign's own
   option data: `taom_cso_version` (1), `taom_cso_wotr`, `taom_cso_wotr_day1`, `taom_cso_wotr_day2`, `taom_cso_troop_weight`,
   `taom_cso_victory`, `taom_cso_eye`, `taom_cso_ai_scaling` and `taom_cso_alignment` (a preset id string). A **loaded** save is never pinned. Screen keys and resolved keys are separate, so the screen
   can change later without migrating saves.
3. **The readers** take `ICampaignStartOptions` (`CampaignStartOptionsStore`: a values-only snapshot, validated, null =
   nothing pinned): `WarOfTheRingService` (switch and days), `TroopWeightSettingsProvider`
   and the phase 2 providers below. Precedence: campaign, then MCM, then JSON. Trigger days: MCM TestMode, JSON `testMode`,
   campaign days, MCM sliders, JSON days; the existing clamp (phase 2 strictly after phase 1) still applies.

### Phase 2 rules

Each is read only through its provider, which asks the campaign first and keeps a `...NewCampaignDefault` member (MCM, else
JSON, never the campaign) for the screen, the pin and the mismatch check. `CampaignOptionReadersGateTests` fails a direct MCM
read of any of these properties elsewhere.

| Start option (screen key, pinned key) | MCM property | Provider |
|---|---|---|
| War can be won (`taom_war_can_be_won`, `taom_cso_victory`) | `MomentumVictoryEnabled` | `MomentumSettingsProvider.VictoryEnabled` |
| Eye of Sauron hunters (`taom_eye_hunters`, `taom_cso_eye`) | `EyeOfSauronEnabled` | `EyeOfSauronSettingsProvider.Enabled` |
| Large AI armies (`taom_large_ai_armies`, `taom_cso_ai_scaling`) | `EnableAiPartyScaling` | `AiPartyScalingSettingsProvider.Enabled`, used by `AiPartySizeService` and `taom.print_ai_food_relief` |
| Alignment rules (`taom_alignment_preset`, `taom_cso_alignment`) | `EnableAlignmentRecruitment`, `EnableMarriageAlignment`, `EnableAlignmentDesertion`, `AllowPlayerAlliancesWithAnyone` | `RecruitmentAlignmentSettingsProvider`, `MarriageAlignmentSettingsProvider`, `AlignmentDesertionSettingsProvider` (`IsEnabled`), `HostilePeaceSettingsProvider.AllowPlayerAlliancesWithAnyone` |

**Alignment rules preset.** The preset id is pinned as a string. **Standard** (the default) pins nothing: the four switches
follow MCM. The two presets are defined once, in `AlignmentRules` (`Strict`, `Relaxed`):

| Preset | Recruitment block | Marriage block | Alignment desertion | Player alliances with anyone |
|---|---|---|---|---|
| Strict | on | on | on | off |
| Relaxed | off | off | off | on |

A preset fixes only these four master switches; the "Apply to player / AI / parties / garrisons" sub-switches and
"Only Good Rejects Evil" keep following MCM. Strict equals the shipped MCM defaults, so it only matters when MCM was changed.

### Mismatch message

`CampaignStartOptionsBehavior` (declared by `CampaignStartOptionsModule`, one instance per campaign start) listens to
`OnSessionLaunchedEvent` and, once per game session, asks `CampaignStartMismatchNotice`. `Begin()` also logs one
`CampaignStartOptions.Loaded:` line with the pinned values of a loaded campaign, so a bug report shows the rules it runs. On a **loaded** campaign (never a
new one) where any pinned value differs from what MCM, else JSON, gives a new campaign now (`CampaignStartMismatch`, pure),
it shows one information message through `ICampaignStartNoticeAdapter` (text key `taom_cso_keeps_choices`) and logs
`CampaignStartOptions.Mismatch`. Only pinned values count. The check is inside a try; a fault logs a warning and nothing else.

### Console command

`taom.campaign_option list` prints each start choice this campaign pinned next to the MCM value now.
`taom.campaign_option set <name> <value>` changes one, for this campaign only. Names: `war_of_the_ring`, `war_day1`,
`war_day2`, `troop_weight`, `war_can_be_won`, `eye_hunters`, `large_ai_armies`, `alignment`. Flags take on,
off, true, false, 1, 0, yes or no; days take 1 to 365 and must keep day 2 after day 1 (change the other day first when
needed); alignment takes standard, strict or relaxed. A bad name or value changes nothing. A campaign that never pinned
(an older save) answers that it follows MCM and writes nothing. The command is a dev-console command (cheat mode on, a
campaign loaded); the work is in `CampaignOptionEditor`, which writes through `CampaignStartOptionsStore.TrySet` into the
campaign's own option data, so a save keeps the change. It logs `CampaignStartOptions.Changed`. It writes only values;
what a changed value does mid-campaign is up to its feature (a phase that already fired stays fired). A write that
sticks also drops the cached party size limits (`IPartySizeCacheInvalidator.InvalidateAll`, as the MCM path does), so
`troop_weight` and `large_ai_armies` apply at once. A co-op client is refused, like the other save-backed cheats
(`CoopSessionPolicy.MayWriteSaveBackedState`). The host and a server console are refused too while a session is
live: Coop does not send `AdvancedStartData` to the clients, so they would keep the old values until they rejoin. Setting `war_day1` or `war_day2` while the stored pair is missing or
invalid writes both days, so the pair becomes valid. The MCM days are clamped once, in
`CampaignStartDefaultsProvider.Current()`, so `list` and the mismatch check compare the same pair the pin writes.

Side effects on other tools: the co-op settings fingerprint still hashes the MCM values of these rules, so in a pinned
campaign a difference there no longer means the two peers simulate differently. On a dedicated server,
`taom-server-settings.json` sets only the defaults for a new campaign for these keys; a pinned save keeps its own. The
Escape menu names the scenario only when the player picked one that differs from the screen default, and a console
`set war_of_the_ring` does not change that label.

### Component Diagram

```
Advanced Start Options screen --(GetChangedOptions)--> Campaign.Options (saved by the engine)
                                                           |
SubModule.OnGameStart -> CampaignStartSession.Begin -------+ pin all resolved taom_cso_* keys (new campaign only)
        |                    |
        |              CampaignStartOptionsResolver (pure) + ICampaignStartDefaults (MCM, else JSON)
        v
CampaignStartOptionsStore (snapshot) --> ICampaignStartOptions --> WarOfTheRingService
                                                              +--> TroopWeightSettingsProvider --> ITroopWeightService.IsEnabled
                                                              +--> Momentum, EyeOfSauron, AiPartyScaling providers
                                                              +--> Recruitment, Marriage, AlignmentDesertion providers
                                                              +--> HostilePeaceSettingsProvider (player alliances)
CampaignOptionEditor (taom.campaign_option) --> store.TrySet --> IPartySizeCacheInvalidator.InvalidateAll
```

The session and the store are separate classes on purpose: the defaults read the Troop Weight and the other providers,
which read the store, so a store that took the defaults would be a dependency cycle (DryIoc reports it, even through a
`Func`).

## Configuration

No new file. The defaults come from MCM (`WarOfTheRingEnabled`, `WarPhase1TriggerDay`, `WarPhase2TriggerDay`,
`EnableTroopWeight`, plus the phase 2 properties in the table above) and, where MCM is missing (a dedicated server), the
existing JSON (`diplomacy/war_of_the_ring.json` and each feature's own config; Troop Weight defaults on). The War of the Ring
defaults come from `IWarOfTheRingService.NewCampaignEnabled` and `NewCampaignPhaseDays` (MCM, else JSON, no Test Mode), the
same members the service falls back to, so there is one source.

| Preset | Phase 1 day | Phase 2 day |
|---|---|---|
| Early | 10 | 20 |
| Standard | MCM slider (30) | MCM slider (44) |
| Late | 60 | 90 |
| Custom | slider 1 to 364 | slider 2 to 365, clamped after day 1 |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CampaignStartOptions/CampaignStartOptionKeys.cs` | Every screen key, item id and pinned key |
| `Main/Features/CampaignStartOptions/CampaignStartOptionsResolver.cs` | Pure: screen values plus defaults to resolved values |
| `Main/Features/CampaignStartOptions/CampaignStartOptionsStore.cs` | Validated, values-only snapshot of what the campaign pinned |
| `Main/Features/CampaignStartOptions/CampaignStartSession.cs` | The pin, at the start of a new campaign |
| `Main/Features/CampaignStartOptions/CampaignStartDefaultsProvider.cs` | MCM-else-JSON defaults, never the campaign |
| `Main/Features/CampaignStartOptions/AlignmentRules.cs` | The Strict and Relaxed presets, defined once |
| `Main/Features/CampaignStartOptions/CampaignStartMismatch.cs`, `CampaignStartMismatchNotice.cs`, `Hooks/CampaignStartOptionsBehavior.cs`, `CampaignStartOptionsModule.cs` | The once-per-session "campaign keeps its own choices" message |
| `Main/Features/CampaignStartOptions/CampaignOptionEditor.cs`, `Cheats/CampaignOptionCheats.cs` | `taom.campaign_option list` and `set` |
| `Main/Features/AiPartySize/AiPartyScalingSettingsProvider.cs` | Large AI armies: campaign, then MCM, then on |
| `Main/Features/CampaignStartOptions/CampaignStartOptionsIoC.cs` | Registration |
| `Main/Adapters/AdvancedStartDataAdapter.cs` | `Campaign.Current.AdvancedStartData`, typed `TryGet`, `Add` and `Set` |
| `Main/Features/AdvancedStartOptions/CampaignStartOptionsScreen.cs` | The screen's options and conditions |
| `Main/Features/TroopWeight/TroopWeightSettingsProvider.cs` | Troop Weight: campaign, then MCM, then on |
| `Main/Adapters/CampaignStartNoticeAdapter.cs` | Shows the message (`InformationManager.DisplayMessage`) |
| `Main/_Module/ModuleData/global_strings.xml` | Menu rows (`taom_aso_*` keys), with the #604 placement rule |

## Dependencies

- `IAdvancedStartDataAdapter` (Adapters): the engine's option data, typed.
- `IWarOfTheRingService` (Diplomacy), `ITroopWeightSettingsProvider` and the phase 2 providers: the defaults.
- `ICoopSessionProvider`, `IPartySizeCacheInvalidator`: the console editor.

## Tests

- `CampaignStartOptionsResolverTests`: scenario to War of the Ring, each preset, custom clamp, defaults when keys absent,
  unknown preset.
- `CampaignStartOptionsStoreTests`: no marker gives nulls, marker gives values, bad days and a newer version warn and fall
  back, the session pins only on a new campaign, only missing keys, idempotently, and forgets the last session.
- `CampaignStartOptionsWiringTests`: the real container resolves with no dependency cycle.
- `CampaignOptionReadersGateTests`: no direct MCM read of the overridable properties (the Readers table in the test) outside their providers, in `Main` and `Client`, through the usual
  receivers or a property pattern. `CampaignStartOptionsWiringTests` also checks that `SubModule.OnGameStart` calls
  `ICampaignStartSession.Begin()` as the first statement of the CampaignGameStarter block (source order).
- `ExtraRulesProvidersPrecedenceTests`: each phase 2 provider, pinned wins, nothing pinned follows MCM or JSON, Strict and
  Relaxed beat a contrary JSON, Standard pins nothing, the preset never changes a new-campaign default.
- `CampaignStartPhase2Tests`: resolver and pin of the four rules, an unknown preset, the mismatch check per value, the
  notice (loaded only, once, a fault swallowed), and the console editor (list, set, bad input changes nothing, days checked
  against each other, a key missing from an older pin is added).
- `WarOfTheRingCampaignOptionsTests`, `SettingsProvidersCampaignPrecedenceTests`, `CampaignStartDefaultsProviderTests`.
- `TaomStartOptionsProviderTests`: on the installed engine, the scenario list, both polarities of the hidden start-date
  and slider conditions, hidden options absent from `GetChangedOptions`, and the string rows in the right files.

## How to verify in game

**Preconditions:** a new campaign, TAOM module set; the `SubModule` and `IoC.cs` lines in place.

1. Open the options screen from the main menu: War of the Ring is first and the default; the War of the Ring start list shows
   only with it; Custom shows two sliders; a "Middle-earth rules" tab has Troop Weight, War can be won, Eye of Sauron hunters, Large AI armies and Alignment rules; no ERROR text.
2. Start with Late and Troop Weight off. The log carries `CampaignStartOptions.Pinned: ... days=60/90 troopWeight=False`;
   the war starts on day 60; party size ignores weight.
3. Change MCM, save, reload: the campaign keeps its values and one message says so (once per session, not on a new
   campaign). Load an old save: it follows MCM as before and shows no message.
4. With cheat mode on, `taom.campaign_option list` shows the values next to MCM; `taom.campaign_option set war_can_be_won on`
   changes it and `list` shows it; a bad value prints a reason and changes nothing.
5. Alignment: start with Relaxed and marry or recruit across the Free and Evil line (allowed); start with Strict and MCM
   changed to off (still blocked).

## Owed

- `IOutcomeLog` lines (`CampaignStartOptions.Pinned`, `.Resolved`) and an `ingame_checks` manifest: the pin, the mismatch and
  the console change log through `IModLogger` only for now.
- The English of the MCM hints for the overridable properties grew by one sentence (the four alignment hints were reworded 2026-11-18), so their
  translations are stale until re-run (`Languages/<LANG>/` files untouched).
- Naval Travel was dropped from the start options (owner decision 2026-11-18): the feature is parked in code, so the toggle did
  nothing. Add it back when the feature is lifted.
- In-game checks above; the Gauntlet look of a new category and Int sliders outside "general" is unverified.

## Changelog

- 2026-11-18: review fixes: Sandbox name in the Escape menu; console set refreshes party size caches, refuses on a co-op client and repairs an invalid day pair; MCM days clamped once.
- 2026-11-18: phase 2 built (War can be won, Eye of Sauron hunters, Large AI armies, Alignment rules preset; mismatch
  message; `taom.campaign_option`; MCM hints say MCM is the default for new campaigns).
- 2026-11-18: phase 1 built (store, pin, War of the Ring, start date, Troop Weight; vanilla war scenarios hidden).

## GitHub Issue

- **Issue:** waived by the owner (2026-11-18).

# War Chronicle

## Overview

The War Chronicle is a feature module (`Main/Features/WarChronicle/`) that tells the War of the Ring on the campaign map and keeps the AI-versus-AI war from snowballing, and measures whether it does. It ships six working parts: a registry of timed, kingdom-keyed effects (volunteer rate and prisoner escape); an event chain of 13 lore events, 7 of them siege windows that resolve Held, Fell or Lapsed from the map and pay both sides small effects; an army focus that leans the attackers' and the defender's armies toward an open siege's target; a rally layer that grants small effects to AI kingdoms that have lost a large share of their towns and castles; a daily war ledger written to the TAOM debug log; and `tools/analyze_war_ledger.py`, which reads that ledger. A resolved event can also award War of the Ring momentum.

The Eye of Sauron hunt (#765 M8) is built as its own module, see [eye-of-sauron.md](eye-of-sauron.md).

## Why This Exists

- **Vanilla behavior:** Bannerlord has one built-in catch-up, small and permanent. `DefaultVolunteerModel.GetDailyVolunteerProductionProbability` scores the fiefs of the notable's settlement's faction: a town counts 1 to 3 by prosperity plus its villages, and a castle counts its villages. Below a score of 46 it raises the per-slot base from 0.7 toward 0.9, so a faction with few fiefs refills volunteer slots faster. At the extreme the first slot goes from 0.525 to 0.675 a day and the last from 0.088 to about 0.40. The term keys on absolute size, not on losses, has no time limit and also applies to player-ruled kingdoms. TAOM's phase machine ([war-of-the-ring.md](war-of-the-ring.md)) adds nothing of its own. There is no timed, kingdom-keyed modifier and no way to see how lopsided the AI war has become.
- **TAOM requirement:** Mike's decisions for #765 (2026-10-08):
  1. **No player gate.** Nothing waits on the player. The player matters only through what happens on the map.
  2. **Small effects only.** Increased recruitment and faster lord prisoner escapes, not combat power.
  3. **Catch-up for losing AI kingdoms.** A kingdom that has lost significantly gets a small, temporary lift.
  4. **No bonus for the player.** No effect is keyed to the player's own clan: its settlements get no volunteer change and its lords no extra escape roll. The rally skips a kingdom the player rules; a kingdom the player serves as vassal or mercenary can still rally (the indirect paths are under Known limitations).
- **Without this feature:** no way to tell a stalemate from a runaway except by watching the map, and no shared place for War of the Ring mechanics (the chronicle's events, and later the Ring) to hang timed effects.

## Architecture

### Module and per-campaign behavior

`WarChronicleModule` (`Main/Features/WarChronicle/WarChronicleModule.cs`) is a `TaomFeatureModule`, appended in `Main/Composition/FeatureModules.cs`. It declares two campaign behaviors: `WarChronicleBehavior`, the one `SyncData` owner, and `WarChronicleEventsBehavior`, the event chain's router, which saves nothing ("The event chain" below). `OwnsSaveData` is true, and both are always registered, so the save payload survives a toggle.

The module builds the behavior with `CampaignBehaviorDecl.Of(r => new WarChronicleBehavior(...))`, a lambda body rather than a container singleton, so a new instance is made in every campaign's `OnGameStart`. The behavior's constructor calls `WarChronicleStateService.ResetForNewSession()`, which empties the effect registry, the baselines, the rally tiers, the chronicle's event rows and first-seen phase days (`ChronicleStateStore`) and the once-per-day gate (`WarChronicleDailyGate`). The router is built the same way, and its constructor clears the announcement queue and the army focus. `OnGameStart` runs before any campaign event and before `SyncData` on a load, so a load restores its saved record over the reset and a save with no record stays reset. There is no latch and no reset from `OnSessionLaunched` (the rule and its history: `.claude/rules/csharp-architecture.md`, "Singleton Services Holding Per-Campaign State"). `WarChronicleWiringTests` pins the `new` shape.

`WarChronicleBehavior` listens to `DailyTickEvent`, `KingdomDestroyedEvent` and `OnGameLoadedEvent`. The daily handler returns unless `ICoopSessionProvider.IsAuthority` and `CoopSessionPolicy.MayWriteSaveBackedState` both hold, then calls `WarChronicleTickService.RunDaily()`. The kingdom-destroyed and game-loaded handlers need only `IsAuthority`; they write the `ev=destroyed` line and the `t=load` marker (`WarLedgerService.LogLoad`).

### Tick order

`WarChronicleTickService.RunDaily()` (`WarChronicleTickService.cs`) first claims the day from `WarChronicleDailyGate` (`WarChronicleDailyGate.cs`) and returns when the day already ran (see "Once per day" below), then runs, in this order:

1. `IWarEffectService.Expire(nowHours)`: drop effects whose end time has passed and re-bake the multipliers, so a changed MCM strength applies from this tick.
2. `WarBaselineService.EnsureBaselines(kingdoms)`: baseline for any kingdom not seen before. When none were held and the day is past 1, the take is late and logs one warning (see the rally's **Baseline**).
3. `RallyService.RunDaily(kingdoms, nowHours)`: tiers and rally effects.
4. `WarEscapeDailyPass.Run()`: the extra prisoner escape rolls.
5. `WarLedgerService.WriteDaily(kingdoms)`: the day's `[WarLedger]` lines.

The whole body is wrapped in one try/catch that logs a warning and skips the rest of the day, because a throwing engine getter must never fail the campaign's daily tick: a throwing snapshot read skips the baselines, the rally, the escape pass and the ledger together. The escape pass and the rally also catch their own failures. The escape pass runs after the rally, so a boost the rally grants, refreshes or removes reaches that same day's roll, with no lag in either direction; turning the rally off ends its escape boost before the next roll.

The event chain does not run inside this tick. It has its own router behavior, `WarChronicleEventsBehavior` (`Events/WarChronicleEventsBehavior.cs`), whose daily listener calls `ChronicleService.RunChronicleDay()`; see "The event chain" below. The engine calls daily listeners newest first (`MbEvent.AddNonSerializedListener` prepends), and feature-module behaviors are added after every hand-wired one, `WarOfTheRingBehavior` included, so the chronicle steps first: an effect it writes is baked at once and already counts in that day's escape roll and ledger line.

**Once per day.** Both daily listeners run at most once per campaign day (whole days, floored, like the ledger's `day=`). On a load the engine waits only the elapsed part of the day before the next daily tick (`Campaign.CreateCampaignEvents`, v1.5.5 `Campaign.cs:1024-1030`; `MBCampaignEvent` sets `NextTriggerTime` from that wait), so a save made at fraction f of day S next ticks at S + 2f: day S a second time when f is under a half (#793, open: read from the managed engine source, not yet seen in game). These are elapsed campaign days, counted from `CampaignStartTime` (09:00 on vanilla's calendar clock, `DefaultCampaignTimeModel`), so the repeat follows a save in the first half of the elapsed day (about 09:00 to 21:00 on the calendar clock), not one made before midday. `WarChronicleDailyGate` keeps the last day each listener ran in its own lane (`tick` for `RunDaily`, `chronicle` for `ChronicleService.RunChronicleDay`), claimed before any work; the days are saved in the payload's `daily` section, so the reloaded run skips the repeat. A save without the section (an older build) holds no day, so its first tick runs, and a new campaign's constructor reset clears it. A day that is not a finite number in range claims nothing, and an unreadable engine clock (no campaign, NaN, infinite or negative) never becomes day 0: `IKingdomWarSnapshotAdapter.TryGetElapsedDay` reports it, so `RunDaily` skips the day with one warning and `LogLoad` writes no load marker.

**Line order.** One daily tick writes the chronicle's `ev=chronicle` lines first (its listener runs first), then the rally's `ev=tier` lines (`RunDaily` runs the rally before `WriteDaily`), then the kingdom, side and share lines. `ev=destroyed` lines and the resolutions of an owner change or a siege land mid-day, between two ticks. A load writes `t=load` before the reloaded run's first tick.

### The timed-effect registry

`WarEffectService` (`Effects/WarEffectService.cs`, `IWarEffectService`) holds `WarEffect` rows: source id, kingdom id, `WarEffectKind`, magnitude, end time in campaign hours. `WarEffectKind` has two values, `VolunteerRate` (0) and `PrisonerEscape` (1). Kinds are saved by name and the values are dense from zero, because the service bakes one array slot per kind.

- **Keying:** `Apply` upserts by (kind, source id, kingdom id). `RemoveSource` removes every row of a source.
- **Validation:** `Apply` rejects, with a warning, a null effect, an empty source or kingdom id, an undefined kind, and a non-finite magnitude or end time.
- **Math:** the multiplier for a kingdom and kind is `clamp(1 + strength x sum of magnitudes)` (`Effects/WarEffectMath.cs`). Clamps are code constants: `VolunteerRate` 0.5 to 1.5, `PrisonerEscape` 1.0 to 3.0. The strength is the MCM `War Effect Strength`, sanitized to 0 to 2 (a non-finite value becomes 1).
- **Hot path:** every multiplier is baked on `Apply`, `RemoveSource`, `Expire`, `RestoreFromSave` and `ResetForNewSession`, and swapped in as one new dictionary. `GetMultiplier` is a lock-free dictionary lookup plus an array read, and returns 1 for a null or unknown kingdom.

### The player rule

Each consumer's engine boundary passes facts, and a service decides (decision D4). The volunteer boundaries (`TaomVolunteerModel` for towns and villages, `CastleNotableMaintainer` for castles) pass the owner clan's kingdom `StringId` and whether the owner is the player's own clan; `VolunteerProductionService.Compute` then skips the war step for the player's clan. The escape adapter flags a lord of the player's clan, and `WarEscapeService` skips him. A player-ruled kingdom's AI clans still get their kingdom's multiplier, so only the player's own clan is excluded. The rally adds its own rule: it never writes effects for a kingdom the player rules (`KingdomWarSnapshot.IsPlayerRuled`).

In a co-op session "the player's clan" means every human player's clan (#1141). The escape adapter, the rally and the two volunteer boundaries ask `PlayerClanCheck`, which reads Coop's player registry; on the co-op server `Clan.PlayerClan` is the server's own clan. So a guest's towns, villages and castles get no `VolunteerRate` multiplier, as the host's do not. The server's own clan, which the registry never lists, counts as AI. If the registry cannot be read, no fief gets the multiplier.

### Consumer 1: volunteers

`TaomVolunteerModel.GetDailyVolunteerProductionProbability` (`Main/Features/TroopProgression/Models/TaomVolunteerModel.cs`) passes vanilla's value, the settlement owner clan's culture adapter, the owner clan's kingdom id and whether the owner is the player's clan to `VolunteerProductionService.Compute` (`Main/Features/TroopProgression/VolunteerProductionService.cs`). The service applies the culture respawn feats as the model did before (clamped to 0 to 1), then, unless the owner is the player's clan, multiplies by the `VolunteerRate` multiplier and clamps to 0 to 1. A multiplier of exactly 1, or a non-finite value, returns the value before the war step, so with no active effect the result is the pre-existing result. A non-finite base or feats result deliberately returns vanilla's value and skips the war step; the old model body returned NaN there (or 1 for an infinite base under a positive feat), which `RecruitmentCampaignBehavior` read as "never produce a volunteer". The model body holds no branch (`gamemodels.md` rule 4).

Vanilla's value already includes its low-fief boost (see **Why This Exists**), so the rally's multiplier stacks on it multiplicatively, before the 0 to 1 clamp. The same daily roll gates both outcomes in `RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement`: it fills an empty slot, and for an occupied slot it opens the chance that the waiting recruit is promoted. So the multiplier fills slots faster and also lets waiting recruits reach higher tiers sooner, up to the volunteer tier cap.

**Castles.** Vanilla's volunteer update returns early for castles, and TAOM fills castle notables through `CastleNotableMaintainer.FillCastleVolunteers` with the fixed curve of `CastleRecruitmentService.GetSlotProductionProbability`. That curve now goes through the same `Compute`, with the castle owner's kingdom and the player-clan fact read once per castle, and no culture: castles never had the respawn feats, so they get the war multiplier only (the war volunteer multiplier but not the culture feats: decided by the maintainer 2026-10-14, keep). Villages bound to a castle were always covered through the model.

### Consumer 2: prisoner escape

`WarEscapeDailyPass` (`Effects/WarEscapeDailyPass.cs`) finds every kingdom whose `PrisonerEscape` multiplier is above 1, asks `IPrisonerEscapeAdapter` (`Main/Adapters/PrisonerEscapeAdapter.cs`) for the captured lords of those kingdoms' clans, and gives each eligible lord one extra roll. `WarEscapeService` (`Effects/WarEscapeService.cs`) decides:

- **Chance:** the extra chance is `p x (multiplier - 1)`. `p` starts at 0.04, is multiplied by `5 - min(81, healthy members)^0.25` when the captor is a mobile party that is not in a settlement, by 0.5 when the player holds the prisoner (the captor is the main party, or a settlement or the current settlement owned by the player clan), and by the perk factor. This mirrors vanilla's private daily roll in `PrisonerReleaseCampaignBehavior` (v1.5.6 :201-250, unchanged from v1.5.4). The perk factor is vanilla's relative terms, which the adapter reads with vanilla's own calls under vanilla's conditions: the governor's Sweet Talker, Dungeon Architect and Mounted Patrols for a town or castle captor; for a mobile captor the captive's Fleet Footed, the party's Mounted Patrols, Ransom Broker and Keen Sight, and the Valor of the party's or its army's leader. It is `1 + the sum of those factors`; at or below 0 vanilla's chance is 0, and so is the extra one, so a captor's anti-escape perks hold for the boost too. Using vanilla's perk and Valor factor for the extra roll was decided by the maintainer 2026-10-14: keep. These constants live once, in `WarEscapeService.VanillaChance` (vanilla's gate, then the base, mobile and player-held terms times the perk factor); `ExtraChance` is that number times `multiplier - 1`, so the dungeon menu's Warden's Tally ([wardens-tally.md](wardens-tally.md)) can show the same chance without a second copy.
- **Exclusions:** a lord is skipped when not alive, not a prisoner, the main hero, or in the player clan; when the captor is in a map event or a siege (these two are TAOM's own exclusions, stricter than vanilla); when vanilla's `CanHeroBeReleased` veto says no (the adapter fails closed if there is no event dispatcher); when the perk factor is not above 0; and when the multiplier is not finite or not above 1. Eliminated kingdoms are not scanned.
- **Effect:** `IPrisonerEscapeAdapter.Escape` re-finds the hero and calls `EndCaptivityAction.ApplyByEscape`. A throwing read or escape is logged and skipped.
- **Shown to the player:** the town and castle dungeon menus add each prisoner lord's daily chance, the terms behind it and, for a lord of a boosted kingdom, his combined chance `1 - (1 - p) x (1 - extra)` ([wardens-tally.md](wardens-tally.md), #903). The boost line follows the pass: it appears only on a peer that runs `WarEscapeDailyPass`, and a co-op client shows none. The perk terms come from the same `ExplainedNumber` that gives `EscapeFactor` (`PrisonerEscapeAdapter.EscapeTerms`), so the lines and the factor cannot differ.

### The rally

`RallyService` (`Rally/RallyService.cs`) runs once per tick over the kingdom snapshots.

- **Measure:** `KingdomWarSnapshotAdapter` (`Main/Adapters/KingdomWarSnapshotAdapter.cs`) reads each non-eliminated kingdom's `Fiefs`. Fortification points are 2 per town and 1 per castle (`KingdomWarSnapshot.FortificationPoints`). Loss is `max(0, (baseline - points) / baseline)`, and is null without a baseline above zero. The rally reads live state each day, so it subscribes to no ownership event.
- **Baseline:** `WarBaselineService` (`Rally/WarBaselineService.cs`) takes a kingdom's points the first time a daily tick sees it: every kingdom on the first tick of a new campaign (day 1, after the campaign clock's one-day initial wait), a later rebel or player-founded kingdom when it first appears. Baselines are saved. Lateness is derived from the data, not saved: when no baseline is held and the day is past 1, the take is late, and `WarChronicleTickService` logs one warning ("loss is measured from today's map"). That covers a save from before this feature (the engine never calls `SyncData` for a behavior the save has no record of, `CampaignBehaviorDataStore.LoadBehaviorData`), a corrupt baselines section and a skipped first tick. `WarBaselineService.LossOf` is the one loss derivation; the rally and the ledger's `loss` key both use it.
- **Tiers and hysteresis:** `RallyTierMachine.Next` (`Rally/RallyTierMachine.cs`). With defaults: tier 2 at 50% lost (a jump from tier 0 is allowed), tier 1 at 25% lost; tier 2 falls to 1 below 40%; any tier falls to 0 below 15%. Between an exit and its enter the tier holds. A non-finite loss gives tier 0. A kingdom with no loss to measure has tier 0.
- **Tracked always, applied conditionally:** the tier is stored (`RallyTierStore`) for every kingdom with a baseline, whether the rally is on or the kingdom is eligible, so a control run's ledger shows what would have fired. A tier change writes an `ev=tier` ledger line.
- **Eligibility:** AI-ruled (not `IsPlayerRuled`), at war with at least one other non-eliminated kingdom, and either `includeNeutral` or an alignment side other than Neutral. The rally is active only when both the `enabled` field in `rally.json` and the MCM `Enable Rally` are true.
- **Writing:** for an eligible kingdom at tier 1 or 2, two effects with source `rally:<kingdomId>`: `VolunteerRate` and `PrisonerEscape`, with the tier's magnitudes (a zero magnitude is skipped), ending `effectTtlHours` from now. They are refreshed daily, and every refresh removes the source and writes it whole again, so a saved kind whose magnitude `rally.json` now sets to zero does not survive the next refresh (Batch A Codex F3, `2ee36e1fa`). Any `rally:` source not kept this tick is removed, so a tier drop, the end of a war, a destroyed kingdom or the switch turned off all clear the effects the same way. A kingdom that left the map has its tier row dropped.
- **Guards:** an empty snapshot list is ignored (it would wipe every tier), and a non-finite clock skips the day.

### The war ledger

`WarLedgerService` (`Ledger/WarLedgerService.cs`) and `WarLedgerFormatter` (`Ledger/WarLedgerFormatter.cs`) write through `IModLogger.LogInfo`, so the lines land in `taom_debug_*.log`. The ledger is stateless: every number comes from the snapshots, the baselines, the tier store, the effect registry and the phase machine, so a reload re-emits a day and the analyzer keeps the last. It has no MCM switch; it runs on every authority tick. Lines are space-separated `key=value` tokens, numbers in the invariant culture, and any id goes through `Token` (whitespace and `=` become `_`). Header: `[WarLedger] v=1`.

| Line | Keys, in order |
|---|---|
| `t=kingdom` (one per kingdom per day) | `cid day phase k side ai war towns castles pts base loss cap str pris tier vr pe` |
| `t=side` (free, evil, neutral) | `cid day side pts base alive` |
| `t=share` | `cid day share` (Free points over Free plus Evil points, `na` when both are 0) |
| `t=event ev=tier` | `cid day k from to loss` |
| `t=event ev=destroyed` | `cid day k` |
| `t=event ev=chronicle` | `cid day id outcome` |
| `t=event ev=pledge` | `cid day k side by` (a Neutral realm joined `free` or `evil`; `by` is `ai` or `console`; [war-pledge.md](war-pledge.md)) |
| `t=load` (once per game load) | `cid day` (the day of the save that was loaded) |

`cid` is `Campaign.UniqueGameId`, `day` the elapsed days since campaign start, `phase` the War of the Ring phase name. `ai` and `war` are 0 or 1; `cap` is 1, 0, or `na` (whether the kingdom's initial home settlement is still held); `str` is the kingdom's total strength as a whole number, or -1 when the engine value is not finite or negative; `pris` counts the kingdom's clan heroes who are prisoners; `vr` and `pe` are the current volunteer and escape multipliers to two decimals, written after that tick's rally and escape pass, so `pe` is the multiplier the day's roll used. `pris` is read before the escape pass, so it still counts lords who escaped that tick. `ev=chronicle` is written once per chronicle resolution by `ChronicleOutcomeService` through `LogChronicleResolution`: `outcome` is `Occurred`, `Held`, `Fell`, `Lapsed` or `Skipped`. `t=load` is written by `WarChronicleBehavior` on `OnGameLoadedEvent`, on the authority only, through `WarLedgerService.LogLoad`, with the same day as the daily lines; it is how the analyzer drops the days of a run the player abandoned. A change to a line is a format change: bump `v` and teach the analyzer.

### The event chain

`ChronicleService` (`Events/ChronicleService.cs`) is the state machine, driven by `WarChronicleEventsBehavior`, a thin router that is built fresh in every campaign like `WarChronicleBehavior` and saves nothing. Every call it makes is gated on `IsAuthority` and `MayWriteSaveBackedState`. Times are whole campaign days: the floor of `IChronicleWorldAdapter.GetElapsedDays`, the same day as the ledger's `day=`. The daily tick fires a few game minutes past an elapsed-day boundary (`CampaignStartTime` plus whole days, 09:00 on vanilla's calendar clock, not midnight), or at a fractional time after a load, so fractional days would let a window or a child slip a day.

- **Adoption (decision D5):** at session launch, and at the start of every daily step, each configured event with no saved row gets one. When its open day is already past, or its anchor was Skipped, it becomes Skipped with no retroactive effect. A campaign started before this feature therefore gets no chronicle events once their days have passed; the rally and the ledger still work. A saved Pending row is not re-adopted: it opens on the next daily step even if late.
- **World check at session launch:** a Pending or Active event that names a settlement or kingdom the campaign does not have (`SettlementExists`, `KingdomExists`, also for effect targets that are kingdom ids) is Skipped with a warning.
- **Open day:** `CampaignStart` is day 0; `Event` is the anchor event's resolution day. `Phase1` and `Phase2` count only once the phase has been reached, from the later of two days: `IWarOfTheRingService.GetEffectivePhaseDay(IsengardWar or FullWar)`, the clamped trigger pair the phase machine itself compares against, and the day before the chronicle first saw the phase reached. The day before, because the chronicle's daily listener runs before the phase machine's ("Tick order" above): a phase entered on day N's tick is first seen on day N + 1, so a campaign on schedule opens on its configured days exactly as before. A phase entered late (the War of the Ring switched on late, a phase day lowered below today, Test Mode switched on mid-campaign) opens its events from that day plus their offsets, one by one, instead of every overdue event on one step. Raising a phase day after the phase began still postpones its Pending events. Each step that runs (session launch, the daily step, a confirmed `taom.chronicle_open`; its dry run records nothing) records the first day it sees each phase; the day is saved ("The save payload"), and a save without it records the day of its first step after the load. Adoption still judges a missing row by the configured day alone, so decision D5 is unchanged. Decided by the maintainer 2026-10-14. The offset is added in days. An event anchored on a Skipped event is Skipped; one whose `requiresOutcome` the anchor did not reach is Skipped.
- **Preconditions at open:** the required anchor outcome; for a siege also the Full War (decision D2: peace between Hostile pairs is blocked only then), the defender alive, at least one attacker alive, the target owned by the defender, and the defender at war (`IAllianceAdapter.AreAtWar`) with at least one live attacker. A failure Skips the event, logged with the reason. One exception, decided by the maintainer 2026-10-14: when an attacker already owns the target as a siege opens (in the Full War, with any required anchor outcome met), the event resolves Fell at once instead of Skipped, whether or not the defender survives or anyone is at war. It pays its Fell effects and momentum award as configured and announces its normal fell line, with no window, opening line or army focus, so its Event-anchored children still open (`gondor` after `osgiliath`; `blackgate`, which needs `gondor` Held, is then Skipped). A target owned by a third kingdom, or by no kingdom, still Skips the siege and its children.
- **Narrative:** Occurred at once; its effects are written and it is announced.
- **Siege:** unless an attacker already owns the target (Fell, above), Active, with `open` = today and `close` = today + `windowDays`, both stored; the army focus is pushed and the opening line is announced. If an attacker already besieges the target, the window counts as assaulted.
- **Assault:** `OnSiegeEventStartedEvent` at the target by an attacker kingdom (the besieging party's map faction), or the same seen on a daily step, marks the window assaulted.
- **Owner change while Active** (`OnSettlementOwnerChangedEvent`, which fires again when a multi-clan kingdom's fief vote grants the town): the new owner is an attacker, Fell at once; any other owner that is not the defender (a third kingdom, or a clan outside any kingdom), Lapsed with the generic "changes hands" line (`taom_wc_siege_lost`), because every event's own lapsed line says no attack came. Its English stays as written (decided by the maintainer 2026-10-14: keep). The second fire finds the window resolved and does nothing; a re-grant inside the defender does nothing. Every daily step runs the same owner check first, because a clan that defects or leaves its kingdom with rebellion takes its fiefs along with no owner-changed event (`ChangeKingdomAction`), and a change while the chronicle is off is never seen.
- **Window close:** the daily owner check has just confirmed the defender holds the target, so assaulted means Held, otherwise Lapsed (no effects, a toast only). While an attacker still besieges the target the close waits: the siege decides it (lifted, Held on the next daily step; taken, Fell). The overtime is capped at one more `windowDays`, because the engine never makes a besieging party leave and a player's camp could otherwise hold the window and its focus open; at the cap the window closes as above.
- **Story offer (#1105 slice 4):** a siege with a `story` block offers its Story Missions defence to the player while the window is Active. `GetStoryOffers` lists each Active story window the player has not declined, while no attacker has besieged the target, the defender still holds it (read live) and no announcement waits to show (none with the chronicle or the MCM switch "Offer Story Missions" off, or after War Ended); Story Missions shows the offer and settles it ([story-missions.md](story-missions.md) "The story offer"). A won mission calls `ResolveStoryWon`: the owner check runs first, then Held at once with its effects. A lost mission first calls `ConfirmStoryWindow` (the same owner check and War Ended guard), then gives the target to an attacker, and the owner check above resolves Fell. "Let fate decide" calls `DeclineStory`, which saves the row's `sd` flag; the window stays open. The map still decides first: once an attacker besieges the target the offer drops, and a capture resolves the window as above. At the close a story window that was never assaulted ends by its `fate` (read as `requiresOutcome` is read): Held with `fateText` (the Held outcome's text when it has none), which pays the Held outcome's effects and momentum, so the events that require a Held Hornburg (`ents`) follow; or Lapsed as a window without a story. Decided 2026-11-13 so that the Hornburg no longer ends with "No assault comes" when no army reaches it; a fate of `Lapsed` restores the old ending.
- **War Ended:** every Pending or Active event is Skipped, silently, and the focus clears. The check sits in the shared wrapper, so an owner-change or siege-start callback arriving before the next daily step cannot resolve or pay an Active window.
- **MCM off:** nothing opens or resolves and the focus clears; the saved states are kept, and an Active window resumes when the switch comes back, re-checking its owner on its first daily step.
- **Errors:** a throwing engine read is logged as `[WarChronicle] Chronicle <step> skipped` and the step is skipped. A non-finite clock skips the step: the adapter passes a non-finite engine value through, and returns NaN with no campaign.

Each resolution goes through `ChronicleOutcomeService` (`Events/ChronicleOutcomeService.cs`): the outcome's effect rows into the registry, its optional momentum award, and one `[WarLedger] ev=chronicle` line. Effect targets: `attacker` is every listed attacker, `defender` the defender, `free` and `evil` every kingdom of that side (`IAlignmentService.ResolveSide` with the kingdom's own culture), anything else a kingdom id; only kingdoms alive at the time are written. Each row is its own source, `chronicle:<event>:<outcome>:<row>`, so two rows reaching one kingdom add up. The end time is now plus `durationDays` × 24 hours. The player rule holds at the consumers ("The player rule" above): the player's own clan never gains or loses.

**Stalemate rule (decision D3).** Every siege pays both sides for 25 days: Held gives the defender volunteers +15% and the attackers escape +20%; Fell gives the attackers volunteers +15% and the defender escape +20%; Lapsed pays nothing. Gondor's auto-resolve edge means Gondor will usually hold, so paying both sides keeps one front from running away. The magnitudes are first values to tune against the ledger.

### Army focus

`ChronicleService` writes every Active siege into ArmyTargeting's `ArmyTargetFocusRegistry` (`Main/Features/ArmyTargeting/ArmyTargetFocusRegistry.cs`) after every chronicle step: the target, the attackers, the defender and the MCM Army Focus Multiplier (1 to 2.5, default 1.4). During a window, Besieger armies of the attackers and Defender armies of the defender score the target times the focus, on top of the priority lists, theater weights and home-defence multiplier ([army-targeting.md](army-targeting.md) "Army focus"). A Defender army's candidate is scored only once the target is attacked (vanilla's `AiMilitaryBehavior` needs an active, at-war `LastAttackerParty`), so the defender's focus waits for the first besieger. The registry stores only finite multipliers above 1, so vanilla's zero (the strength gate in `DefaultTargetScoreCalculatingModel`) stays zero; the distance veto returns before the model is called. The focused score also reaches `CurrentObjectiveValue`, which `Army.ThinkAboutCohesionBoost` reads, so during a window armies at the target spend influence on cohesion boosts up to 2.5 times as often. It is not saved: session launch pushes the Active windows again, a resolution drops its window, War Ended and the MCM switch clear it, and the router's constructor resets whatever an earlier campaign left (`ArmyTargetFocusRegistry.ResetForNewSession`). It needs AI Strategic Intelligence on.

### Announcements

`ChronicleAnnouncementQueue` (`Events/ChronicleAnnouncementQueue.cs`) holds texts and their `{TARGET}` and `{DAYS}` values in memory, not in the save, at most 16 (the oldest drop). The router's `TickEvent` listener calls `TryShowNext`, which returns at once on an empty queue, drops what is queued when the chronicle or its announcements are switched off, needs the map free, and then re-checks `IsAuthority` (the co-op probe uses reflection, so the cheap map test goes first). `ChroniclePresenter.IsMapFree` is `MapPopupGate.IsMapFree` (`Main/Adapters/MapPopupGate.cs`, #929, shared with the hunt news and the Hidden Hosts hint): a main party, no encounter, the map state not at a menu, and no inquiry open (`InformationManager.IsAnyInquiryActive`), the chronicle's own included: that is what keeps popups from stacking, so the queue holds no latch of its own and a popup closed without its button action (`InformationManager.HideInquiry`) cannot stall it. The presenter shows a one-button `InquiryData` ("Continue") with the event title and line; never a scene notification, which NREs for custom races. A show that throws is logged and the item dropped, so the queue never sticks. Openings, narratives, Held, Fell and Lapsed are announced; Skipped is silent. With Show Announcements off nothing is queued, and what was queued is dropped unshown. Co-op clients see no announcements, as with the momentum meter.

### Momentum awards

An outcome may carry `"momentum": {"side": "free"|"evil", "units": 1..300}`. `ChronicleOutcomeService` calls `IMomentumAwardService.TryAwardScripted` with the resolved title as the breakdown description; the award is a `MomentumActionType.Chronicle` event that decays after `durationsHours.chronicle` ([war-of-the-ring-momentum.md](war-of-the-ring-momentum.md) "Chronicle awards"). It is refused before the momentum war starts and after it ends. Momentum victory stays off, so an award only moves the meter. The shipped file gives the Free side 100 units for every held siege, because a held siege earns nothing from the capture path.

### The save payload

`WarChronicleStateService` (`WarChronicleStateService.cs`) assembles one JSON payload through `WarChronicleSaveCodec` (`WarChronicleSaveCodec.cs`): `{"v":1,"effects":[{"s","k","kind","m","end"}],"baselines":{},"rally":{"tiers":{}},"chronicle":[{"id","s","open","close","a","res","sd"}],"phases":{"IsengardWar":day,"FullWar":day},"daily":{"tick":day,"chronicle":day}}`. The codec reads and writes the `chronicle` rows itself, as typed records the payload hands to `ChronicleStateStore` (`Events/ChronicleStateStore.cs`): the event id, its state as an int (`ChronicleEventState`: Pending 0, Active 1, Occurred 2, Held 3, Fell 4, Lapsed 5, Skipped 6, never renumbered), the open and close days of a window, whether it was assaulted, the resolution day, and `sd`, true when the player let fate decide the window's story offer (absent means false, so an older save loads unchanged). A row with no id, a duplicate id, an undefined state, an Active state without a finite window, a resolved state without its resolution day (an Event-anchored child counts from it), or a day that is not a finite number is skipped alone with a warning, and the adoption rule re-creates it at session launch. A row for an id the configuration no longer lists is kept, so a later release's save survives a load in this one. The `phases` object holds the day the chronicle first saw each anchoring phase reached, keyed by the `WarPhase` name, and also lives in `ChronicleStateStore`; an entry for another phase (`Peace`, `WarEnded`, an unknown name) or without a finite day is skipped alone with a warning, and a missing day is recorded again on the first step after the load. Section isolation holds: a `phases` value that is not an object resets alone. The `daily` object holds the last whole day each `WarChronicleDailyGate` lane ran ("Once per day"); an entry that is not a non-negative integer is skipped alone, a value that is not an object resets alone, and a save without it has run no day. The army focus and the announcement queue are not saved.

- **Chunking:** `SyncDataChunker.Split` (`Main/Core/Persistence`, shared with the momentum behavior) cuts the string into chunks of at most 10,000 characters, because one `SyncData` string over 32,763 UTF-8 bytes corrupts the save at write time (the engine writes a short length of 4 plus the bytes, v1.5.5 `SaveContext.SaveStringTo`). `WarChronicleBehavior` writes `_taom_war_chronicle_v1_count` and `_taom_war_chronicle_v1_{i}` through `SyncDataChunker.SyncChunks`. A count below 0 or above 4,096 is ignored with a warning.
- **Forgiving parse:** an unreadable payload, a non-object or an unknown `v` resets every section. A section that does not parse resets alone, and a bad row inside a section (an unknown effect kind, a non-finite number, a rally tier outside 0 to 2) is skipped alone; each leaves a `[WarChronicle] Save:` warning. An empty payload (a corrupt or out-of-range chunk count) restores nothing; a save from before this feature never reaches `SyncData` at all. A development save that still carries the dropped `baselinesMidCampaign` key loads fine, because unknown keys are ignored.

### Component flow

```
rally.json            MCM (War of the Ring/Rally)
    |                       |
RallyConfigProvider   WarChronicleSettingsProvider
        \                  /
   DailyTickEvent -> WarChronicleBehavior -> WarChronicleTickService
                                   |
        effects.Expire -> WarBaselineService -> RallyService -> IWarEffectService
        WarEscapeDailyPass -> IPrisonerEscapeAdapter
        WarLedgerService -> taom_debug_*.log -> tools/analyze_war_ledger.py

TaomVolunteerModel ------------------------\
CastleNotableMaintainer -> CastleRecruitmentService -> VolunteerProductionService -> IWarEffectService.GetMultiplier

war_chronicle.json -> ChronicleConfigProvider (ChronicleConfigValidator)
SessionLaunched, DailyTick, OwnerChanged, SiegeStarted -> WarChronicleEventsBehavior -> ChronicleService
        ChronicleStateStore (saved in WarChronicleBehavior's payload)
        IChronicleWorldAdapter, IAllianceAdapter, IWarOfTheRingService (phase days)
        ChronicleOutcomeService -> IWarEffectService, IMomentumAwardService, WarLedgerService
        ChronicleService -> ArmyTargetFocusRegistry <- ArmyTargetingService (TaomTargetScoreModel)
        ChronicleAnnouncementQueue -> IChroniclePresenter (TickEvent, one inquiry at a time)
```

## Configuration

### Config file: `Main/_Module/ModuleData/war_chronicle/war_chronicle.json`

Loaded by `ChronicleConfigProvider` (`Events/ChronicleConfigProvider.cs`) through `ChronicleConfigValidator` (`Events/ChronicleConfigValidator.cs`). A missing or unreadable file loads no events: the chronicle does nothing and everything else runs. `Reuse.Singleton` with a `Lazy` load, so an edit needs a full game restart.

```json
// Abridged: two of the 13 events, texts shortened.
{ "schemaVersion": 1, "events": [
  { "id": "hornburg", "kind": "Siege", "anchor": "Phase2", "offsetDays": 5, "windowDays": 20,
    "target": "town_V2", "attackers": ["isengard", "empire"], "defender": "vlandia",
    "title": "{=taom_wc_hornburg_title}...", "openText": "{=taom_wc_hornburg_open}... {TARGET} ... {DAYS} ...",
    "lapsedText": "{=taom_wc_hornburg_lapsed}...",
    "story": { "mission": "helms_deep_defend", "fate": "Held", "fateText": "{=taom_wc_hornburg_fate}..." },
    "outcomes": { "held": { "text": "...", "effects": [ { "target": "defender", "kind": "VolunteerRate", "magnitude": 0.15, "durationDays": 25 } ],
                            "momentum": { "side": "free", "units": 100 } },
                  "fell": { "text": "...", "effects": [] } } },
  { "id": "ents", "kind": "Narrative", "anchor": "Event", "anchorEvent": "hornburg", "requiresOutcome": "Held", "offsetDays": 2,
    "title": "...", "outcomes": { "occurred": { "text": "...", "effects": [] } } } ] }
```

Every text is `{=taom_wc_<event>_<part>}English`, registered in `taom_wotr_strings.xml`.

#### Validation rules

One test each in `ChronicleConfigValidatorTests`. Each problem is a warning; one summary warning follows any.

- **The file:** readable JSON, `schemaVersion` 1, an `events` array; otherwise no event loads. At most 32 events; the rest are skipped.
- **An event is skipped** for: a blank or duplicate id; a `kind` other than `Narrative` or `Siege`; an `anchor` other than `CampaignStart`, `Phase1`, `Phase2` or `Event`; an `Event` anchor without `anchorEvent`, or one naming an event that is not earlier in the file and loaded (so no cycle can form); an `anchorEvent` on a phase anchor; a `requiresOutcome` without an Event anchor, of an unknown state, or one the anchor's kind never reaches (a narrative only `Occurred`, a siege `Held`, `Fell` or `Lapsed`); `offsetDays` outside 0 to 365; no `title`; a narrative without an `occurred` outcome; a siege with `windowDays` outside 1 to 60, no `target`, no `defender`, no `attackers`, the defender among the attackers, or an anchor chain that does not reach Phase 2 (anchor `Phase2`, or an `Event` chain through a Phase 2 event or a siege).
- **An effect row is skipped alone** for: a blank `target`; `attacker` or `defender` on a narrative; a `kind` other than `VolunteerRate` or `PrisonerEscape`; a `magnitude` that is not finite (checked first), is zero, or is past 0.25 either way; `durationDays` outside 1 to 60.
- **A momentum award is dropped alone** when it is not an object, for a `side` other than `free` or `evil`, or for `units` outside 1 to 300 (the momentum service's own cap).
- **Names only:** `kind`, `anchor`, `requiresOutcome` and an effect's `kind` match a name, trimmed and in any case; a number, a sign or a comma list never parses.
- **Unknown keys warn:** a key the level does not know (event, outcome, effect, momentum) is ignored with a warning, so a typo such as `momentm` cannot drop a block silently; a key starting with `_` is an author's note. An optional text that is not a string, a siege's `outcomes` that is not an object and `effects` that is not an array warn too; a `requiresOutcome` that is not a string skips the event.
- An outcome the kind never reaches (`held` on a narrative, `lapsed` anywhere) is ignored with a warning.
- **A story block** (sieges only; on a narrative it is an unknown key) that is not an object, or names no `mission`, is dropped with a warning and the event stays. A `fate` other than `Held` or `Lapsed` (exact case) is Held with a warning; no `fate` is Held. Its keys are `mission`, `fate` and `fateText`; any other warns. Whether `mission` names a loaded defence is checked by `ShippedStoryOffersTests` and, at offer time, by `StoryOfferService`, because this loader has no story catalog.
- Settlement and kingdom ids are checked against the campaign at session launch, not at load.

#### The v1 content

Every siege opens at Phase 2 or later. Target owners were confirmed by hand in the live `TAOM_Map/ModuleData/settlements.xml` and the clan files on 2026-10-08; at runtime the open precondition Skips a siege whose target the defender no longer holds. `ChronicleShippedContentTests` pins the target, defender and attacker ids, and that every attacker and defender pair is Hostile in `diplomacy.json`. Siege rows use the stalemate rule and award the Free side 100 momentum units when held.

| # | Event (`id`) | Kind | Opens | Window | Target: attackers -> defender | Effects |
|---|---|---|---|---|---|---|
| 1 | The Fellowship Sets Out (`fellowship`) | Narrative | Start + 20 | | | rivendell volunteers +10%, 20 d |
| 2 | The Bridge of Khazad-dûm (`khazaddum`) | Narrative | #1 + 6 | | | mistymountainorcs volunteers +10%; free volunteers -5%; 15 d |
| 3 | The Breaking of the Fellowship (`breaking`) | Narrative | Phase 1 + 0 | | | isengard volunteers +10%; empire_w volunteers -5%; 15 d |
| 4 | Théoden King (`theoden`) | Narrative | Phase 1 + 7 | | | vlandia volunteers +15%, 20 d |
| 5 | The Battle of the Hornburg (`hornburg`) | Siege | Phase 2 + 5 | 20 d | town_V2 (Helm's Deep): isengard, empire -> vlandia | stalemate rule |
| 6 | The Last March of the Ents (`ents`) | Narrative | #5 + 2, needs #5 Held | | | isengard volunteers -15%, 20 d |
| 7 | Lothlórien Assailed (`lorien`) | Siege | Phase 2 + 8 | 20 d | town_L1 (Caras Galadhon): dolguldur -> lothlorien | stalemate rule |
| 8 | The Fall of Osgiliath (`osgiliath`) | Siege | Phase 2 + 10 | 15 d | town_EW3 (East Osgiliath): empire_s -> empire_w | stalemate rule |
| 9 | The Battle of Dale (`dale`) | Siege | Phase 2 + 12 | 20 d | town_S3 (Eldby): khuzait, dolguldur -> sturgia | stalemate rule; Fell also gives erebor escape +20%, 25 d |
| 10 | The Corsairs of Umbar (`corsairs`) | Siege | Phase 2 + 14 | 15 d | town_EW4 (Pelargir): umbar, empire_s -> empire_w | stalemate rule |
| 11 | War in the North (`north`) | Siege | Phase 2 + 16 | 20 d | town_AN3 (Bree): gundabad, empire -> arthedain | stalemate rule |
| 12 | The Siege of Gondor (`gondor`) | Siege | #8 + 3, any result | 20 d | town_EW1 (Minas Tirith): empire_s -> empire_w | stalemate rule |
| 13 | The Black Gate Opens (`blackgate`) | Narrative | #12 + 5, needs #12 Held | | | free volunteers +10%; empire_s escape +20%; 15 d. End of the chain; the Ring seam |

Reach is not verified for #9, #10 and #11; those may often lapse, and the ledger will show it.

### Config file: `Main/_Module/ModuleData/war_chronicle/rally.json`

Loaded by `RallyConfigProvider` (`Rally/RallyConfigProvider.cs`). A missing file or a parse failure uses the compiled defaults (`Rally/RallyConfig.cs`). Every float is finite-checked before its range check; a bad field reverts to its default with a warning, and one summary warning follows any revert.

| Field | Type | Default | Validation |
|---|---|---|---|
| `enabled` | bool | true | Master switch in the file; the MCM `Enable Rally` must also be on |
| `includeNeutral` | bool | true | Whether Neutral-side kingdoms can receive effects |
| `effectTtlHours` | float | 48 | Finite, 24 to 168 |
| `tier1.enterLoss` | float | 0.25 | Finite, above 0 and below 1 |
| `tier1.exitLoss` | float | 0.15 | Same |
| `tier1.volunteerRate` | float | 0.10 | Finite, 0 to 2 |
| `tier1.prisonerEscape` | float | 0.50 | Finite, 0 to 2 |
| `tier2.enterLoss` | float | 0.50 | As tier 1 |
| `tier2.exitLoss` | float | 0.40 | As tier 1 |
| `tier2.volunteerRate` | float | 0.20 | As tier 1 |
| `tier2.prisonerEscape` | float | 1.00 | As tier 1 |

Ordering rules cannot be repaired field by field, so a broken ordering reverts its whole group. For the four loss thresholds: each tier's `exitLoss` must be below its `enterLoss`, tier 1's `enterLoss` must not exceed tier 2's, and tier 1's `exitLoss` must not exceed tier 2's. For the four magnitudes: tier 2's `volunteerRate` and `prisonerEscape` must not be below tier 1's.

Magnitudes are added to the registry sums before the MCM strength, so with strength 1 the defaults mean tier 1: volunteer roll x1.10, escape x1.50; tier 2: volunteer roll x1.20, escape x2.00 (inside the 0.5 to 1.5 and 1.0 to 3.0 clamps). The volunteer multiplier scales a per-slot daily probability, not a volunteer count; the escape multiplier scales vanilla's own chance, perks and Valor included.

### MCM settings (`Main/Features/TaomSettings.cs`)

Both sit in the group `War of the Ring/Rally`, with `RequireRestart = false`.

| Setting | Property | Default | Notes |
|---|---|---|---|
| Enable Rally | `WarRallyEnabled` | true | Takes effect at the next daily tick; off removes every rally effect |
| War Effect Strength | `WarEffectStrength` | 1.0 | Slider 0 to 2; scales every effect, the rally's and the chronicle events' alike. 0 turns effects off |

The group `War of the Ring/War Chronicle`, also `RequireRestart = false`:

| Setting | Property | Default | Notes |
|---|---|---|---|
| Enable Chronicle | `WarChronicleEnabled` | true | Off stops new work: nothing opens or resolves and the army focus clears. The saved states are kept |
| Show Announcements | `WarChronicleAnnouncements` | true | Off queues no popup; the events still run. Presentation only, so it is excluded from the co-op settings fingerprint |
| Army Focus Multiplier | `WarChronicleArmyFocus` | 1.4 | Slider 1 to 2.5 (the registry's cap, one constant); 1 turns the focus off. The defender's focus applies once the target is attacked. Needs AI Strategic Intelligence |
| Offer Story Missions | `WarChronicleStoryMissions` | true | Off: no story offer; the map and the window's fate still decide (#1105 slice 4). Read live through `IWarChronicleSettingsProvider.StoryOffersEnabled` |

`WarChronicleSettingsProvider` re-validates the strength (non-finite to 1, else clamp 0 to 2) and the focus multiplier (non-finite to 1.4, else clamp 1 to 2.5), and takes the settings object once, lazily, on first read; it is registered in `CampaignHotPathSettingsProvidersTests`. The focus multiplier is read when the chronicle pushes its windows (every daily step), never on the scoring path.

### Reload scope

`RallyConfigProvider` and `ChronicleConfigProvider` are `Reuse.Singleton`s with a `Lazy` load, so each reads its file once per Bannerlord process. An edit needs a full game restart, not a new campaign or a save and load. The MCM values are live: the rally's two are read on the daily tick (the strength by the re-bake in `Expire`, the switch by `RallyService.IsActive`), and the chronicle's switch and focus multiplier on its next step, so a change applies at the next daily tick without a restart.

## Console commands

Registered in `Main/Features/WarChronicle/Cheats/WarChronicleCheats.cs` and `ChronicleCheats.cs`, all through `TaomConsole.RunInCampaign` (campaign plus cheat mode). Full rows in [dev-console.md](dev-console.md).

| Command | Effect |
|---|---|
| `taom.war_effects` | Lists every active effect with its time left, and each affected kingdom's volunteer and escape multiplier (MCM strength applied) |
| `taom.war_effect_add <kingdomId> <VolunteerRate\|PrisonerEscape> <magnitude> <days>` | Adds an effect from source `console`: magnitude -1 to 1, days above 0 up to 365; the kingdom must exist in the campaign. Echoes the multiplier before and after. Refused on a BannerlordCoop client |
| `taom.rally_status` | Per kingdom: points, baseline, loss, stored tier and eligibility, plus whether the rally is on |
| `taom.print_war_ledger` | Prints the ledger lines today's tick would write; logs and changes nothing |
| `taom.chronicle_status` | Every chronicle event with its state, its open, close or resolution day (an Active window past its close day reads "overtime: an attacker still besieges it"), whether an Active siege was assaulted, and the army focus, marked inactive when AI Strategic Intelligence is off. Read-only |
| `taom.chronicle_open <id> [confirm]` | Tier C. Without `confirm` a dry run: the same checks, a report of what would happen, and no change. With the literal `confirm` it opens a Pending event now, ignoring its day but still checking its preconditions: a narrative occurs at once; a siege opens its window, or resolves Fell at once when an attacker already holds the target. A failed check leaves it Pending and says why. Refused on a co-op client, while the chronicle is off and after the War of the Ring has ended |

## How to measure

The ledger is the proof that the rally changes anything.

1. **Run an observer campaign.** Start a campaign with the War of the Ring test mode (short phase days, see [war-of-the-ring.md](war-of-the-ring.md)), park the player somewhere harmless, and fast-forward with the TAOM time tiers for a few hundred in-game days. One run per setting: for example the MCM `Enable Rally` off for a control and on for the test.
2. **Read the log.** The default is the newest `taom_debug_*.log` under the game's `bin/Win64_Shipping_Client/Logs/`; `BANNERLORD_GAME_DIR` overrides the install path. Run `python tools/analyze_war_ledger.py [logs ...] [--cid ID]`. The tool is read-only, groups by campaign id, reads the logs oldest first, and keeps the last line per kingdom and day, so reloading the same save does not double-count. A load is a rewind: a `t=load` line at day S drops every stored record of that campaign with a day after S (kingdom, side and share lines, events and chronicle resolutions), which belong to the run the player abandoned. Day S is kept from both runs, since a save made in the first half of elapsed day S can tick day S again ("Once per day") and the old run's day-S lines came before the save or in the same day; exact repeats still collapse. A log with no load markers gets no rewind: logs from builds before the marker can keep an abandoned run's days. A malformed line is skipped and counted, and the first one is printed as a warning. Exit codes: 0 success; 1 no ledger lines, an unknown cid, an unreadable log or an unwritable `--csv` path; 2 an unknown `v` or a usage error.
3. **Metrics in the summary:** days covered; the Free share series (mean deviation from 0.5, maximum deviation and its day, fraction of days in 0.4 to 0.6, lead changes with a dead band of 0.48 to 0.52); first capital loss per kingdom and per side; kingdoms destroyed; realms pledged; fief churn per 10-day bucket; days at tier 1 and tier 2 and tier changes per kingdom; net change per side; chronicle outcomes by day, one row per event, then counted per outcome (`resolutions by outcome: Held 2, Lapsed 3`), keeping the last line read for each event even when it repeats an earlier line byte for byte, so a reload that replays a resolution counts once; and a target check, (a) share in 0.4 to 0.6 on at least 80% of non-na days, (b) no capital lost before day 120, with four verdicts, judged per kingdom: FAIL (any `cap=0` before day 120, reported as "day N" for an observed transition and "lost by day N" when the first capital state is already 0), PASS (every kingdom has a capital state on every day 1 to 119, or up to the day before its `ev=destroyed` line, and none was lost), NO DATA (no line carries a capital state) and UNKNOWN (some kingdom has a hole in that range: `cap=na` days, a kingdom first seen after day 1, a destroyed kingdom with no rows, or missing days; the reason names the kingdom, and another kingdom's complete rows never fill it).
4. **Compare two runs.** `--compare off.log on.log` (each argument is `path` or `path:cid`; with no cid the campaign with the most days is used) prints each metric for A, B and B minus A, then the target verdicts for each. `--compare` reads one file per side (concatenate a run's logs, oldest first) and refuses `--csv`, `--cid` or extra log paths. `--csv PATH` writes the per-day series.

The analyzer's tests are `tools/tests/test_analyze_war_ledger.py` (run `python -m unittest tools.tests.test_analyze_war_ledger` from the repo root; `/verify`'s `python -m unittest discover -s tools/tests -t .` covers them too).

## How to test the chronicle in game

Use the War of the Ring test mode (Phase 1 on day 1, Phase 2 on day 3) on a new campaign, with cheat mode on. Watch `taom_debug_*.log` for `[WarChronicle]` and `[WarLedger] ... ev=chronicle` lines.

1. **Narratives:** on day 1 or 2 "The Breaking of the Fellowship" and on day 8 or 9 "Théoden King" pop up on the map (one at a time, never over a menu or a battle). `taom.war_effects` lists `chronicle:breaking:Occurred:0` on isengard.
2. **A siege opens:** on day 8 the Hornburg window opens (Phase 2 + 5). `taom.chronicle_status` shows it Active with its close day and the focus line `town_V2 x1.40`. Or open one at once: `taom.chronicle_open osgiliath` reports what would happen, then `taom.chronicle_open osgiliath confirm` opens it.
3. **Held:** let an Isengard or Dunland army besiege Helm's Deep and fail (or, as a vassal of Isengard, besiege it yourself and lift the siege); at the close the event is Held, vlandia shows volunteers x1.15 and isengard and empire escape x1.20 in `taom.war_effects`, and the momentum breakdown gains a "Chronicle Events" row for the Free side.
4. **Fell:** take a target town for an attacker (join Isengard and capture Helm's Deep). The window resolves Fell the moment the town changes hands. Check the double fire: in a kingdom of more than one clan the fief vote fires the owner change again when it resolves, and it must not log a second `ev=chronicle` line or a second popup.
5. **Lapsed:** open a window early (`taom.chronicle_open north confirm`) and watch that no Gundabad or Dunland army besieges Bree before the close: a Lapsed popup, no new effect. Repeat on another front if one does.
6. **Save and reload mid-window:** save while a window is Active, reload: `taom.chronicle_status` shows the same close day and the focus line again, and the window resolves on its old close day unless an attacker still besieges the target.
7. **New campaign after a resolved chain:** exit to the menu and start a new campaign in the same process: every event is Pending again.
8. **Old save:** load a campaign past day 60 that was started before this feature: the events whose day has passed are Skipped (silently, one `outcome=Skipped` ledger line each); later events still open.
9. **Late phase:** in a new campaign, switch the War of the Ring off, play past day 60, switch it on: the next daily tick enters both phases, and the Phase 1 and Phase 2 events then open one by one from that day plus their offsets, not together.
10. **Once per day after a load:** save at about 12:00 on the game clock (an eighth into elapsed day S, since elapsed days start at 09:00) and load it. The log shows `[WarLedger] v=1 t=load cid=<cid> day=S`, and no second set of day-S `t=kingdom` lines and no second chronicle step follow before day S + 1. The gate skips a repeat without a log line; whether the engine ticked day S again at about 15:00 is #793's own check.

## Key files

| File | Purpose |
|---|---|
| `Main/Features/WarChronicle/WarChronicleModule.cs` | Feature module, both behavior declarations |
| `Main/Features/WarChronicle/WarChronicleBehavior.cs` | Per-campaign behavior: daily tick, destroyed and load lines, SyncData |
| `Main/Features/WarChronicle/WarChronicleTickService.cs` | Tick order |
| `Main/Features/WarChronicle/WarChronicleDailyGate.cs` | Once-per-day guard for both daily listeners |
| `Main/Features/WarChronicle/WarChronicleStateService.cs`, `WarChronicleSaveCodec.cs`, `WarChroniclePayload.cs` | Save payload, chunking, parsing |
| `Main/Features/WarChronicle/Effects/` | Registry, math, escape service and daily pass |
| `Main/Features/WarChronicle/Rally/` | Baselines, tier machine and store, rally service, config provider |
| `Main/Features/WarChronicle/Ledger/` | Ledger service and formatter |
| `Main/Features/WarChronicle/Cheats/` | Console commands and their report text |
| `Main/Features/WarChronicle/Events/` | The event chain: definitions, validator and provider, state store, service, outcome service, announcement queue, presenter, router behavior |
| `Main/Adapters/ChronicleWorldAdapter.cs`, `IChronicleWorldAdapter.cs` | Clock, settlement and kingdom ids, target owner and besieger |
| `Main/Features/ArmyTargeting/ArmyTargetFocusRegistry.cs`, `ArmyTargetFocus.cs` | The army focus windows read by `ArmyTargetingService` |
| `Main/Features/WarOfTheRingMomentum/MomentumAwardService.cs` | The `Chronicle` momentum award |
| `Main/_Module/ModuleData/war_chronicle/war_chronicle.json`, `Main/_Module/ModuleData/taom_wotr_strings.xml` | Event data and its English text |
| `Main/Features/WarChronicle/WarChronicleIoC.cs`, `WarChronicleSettingsProvider.cs` | Registrations, MCM provider |
| `Main/Adapters/KingdomWarSnapshotAdapter.cs`, `KingdomWarSnapshot.cs` | Kingdom read model |
| `Main/Adapters/PrisonerEscapeAdapter.cs`, `PrisonerEscapeSnapshot.cs`, `DungeonReading.cs` | Captured-lord read model, the escape call and the per-dungeon read the dungeon menu uses |
| `Main/Features/TroopProgression/VolunteerProductionService.cs`, `Models/TaomVolunteerModel.cs` | Volunteer consumer, towns and villages |
| `Main/Features/CastleRecruitment/CastleRecruitmentService.cs`, `Hooks/CastleNotableMaintainer.cs` | Volunteer consumer, castles |
| `Main/_Module/ModuleData/war_chronicle/rally.json` | Rally data |
| `tools/analyze_war_ledger.py` | Ledger analyzer |

## Dependencies

- `IWarOfTheRingService` (Diplomacy): the current phase, for the ledger's `phase` key, the War Ended check and the siege's Full War precondition; `GetEffectivePhaseDay`, for the Phase 1 and Phase 2 anchors.
- `IAlignmentService` (Execution): `ResolveSide`, for the ledger's `side` key, the rally's Neutral eligibility check and the chronicle's `free` and `evil` effect targets.
- `ICoopSessionProvider` and `CoopSessionPolicy` (CoopInterop): the authority and save-write gates in the daily tick, the chronicle router, `taom.war_effect_add` and `taom.chronicle_open`; the authority check alone for the `ev=destroyed` and `t=load` lines and the announcement queue.
- `SyncDataChunker` (Core/Persistence): SyncData chunking and the count-and-chunks pass, shared with the momentum behavior.
- `TaomConsole` (DevConsole): the six commands.
- `IKingdomWarSnapshotAdapter`, `IPrisonerEscapeAdapter` and `IChronicleWorldAdapter` (Adapters, registered by this feature): kingdom fiefs, strength and the elapsed day; captured lords, their escape factors and `EndCaptivityAction.ApplyByEscape`; the clock, settlement and kingdom ids, and a target's owner and besieger.
- `IAllianceAdapter` and `ITextLocalizerAdapter` (shared adapters): live kingdoms, kingdom cultures and wars for a siege's preconditions and the effect targets; the award's description.
- `ArmyTargetFocusRegistry` and `IArmyTargetingSettingsProvider` (ArmyTargeting): the focus windows, and the AI Strategic Intelligence flag that `taom.chronicle_status` reports.
- `IMomentumAwardService` (WarOfTheRingMomentum): the `Chronicle` award.
- `IPathService` and `IModLogger` (Core), and `TaomSettings` through `WarChronicleSettingsProvider` (MCM).

**Used by:** `TaomVolunteerModel` (TroopProgression) and `CastleRecruitmentService` (CastleRecruitment) read `IWarEffectService` through `VolunteerProductionService`. Only `WarChronicleIoC` registers that service, so `WarChronicleModule` must stay in `FeatureModules.All`, or resolving `VolunteerProductionService` in SubModule fails. `VolunteerProductionWiringTests` validates the graph.

## Tests

Counts measured by grepping `[TestMethod]` and `[DataTestMethod]` attribute lines (2026-10-14); they exclude `[DataRow]` expansions. The 564 under `TAOM.Tests/Features/WarChronicle/` plus 77 in the eight outside files below make 641 C# tests, of which 107 are `[DataTestMethod]`. The once-per-day gate has its own class (`WarChronicleDailyGateTests`) and a test each in the tick, chronicle, codec, state and behavior classes; the load marker has a golden line, a line in the shared fixture, two `LogLoad` tests and three behavior tests. An unreadable clock has three tick tests, a `LogLoad` test, a behavior test, the adapter's `TryFloorElapsedDays` cases and a chronicle case per invalid value. `MomentumConfigProviderTests` also gained 2 for the `chronicle` duration.

| Class | File | Tests |
|---|---|---|
| `WarEffectServiceTests` | `TAOM.Tests/Features/WarChronicle/Effects/` | 33 |
| `WarEscapeServiceTests` | same | 45 |
| `WarEscapeDailyPassTests` | same | 13 |
| `RallyServiceTests` | `.../Rally/` | 42 |
| `RallyConfigProviderTests` | same | 29 |
| `RallyTierMachineTests` | same | 14 |
| `RallyTierStoreTests` | same | 10 |
| `WarBaselineServiceTests` | same | 16 |
| `WarLedgerFormatterTests` | `.../Ledger/` | 20 |
| `WarLedgerServiceTests` | same | 18 |
| `WarChronicleCheatReportTests` | `.../Cheats/` | 19 |
| `WarChronicleSaveCodecTests` | `.../WarChronicle/` | 32 |
| `WarChronicleStateServiceTests` | same | 13 |
| `WarChronicleTickServiceTests` | same | 21 |
| `WarChronicleBehaviorTests` | same | 11 |
| `WarChronicleDailyGateTests` | same | 9 |
| `WarChronicleSettingsProviderTests` | same | 7 |
| `WarChronicleIoCTests` | same | 5 |
| `WarChronicleWiringTests` | same | 5 |
| `VolunteerProductionServiceTests` | `TAOM.Tests/Features/TroopProgression/` | 21 |
| `VolunteerProductionWiringTests` | same | 1 |
| `ChronicleServiceTests` | `.../WarChronicle/Events/` | 86 |
| `ChronicleConfigValidatorTests` | same | 55 |
| `ChronicleConfigProviderTests` | same | 5 |
| `ChronicleAnnouncementQueueTests` | same | 12 |
| `ChronicleShippedContentTests` | same | 9 |
| `ChronicleStateStoreTests` | same | 10 |
| `ChroniclePersistenceTests` | same | 5 |
| `ChroniclePhaseAnchorTests` | same | 10 |
| `WarChronicleEventsWiringTests` | same | 5 |
| `ChronicleCheatReportTests` | `.../WarChronicle/Cheats/` | 13 |
| `WarChronicleEventSettingsTests` | `.../WarChronicle/` | 5 |
| `ArmyTargetFocusRegistryTests` | `TAOM.Tests/Features/ArmyTargeting/` | 19 |
| `ArmyTargetFocusScoringTests` | same | 7 |
| `MomentumAwardServiceTests` | `TAOM.Tests/Features/WarOfTheRingMomentum/` | 9 |
| `WarOfTheRingPhaseDayTests` | `TAOM.Tests/Features/Diplomacy/` | 5 |
| `PrisonerEscapeAdapterTests` | `TAOM.Tests/Adapters/` | 9 |
| `KingdomWarSnapshotAdapterTests` | same | 11 |

`WarChronicleSettingsProvider` is also a row in `CampaignHotPathSettingsProvidersTests`, and `CastleRecruitmentServiceTests` covers the castle consumer. The analyzer has 116 Python tests (`def test_` count, 2026-11-10) in `tools/tests/test_analyze_war_ledger.py`, the load-marker cases among them (`LoadMarkerTests`).

## Known limitations

- **Old saves get a mid-campaign baseline.** A save from before this feature has no baselines, so they are taken at the first tick after the load (the engine never calls `SyncData` for it), and the rally measures loss from that day's map. It under-fires on such a campaign; a warning is logged once, because the take happens past day 1 with no baselines held.
- **The player still meets effects indirectly.** No effect is keyed to the player's own clan, but the cases below remain. The Enable Rally hint keeps its wording ("Never applies to a kingdom you rule, or to your own clan's settlements and lords"), decided by the maintainer 2026-10-14: keep.
  - A kingdom the player serves as vassal or mercenary can rally: only `RulingClan == PlayerClan` is skipped, and mercenary service sets `clan.Kingdom`. The rule is ruler-only, as in `PlayerKingdomHelper`.
  - The extra volunteers sit in shared notable slots, so the player can recruit volunteers a rallying kingdom produced.
  - A rallying kingdom's lord held by the player escapes faster: at half the extra chance in vanilla's three player-held cases, and at the full extra chance when another party of the player's clan holds him outside a player-clan settlement.
  - A kingdom whose throne passes to the player keeps its rally effects until that tick's rally pass, which now runs before the escape pass, so its lords get no further boosted roll.
- **Co-op: host-only under BannerlordCoop.** Under BannerlordCoop the tick, the escape pass, the rally, the baselines and the ledger run only on the host (`IsAuthority` and `MayWriteSaveBackedState`), `taom.war_effect_add` is refused on a client, and a client gets the host's state from the save at join and nothing after (see [coop-interop.md](coop-interop.md), "No sync of TAOM's own campaign state"). The volunteer consumer does not run on a BannerlordCoop client either: it is reached only from `DailyTickSettlementEvent` (and new-game creation), and that per-entity ticker never reaches a client. Under BannerlordTogether, which TAOM cannot probe, `IsAuthority` fails open and `IsCoopClient` reads false: every peer then runs the daily tick with its own escape rolls (`EndCaptivityAction.ApplyByEscape`) and its own rally writes, and the console refusal does not fire (UNVERIFIED whether that mod's client-side suppression contains it). This follows the `IsAuthority` policy in coop-interop.md.
- **Effects show no tooltip line.** The volunteer probability is a plain float and the escape is an internal roll, so neither has a tooltip to carry a line. `taom.war_effects` and the ledger are the only views; the chronicle's announcements tell the player what an event did.
- **Small by design.** Effects touch volunteers and escapes only. They do not change a troop's combat power or auto-resolve scoring, though waiting recruits reach higher tiers sooner (the volunteer roll also gates promotion), so they cushion a skewed war rather than reverse it. Vanilla's own low-fief boost for a small kingdom can exceed the rally's x1.10 and x1.20 volunteer multipliers; the `vr` ledger column shows only the War Chronicle multiplier.
- **The Eye of Sauron hunt (M8) is a separate module** and keeps its own `_taom_eye_*` save keys ([eye-of-sauron.md](eye-of-sauron.md)).
- **Old saves get no chronicle.** A campaign past an event's day when it first loads this feature skips that event (decision D5); one past about day 60 gets no chronicle events at all.
- **Lapsing sieges.** An attacker may not reach its target in the window (Dunland drawn north, Umbar by sea, Osgiliath before Minas Tirith). The window lapses with no effects; the ledger shows how often.
- **The chronicle runs one day behind a phase change.** The chronicle's daily listener always runs before the phase machine's, so a `Phase1 + 0` event opens one daily tick after the phase changes; it is never skipped.
- **The army focus needs AI Strategic Intelligence.** With that MCM switch off, TAOM's army scoring, the focus included, is vanilla's.
- **Co-op clients see no announcements** and run no chronicle step; they take the host's chronicle state through the save.
- **Translations are AI first drafts.** The 48 `taom_wc_*` lines are translated in all 12 languages (`a9437a492`). A correction goes in `tools/translation_overrides/<lang>.json`, and a reworded English line needs its 12 rows reset ([TRANSLATOR_GUIDE.md](../localization/TRANSLATOR_GUIDE.md)).
- **Phase anchors count from the day the chronicle first saw the phase.** A phase that begins late no longer opens every overdue event on one step (decided by the maintainer 2026-10-14; "Open day" above). What remains: raising a phase day, or turning Test Mode off, after the phase began postpones its Pending events; a phase first seen while the chronicle was switched off counts from the day it is switched back on, less one; and switching the chronicle off and on after the phase was seen still opens on one step whatever came due in between.
- **A siege whose target an attacker already holds falls at once.** It pays its Fell outcome on the day it would have opened, with no window to fight over (decided by the maintainer 2026-10-14).
- **Test Mode inverts the lore order.** With Phase 1 before day 20 (Test Mode puts it on day 1), `breaking` (Phase 1 + 0) fires before `fellowship` (day 20) and `khazaddum` (day 26).
- **A siege can hold its window past the close.** While an attacker still besieges the target, the close waits for one more `windowDays` at most, and the children anchored on its resolution (ents, gondor, blackgate) open later.
- **The north event needs Arthedain's towns in TAOM_Map.** Its target, Bree (`town_AN3`), exists only in the live TAOM_Map: the TAOM_Map packaged in `E:\LOTRAOM_Releases\{patreon,public,testing}` had none of Arthedain's towns when checked on 2026-10-14. Played on that package, the event is Skipped at session launch with a logged warning and no effects (`ChronicleService.LaunchChronicle`). The next release must ship a TAOM_Map package that carries them ([release-process.md](../reference/release-process.md)).

## Changelog

- 2026-11-10: the ledger and the analyzer know `ev=pledge`, the line War Pledge writes when a Neutral realm joins a side (#1113).
- 2026-11-13: story offers (#1105 slice 4): a siege's optional `story` block, its fate at the close (the Hornburg holds when no army reaches it), the `sd` save flag, `IChronicleStoryPort` and the MCM switch Offer Story Missions.
- 2026-10-08: first slice (#765): the timed-effect registry with its volunteer and escape consumers, the rally, the war ledger and analyzer, the save payload and four console commands.
- 2026-10-08: the event chain (M3: 13 events, siege windows, adoption, announcements, `taom.chronicle_status`, `taom.chronicle_open`), the army focus (M4) and the `Chronicle` momentum award (M6), with the MCM group `War of the Ring/War Chronicle` (#765).
- 2026-10-09: Codex review fixes (#765, `2ee36e1fa`): the analyzer judges the capital target per kingdom and fails a capital already lost in its first row ("lost by day N"); the rally rebuilds its effects on every refresh.
- 2026-10-14: the maintainer's decisions (#765): phase anchors count from the later of the configured day and the day the phase was first seen (saved as `phases`); a siege whose target an attacker already owns resolves Fell, not Skipped; escape factor, castle feats, rally hint wording and `taom_wc_siege_lost` kept as they are.
- 2026-10-14: no chronicle payout after the War of the Ring ends. The War Ended check sits in the shared callback wrapper, so an owner-change or siege-start callback cannot resolve or pay a window (#765, `8f2c9e458`).
- 2026-10-14: the chronicle's 48 lines are translated into the 12 languages (`a9437a492`).
- 2026-10-14: review follow-ups (#792):
  - both daily listeners run at most once per elapsed campaign day (`WarChronicleDailyGate`, saved as the payload's `daily` section), because a load can repeat the save's day (#793);
  - each game load writes a `t=load` ledger line, and the analyzer rewinds to it;
  - an unreadable clock skips the day and writes no load marker;
  - the chunk loop moved to `SyncDataChunker.SyncChunks` in `Main/Core/Persistence`, shared with momentum.
- 2026-10-14: `taom.chronicle_open` is Tier C: without the literal `confirm` it is a dry run that reports what would happen and changes nothing.

## GitHub Issue

- **Issue:** #765 (closed with `triage-needs-ingame`: the checks under "How to test the chronicle in game" are owed). Review follow-ups: #792 (closed). The repeated daily tick after a load: #793 (open; this feature's two daily listeners are guarded, and TAOM's other daily listeners are not audited yet).
- **Status:** Closed (the Eye of Sauron hunt is in [eye-of-sauron.md](eye-of-sauron.md))

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/army-targeting.md](./army-targeting.md)
- [docs/features/castle-recruitment.md](./castle-recruitment.md)
- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/features/dev-console.md](./dev-console.md)
- [docs/features/troop-progression.md](./troop-progression.md)
- [docs/features/war-of-the-ring-momentum.md](./war-of-the-ring-momentum.md)
- [docs/features/war-of-the-ring.md](./war-of-the-ring.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/engine/campaign-tick-time-and-party-ai.md](../reference/engine/campaign-tick-time-and-party-ai.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->

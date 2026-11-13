# Bond of Hire

## Overview

A clan that signs on as a mercenary now stays at least a minimum number of days (30 by default) before it can leave a kingdom whose ruler is in debt, be hired away by another kingdom, or leave in its daily leave consideration. In the kingdom screen's Clans tab, selecting a mercenary clan shows how long it has agreed to serve: "These mercenaries agreed to serve for at least 30 more days", or "It is not known how long these mercenaries will serve" when less than a day remains.

## Why This Exists

- **Vanilla behavior:** the engine has a saved per-clan date, `Clan.ShouldStayInKingdomUntil`, and three places read it to hold an AI clan in place. It never writes it for a new mercenary: `StartMercenaryServiceAction.ApplyStart` sets the award multiplier, the kingdom and the service flag, and every caller of `ChangeKingdomAction.ApplyByJoinFactionAsMercenary` passes the default date, which `ChangeKingdomAction.ApplyInternal` writes before the mercenary action runs. So a mercenary hired this week can leave the next day, and nothing in the screen says how long a hire lasts.
- **TAOM requirement:** hiring a clan should buy a stretch of service, and the player should be able to see it.
- **Without this feature:** a hired clan can be bartered away or walk out at once, and the Clans tab shows nothing about the contract.

Both pieces ship together: #867 (the minimum) and #868 (the panel line).

## Architecture

### Design Challenge

- **The three readers of the date** (engine v1.5.5), the three departures: a debt departure, a hire away and the daily leave consideration. All skip the player's clan or never reach it:
  - `ClanVariablesCampaignBehavior.cs:404`: in the daily clan tick, a mercenary leaves (a 25 percent roll a day) when the ruling clan's debt to the kingdom is above 10000, only once the date is past, for a clan that is not the player's.
  - `DiplomaticBartersBehavior.cs:117`: a clan weighing a move to another kingdom (a hire away) returns early unless the date is past.
  - `DiplomaticBartersBehavior.cs:147`: the daily consideration of leaving (`ConsiderClanLeaveAsMercenary`, a barterable that must be worth more than 500) runs only once the date is past, for a clan that is not the player's.
  - The field itself is `[SaveableProperty(188)]` on `Clan`, so the date rides in the save with no TAOM data. The other writers are `ChangeKingdomAction.ApplyInternal` (the caller's date on a join, zero on any leave) and the advanced start (`StartGameAsRuler`, `StartGameAsVassal`), on the player's clan only.
- **Where the date can be set.** `ChangeKingdomAction.ApplyInternal` writes the caller's date at `:41`, then, for a mercenary join, calls `StartMercenaryServiceAction.ApplyByDefault` (`:68`), whose `ApplyStart` raises `CampaignEvents.OnMercenaryServiceStartedEvent` (`:23`) after its own writes. The clan-changed event fires later (`:161`). Nothing writes the field after the mercenary event, and vanilla has no other listener of it.
- **Every path that signs a mercenary** (all pass `default(CampaignTime)`): the clan barters (`JoinKingdomAsClanBarterable:124`, which the AI reaches and the player's own defection, persuasion and prisoner lines also reach through `LordDefectionCampaignBehavior` with `isDefecting` true, so those joins as a mercenary are bound too; `MercenaryJoinKingdomBarterable:67`), the player hiring a clan (`LordConversationsCampaignBehavior:2575`), the player signing on (`:2728`, `:3229`, `VassalAndMercenaryOfferCampaignBehavior:270`), the cheat (`CampaignCheats:2139`) and the story mode conspiracy quest (`DefeatTheConspiracyQuestBehavior:344`). All of them raise the event. The advanced-start Mercenary option calls `Clan.StartMercenaryService()` directly and raises none; it is this game's own player clan, which is never bound, so the gap costs nothing.
- **The panel.** `KingdomClanVM` routes every selection through the public `CurrentSelectedClan` setter, which raises `PropertyChangedWithValue`. The first selection happens inside the constructor (`RefreshClanList`) before any mixin exists, and the list is rebuilt when a clan changes kingdom.

### Solution Approach

No Harmony patch, no game model, nothing of TAOM's saved.

**The minimum (#867).** `BondOfHireBehavior` (`Hooks/`) listens to `OnMercenaryServiceStartedEvent` and hands `BondOfHireService.OnServiceStarted` the clan wrapped in `IMercenaryClanAdapter`. The service, in order: stands down on a co-op client that defers to the host (`ShouldDeferToHost`; the dedicated server binds, as it is the authority for AI clans); reads the MCM minimum and stops at 0; skips this game's own player clan; and, when the clan's remaining time is shorter than the minimum, sets the date to the minimum days from now. A longer date is never shortened. The positive requirement `remaining >= days` fails NaN, so a poisoned date is replaced by a real one. The body sits inside a try, because an `MbEvent` invokes its listeners with no try.

**The panel line (#868).** `MercenaryTermMixin` (TAOM.Client, a `BaseViewModelMixin<KingdomClanVM>`) subscribes to `PropertyChangedWithValue` in its constructor, recomputes only for `CurrentSelectedClan`, and unsubscribes in `OnFinalize`; `OnRefresh` covers the constructor's first selection (UIExtenderEx replays `RefreshValues` on the mixin). It converts the selected clan to plain values (mercenary flag, `MercenaryClanAdapter.IsPlayerClan`, remaining days) and asks `ContractTermRules.Describe`: Hidden for a clan that is not a mercenary and for the player's clan, Unknown for under one day, past or non-finite, otherwise Known with the days rounded DOWN, so "at least N more days" is always true. `BondOfHireTexts` turns the result into the string. `MercenaryTermPrefab` inserts one `TextWidget` (a fixed 560 px wide, left aligned, so a long line wraps) as child 1 of the `MembersList` (`InsertType.Child` inserts before the child at `Index`; child 0 is the Members header, child 1 the fixed 440 px grid). The line shows the engine's date, whoever set it, so it is right even with the minimum switched off.

**What it does not bind.** The player's own dialogue (dismissing a mercenary, hiring one away from another kingdom) and expel votes act through other engine paths and ignore the date; the minimum constrains the AI's own departures. Clans already serving when a save loads get the minimum at their next signing; until then the panel says "not known". There is no backfill.

### Component Diagram

```
OnMercenaryServiceStartedEvent (engine, after its own clan writes)
        |
  BondOfHireBehavior (try/catch)
        |
  BondOfHireService  --  IBondOfHireSettingsProvider (MCM minimum)
        |                ICoopSessionProvider
  IMercenaryClanAdapter  ->  Clan.ShouldStayInKingdomUntil (engine, saved)

Kingdom > Clans (select a clan)
        |
  MercenaryTermMixin (KingdomClanVM, PropertyChangedWithValue)
        |  ContractTermRules.Describe  +  BondOfHireTexts
  MercenaryTermPrefab: TextWidget under the Members header
```

## Interactions

- **Alignment desertion.** `AlignmentDesertionService` purges a mercenary clan's troops that oppose its employer. A misaligned contract now lasts at least the minimum, so the purge also runs for that time.

## Configuration

### MCM group "World/Bond of Hire" (GroupOrder 72)

| Setting | Type | Default | Notes |
|---|---|---|---|
| Minimum Mercenary Contract (Days) | integer, 0 to 120 | 30 | 0 turns the minimum off. Read when a clan signs on, so no restart. A hand-edited value is clamped into the range |

The default and the range live in `BondOfHireSettingsProvider`; the MCM hint quotes them and `BondOfHireSettingsProviderTests` pins the quote. The setting changes what AI clans do in the simulation, so it is one of the settings the co-op fingerprint hashes ([coop-interop.md](coop-interop.md)).

### Player text

| Key | English |
|---|---|
| `taom_boh_term_days` | These mercenaries agreed to serve for at least {DAYS} more {?IS_PLURAL}days{?}day{\?} |
| `taom_boh_term_unknown` | It is not known how long these mercenaries will serve |

The subject is the mercenaries, never the player, and no word is gendered. The 12 language files carry the translated text (2026-11-12, #763).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BondOfHire/BondOfHireService.cs` | The minimum: gates, then extend only when shorter |
| `Main/Features/BondOfHire/ContractTermRules.cs` | Pure rules for the panel line (Hidden, Unknown, Known) |
| `Main/Features/BondOfHire/BondOfHireSettingsProvider.cs`, `IBondOfHireSettingsProvider.cs` | The clamped MCM minimum |
| `Main/Features/BondOfHire/Hooks/BondOfHireBehavior.cs` | The one event listener |
| `Main/Features/BondOfHire/BondOfHireModule.cs` | Registrations and the campaign behavior (one line in `FeatureModules.cs`; `IoC.cs` and `SubModule.cs` untouched) |
| `Main/Features/BondOfHire/UI/BondOfHireTexts.cs` | The two lines of player text |
| `Main/Adapters/IMercenaryClanAdapter.cs`, `MercenaryClanAdapter.cs` | `IsPlayerClan`, `RemainingStayDays`, `SetStayUntilDaysFromNow` over a `Clan` |
| `Client/Features/BondOfHire/UI/MercenaryTermMixin.cs` | The Clans panel view-model extension |
| `Client/Features/BondOfHire/UI/MercenaryTermPrefab.cs` | The inserted `TextWidget` |
| `Main/Features/TaomSettings.cs` | The MCM property `BondOfHireMinimumDays` |

## Dependencies

- `ICoopSessionProvider` (CoopInterop): the authority gate, `ShouldDeferToHost`.
- `IModLogger` (kernel): the behavior's and the mixin's fault log.
- UIExtenderEx (TAOM.Client): the mixin and the prefab extension.

## Tests

- `TAOM.Tests/Features/BondOfHire/ContractTermRulesTests.cs`: not a mercenary, the player's clan, negative, 0.5, 1.0, 1.9, 30, NaN, infinities, a huge value.
- `BondOfHireSettingsProviderTests.cs`: the clamp, the fallback, the MCM group (declared once at GroupOrder 72, slider range, hint quoting the default and range and naming the three departures, counted by the co-op fingerprint, gender-neutral, no dash).
- `BondOfHireServiceTests.cs`: shorter extended, longer kept, equal kept, 0 off, the player's clan skipped, a deferring co-op client skipped (the clan never read), a dedicated server bound, a NaN remaining time extended, the setting read at each signing.
- `BondOfHireBehaviorTests.cs`, `BondOfHireWiringTests.cs`: the listener is thin and try-wrapped, the module is listed once, nothing is saved, no Harmony patch, a fresh behavior per campaign, the adapter says "not the player's clan" with no campaign and rejects a null clan.
- `MercenaryTermPrefabTests.cs`: bindings resolve through `PrefabBindingWalker`, no height `StretchToParent`, one inert fixed-width `TextWidget`, the view model declares its own `RefreshValues` and `OnFinalize`, the brush exists and the XPath lands between the Members header and the grid in the installed prefab, whose list has exactly two children (LiveInstall).
- `MercenaryTermMixinTests.cs`: the line follows the selection, hides for a non-mercenary and the player's clan, says "not known" under a day, unsubscribes on finalize, a fault hides it and logs once while later selections still run.
- `BondOfHireTextsTests.cs`, `BondOfHireWordingTests.cs`: the strings, the singular for one day, the rows in all 12 languages.
- `PrefabExtensionBindingTests` (exactly one XPath match) and `ViewModelMixinBindingTests` cover the new extension; `ClientIsolationGateTests` counts the two new UI types; `CoopGateDocCoverageTests` needs the co-op row.

## How to change the minimum

1. In Mod Options, "World/Bond of Hire", move the slider (0 turns it off).
2. It applies to clans that sign on afterwards; a clan already serving keeps the date it has.

## In-game checklist

Not yet confirmed in the running game:

1. Mod Options shows "World/Bond of Hire" with the slider at 30.
2. As a ruler, hire a mercenary clan, then open Kingdom > Clans and select it: "These mercenaries agreed to serve for at least 30 more days" sits under the Members header. The panel rounds down, so it shows 29 once any campaign time has passed after the hire (expect 29 or 30 days on the first look); at one day left it reads "1 more day".
3. With the slider at 0, hire a clan: the line shows "not known" (the engine's date is the unset default). Set the slider to 60 and hire another clan: its line reads 60.
4. Select a vassal clan, the ruling clan and your own clan: no line, and no gap where it would be.
5. Select a mercenary clan an AI barter hired earlier in the game (an old hire): the line shows the date set when it signed, or "not known" for a clan that was already serving when the save was made.
6. A kingdom whose ruler is in debt: a freshly hired mercenary stays for the minimum, then may leave. Watch that an AI kingdom does not hire it away inside the window.
7. At 1080p the line does not overlap the Support and Expel block, the Members grid or the right-hand illustration, and the grid still scrolls.
8. Switching the selected clan quickly, then leaving and re-entering the Clans tab, never leaves a stale line.

## Known limits

- The dedicated server binds AI clans; a co-op client defers to the host. With a co-op mod TAOM cannot probe, or with `coop-force-active.flag`, a solo player defers and gets no minimum.
- Under the installed co-op module, two of the three readers (the hire away and the daily leave consideration) never run, because that module disables `DiplomaticBartersBehavior` on every peer; the debt departure still reads the date on the server. The date is not synced to clients, so a client's panel shows "not known" for clans hired after it joined.
- "Never bound" means this game's own player clan. A remote co-op player's clan looks like an AI clan on the server; that is harmless, since nothing reads the date for it in a session.
- The panel line is computed when a clan is selected or the screen refreshes; it does not tick while the tab stays open.
- The player's own hiring and dismissing dialogue, and expel votes, are not bound.

## Changelog

- 2026-10-24: added (#867, #868).

## GitHub Issue

- **Issues:** [#867 Bond of Hire](https://github.com/haterade22/TAOM/issues/867), [#868 the Clans panel line](https://github.com/haterade22/TAOM/issues/868)
- **Status:** Shipped on `bannerlord-1.5.x` in `c7487f075` (v2.0.35). Closed with `triage-needs-ingame`: the in-game checks are owed.

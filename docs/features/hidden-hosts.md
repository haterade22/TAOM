# Hidden Hosts

## Overview

On the campaign map, another party's troop count is blurred by its distance from the player's main party, measured
against the main party's seeing range: `1??`, `12?`, `???`, and exact once the party is close. Past nine tenths of that
party's own spotting reach (the horizon) the count is `...`, the plate's name is "Unknown party", and the encyclopedia
hotkey opens nothing for it. Wounded and prisoner text shows only when the count is exact. The counts are local UI only: the
AI and the map visibility model are untouched. The one write outside the UI is the Scouting XP phase 3 grants to the main
party's scout (`Hero.AddSkillXp`, saved hero XP), by the authority only.

A masked or horizon party or army tooltip also hides the host's orders ("Orders unknown.", #848, see "Hidden party orders").

Phases 1 (nameplates), 2 (hover tooltips) and 3 (memory) are built. Phase 3 is the memory fade and the `~` symbol (a count
remembered from an earlier sighting), with its own popup text under its own key and saved flag; the Phase 1 popup does not
mention `~`. Tracked in [#846](https://github.com/haterade22/TAOM/issues/846).

Far Watch ([#847](https://github.com/haterade22/TAOM/issues/847), [far-watch.md](far-watch.md)) adds three rules, all through
the service and the `IScoutIntelQuery` seam its store implements (see "Far Watch" below): a scout away on a settlement ride
reads Scouting as 0 for how far digits reach, the six hosts a scout's report names keep their name past the horizon, and the
report raises the digits of every host in sight and stamps a scouted settlement's defenders.

Under the Eaves ([#849](https://github.com/haterade22/TAOM/issues/849), [under-the-eaves.md](under-the-eaves.md)) shortens the main party's sight while it travels through forest (half that with Keen Sight, none inside a settlement). The digit thresholds and the horizon are fractions of `SeeingRange`, so in forest every threshold pulls in by the same share and counts blur closer. No Hidden Hosts code changed.

What the player sees in vanilla, and so here: the nameplate count is always drawn, but the party's name and its wounded
and prisoner text only while the ShowIndicators key (Left Alt) is held. The mask therefore shows on the count at all times
and on the name and extra text only while that key is down.

The encyclopedia is opened by a hotkey, not a click: game key 67 (`MapGoToEncylopedia`), pressed while the cursor is on a
party (`MapScreen.cs:1649-1651`). The keyboard default is unbound; the controller default is Left Option. A left click
engages or escorts as in vanilla and is not touched.

## Why This Exists

- **Vanilla behavior:** every visible party shows its exact healthy troop count, however far away it is; its name and
  wounded and prisoner counts show with the indicators key.
- **TAOM requirement:** scouting should matter. A host seen at the edge of the horizon is a shape on the hill, not a
  muster roll; a closer look, a good scout and a party of your own realm tell you more.
- **Without this feature:** the map gives away the strength of every enemy army at the first sighting.

## Architecture

### Design Challenge

`PartyNameplateVM.RefreshDynamicProperties` writes the bind fields `_countBind`, `_extraInfoTextBind` and `_fullNameBind`
from int caches and a cached name object, never from the field text, and it may run on `TWParallel` workers at 32 or
more plates. `RefreshBinding` runs on the main thread afterwards and is the only reader of those fields (v1.5.5; pinned
by `HiddenHostsBindingTests`): it copies each to its property, and every property setter returns early on an equal value.
A hook that overwrites a field for good would leave the mask in place after the party comes close, because vanilla does
not rewrite the field until its cache says the value changed. `_fullNameBind` in particular is rewritten when the name
changes, on every forced refresh and when a pooled plate is reused (`_latestNameTextObject = null`), not every frame.

### Solution Approach

A void prefix on `PartyNameplateVM.RefreshBinding` (Patch105) lays the mask over the three fields for that one call and
hands vanilla's values back as Harmony's `__state` (`VanillaBinds`); a finalizer on the same method puts them back, even
when the original throws. No mask outlives the call that copies it to the properties, so there is nothing to track
between frames, no per-plate table and no "lift" when the party comes close or the toggle goes off: the next frame simply
lays no mask. A bool prefix on `MobilePartyVisual.OnOpenEncyclopedia` blocks the hotkey at the horizon.

`HiddenHostsService` decides the masks from a `PartyIntelSnapshot` of raw facts:

- **Digits:** the digit count of the party's healthy troops picks a threshold row; digit `k` is known when
  `distance / seeingRange < row[k] * multiplier`. Known digits are always a prefix.
- **Multiplier:** `(1 + 0.12 * Scouting / 50) * (same realm ? 2 : 1)`, capped at 2.25. Scouting is the skill of the main
  party's `EffectiveScout`. Same realm means the party's map faction, or its army leader's, is the main party's.
- **Latch:** a digit already known stays known to 1.1 times its threshold, per party, so a party at the edge does not
  flicker. The latch clears when a party reaches the horizon and for every new campaign.
- **Horizon:** `distance / seeingRange > 0.9 * GetPartySpottingRatioForMainPartySeeingRange(party)`: past nine tenths of
  the party's own spotting reach. A besieger or raider of a settlement the player can see stays visible farther out than
  that ratio says, so for those parties the horizon band is wider than a tenth of the range.
- **Exempt (never masked):** the main party, parties of the player's clan, parties of the player's army, parties whose
  info the engine already hides (`IsInfoHidden`), everything while `Campaign.TrueSight` is on, and any snapshot with
  missing or non-finite data.

The adapter reports the facts (`IsMainParty`, `InPlayerClan`, `InPlayerArmy`, `TrueSight`, `SameFaction`,
`ArmyLeaderSameFaction`); the service composes them into exempt and same realm. The mask text is cached per party by
(count, known digits), so a steady plate allocates nothing.

**Errors.** A fault in a hook shows vanilla's text for that frame (the prefix writes nothing it cannot take back). After
32 faults (`HiddenHostsCalls.FaultBudget`) the hooks stop for the rest of the process and one log line says so; a fault is
logged once per hook and exception type. A missing engine field leaves its bind null: the nameplate hook reports it once
and does nothing. Toggle off: every hook returns at once with no work.

One-time popup: the first campaign tick on a free map with the feature on shows one inquiry explaining `1??` and `...`; the
flag `_taomHiddenHostsHintShown` is saved with the campaign. It waits while the hooks are stopped by the fault budget. A popup that throws counts as shown (saved,
never retried in that campaign); only a throw from the free-map test gives the hint up for the session alone, logged once.

### Component Diagram

```
MobileParty
    |
PartyIntelAdapter (cached spotting ratio, seeing range, scout skill)
    |  PartyIntelSnapshot (raw facts)
HiddenHostsService  --  HiddenHostsMath (thresholds, digits, horizon)
    |  NameplateIntel
Patch105 prefix -> VanillaBinds (__state) -> RefreshBinding -> finalizer restores
```

## Phase 2: hover tooltips

The same distance rule, applied to the tooltips the player opens by hovering (and from the screens that open the same
tooltips): the troop total, the prisoner total, and the per-party and per-side totals of a party, an army, a settlement, a
field battle and a siege read `1??`, `12?` or `...` instead of the exact figure. Phase 2 adds no setting and no player
text: the one toggle covers it, and the only string is the phase 1 "Unknown party".

### What the player sees

| State | Title | Troops value | Prisoners | Formation and troop-type rows | Battle per-troop rows |
|-------|-------|--------------|-----------|-------------------------------|-----------------------|
| Exempt, or the subject's total is read in full | vanilla | vanilla (with wounded) | vanilla | vanilla | vanilla |
| Partly known | vanilla | ` (1??)` | its own digits, such as ` (2?)` | removed | not built |
| Horizon (a visible party or army only) | "Unknown party" | ` (...)` | ` (...)` | removed | not built |
| Vanilla itself passed `isInspected == false` | vanilla | vanilla `???` | vanilla | none | n/a |

The clan and faction rows stay at the horizon (decision Q2): only the title and the counts change. The structure is fixed
when the tooltip is built: rows removed while the party is far stay removed until the next hover or key toggle rebuilds the
tooltip, but the numbers sharpen every two seconds, except a settlement's defenders row and party rows: those are plain
strings, fixed when the tooltip is built. A settlement tooltip's defenders row and the count of each party row inside it are
masked by the settlement's distance; its other rows (food, prosperity, buildings) are untouched.

### Why the periodic refresh has its own bracket

A tooltip's count rows are functions, not strings. `PropertyBasedTooltipVM` evaluates each once when the row is added and then
again from `OnPeriodicRefresh` every two seconds (`_isPeriodicRefreshEnabled`, a two second delay, the first run on the first
tick), driven by `TooltipBaseVM.Tick` from the UI tick. So a bracket on the five builders alone shows the mask for one frame
and vanilla numbers from the next. `OnPeriodicRefresh` has its own bracket (T6), which opens a fresh frame for the subject
the tooltip was invoked with, so each refresh reads the current distance.

### The tooltip frame

The helpers that print a count (`GetPartySizeText`, `GetPartyNameplateText`) take no parameter that names the subject. The
bracket around the call that builds or refreshes the tooltip publishes a `TooltipFrame` (subject kind, a `TooltipVerdict`
of plain numbers) in `HiddenHostsTooltipScope`, and restores the previous frame when it closes.

- **Opens:** the prefix of the T1 to T5 class (subject from `args[0]`) and of T6 (subject from `_invokedArgs[0]`, only when
  `_invokedType` is one of the five). Everything that can throw runs before the frame is published; a fault leaves no frame,
  so that tooltip shows vanilla text.
- **Closes:** the finalizer of the same class, through Harmony's `__state` (`ScopeToken`), always: no toggle or fault-budget
  gate sits in front of it. A stripped prefix leaves the token default and the finalizer does nothing; Harmony rerunning a
  finalizer closes the same previous frame again; the original throwing still closes.
- **Exempt subjects** still push a frame, with the vanilla verdict, so a reader never sees an outer frame through them.
- **Thread:** only the thread that opened a frame sees it; another thread reads null and keeps vanilla text.
- **Nothing is kept:** the frame holds numbers only, no engine object, and lives for the one call that opened it.

### Targets

All are in category `Patch105_HiddenHosts`; none of these phase 2 targets is a bool prefix, so
`CoopVetoClassificationTests` needs no row for them (the phase 1 encyclopedia prefix has one), and each
obeys the phase 1 fault budget and the one toggle.

| # | Target (v1.5.5) | Trigger | Hook |
|---|-----------------|---------|------|
| T1 | `TooltipRefresherCollection.RefreshMobilePartyTooltip` (`:1202`) | Registered for `MobileParty`. Runs from the tooltip's constructor and on every key-toggle rebuild. Opened by the map hover on a party, the map tracker, the settlement and encounter menu party rows, army management, a conversation, and the kingdom army list | prefix, postfix, finalizer (class `TooltipRefresherCollection_Subject_HiddenHosts_Patch`) |
| T2 | `RefreshArmyTooltip` (`:1328`) | Registered for `Army`: the map hover on any party of an army, and the map tracker | same class |
| T3 | `RefreshSettlementTooltip` (`:898`) | Registered for `Settlement`: the map hover on a visible settlement, the game menu and settlement overlay, the encyclopedia, and the clan, kingdom and town management items | same class; its postfix rewrites the defenders row |
| T4 | `RefreshMapEventTooltip` (`:851`) | Registered for `MapEvent`: the map hover on a party or settlement in a battle | same class |
| T5 | `RefreshSiegeEventTooltip` (`:821`) | Registered for `SiegeEvent`: the map hover on a besieging party or a besieged settlement | same class |
| T6 | `PropertyBasedTooltipVM.OnPeriodicRefresh` (protected override, `:88-96`) | `TooltipBaseVM.Tick`, every UI frame, firing every two seconds while a tooltip is active | prefix and finalizer; reads `TooltipBaseVM._invokedType` and `_invokedArgs` |
| T7 | private `TooltipRefresherCollection.AddPartyTroopProperties` (`:2031`) | Only T1, T2, T3 and the encounter tooltip (which opens no frame) | prefix (`out TrimToken`) and postfix (`PropertyBasedTooltipVM, TextObject title, TrimToken`): remove the rows this call added after its title; phase 3 also wraps the troop-total row (see Phase 3) |
| T8 | private `AddEncounterParties(..., MBReadOnlyList<PartyBase>, ..., bool isExtended)` (`:1573`) | Only T5 | prefix with `ref bool isExtended`: false while masked |
| T9 | private `AddEncounterParties(..., MBReadOnlyList<MapEventParty>, ..., bool isExtended)` (`:1774`) | Only T4 | same class as T8 |
| T10 | `Helpers.PartyBaseHelper.GetPartySizeText(int, int, bool)` (`:47`) | Every troop total and count row of T1 to T5 except a settlement's defenders and party rows, inside the call and in the functions the periodic refresh re-runs. The only other callers are the army menu overlay's man-count hint (`GetArmyManCountTooltip`) and `EncounterMenuOverlayVM`, which open no frame; the party screen is not a caller | postfix, reads the frame |
| T11 | `CampaignUIHelper.GetPartyNameplateText(MobileParty, bool)` | The settlement tooltip's party rows and the army menu overlay (no frame) | postfix, acts only inside a settlement frame |
| T19 | `MobileParty.GetBehaviorText()` (public, instance, non-virtual) | The orders row of T1 and T2 through `CampaignUIHelper.GetMobilePartyBehaviorText`; the menu rows and patrol tooltips call it with no frame (#848) | postfix, acts only inside a masked or horizon party or army frame |

Notes on single targets:

- **T7 trims, it does not pass `isInspected = false`.** Forcing `false` would make vanilla's title call print its own `???`,
  and T10 could no longer tell its own `false` from vanilla's. The rule is: hide rows, never change what a count call
  receives. The `TrimToken` carries `IsSet` because a bare index of 0 with a stripped prefix would delete the tooltip from
  row 2 on. Rows are only appended (pinned), so the trim never touches rows added before the call.
- **T10 leaves vanilla's own fog alone.** A call made with `isInspected == false` already prints `???`; Hidden Hosts never
  shows more than vanilla. Otherwise it prints the known leading digits and `?` (no wounded part, as on the nameplate), `...`
  at the horizon, or keeps vanilla's text when that number's every digit is in reach. Its first parameter is spelled
  `healtyNumber` in the engine; Harmony binds by name, and the overload is pinned by its parameter types because a
  `PartyBase` overload exists.
- **T10 and the JIT.** The helper (188 bytes of IL) could in principle be inlined into a caller compiled before the patch. The pair test
  runs the vanilla builder once to compile it, applies the patch, and runs it again: the masked text reaches the title row.
  That shows it for the test host's JIT; the in-game process is covered only by the in-game check below.
- **T11 is frame-gated** so the army menu overlay (a party in plain sight) stays exact. It masks the number vanilla printed
  (`includeAttachedParties` is a postfix parameter): the party's own roster (`OwnHealthyCount`) when it is false, which is the
  settlement tooltip's call, and the army total only when it is true. An earlier build always used the army total, so a
  45-man leader of a 345-man army would have read `???`, a false magnitude; `Row_SettlementFrame_ArmyLeader_...` and
  `MaskSettlementPartyRow_ALeaderOfAnArmy_...` pin the fix.
- **T3's defenders row** is found by the text of the game id vanilla passed (`str_map_tooltip_defenders`), read
  once per build, not cached (unlike "Unknown party"); its value is a plain string, so the periodic refresh never rewrites it. The title of a party
  or army is found by position (row 0) and the `Title` flag, never by its text.

### Subjects and exemptions

| Tooltip | Distance from | Count the verdict uses | Same-realm factor | Exempt (whole frame vanilla) | Horizon |
|---------|---------------|------------------------|-------------------|------------------------------|---------|
| Party | the party | its healthy count, as the nameplate; an army leader's party frame uses its own roster, no latch | its or its army leader's faction is the main party's | main party, player clan, player army, `IsInfoHidden`, true sight, in plain sight | only when the party is drawn on the map: past nine tenths of its spotting reach. A subject out of sight (tracker, menus) has none: it keeps its name and shows its digits |
| Army | the leader | leader plus attached | the leader's faction | as party, judged on the leader | the leader's |
| Settlement | its gate (vanilla inspects a settlement by its gate) | the defenders vanilla prints, mirrored by the adapter | the settlement's faction | the main party's current settlement, owned by the player's clan, a player-clan hero inside, true sight | none: a settlement never renames |
| Battle | its position | every involved non-settlement party's healthy troops, a hidden party left out as vanilla does | either side's leader faction | the player is involved, a player-clan or player-army party is involved, true sight | none |
| Siege | the besieged gate | both sides' non-settlement parties, hidden ones counted as vanilla does | the besieger or besieged faction | the main party's siege or settlement, a player-clan or army party, true sight | none |

**In plain sight:** the party is in the main party's current settlement, is the party the player is encountering, is in the
main party's map event, is in the player's own siege on either side, or is the party the player is talking to. The siege is the
engine's `PlayerSiege.PlayerSiegeEvent` (the main party's, else the one of the settlement it is in): a party is in it when its
`SiegeEvent` is that one (the besieger camp) or it is inside the besieged settlement, because `MobileParty.SiegeEvent` alone
is `BesiegerCamp?.SiegeEvent` and null for a defender (pinned). It is adjacent and already exact by distance, and
the settlement and encounter menus that list such parties must not blur. It also exempts the nameplate (phase 1) and the
encyclopedia hotkey.

The subject's own total in a tooltip equals its nameplate: the party verdict shares the nameplate's per-party latch, so a
digit the plate holds is held in the tooltip too (an army leader's party frame is the exception: its own roster, no latch). Other numbers (prisoners, side totals) get the same reach with their own
digit count.

**Decision Q1:** tooltips opened from the map tracker, army management, the kingdom and clan screens and the encyclopedia
are masked by the same rule (one rule everywhere); the plain-sight exemption still applies. Those screens' own count rows
stay exact in this phase (see below).

## Phase 3: memory

A count the player has read stays in the player's head. Reading a party's count, on its nameplate or in its tooltip, records
it. When the same party is later too far for those digits, the display falls back to the remembered count marked with a tilde
(`~15?`), and the remembered digits fade as the hours pass. Hosts the player meets, talks to or shares a settlement with are
counted exactly. Reading an enemy host's count from afar trains the main party's scout. Local UI and the scout's XP only;
the AI and the visibility model are untouched. Phase 3 adds no setting: the one toggle covers it.

### What the player sees

| State | Nameplate | Troop total in a party or army tooltip | Settlement defenders row | Reason row |
|-------|-----------|----------------------------------------|--------------------------|------------|
| Live reading as good as the memory (or better) | live (`12?`) | live | live | none |
| Memory beats the live reading | `~123` or `~12?` | ` (~12?)` (no wounded part) | `~245` | "Counted 5 hours ago" |
| Horizon (a visible party or army) | `...`, memory ignored (decision Q5) | ` (...)` | n/a | none |
| Nothing remembered | the phase 1 and 2 mask | the mask | the mask | none |

Only the subject's own troop total shows the memory. A masked party or army total row is always wrapped, with or without a
memory yet, so a memory that appears while the tooltip stays open (the player rides away) shows at the next two-second refresh,
as the nameplate already does; the "Counted" row, though, is added only at build time and only when a "~" really reached the
tooltip, so a hideout or an extended settlement view with no defenders row shows neither the row nor a popup trigger. The prisoner total, formation rows and a battle's or siege's numbers stay
live readings, and battles and sieges are never remembered. A party tooltip of an army leader lists the leader's own roster
while its remembered count is the army's total, so it carries no memory (the army tooltip and the nameplate do). The reason
row is a row of the tooltip: its function reads the frame that is current at each two-second refresh, so the age moves on and
the row goes blank once the player is close enough to read the count again. The age text is vanilla's hours-and-days text
(`CampaignUIHelper.GetHoursAndDaysTextFromHourValue`, v1.5.5: "5 hours", "1 day and 3 hours"), at least one hour (decision Q4).

From v1.5.6 the five subject builders end with vanilla's `AddTooltipHintProperties` (a spacer and the extend-key hint, each
only when a hint applies, and a parley hint when `CanMainHeroDoParleyWithParty` is true) in place of the
inline hint rows. The parley hint appears only on party and settlement tooltips: the army, map-event
and siege builders pass no parley party to `AddTooltipHintProperties`. The reason row is still appended
by the postfix after the original returns, so it stays the last row; on a party tooltip it can now follow the parley hint,
so a masked party whose name is visible shows that hint just above "Counted ... ago". The title, defenders and troop rows
the patches rewrite sit before the hint tail and are unchanged.

### Rules

| Event | Effect |
|-------|--------|
| A live reading of a non-exempt party or settlement | An existing row is overwritten when the reading shows at least as many digits as the row, or the count is read in full, or the reading contradicts the row (`HiddenHostsMath.Agrees` is false: another digit count or other leading digits), so the live view wins over a stale count even when no digit is readable ("~123" over a 7-man host becomes "?", a 300-man army read with one digit replaces a remembered 80). A worse reading that agrees keeps the row ("~150" stays over "15?"). A reading with no digit never makes a new row. The clock is fresh while the subject is visible (a hovered settlement counts as in sight), else stamp plus the hold |
| Display | The row holds more known digits than this reading shows: `~` plus the row's count with its remembered digits. Otherwise the live text. The horizon ignores the memory. Without a usable clock (hour -1) nothing is recorded or consulted |
| Exact stamp (`GameMenuOpened`, unthrottled, and the campaign tick at most every half second of real time; toggle on) | Every digit of each party in plain sight that is not the player's own side or hidden (the service's one predicate, shared with the mask): in the main party's current settlement, encountered, talked to, in its map event or in the player's own siege on either side (decision Q7), and the defenders of the settlement the player is in. The menu event is what stamps a town, castle, village, encounter or conversation visit: a plain game menu stops campaign time and `Campaign.Tick` raises the tick event only for a positive `dt` (pinned). Parties inside a settlement restart their clock at every stamp. No XP (decision Q3) |
| `PartyVisibilityChangedEvent` | Back in sight: the row is fresh. Out of sight: the clock is stamp plus the hold, which may already be past (the next hourly tick then drops a digit). Runs with the toggle off |
| Hourly tick | Settlement rows ask the adapter: gone, remove; inspected, fresh; else, if fresh, the clock starts at stamp plus the hold. Then every row whose clock is due loses one digit and schedules the next loss one step ahead, so a long gap costs one digit per tick. A row with no digit left and no Scouting latch for today is removed. Runs with the toggle off |
| `MobilePartyDestroyed` | The party's row is removed (`DestroyPartyAction` raises it before `RemoveParty`, pinned) |
| New campaign (`HiddenHostsMemoryBehavior` constructor) and `LoadFrom` | Both clear the latch, the text caches, the rows, the pending XP and the drawn flag; `LoadFrom` then decodes |

Information ages from when it was counted, not from when it left sight. A party that stays in sight but out of counting range
does not fade (it is still in sight). "Out of sight" for a party is the engine's `MobileParty.IsVisible`, observed through the
event; for a settlement it is `Settlement.IsInspected == false`, polled hourly through `Settlement.Find` (a dictionary lookup).
There is no `MobileParty.All` scan and no per-row party lookup.

| Constant | Value |
|----------|-------|
| Hold | `6 x (1 + Scouting / 150)` hours, rounded up, Scouting clamped to 0 to 1000: 6 at skill 0, 12 at 150, 18 at 300, 46 at 1000 |
| Fade step | `8 x (1 + Scouting / 300)` hours, rounded up: 8, 12, 16, 35 |
| Rows kept | 4096 parties, 2048 settlements. At the cap an insert evicts the stale row with the oldest stamp; if every row is fresh the insert is skipped and one warning per session is logged |

Both are integer arithmetic (`HiddenHostsMath.HoldHours`, `FadeStepHours`), so no float is cast and every row is exact. The
scout is the main party's `EffectiveScout`'s Scouting, read when the clock is set or ticked.

### Scouting XP

A live reading of a party at war with the player (`FactionManager.IsAtWarAgainstFaction`) pays raw Scouting XP the first time
it reaches each digit stage on a campaign day: stage 1 (one digit known) 20, stage 2 30, stage 3 (three or more) 50. A jump pays
every stage it passes, so a one-digit party tops out at 20, a two-digit one at 50 and a party of 100 or more at 100 per day. The
latch (`XpStage`, `XpDay`) lives in the row and is saved, so a reload cannot replay the reads it took back. Stamps, memory
displays, exempt parties, settlements and battles pay nothing (decision Q3).

The hooks only add to a pending total in the service. `HiddenHostsMemoryBehavior.OnTick` takes it every tick, and again just
before a save (`OnBeforeSaveEvent`, raised by `SaveHandler.SaveTick` before it starts writing; a save made while time is stopped
has no tick), so the saved latch and the saved XP agree, and calls
`Hero.AddSkillXp(DefaultSkills.Scouting, xp)` on the main party's effective scout (focus and learning rate apply, as for vanilla's
own Scouting XP) only with the toggle on, `ICoopSessionProvider.IsAuthority` true and
`CoopSessionPolicy.MayWriteSaveBackedState(IsCoopClient)` true; otherwise the XP is dropped, so a client cannot bank it. A
dedicated server draws no UI, so it never earns any.

### The save

The rows ride the behavior's `SyncData` as compact text through `SyncDataChunker` (count key `_taomHiddenHostsMemoryCount`, chunk
keys `_taomHiddenHostsMemory_<n>`); no `SaveableTypeDefiner`. Rows with nothing left to remember and no latch for today are
dropped before encoding. `HiddenHostsMemoryCodec` is pure:

```
HH1
p|<id>|<count>|<known>|<stampHour>|<fadeHour>|<xpStage>|<xpDay>
s|<id>|...
```

- **Escape (id only):** `%` to `%25`, `|` to `%7C`, newline to `%0A`, carriage return to `%0D`; everything else, non-ASCII included,
  passes through. The decoder accepts exactly those four sequences; any other `%` makes the row invalid.
- **Numbers:** digits only (`NumberStyles.None`); `fadeHour` and `xpDay` also accept exactly `-1`.
- **Valid row:** kind `p` or `s`; id of 1 to 256 characters; `0 <= count <= 1,000,000`; `0 <= known <= digits(count)`;
  `stampHour >= 0`; `fadeHour >= -1`; `0 <= xpStage <= 3`; `xpDay >= -1`. An invalid row is skipped and counted; one warning per load
  reports the count. A duplicate id keeps the last. The encoder skips a row the decoder would refuse.
- **Header:** a missing or different header (a future or foreign format) loads nothing, with one warning. An empty payload is an
  empty store, no warning. Decoding stops at the table cap, with one warning.
- **Size:** the worst case (4096 plus 2048 rows of long ids and maximal numbers) stays under 400 KiB, about 40 chunks of 10,000
  characters, each under the engine's 32,763-byte entry limit even for three-byte characters (pinned).
- **Faults:** an encode fault saves zero chunks and logs; a decode fault starts empty and logs.

### Co-op

The rows are per-player presentation that each peer keeps in its own `SyncData` (coop-interop.md "What is NOT done"), so writing
them is not gated. Only the XP grant is. See [coop-interop.md](coop-interop.md).

### Popup

The memory hint shows once, under its own key (`taom_hhost_memory_hint_*`) and its own saved flag
(`_taomHiddenHostsMemoryHintShown`), the first time a `~` count has actually been drawn this session (a nameplate, a tooltip total or
a defenders row), after the phase 1 hint has been shown (never on the same tick) and on a free map. The failure rules are
phase 1's. Its text describes only what this build shows: a remembered count, digits lost with the hours (slower with a better
scout), the hover age, exact counts for hosts met, and the Scouting XP; the tilde example comes from the code as `{MEMORY_EXAMPLE}`.

### Behavior layout

`HiddenHostsMemoryBehavior` (built with `new` in the module's declaration, like `HiddenHostsBehavior`, so its constructor is a
per-campaign reset) registers five events: `TickEvent` (the XP grant), `OnBeforeSaveEvent` (the same grant), `HourlyTickEvent`
(the fade), `PartyVisibilityChangedEvent` and `MobilePartyDestroyed`. `HiddenHostsPlainSightBehavior` (also built with `new`; it
holds no campaign state) registers `GameMenuOpened` and `TickEvent` for the exact stamps and keeps the plain-sight collect,
the throttle and the fault log out of the memory behavior. A handler that throws logs once per kind and carries on.

**Threads.** `MapState.OnMapModeTick` calls `Campaign.RealTick` (which runs `UpdateVisibilitiesAroundMainParty`, the sequential
loop after the parallel party passes, `CampaignTickCacheDataStore.cs:334` and `:401` in v1.5.5, and so raises the visibility
event), then the UI handler's tick, then `Campaign.Tick` (the tick event): one method, one thread. `SaveHandler.SaveTick` runs
from `MapState.OnTick` too. So every writer of the memory and every nameplate and tooltip reader is on the main thread. The
design's owner-thread check on the memory writes (risk R8) was therefore not built: it would guard a thread that cannot occur,
and a wrong guess about the loading thread would silently drop every write.

### Component diagram

```
CampaignEvents: Tick, OnBeforeSave, HourlyTick, PartyVisibilityChanged, MobilePartyDestroyed, GameMenuOpened
    |
HiddenHostsMemoryBehavior -- IHiddenHostsCampaignAdapter (clock, scout, sight poll, XP grant)
HiddenHostsPlainSightBehavior -- IPartyIntelAdapter.CollectPlainSight (light snapshots in plain sight)
    |  ids, ints, hours
HiddenHostsService -- HiddenHostsMemory (rows) -- HiddenHostsMemoryCodec (save text)
    ^  Evaluate, EvaluateTooltip* record and read it
the nameplate and tooltip hooks (T7 wraps the total row, T1 to T5 add the reason row)
```

## Configuration

MCM `Map UI/Hidden Hosts`: "Hide Exact Troop Counts" (`HiddenHostsEnabled`), default on, no restart, applies at once. The
thresholds are constants in `HiddenHostsMath`.

### Thresholds

Digit thresholds are fractions of the main party's seeing range, left digit first, by the digit count of the party's
healthy troops (a count of 0 is one digit). Six or more digits use the five-digit row; extra digits take its last value.

| Digits | Digit 1 | Digit 2 | Digit 3 | Digit 4 | Digit 5 |
|--------|---------|---------|---------|---------|---------|
| 1 | 0.28 | | | | |
| 2 | 0.50 | 0.26 | | | |
| 3 | 0.62 | 0.32 | 0.20 | | |
| 4 | 0.70 | 0.45 | 0.28 | 0.16 | |
| 5 or more | 0.75 | 0.50 | 0.32 | 0.20 | 0.14 |

| Constant | Value | Meaning |
|----------|-------|---------|
| Scouting bonus | +0.12 per 50 skill | Multiplies every threshold |
| Same-realm factor | x2 | Party or its army leader in the main party's faction |
| `MaxScoutMultiplier` | 2.25 | Cap on the product of the two |
| `Hysteresis` | 1.1 | A known digit holds to 1.1 times its threshold |
| `HorizonFraction` | 0.9 | Horizon starts past this share of the party's spotting reach |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HiddenHosts/HiddenHostsMath.cs` | Threshold rows, scout multiplier, known digits, horizon, mask format |
| `Main/Features/HiddenHosts/HiddenHostsService.cs` | The decision per snapshot, the latch and the string cache; `FogSkill` and `IsNameHidden` (Far Watch) |
| `Main/Features/HiddenHosts/NameplateIntel.cs` | What a plate shows: masked, count text, horizon |
| `Main/Features/HiddenHosts/VanillaBinds.cs` | Lays the mask over the three bind fields and restores vanilla's values (`__state`) |
| `Main/Features/HiddenHosts/Hooks/PartyNameplateVM_RefreshBinding_HiddenHosts_Patch.cs` | Patch105 prefix and finalizer on `RefreshBinding` |
| `Main/Features/HiddenHosts/Hooks/MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch.cs` | Patch105 bool prefix on the encyclopedia hotkey |
| `Main/Features/HiddenHosts/HiddenHostsService.Tooltips.cs` | Phase 2: the tooltip verdicts and the number texts |
| `Main/Features/HiddenHosts/TooltipVerdict.cs`, `TooltipFrame.cs`, `ScopeToken.cs`, `TrimToken.cs`, `HiddenHostsTooltipScope.cs` | Phase 2: the verdict, the frame, the Harmony `__state` tokens and the open frame |
| `Main/Features/HiddenHosts/Hooks/HiddenHostsTooltipFrames.cs` | Phase 2: subject to open frame (adapter read plus verdict) |
| `Main/Features/HiddenHosts/Hooks/TooltipRefresherCollection_Subject_HiddenHosts_Patch.cs` | T1 to T5: the bracket, the title rename, the defenders row |
| `Main/Features/HiddenHosts/Hooks/PropertyBasedTooltipVM_OnPeriodicRefresh_HiddenHosts_Patch.cs` | T6: the two-second refresh bracket |
| `Main/Features/HiddenHosts/Hooks/TooltipRefresherCollection_AddPartyTroopProperties_HiddenHosts_Patch.cs` | T7: trims the rows below a masked title |
| `Main/Features/HiddenHosts/Hooks/TooltipRefresherCollection_AddEncounterParties_HiddenHosts_Patch.cs` | T8 and T9: no per-troop rows while masked |
| `Main/Features/HiddenHosts/Hooks/PartyBaseHelper_GetPartySizeText_HiddenHosts_Patch.cs` | T10: the count text |
| `Main/Features/HiddenHosts/Hooks/CampaignUIHelper_GetPartyNameplateText_HiddenHosts_Patch.cs` | T11: a settlement tooltip's party rows |
| `Main/Features/HiddenHosts/Hooks/MobileParty_GetBehaviorText_HiddenHosts_Patch.cs` | T19 (#848): "Orders unknown." in a masked or horizon party or army tooltip |
| `Main/Features/HiddenHosts/Hooks/DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch.cs` | #1037: postfix, a masked host's field parley is refused and vanilla's nameless fallback line applies |
| `Main/Adapters/SettlementIntelSnapshot.cs`, `EventIntelSnapshot.cs` | Phase 2: raw facts about a settlement, a battle or a siege |
| `Main/Features/HiddenHosts/HiddenHostsService.Memory.cs` | Phase 3: record, stamps, fade, pending XP, save text, reset |
| `Main/Features/HiddenHosts/HiddenHostsMemory.cs`, `MemoryRow.cs` | Phase 3: the rows and the record, visibility, fade and cap rules |
| `Main/Features/HiddenHosts/HiddenHostsMemoryCodec.cs` | Phase 3: the pure save text |
| `Main/Features/HiddenHosts/HiddenHostsMemoryBehavior.cs` | Phase 3: the five campaign events, the XP grant (tick and before a save) and its co-op gate, `SyncData` |
| `Main/Adapters/IHiddenHostsCampaignAdapter.cs`, `HiddenHostsCampaignAdapter.cs`, `SettlementDefenders.cs` | Phase 3: clock, scout, settlement sight, visibility change, XP grant; the defenders sum both snapshots share. Plain sight is `IPartyIntelAdapter.CollectPlainSight`, next to the one definition of it |
| `Main/Features/HiddenHosts/HiddenHostsCalls.cs` | Statics the hooks reach, the fault budget |
| `Main/Features/HiddenHosts/HiddenHostsTexts.cs` | "Unknown party", "Orders unknown.", the defenders and troops labels, "Counted ... ago" |
| `Main/Features/HiddenHosts/HiddenHostsBehavior.cs` | Per-campaign reset, the two one-time popups, their two saved flags |
| `Main/Features/HiddenHosts/HiddenHostsPlainSightBehavior.cs` | Phase 3: the exact stamps, on every game menu and on a real-time throttled tick |
| `Main/Features/HiddenHosts/HiddenHostsModule.cs` | Module wiring, service registration, patch category, statics |
| `Main/Features/HiddenHosts/IHiddenHostsSettings.cs`, `HiddenHostsSettingsProvider.cs` | The MCM toggle, read through a cached settings object |
| `Main/Features/TaomSettings.cs` | The `HiddenHostsEnabled` property |
| `Main/Adapters/IPartyIntelAdapter.cs`, `PartyIntelAdapter.cs`, `PartyIntelSnapshot.cs` | `MobileParty` to raw facts |
| `Main/Adapters/IHiddenHostsHintAdapter.cs`, `HiddenHostsHintAdapter.cs` | The popup and the free-map test |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_hhost_*` keys |
| `docs/reference/harmony-patch-registry.md` | `Patch105_HiddenHosts` entry |
| `docs/reference/taleworlds-api-snapshot/reflection-sites.md` | The six reflected fields: four on `PartyNameplateVM`, two on `TooltipBaseVM` |

## Dependencies

- `IHiddenHostsSettings` (HiddenHosts) over MCM `TaomSettings`.
- `IPartyIntelAdapter` (Adapters) over the party, settlement, battle and siege, faction, skill and `MapVisibilityModel` data.
- `IHiddenHostsHintAdapter` (Adapters) over `InformationManager.ShowInquiry`.
- `IHiddenHostsCampaignAdapter` (Adapters) over `CampaignTime`, the main party's scout, the sight poll, `Settlement.Find` and `Hero.AddSkillXp`; the plain-sight reads of `PlayerEncounter`, the map event and the siege are `IPartyIntelAdapter.CollectPlainSight`.
- `ICoopSessionProvider` and `CoopSessionPolicy` (CoopInterop) for the XP gate; `SyncDataChunker` (Core.Persistence) for the save.
- `IScoutIntelQuery` (Core.Domain), implemented by Far Watch's store: whether a scout is away and which hosts he named. Hidden Hosts holds the seam, not the feature.

## Tests

- `TAOM.Tests/Features/HiddenHosts/HiddenHostsMathTests.cs`: every threshold row boundary, the cap, hysteresis, count 0, six digits, mask format and a NaN row per gate.
- `HiddenHostsServiceTests.cs`: the gates, one row per exempt and same-realm fact, the horizon, the Scouting reach, the latch, the string instance cache, the party cap, the infinite-ratio gate and reset.
- `VanillaBindsTests.cs`: mask, restore, nulls, the unset no-op.
- `HiddenHostsHookTests.cs`: the prefixes and the finalizer run against bare engine objects (`FormatterServices.GetUninitializedObject`): mask then restore, horizon rename, toggle off, invisible plate, faults and the fault budget, and the patch classes applied and JIT-compiled against the installed engine.
- `HiddenHostsBindingTests.cs`: the engine facts the finalizer design rests on (the only reader of the three fields, the three setters).
- `HiddenHostsFaultBudgetTests.cs`, `HiddenHostsWiringTests.cs`, `HiddenHostsBehaviorSessionResetTests.cs`, `HiddenHostsSettingsProviderTests.cs`, `PartyIntelAdapterTests.cs`: the budget, the module and the per-campaign reset, the saved flag, the toggle, and that the adapter keeps no campaign object.

Phase 2:

- `HiddenHostsTooltipServiceTests.cs`: every verdict (party, army, settlement, battle, siege), one row per exemption and per float gate, the horizon only for a visible party, the latch shared with the nameplate, and the number texts (`MaskTooltipCount` equals the nameplate for the subject total).
- `HiddenHostsTooltipScopeTests.cs`: open and close, nesting restores the outer frame, a double close is idempotent, an unset token does nothing, another thread sees null.
- `HiddenHostsTooltipHookTests.cs`: every branch of the number helpers, the trim, the encounter prefix, the title and defenders postfix, and the frame opening, against bare engine objects.
- `HiddenHostsTooltipHarmonyPairTests.cs`: the T1 to T5 and T6 brackets through real Harmony (frame visible inside the original, closed after a normal return and after the original throws, the prefix stripped, a later finalizer that throws, nesting), the T7 pair, a vanilla caller of `GetPartySizeText` compiled before the patch, and every patch class applied and JIT-compiled.
- `HiddenHostsTooltipBindingTests.cs`: the engine facts (the periodic refresh, the only callers of the row evaluator and of the helpers, the builders' shapes, the registration, the parameter names).

Phase 3:

- `HiddenHostsMemoryMathTests.cs`: the hold and the fade step at the Scouting levels the design names and at the clamps, the whole-hour conversion for NaN, infinity, negatives and past int, the XP table, the tilde text.
- `HiddenHostsMemoryTests.cs`: the store: record rules (new, better, equal, worse, read in full), the clock, the cap and its eviction, visibility, the hourly fade at exact hours for the three Scouting levels, settlement sight, pruning.
- `HiddenHostsMemoryCodecTests.cs`: round trip, every escape, an id with every reserved character, foreign and missing headers, each invalid field, duplicates, the cap, chunk sizes under the engine limit for non-ASCII ids, a payload over one chunk, the worst case size.
- `HiddenHostsMemoryServiceTests.cs`: the display (memory against live, the horizon, exempt parties, no clock), the fade by the hour, stamps, the tooltip verdicts, XP once per party per stage per day and only at war and only from live readings, save and load, reset.
- `HiddenHostsMemoryBehaviorTests.cs`: the XP grant for every peer role (the co-op gate) from the tick and before a save, the hourly fade, visibility, destroy, `SyncData` round trip, a foreign payload, a fault on save.
- `HiddenHostsPlainSightBehaviorTests.cs`: the menu event stamps without a tick, is unthrottled and restarts the throttle; the tick is throttled in real time (a fake clock), not by the frame time; toggle off; a fault is reported once and a faulted tick does not retry every frame; the service's predicate decides who is remembered.
- `HiddenHostsTooltipFrameTests.cs`: the frame's decisions (rename, wrap, trim, defenders, reason row) over every kind and mode as plain code. `HiddenHostsTooltipScopeTests.cs` gains the interleaved two-thread test of the thread-local slot.
- `HiddenHostsMemoryBindingTests.cs`: the engine facts (the visibility setter raises the event only on a change, the destroy order, `Settlement.Find`, `Hero.AddSkillXp`, the troops game text id, the `_dt > 0f` gate in `Campaign.Tick`, `GameMenu.RunOnInit` raising `GameMenuOpened`, `SaveHandler.SaveTick` raising before-save before it writes, `MobileParty.SiegeEvent` being the camp's event, `PlayerSiege.PlayerSiegeEvent`'s fallback).
- `HiddenHostsTooltipHookTests.cs` and `HiddenHostsTooltipHarmonyPairTests.cs` (extended): the troop-total wrapper (only the troops title, only a masked party or army, with a memory or not, flag cleared in a finally), the reason row, the total through the vanilla builder and across the two-second refresh via real Harmony.
- `HiddenHostsScoutSeamTests.cs` (Far Watch, #847; `NoScoutIntel` is the test double every other Hidden Hosts test builds the service with): `FogSkill` at all five sites, the named-horizon plate and tooltip frame, `RaiseByScout` (digit stages, the horizon, a remembered count that disagrees with the host, the toggle, the clock, the own side), `RecordScoutedSettlement`, and a steady horizon read with the real Far Watch store that allocates nothing. The named-horizon case also runs through real Harmony in `HiddenHostsHarmonyPairTests`, `HiddenHostsHookTests` and `HiddenHostsTooltipHookTests`, and `HiddenHostsWiringTests` loads the game assemblies before it scans the patch classes.
- #848 (orders): `HiddenHostsTooltipFrameTests.cs` (`HidesOrders` over every kind and mode, with and without a memory; a named horizon host). `HiddenHostsTooltipHookTests.cs` (hand calls of the postfix): masked and horizon party or army frames and a named horizon party replace the text; no frame, a vanilla, settlement or map event frame and a spent budget keep the original; the real frame opener far, near and with the toggle off. `HiddenHostsTooltipHarmonyPairTests.cs` (`RequiresGame`, a probe prefix standing in for the engine's branch chain on a bare party): through real Harmony a masked frame replaces the original with the shipped text, and no frame or a vanilla frame keeps it; the wrapper reaches the patch from a caller compiled before and after it. `HiddenHostsTooltipBindingTests.cs` (`BindingVerification`, `RequiresGameIL`): `GetBehaviorText`'s shape, the two builders each print it through exactly one wrapper call as a plain string row, and the exact caller sets of both methods, with the SandBox UI assemblies scanned. `HiddenHostsWiringTests.cs` lists the new class in its category pin. The postfix's catch has no test: its try body only constructs a `TextObject`, which cannot throw at run time (the rethrow mutant survives as an equivalent one).
- `HiddenHostsBehaviorSessionResetTests.cs` (extended): the memory hint, its own flag and key, never on the tick the first hint shows. `HiddenHostsWiringTests.cs`: the memory behavior is built with `new` in the declaration, registers exactly five events (the plain-sight behavior, two: `GameMenuOpened` and `TickEvent`) and resets in its constructor.

`PartyIntelAdapter`, `HiddenHostsCampaignAdapter` and `HiddenHostsHintAdapter` need a live campaign, so the in-game check below is their coverage (`PartyIntelAdapterTests` pins that neither adapter keeps a campaign object).

## How to retune the thresholds

1. Edit the `Rows` table or the constants (`HiddenHostsMath.cs`: `Hysteresis`, `HorizonFraction`, `MaxScoutMultiplier`, the 0.12 per 50 skill and the same-realm factor in `ScoutMultiplier`).
2. Update `HiddenHostsMathTests` (each row is pinned just inside and just outside) and the table above.
3. Rebuild and restart the game; there are no sliders yet, so nothing is read at run time.

## Performance

The prefix and finalizer run once per visible party nameplate per map frame. Plates that are not on the map, and every
call while the toggle is off or the hooks are stopped, return before any work. For an evaluated plate the cost is two
dictionary lookups (the adapter's spotting cache and the service's party state), a second `GetPartyHealthyCount` (vanilla
computes it too; it loops the attached parties for an army leader), a distance, and the cached reads: the main party's
seeing range and Scouting skill and each party's spotting ratio (a terrain lookup) are kept for half a second of real
time in `PartyIntelAdapter`. The mask text is cached per party, so a steady plate allocates nothing.

Memory (phase 3): a nameplate read adds one dictionary lookup in the memory and, when the reading has a digit, a few field
writes; it allocates nothing in steady state (pinned by an allocated-bytes test). The adapter reads the clock live (one property
read) and caches the war flag with the spotting ratio for half a second. The memory behavior's tick does a take-and-zero of the
pending XP every frame. The plain-sight behavior stamps on each game menu and at most every half second of real time, reading
a light snapshot per occupant (counts, flags, clock, scout: no spotting-ratio terrain query and no war check, which would miss
their half-second cache for every occupant). The visibility event does one dictionary lookup per party flip. The hourly tick
walks the rows once.

Tooltips: a tooltip build or refresh costs one adapter read and one verdict per open and per two seconds, nothing per frame;
a settlement build adds one party read per row inside it.
The T6 prefix is two reference reads and a type comparison per periodic refresh; T10 and T11 read one static and return when
no frame is open, which is every call outside a map tooltip.

## Far Watch (#847)

The scout of [far-watch.md](far-watch.md) changes Hidden Hosts in four places. `HiddenHostsService` now takes an
`IScoutIntelQuery` (`IsScoutAway`, `IsNamedByScout(partyId, nowHour)`); the Hidden Hosts tests build it with `NoScoutIntel`, a
test double that is never shipped.

- **Scout away: reach only.** `FogSkill` reads Scouting as 0 while a settlement ride runs, at the five `ScoutMultiplier`
  sites (the nameplate, the party, settlement and battle tooltips, and a settlement tooltip's party row) and in the live
  reading of `RaiseByScout`. The hold of a counted figure and the fade step (`HoldHours`, `FadeStepHours`) and the skill handed to
  the memory keep the real skill, and the adapter keeps reporting the raw one: they say how long a party remembers, not how far
  it reads.
- **No fog multiplier for the report.** The thresholds are fractions of `SeeingRange`, so the sight Far Watch adds already
  scales every threshold and the horizon in absolute distance; a multiplier in the fog stack would apply it twice. The 2.25 cap
  still covers skill times realm only. (The same reasoning as Vantage Point under "Not in Phase 1".)
- **Named past the horizon.** A host the last report listed keeps its name, its encyclopedia hotkey and its tooltip title past
  the horizon until the raised sight is over; its count stays `...` (decision Q5 unchanged). One predicate,
  `IsNameHidden(snapshot)`: at the horizon and not named. `NameplateIntel.HidesName` (set only on the horizon branch),
  `TooltipVerdict.Named` and `TooltipFrame.RenamesTitle` follow it, and the encyclopedia prefix calls `IsNameHidden` instead of
  `IsHorizon`. An army tooltip is judged on its leader's id. The `IsNamedByScout` call is made only at the horizon branch: a
  scan of at most 6 ids, ordinal compares, no allocation.
- **`RaiseByScout(snapshot, stages)`** (memory half; returns the text to print): `raised = min(digits, max(live known digits, remembered digits) + stages)`, where a remembered count counts only if `HiddenHostsMath.Agrees` with the live one,
  written to the memory (the latch is read and never written; no XP). With the fog off, no clock, or an own-side or
  engine-hidden host it returns the whole number and writes nothing. **`RecordScoutedSettlement(snapshot)`** is the exact
  stamp of any settlement, behind the same gates as `StampSettlement`, with the row on the out-of-sight clock.

In the Harmony pair of Patch105 the only change is that `VanillaBinds.LayMask` swaps the name on `HidesName`, not on
`IsHorizon`; the pair still goes through `__state` and the finalizer, and `HiddenHostsHarmonyPairTests` runs the named-horizon
case through real Harmony.

## Field parley (v1.5.6)

Bannerlord v1.5.6 lets the player parley with an at-war lord party on the map, not only with a settlement. The range is
the main party's seeing range, measured in a straight line, so a host at the horizon can be inside it. That undid the
mask: hovering a nameless "Unknown party" showed the "Press {MODIFIER_KEY} + {CLICK_KEY} to parley." hint (vanilla's text, Native `module_strings.xml`) (the hint row is added on any hover;
only the plate's parley icon needs Ctrl held), and the icon and the hint both mark it as an at-war lord party; the click
opened a conversation with the lord or a meeting menu listing every hero by name; and the refusal text ("You are too far
away from {PARTY} to start parley.") printed the party's name.

**What it does.** One postfix, `DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch`, on
`DefaultEncounterModel.CanMainHeroDoParleyWithParty(PartyBase, out TextObject)`. All four v1.5.6 callers ask that one
method through `Campaign.Current.Models.EncounterModel`: `TooltipRefresherCollection.AddTooltipHintProperties` (the hint
row), the party and settlement nameplate `CanParley` flags (`SandBox.GauntletUI`) and `MapScreen.TryParleyWith` (the click).
So one seam covers the icon, the hint and the click. When Hidden Hosts is on and the party is a mobile party for which
`HiddenHostsService.MasksParley` is true, the answer becomes false and the explanation is set to null. The click then prints
vanilla's own fallback, "Cannot parley with this party." (`str_cannot_parley_with_party`, which `MapScreen.TryParleyWith` uses when
the explanation is null); the tooltip hint row and the nameplate icon drop out, and the text names nobody. Mike chose vanilla's
line over a TAOM text, so the feature adds no localization key.

**Same verdict as the plate and the tooltip title.** `MasksParley` is `IsVisible && IsNameHidden`: the party is drawn on the
map, is past nine tenths of its spotting reach and no scout named it. The visibility gate matches the tooltip title rule: only a
party drawn on the map has a horizon. A tracked at-war lord party that is out of sight but still inside your seeing range
(for example one hidden by forest cover, spotting ratio below 1) keeps its parley hint and its name. Vanilla refuses a mobile
party beyond `PlayerParleyDistance`, which is `MobileParty.MainParty.SeeingRange`, and a party leaves sight at the seeing
range times its spotting ratio. Past your seeing range vanilla refuses with "You are too far away from (name)", which is
expected because the tracker already names it. A host the scout named (#847) keeps its name, so its parley stays open; a
party in plain sight, the player's own side, an engine-hidden party and true sight are exempt, as on the plate. Close in
until the plate names the lord and the parley comes back.

**What it leaves alone.** A settlement (Hidden Hosts does not mask settlements), the toggle off, an unmasked host, a refusal
that carried no text (vanilla's fallback line names no party), any error and the shared fault budget all keep vanilla's
answer. The postfix returns early only when vanilla refused with no text; every other answer for a masked host loses its
text. That covers the refusals that name the party (too far away, or one side at sea) and the one that
names none ("You can't start parley with Rebels."), which is dropped too; at the horizon vanilla usually answers yes, since the parley distance is the seeing range
itself (`PlayerParleyDistance` returns `MobileParty.MainParty.SeeingRange`, `E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.CampaignSystem.cs:61069`),
and the postfix turns that yes into a refusal.

**Cost.** The nameplate view asks once per party plate per frame while Ctrl is held. The postfix returns before any read
when vanilla refused without a text, when Hidden Hosts is off or when the party is not mobile; otherwise it makes the same
cached `IPartyIntelAdapter.Read` the plate hook makes. The service and adapter are reached through `HiddenHostsCalls`,
handed over once at module start.

**Co-op.** A postfix, not a veto, so `CoopVetoClassificationTests` has no row for it (a row would fail
`Registry_HasNoStaleEntries`). It changes only whether this client's own UI offers a parley; every vanilla caller is UI.

**Tests.** `HiddenHostsParleyTests` (the service verdict) and `HiddenHostsParleyHookTests` (the postfix against bare
engine objects and the installed `DefaultEncounterModel`).

## Hidden party orders (#848)

A party or army tooltip prints what the host is doing ("Besieging X.", "Holding.", "Following Y.") on one row under its title.
Reading a far host's orders defeats the blurred count, so that row follows the count.

| Tooltip state | Orders row |
|---------------|------------|
| Exempt (own, clan, player army, plain sight, true sight, info-hidden), or the tooltip's own count read in full | vanilla |
| Masked (`1??`), with or without a remembered `~` count | `Orders unknown.` |
| Horizon, named by a scout or not | `Orders unknown.` |
| Toggle off, no frame, or a settlement, battle or siege tooltip | vanilla |

The table covers the row vanilla prints: a party tooltip shows its orders only for an inspected party (in sight, or any member
of the player's army); an army tooltip also prints them when its caller passes false as the third argument, which only
the map tracker does. The kingdom army's party items and the menu's party items also pass false, but they open a party
tooltip, whose orders row depends on inspection alone.

**Rules.**

- **One predicate, no new state.** `TooltipFrame.HidesOrders` is `IsPartyOrArmy && Verdict.Mode != Vanilla`. The frame's
  verdict is the one that drew that tooltip's counts, so the orders show exactly when the count is read in full or the host is
  exempt. There is no separate short-distance value and no setting (decision Q1): a host of 1000 or more shows its orders only
  once it is close enough to read four digits.
- **Live, not remembered (decision Q2).** A remembered `~` count and a scout-named host are not `Vanilla` verdicts, so both
  hide the orders: the memory is an old count, the scout names a host, and neither is a live reading. `Agrees` is never read.
- **Replacement (decision Q3).** `{=taom_hhost_orders_unknown}Orders unknown.` (a line with a full stop, like vanilla's own
  orders). It is built as a fresh `TextObject` per call and not cached: it is made once per masked tooltip build, never per
  frame, because the row is a plain string.

**The seam: T19.** One postfix, `MobileParty_GetBehaviorText_HiddenHosts_Patch`, on `MobileParty.GetBehaviorText()` (public,
instance, non-virtual, no overloads), binding only `ref TextObject __result`. It never evaluates the party: it reads
`HiddenHostsTooltipScope.Current`, returns when the frame is null, `!HidesOrders` or the fault budget is spent, and otherwise sets `__result` inside a try (a fault is reported once and vanilla's text stays). With no frame
it reads one `[ThreadStatic]` and returns, allocating nothing.

- **Why the inner method, not the wrapper the issue names.** The tooltip rows call
  `CampaignUIHelper.GetMobilePartyBehaviorText`, a 12-byte wrapper (`GetBehaviorText().ToString()`) in the same assembly as the
  two builders: a JIT inlining candidate. Patching the long inner method is reached either way, so the inlining question is
  moot. The pair test also compiles the wrapper before and after the patch.
- **Callers (pinned from the IL, v1.5.5).** The wrapper is called by `RefreshMobilePartyTooltip`, `RefreshArmyTooltip` and
  `GameMenuPartyItemVM` only; `GetBehaviorText` is called only by the wrapper and `CampaignUIHelper`'s town patrol tooltip. The
  menu rows and the clan and kingdom patrol tooltips therefore run with no frame and stay vanilla (management screens stay
  exact, T17); the kingdom army column runs with no frame because it reads `Army.GetLongTermBehaviorText` directly. `SandBox.ViewModelCollection` (the nameplate workers) calls neither.
- **Postfix, not a skipping prefix:** vanilla runs unchanged, so `CoopVetoClassificationTests` needs no row and a vanilla throw
  behaves as today. It is local UI and writes no state.
- **PatchShield:** the target is on no exclusion list, so it gains the value-returning finalizer. Harmony 2.4.2 stores
  vanilla's `TextObject` in the result local before any postfix runs and returns it after the shield swallows a fault, so a
  missing-API fault in this postfix (its text call is direct, inside its try) keeps vanilla's orders. Only a fault raised before
  that store (another owner's prefix or transpiler) returns null, and then the wrapper's `ToString()` would throw.

**The limit.** The row is a plain string, so the periodic refresh never rewrites it: it is decided when the tooltip is built
and updates on the next hover or the key-toggle rebuild, like the trimmed troop rows. A tooltip held open while the host closes
in keeps "Orders unknown." until it is rebuilt. It freezes both ways: orders shown at build also stay beside a count that
re-masks at the next two-second refresh, until the tooltip is rebuilt. Vanilla's row is just as frozen, so nothing new leaks.

## Not in Phase 1

- The folder is `HiddenHosts`, not `PartyIntel`; the adapter and snapshot keep the neutral name `PartyIntel` because later phases read more of the party.
- No MCM tuning sliders yet: the thresholds are constants.
- No prefab widget: the mask is text in the existing nameplate fields only.
- Vantage Point is not a separate x1.25: vanilla adds it to the spotting range and the fog reads `SeeingRange` (`DefaultMapVisibilityModel.cs:61-63`), so it already widens the reach.
- The scout-away rule and the scout report moved to [#847](https://github.com/haterade22/TAOM/issues/847) and are built: see "Far Watch (#847)" above.
- Hover tooltips still show exact numbers and the name until Phase 2.
- The memory symbol `~` and its popup text arrive with Phase 3.
- Translation: the seven `taom_hhost_*` keys (three from phases 1 and 2, three from phase 3, one from #848) are translated in all 12 languages (2026-11-12; the six in #942, the #848 key in #763).

## Not in Phase 2

- **Other surfaces' own count rows (T17):** the encyclopedia settlement page's garrison stat, `ArmyManagementItemVM.Strength`,
  `KingdomArmyItemVM.Strength` and `GetTownGarrisonTooltip` print exact counts on management and encyclopedia screens. They
  stay exact for now (decision Q1); the tooltips those screens open are masked.
- **`GetArmyManCountTooltip` (T13) is not patched.** Its only caller is the game-menu overlay of an army in plain sight.
- **`GetPartyNameplateText(PartyBase)` (T12) is not patched.** Its only caller prints the main party, which is exempt.
- **`GameMenuPartyItemVM`'s own count (T14) is not patched.** The settlement and encounter menu lists are plain sight; the
  tooltips their rows open (T1, T3) are exempt through the plain-sight rule.
- **`RefreshEncounterTooltip` and `EncounterMenuOverlayVM` (T15), `MapInfoVM` (T16):** encounter screens and the main party,
  no frame opens.
- **Ships (T18):** `AddPartyShipProperties` and `GetShipSizeText`: TAOM parks naval travel and refuses the naval expansion.
- **Another mod that re-registers a tooltip type** is not covered: the registration pin covers vanilla's only (UNVERIFIED for others).
- **Rows removed while far stay removed** until the tooltip is rebuilt (a new hover or the key toggle); only the numbers
  update every two seconds.
- **Phase 3:** built, see above.

## Not in Phase 3

- **No memory at the horizon** (decision Q5): `...` stays even when a count is remembered.
- **No memory for battles and sieges**, for the prisoner total, or for a party tooltip of an army leader (its tooltip lists the
  leader's own roster, the memory holds the army).
- **No memory on the surfaces phase 2 left exact** (T17): the encyclopedia garrison stat, army management and kingdom strength.
- **No XP from stamps, memory, settlements, battles, exempt parties or hosts at peace** (decision Q3), and none on a co-op client.
- **No setting.** The toggle off stops stamping, the XP and the display, and keeps the rows; the fade, visibility and destroy
  handlers keep running so a toggle cannot freeze a row.
- **The scout-away rule and the scout report** are Far Watch ([#847](https://github.com/haterade22/TAOM/issues/847)): see "Far Watch (#847)" above.
- **A destroyed party** whose destruction bypassed `DestroyPartyAction` keeps its row until it fades out; the visibility event
  makes it stale, so the row ages out on the normal clock. `MobileParty.RemoveParty` has three callers in v1.5.5, by the
  decompile and not pinned: `DestroyPartyAction`, a cheat, and an old-save fixup in `MobileParty` (saves older than v1.3.0).
- **Siege single pass** and a **shared test snapshot builder** were considered in the deep review and not done: the first is a
  wash, the second is a follow-up.

## Changelog

- 2026-10-27 (v2.0.36): field parley (#1037): a masked host's parley is refused with vanilla's nameless "Cannot parley with this party."; one postfix, no key. The veto needs the party drawn on the map.
- 2026-10-20: hidden party orders (#848): a masked or horizon party or army tooltip shows "Orders unknown." in place of the host's orders (T19); one new key, no setting.
- 2026-10-20: Far Watch (#847): the scout-away rule (`FogSkill`), hosts named by a scout keep their name past the horizon (`IsNameHidden`), `RaiseByScout` and `RecordScoutedSettlement`; the service takes an `IScoutIntelQuery`.
- 2026-10-19: deep-review fixes: exact stamps also on every game menu (the tick never fires while a menu stops time), a live view that contradicts a memory replaces it, own side and plain sight judged by one predicate, the player's own siege on both sides, battle totals leave hidden parties out, the Scouting XP is also granted before a save, a masked total row is always wrapped, a thread-local frame slot, the reason row and the drawn flag follow what reached the screen.
- 2026-10-19: phase 3 (memory) built: the `~` count, the fade, exact stamps, Scouting XP, the save as chunked text, the reason row and its own popup; the settlement tooltip row of an army leader now masks the number vanilla printed.
- 2026-10-19: phase 2 (hover tooltips) built: eleven targets, a frame bracket on the five builders and on the two-second refresh, the in-plain-sight exemption (also for nameplates and the hotkey).
- 2026-10-18: phase 1 (nameplates) built; the mask moved from a remembered-value table to a prefix and finalizer pair.

## In-game check owed

Bind the encyclopedia key first (it is unbound on the keyboard by default). Approach a lord army and a small party with
Left Alt held, so the name and extra text show: counts sharpen with distance; step back at a boundary and the digit holds
for a tenth of the threshold; past nine tenths of the party's spotting reach the plate reads `...` and "Unknown party" and
the encyclopedia key does nothing, while a left click still engages; turn the toggle off mid-chase and the exact numbers
return on the next frame; the popup appears once per campaign.

### Field parley checks (#1037)

With Hidden Hosts on and an enemy at war with you, hold Ctrl over an at-war lord party in the horizon band (plate reads
"Unknown party"): no parley icon, no "Press {MODIFIER_KEY} + {CLICK_KEY} to parley." hint, and a click prints "Cannot parley with this
party." with no name in it. Close in until the plate names the lord: the icon and the hint return and a click within the
parley distance parleys. Name a host past the horizon with a scout's report: its parley is offered once it is in parley
range. Hold Ctrl over a settlement you are at war with: its parley is unchanged. Toggle Hidden Hosts off: vanilla icon and
text at any distance. No `[HiddenHosts] ... fell back to vanilla` line in the log.

Track an at-war lord party that is out of sight but still inside your seeing range (for example one hidden by forest cover,
spotting ratio below 1): it keeps its parley hint and its name. Past your seeing range vanilla refuses with "You are too far
away from (name)", which is expected because the tracker already names it.
Parley a far host whose count is masked, then check its plate: a parley conversation party counts as in plain sight
(`PartyIntelAdapter.cs` near :229-230), so it may stamp an exact count (UNVERIFIED).

### Hidden party orders checks (#848)

Hover a far enemy lord party with a masked count: the orders row reads "Orders unknown."; close in until the count is exact and
hover again (or press the extend key): the real orders show. Your own party, a clan party and a member of your army show
orders while in sight. A `~` remembered host and a scout-named host past the horizon hide them. An army, far and near. The map
tracker: track a far enemy army and hover its tracker icon: Orders unknown. while its count is masked. A member of your army, also an unmerged one while you serve in an AI army, shows its orders. A besieger while you defend the town shows its orders. The clan and kingdom patrol tooltips, the kingdom army column and an encounter
menu row description stay vanilla. Toggle off and hover again: vanilla orders. No `[HiddenHosts] ... fell back to vanilla`
line in the log. Keep a tooltip open while the host closes in: it stays "Orders unknown." until the next hover (the known
limit).

### Far Watch checks

With a Ranger companion in the Scout role, send him to a town: while he is away the counts blur sooner (the plate that read `12?` reads `1??`); on his return the report rows carry digits, a listed host far off keeps its name and the encyclopedia key opens it, and the nameplate later shows `~12?` for it; scout a town and hover it from afar (`~` defenders and the "Counted" age); with Hidden Hosts off, the report gives exact counts.

### Phase 3 checks

Own fief stays exact and unremembered; a companion inside a settlement; the conversation partner; a battle or siege a
player-clan party fights; an army leader's settlement row shows its own roster ("4?", not "???"); true sight; enter a town and
leave without waiting, then open one of its lords from the tracker ("~" shown: the menu event stamped it); meet a party in the
encounter menu; talk to a lord; defend a besieged town (the besiegers read exactly); a host that grows or shrinks past a digit
boundary ("~" replaced by the live view); hold Alt over a remembered settlement (no "Counted" row, no popup).

Count a host up close and ride out of sight: the nameplate shows `~` and the count, the reason row ages in the tooltip, a digit
drops at the hold (6 hours at no Scouting) and every 8 hours after; save and load and see the same `~` (and that a reload does not
pay the Scouting XP of reads it took back); the memory popup shows once, after a `~` is drawn; hover a settlement you left
(`~` defenders and the age); watch the scout's Scouting rise by 20, 30 and 50 as an enemy host's digits come into view, once per
host per day, and not for hosts at peace; a host in your settlement, encounter or siege reads exactly and is remembered exactly.

### Phase 2 checks

Hover an enemy party at three distances with the extend key held (rows appear only when the count is read in full); keep a
tooltip open while the party closes in (the figure sharpens within two seconds); hover a party at the horizon ("Unknown
party", ` (...)`); hover a settlement far and near (defenders row and party rows); hover a field battle and a siege far
away; open the settlement menu's party list tooltips (exact, plain sight); open a tooltip from the map tracker and army
management (masked); check that the troop totals really go through the postfix in the game's own process (the test shows it
for the test host's JIT only).

## GitHub Issue

- **Issue:** #846 [Hidden Hosts](https://github.com/haterade22/TAOM/issues/846)
- **Status:** Open (phases 1 to 3 built, in-game check owed)
- **Issue:** #848 hidden party orders (built, in-game check owed)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->

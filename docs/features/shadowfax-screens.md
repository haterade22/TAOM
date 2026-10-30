# Shadowfax Screens

## Overview

The clan, kingdom, character, career, inventory, party and quest screens open at once. Their panels
no longer slide in, and the rows in the inventory and party lists no longer animate when they expand
or change state. The instant transitions are always on.

The screen open speed work then cuts repeated work while four screens build: the clan screen builds
its parties list once instead of three times, the inventory and trade screens skip a name sort they
replace a moment later, the party screen builds each troop portrait code once per screen, and the
quest log builds a finished quest's stages only when the player selects it. Each has a switch in the
"Screen Performance" MCM group (on by default), and "Log Screen Open Times" (off by default) writes
one timing line per open so the switches can be compared off against on. What the screens show is
unchanged.

## Why This Exists

- **Vanilla behavior:** in Bannerlord v1.5.5 most screen panels slide in over 0.45 s, and the
  character screen's side panels over 0.4 s. Inventory and party rows animate for 0.075 to 0.15 s
  each, and the equipped-item controls slide for 0.2 s. TAOM's own career screen slid its header and
  footer in over 0.35 s.
- **TAOM requirement:** players open these screens many times in a session, and the slide carries
  no information.
- **Without this feature:** each screen open spends up to 0.45 s moving its panels into place.

## Architecture

### Design Challenge

The animations live in the `VisualDefinitions` block of each screen's prefab XML. TAOM ships its own
copies of the party screen, its two troop row prefabs and the career screen, but uses vanilla's
prefabs for the other nine movies. A UIExtenderEx `PrefabExtensionSetAttributePatch` resolves one
node per `[PrefabExtension]` attribute, so it would need an XPath for each of the 27
VisualDefinitions, and each XPath misses when an engine update renames its node.

### Solution Approach

- **Vanilla movies:** `ShadowfaxScreensPrefab` is a UIExtenderEx `CustomPatch<XmlDocument>`. It
  receives the whole prefab document, and its nine `[PrefabExtension]` attributes register it for
  ClanScreen, ClanMembers, KingdomManagement, FiefsPanel, CharacterDeveloper, Inventory,
  InventoryEquippedItemControls, InventoryItemTuple and QuestsScreen. UIExtenderEx applies it from
  `WidgetPrefab.LoadFrom` each time the prefab loads from disk, which also covers the four that load
  as child prefabs (ClanMembers, FiefsPanel, InventoryEquippedItemControls, InventoryItemTuple).
- **The rewrite:** `InstantTransitionRewriter.MakeInstant` sets `TransitionDuration="0"` on every
  definition in `Prefab/VisualDefinitions`, the only block the engine reads. A definition without
  the attribute gets it too, because Gauntlet's default is 0.2 s. The ease settings and
  `VisualState` targets stay, so each panel ends where vanilla puts it. With a zero duration
  `Widget.UpdateVisualDefinitions` snaps to the target state on its first tick; it never divides by
  the duration, and `DelayOnBegin` has no effect. Vanilla uses the same zero duration on the
  tournament screen.
- **Gamepad navigation:** two vanilla widgets wait for the animation before they hand gamepad focus
  on: an expanded inventory or party row's controls (`InventoryTupleExtensionControlsWidget`, 0.15 s)
  and the equipped-item controls (`InventoryEquippedItemControlsBrushWidget`, 0.2 s). Both compare
  against the duration, so with 0 they hand focus on after one frame.
- **TAOM's own prefabs:** every VisualDefinition in `Party/PartyScreen.xml`,
  `Party/PartyTroopTuple.xml`, `Party/PartyTroopTupleLeft.xml` and `CareerSystem/CareerScreen.xml`
  has `TransitionDuration="0"` in the XML. That includes the party section headers' 0.045 s press
  squish. FormationListPanel and FormationSeperator belong to the formation dropdown, which is
  commented out in PartyScreen.xml; their zeros take effect only if it returns.
  The two tuples also carry the block marked `TAOM #902` ([prisoner-banners.md](prisoner-banners.md)); reapply it after a re-sync to vanilla.
- **Registration:** `UIExtender.Register` in `ClientUiExtensions.Register` (called from `ClientSubModule.OnGameStart`, once) finds the class, in
  solo and, through `CoopUiRegistrationPolicy`, under co-op (the class has no `[CoopSuppressedUi]`;
  the change is visual only). No IoC entry and no Harmony patch.
- **Screen open speed on a dedicated server (ADR-013):** the 17 hooks of the four screen-speed categories name
  ViewModelCollection types, so each carries `[ClientOnly]` and a server never indexes them. The Clan transpiler's
  swap helpers live in its own `[ClientOnly]` patch class, so `ClanFastOpenCalls` names no client type. The quest
  log's engine reads live in TAOM.Client (`QuestJournalAdapter`); on a server core's `NullQuestJournalAdapter` never
  binds, and `ShadowfaxScreensModule.OnPhase` writes neither install line there.
- **Generated prefabs:** TAOM.Dependencies sets `UIConfig.DoNotUseGeneratedPrefabs = true`, so the
  engine always loads these movies from XML and the patch always applies.

### Component Diagram

```
SandBox prefab XML (9 movies)           TAOM prefabs (party x3, career)
        |                                        |
  WidgetPrefab.LoadFrom (UIExtenderEx hook)   TransitionDuration="0"
        |                                     written in the XML
  ShadowfaxScreensPrefab (CustomPatch<XmlDocument>)
        |
  InstantTransitionRewriter.MakeInstant
```

## Clan fast open

**What it does:** the Clan screen builds its party lists once per open instead of three times. The
screen shows the same rows and headers; only the repeated work goes. The switch is "Clan Fast Open"
(default on). It is off under co-op, whatever the setting says.

**Why three builds:** `ClanPartiesVM.RefreshPartiesList` clears and refills the Parties, Garrisons
and Caravans lists and constructs a row view model for every party, caravan and garrison of the
clan. Vanilla runs it three times in one open, all for the same instance:

1. The `ClanPartiesVM` constructor calls `RefreshPartiesList` directly.
2. The same constructor then calls `RefreshValues`, which calls `RefreshPartiesList` again.
3. The `ClanManagementVM` constructor calls `UpdateKingdomRelatedProperties`, then
   `UpdateBannerVisuals`, then its own `RefreshValues`, which calls `_clanParties.RefreshValues()`,
   and that calls `RefreshPartiesList` a third time.

Builds 2 and 3 rebuild lists that build 1 just filled with nothing in between to change them.

**What is skipped:** only the `RefreshPartiesList()` call inside `ClanPartiesVM.RefreshValues`, which is by
construction that instance's own call. One transpiler on `RefreshValues` swaps that single call for
`ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.RefreshPartiesListInRefresh(vm)`. With the fast path on (the switch on and co-op off) the
helper renders the two headers and returns; otherwise it calls `vm.RefreshPartiesList()` as vanilla does. The
rewrite is the soft-failing `TickProfilerTranspiler.Rewrite`: a call count other than one leaves the whole method
vanilla, and one line at game init says which happened (`[Shadowfax] Clan fast open installed: ...` or `not
installed: ...`). Direct callers are never touched, because the swap is at one call site, not on the callee:
`ClanManagementVM.RefreshCategoryValues` (after a fief, member or income change) and any co-op handler call
`RefreshPartiesList` itself and always build.

**Harmony fact that shaped this:** a prefix that returns false does not stop postfixes. An earlier design skipped
through a bool prefix, so every postfix of `RefreshPartiesList` (Refuge's, for one) still ran for the skipped call.
With the call-site swap the skipped call never reaches the method, so its postfixes run only for real builds.
`ClanFastOpenHookTests.APostfixOnTheListBuild_RunsOncePerRealBuild_NotForTheSkippedCall` pins it.

**Headers:** vanilla sets the "Caravans (N)" and "Garrisons (N)" header texts at the end of
`RefreshPartiesList` (`ClanPartiesVM.cs:500-503`), while `RefreshValues` first resets them with whatever `{CURRENT}`
was last set to. After build 1 that variable holds the garrison count, so the Caravans header briefly shows that
number (not the bare text). After a skip the helper renders both headers from the current lists, with the
same `GameTexts` calls vanilla makes; if that throws, it falls back to the full build. The Parties header is not
touched by `RefreshValues`, so it keeps the text build 1 gave it.

**Interplay:**
- **Refuge** (`RefugeClanScreenPatch`, a postfix on `RefreshPartiesList`) runs after every build that runs, so its
  refuge rows are added once, in the first build, and stay. The header render counts them, as vanilla's did.
- **TroopWeight** patches the row's `UpdateProperties`, not the list build. `RefreshValues` still refreshes every
  party row after the skipped build, so it runs as before.
- **Timer:** the `ClanManagementVM` constructor `(Action, Action<Hero>, Action<Hero>, Action)`, its only
  constructor, is wrapped by the shared timer. All three list builds happen inside it, so with "Log Screen Open
  Times" on, one line covers the VM build and not the movie load that follows in `GauntletClanScreen`, with the
  list-build counts as its detail:
  `[ScreenOpen] Clan open: 140.2 ms (fast open on; list builds: 1 run, 2 skipped, 38.1 ms)`.
  The detail starts `fast open on;` or `fast open off;`, the path the open ran with (off under co-op). The build
  milliseconds are the real builds only; a build the screen makes after the constructor (the category refresh
  that `OnActivate` runs) is outside the window.

**Callers that rebuild and then call `RefreshValues`:** two places pair a list rebuild with a `RefreshValues` in
the same breath, and the skip keeps both correct because the rebuild is a direct `RefreshCategoryValues` call that
still builds: `OnChangeClanNameDone` (`ClanManagementVM.cs:1389-1390`) and `GauntletClanScreen`'s protected
`OnActivate` (`GauntletClanScreen.cs:226-239`, `RefreshCategoryValues` then `UpdateBannerVisuals`, which reaches
`RefreshValues`). After party changes (a disband, a new leader) `ClanPartiesVM` calls `_onRefresh`, which the
constructor wires to `RefreshCategoryValues`, so those also rebuild.

**Known limit:** with the switch on, a `RefreshValues` call that has no rebuild in front of it refreshes the
existing rows without rebuilding the lists. None of the vanilla callers listed above is such a call.

**Co-op:** the switch is off while `ICoopPresenceProvider.IsCoopActive`, so under co-op the helper always builds and
the screen is vanilla. The skip itself writes only this client's view-model lists and header texts.

**In-game checklist:** (1) Open Clan with the switch on: Parties, Caravans and Garrisons headers show their
counts, refuge rows appear once. (2) Disband a party or change a leader: the lists update. (3) Rename the clan:
the lists and headers update. (4) Close and reopen the screen three times: no stale rows. (5) With "Log Screen
Open Times" on, the open line reads `list builds: 1 run, 2 skipped`. (6) Switch off, reopen: `3 run, 0 skipped`.

**How to measure:** turn "Log Screen Open Times" on in the Battle Load Diagnostics page, open the
Clan screen six times with "Clan Fast Open" off, then six times with it on, on a large save. Drop
the first open of each arm and compare the medians of the `[ScreenOpen] Clan open` lines in
`taom_debug.log`. Keep the change only if the median drops by at least 16 ms (one 60 fps frame) and
by more than the off arm's spread; otherwise delete it and keep the timer.

**Removal checklist:** delete `ClanScreenFastPathService.cs`, `ClanFastOpenCalls.cs` and the three
`Clan/Hooks` files; in `ShadowfaxScreensModule.cs` delete the Clan category line, the service registration, the
`ClanFastOpenCalls.Initialize` line and the `LogInstall` line in `OnPhase`; delete the Clan tests; delete the
"Clan Fast Open" setting in `BattleLoadDiagnosticsSettings` and its provider property and interface member, and
its `CoopSettingsRelevance` entry (`Main/Features/CoopInterop/CoopSettingsRelevance.cs`); delete the three
`ShadowfaxScreens.Clan.Hooks` rows of `docs/reference/taleworlds-api-snapshot/patch-targets.md` and fix its count; delete
the Clan sections of this page and of `harmony-patch-registry.md`, and the `feature-map.md` clause.

**Files:**

| File | Purpose |
|------|---------|
| `Main/Features/ShadowfaxScreens/Clan/ClanScreenFastPathService.cs` | The skip decision, the open window's counts, the install line |
| `Main/Features/ShadowfaxScreens/Clan/ClanFastOpenCalls.cs` | Static facade, the patch category name, the swap count |
| `Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.cs` | The transpiler that swaps the one list-build call, and the helper it swaps in |
| `Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch.cs` | Counts and times the builds that really run |
| `Main/Features/ShadowfaxScreens/Clan/Hooks/ClanManagementVM_Ctor_ClanFastOpen_Patch.cs` | The `[ScreenOpen] Clan open` window |

**Tests** (`TAOM.Tests/Features/ShadowfaxScreens/Clan/`): `ClanScreenFastPathServiceTests` (off, co-op, co-op probe
throw, counting window, detail format, install line); `ClanFastOpenHookTests` (real Harmony on dummy classes:
inner call swapped and a direct call not, a postfix runs once per real build, the soft-fail cases, the open line);
`ClanFastOpenBindingTests` (both targets, the three-build chain in the installed IL, the single swapped call, the
header tail, the one constructor, and a JIT-acceptance test that applies the transpiler to the real method). The
module wiring is in `ShadowfaxScreensModuleTests`.

## Inventory and trade fast open

One screen covers both inventory and trade: `GauntletInventoryScreen.OnReady` builds `SPInventoryVM` and
loads the `Inventory` movie, and a merchant's stock is the same screen with a larger left list. One
change sits behind "Inventory Fast Open" (default on, read per open, no restart): an IL call-site swap
under the category `Patch128_ShadowfaxInventory`, named by
`ShadowfaxInventoryCalls.PatchCategory`, which the module and every inventory hook share.

**Name-sort skip.** `SPInventoryVM.InitializeInventory` sorts each side by item name with
`OrderBy<ItemRosterElement,string>` straight into `ToArray<ItemRosterElement>` before building one
`SPItemVM` per row. With the toggle on, `NameSortRewrite` swaps both `OrderBy` calls for
`ShadowfaxInventoryCalls.SortByNameOrPassThrough`, which hands the source on unsorted; off, or with no
service, it calls `OrderBy` as vanilla does. The rewrite applies only when the method holds exactly two
such sites, each immediately followed by `ToArray<ItemRosterElement>`; any other shape returns the IL
unmodified and names the reason (`out string? problem`), which the service writes as the one install or bail
line (so a re-applied category, whose second pass finds no `OrderBy`, is a no-op and logs once).

**Why the order the player sees is unchanged, and where it can differ.** `InitializeInventory` has two
callers, the 3-argument `SPInventoryVM` constructor and `AfterReset`, and both build the two sort controllers
and call `SortByDefaultState` (then the saved sort preference) right after it. Every comparer falls back to
`ItemDescription` on a tie (`ItemComparer.ResolveEquality`), so rows land where vanilla puts them unless they tie
on every key the active comparer reads. `MBBindingList.Sort` uses the unstable `List<T>.Sort`, so fully tied
rows (same name, type and value) can appear in a different relative order than with the pre-sort. In loot mode
the other side is sorted by cost, not name; the skip does not change that. `InventoryBindingTests` pins the
caller list, the sort after the call and the tie-break against the installed DLLs.

**Dropped: the concept lookup memo.** An earlier build also swapped the two `SingleOrDefault<Concept>` calls in
the 8-argument `SPItemVM` constructor for a per-site memo. It is gone: the saving measured about 0.7 us per row,
too small to justify a second transpiler, a weak-keyed memo and their tests (`simplicity-criterion.md`).

**Timing.** With "Log Screen Open Times" on, a prefix and void finalizer on the one `SPInventoryVM` constructor
`(InventoryLogic, bool, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>)` open the `Inventory open`
window and end it, also when the constructor throws. The span is the view-model build (including
`InitializeInventory`); the `Inventory` movie load that follows in `GauntletInventoryScreen.OnReady` is not in
it. `InitializeInventory` is timed by the service as a phase inside that window (a Reset re-runs it outside an
open and logs nothing). The finalizer reads the mode from the private `_usageType` field (the real
`InventoryScreenHelper.InventoryMode`, not `IsTrading`) and the side counts from the constructor's
`inventoryLogic` argument, so no adapter and no active-state lookup exist. The service closes its open latch
first, then formats. The line looks like:

`[ScreenOpen] Inventory open: 212.4 ms (fast open on; mode=Trade, left=312, right=88, rows=118.0 ms, sorts skipped=2)`

`fast open on` or `fast open off` is the fast path the open started with, `mode` is the `InventoryMode` name,
`left` and `right` are the other party's and the player's element counts (`n/a` when unreadable), `rows` is the
`InitializeInventory` time and `sorts skipped` counts the pass-throughs. The install line is one per site in
`taom_debug.log`: `[ShadowfaxInventory] name sort: swapped 2 of 2 call sites`.

**Left out, and why.** Trade-data and button-hint deferral (the ensure paths are unproven, and the
`ThisStock` setter can apply a transaction), a banner cache, and widget cost are not built. If the rows stay
slow after this, the next target is `FindItemFromSide`'s linear lookup.

**How to measure.** Turn "Log Screen Open Times" on and load a save. Go to the largest merchant. For
"Inventory Fast Open" off and then on, open the trade screen 6 times each, drop the first open of each arm,
and compare the medians of the `[ScreenOpen] Inventory open` lines. Keep the change only if the median drops by
at least 16 ms and by more than the off arm's spread; otherwise delete it and keep the timer points.

**In-game correctness checklist.** With the switch on: open a merchant and sort by each column, then reopen and
confirm the saved sort holds; trade an item and confirm the list stays in order; open a loot screen and a stash
and check the rows are sorted; use the character switch arrows and a Reset (rows must stay sorted); open from a
cold start and confirm nothing about the screen differs from the switch off.

**Removal checklist** (if the A/B misses the 16 ms rule): delete `NameSortRewrite.cs`,
`ShadowfaxInventoryCalls.cs` (keep or fold the `ScreenOpenTiming` calls if the timer stays),
`InventoryOpenFastPathService.cs` and the two files in `Inventory/Hooks/`; delete the five
`TAOM.Tests/Features/ShadowfaxScreens/Inventory/` test files; in `ShadowfaxScreensModule.cs` delete the
inventory category line, the `InventoryOpenFastPathService` registration and the `ShadowfaxInventoryCalls.Initialize`
line (plus its `using`); drop the `InventoryFastOpen` member of `IShadowfaxScreensSettingsProvider`, its
provider property and the MCM setting; its `CoopSettingsRelevance` entry
(`Main/Features/CoopInterop/CoopSettingsRelevance.cs`); the two `ShadowfaxScreens.Inventory.Hooks` rows of
`docs/reference/taleworlds-api-snapshot/patch-targets.md` (and fix its count); delete the
`Patch128_ShadowfaxInventory` section of `harmony-patch-registry.md`, this section and its Key Files rows, and
the inventory clause of the `feature-map.md` row.

**Co-op.** BannerlordCoop's `GameInterface` module carries an `SPInventoryVMPatches` class that names
`SPInventoryVM`; its inventory patches only add postfixes to `SPInventoryVM.SaveItemLockStates` and
`SaveItemSortStates`, so they do not touch the constructor or `InitializeInventory`. These hooks only observe the constructor and rewrite two calls inside `InitializeInventory`; they
write nothing to the network or the save.

## Party fast open

Issue #853. The party screen builds one `PartyCharacterVM` row per troop and per prisoner, on both sides, and
vanilla recomputes each row's character code. "Party Fast Open" (MCM, Screen Performance, default on) caches those codes
for the life of one screen. Off restores vanilla's build exactly.

**The cache** (`PartyScreenFastPathService`, no TaleWorlds types, keys compared by reference):
`PartyCharacterVM.GetCharacterCode(character, type, side)` computes the equipment code and the body properties itself,
and `CharacterCode.CreateFrom` only builds the string from them. The same character appears in several rows and in every
rebuild, so a hit skips the whole `GetCharacterCode` body. The cache is keyed by (character, troop type, side).

**Session lifetime.** A session begins in the `PartyVM` constructor and ends at that `PartyVM`'s `OnFinalize` or at the
next `PartyVM` constructor, whichever comes first. Talk deactivates and reactivates the party screen and abandons the old
view model without `OnFinalize`; the new constructor's `BeginScreen` clears the cache, so nothing leaks across screens.

**Why this is safe next to BannerColorPersistence.** Patch23's `PartyCharacterVM.GetCharacterCode` postfix (#993)
replaces the returned code with `ClanColoredCharacterCode.Recolor`: a code rebuilt with the clan colours in `Code`, or
the same instance when its colours already match. Patch23 is applied before the GameInit categories and both postfixes
have the default priority, so Patch23's runs first and the cache stores the recoloured code; on a hit Patch23 still
runs and `Recolor` hands the cached code back unchanged. Nothing writes to a built code, so rows can share one code
object. `PartyFastOpenBindingTests` reads the IL of the `CharacterImageIdentifier`, `CharacterImageIdentifierVM` and
`UpgradeTargetVM` constructors (RequiresGameIL) and fails if any of them touches another member or passes the code
anywhere else. If the order ever flipped, each hit would rebuild the code string again: slower, not wrong.

**Toggle.** The toggle is read once, when the `PartyVM` constructor starts. Off at that point means every lookup misses
and every store is ignored, but the session transitions still run. The open line's detail starts `fast open on;` or
`fast open off;`, the fast path that open actually ran with. The constructor patch ends the session if the constructor
throws.

**Co-op.** The cache holds codes for the screen's own rosters and no campaign state. Coop's roster refresher calls the
screen's `OnReset`, which rebuilds rows through the same hook, so codes stay correct for the current roster.

**What was removed, and why.** A second cache for the item-category count (`GetNumOfCategoryItemPartyHas`, one roster
walk per row) was measured at 0.1 to 1 ms per open, too little to keep next to its window bookkeeping, and is gone. Also
left out on purpose: the `InitializePartyList` loop fix (tens of microseconds), deferring the trade rows, a `TransferAll`
rewrite, pre-sorting the rosters, and skipping `RefreshValues` or property-change notifications.

**How to measure.** Turn "Log Screen Open Times" on. Use a large army with many prisoners, plus a garrison at a big town.
Open the party screen six times with Party Fast Open off, then six times on, and drop the first open of each arm. Compare
the medians of the `[ScreenOpen] Party open` lines in `taom_debug.log`; the detail in parentheses lists the fast-open
flag, the character-code lookups, code hits and misses, and the time in `InitializeTroopLists` and the final
`RefreshValues`. Press Transfer All in the same save and compare the `[ScreenOpen] Party transfer all` lines the same
way. Keep the change only if the median drops by at least 16 ms and by more than the off arm's spread.

**In-game correctness checklist.** With the toggle on: open the party screen with a mixed army and prisoners and check
that every row shows its own portrait (two troops of one type on both sides, a hero, a prisoner); expand an upgrade row and
check the upgrade-target portraits; press Transfer All and Reset; Talk, return and reopen the screen, checking portraits
are still right; repeat once with the toggle off.

**Removal checklist** (deleting Party Fast Open touches): the folders `Main/Features/ShadowfaxScreens/Party/` and
`TAOM.Tests/Features/ShadowfaxScreens/Party/`; in `ShadowfaxScreensModule.cs` the `using`, the category line, the
registration and the `PartyFastOpenCalls.Initialize` line; the `PartyFastOpen` member in
`IShadowfaxScreensSettingsProvider`, `ShadowfaxScreensSettingsProvider` and `BattleLoadDiagnosticsSettings`, its
`CoopSettingsRelevance` entry, and its rows in `ShadowfaxScreensSettingsProviderTests` and the module wiring tests; the
`PartyCharacterVM_GetCharacterCode_ShadowfaxParty_Patch` entry in `CoopVetoClassificationTests`; the setting's
translation rows; this section, the registry section and the Configuration row.

**Files:** `Main/Features/ShadowfaxScreens/Party/` (`PartyScreenFastPathService`, `PartyPhase`, `PartyFastOpenCalls`,
`Hooks/`).
**Tests:** `TAOM.Tests/Features/ShadowfaxScreens/Party/` (`PartyScreenFastPathServiceTests`, `PartyFastOpenPatchShapeTests`,
`PartyFastOpenBindingTests`; the module wiring is in `ShadowfaxScreensModuleTests`).

## Quest log fast open

Opening the Quest log builds a `QuestItemVM` for every finished quest, and each builds one stage view model per
journal entry, for rows nobody reads until a quest is selected. With "Quest Log Fast Open" on, a finished quest built
during the open skips its stages and builds them when it is selected. Active quests and issues are untouched.

- **What is deferred:** only `QuestItemVM.PopulateQuestLog` for an item made by the journal constructor, inside the
  `QuestsVM` constructor. The quest giver's portrait stays vanilla's real `HeroVM`, so there is no placeholder and no
  change to the right-hand panel.
- **Building on demand:** a prefix on `QuestsVM.SetSelectedItem` replays `PopulateQuestLog` for each journal entry,
  then `UpdateIsUpdated` and `RefreshValues`, before vanilla copies the stages into the panel. It builds whatever the
  switch says at that moment, so turning the switch off mid-open never strands an empty quest.
- **The updated dot:** vanilla sets a finished quest's dot from its stages. For a deferred item a postfix on
  `UpdateIsUpdated` sets it from the same data the stages read, whether any journal entry is still unexamined.
- **Sorting by date:** vanilla reads a finished quest's date from its first or last stage and treats an item with no
  stages as undated. The `PopulateQuestLog` prefix passes the service each journal log (the parameter `log`), which
  records the first and the last one on the item's mark, because the journal constructor feeds `GetEntries()` in order.
  A postfix on the sort's `GetJournalLogAt` supplies that first or last log, only when vanilla returned nothing and only
  for a deferred item. The postfix reads the sort's `logIndex` as an `int`: the enum (`First` 0, `Last` 1) is
  `protected` (nested family) in a private type, so it cannot be named, and its underlying type is `int`.
- **Fail closed:** nothing defers unless the adapter is bound, which needs the private method and all four patches
  attached (the log says `[QuestLog] fast open installed` or `unavailable (reason)` at game start). The patches are
  checked again on every open, so one removed after the install (another mod, PatchShield) stops deferral from the next
  open. The switch and the bound state are read once when the screen opens. If an on-demand build ever throws, it is
  logged once, deferral is off until the game restarts, the other unbuilt quests still build when selected, and that
  quest keeps sorting by date.
  The partial-stage case: a rebuild that throws part way can leave some stages added, and the item stays marked and is
  never retried; the updated dot postfix answers for any marked item, so a later `UpdateIsUpdated` call (a stage's
  examination callback) derives the dot from the journal entries' unexamined logs, not from the partial stages.
- **Co-op:** no co-op reference to the quest view models and nothing is written to campaign state; the switch is
  presentation.
- **How to measure:** turn "Log Screen Open Times" on, load the longest campaign, and open the Quest log six times
  with the switch off and six with it on, dropping the first open of each run. Compare the medians of the
  `[ScreenOpen] Quests open` lines in `taom_debug.log`. The detail starts with `fast open on|off;` (the effective path), then gives the open's counts: finished quests built
  (`old`), items and journal entries deferred, how many show the updated dot, the switch state, and the milliseconds
  the journal constructors took (timed only while the timer is on). Keep the change only if the median drops by at
  least 16 ms and by more than the off runs' spread.

Code: `Main/Features/ShadowfaxScreens/Quests/` (`QuestLogDeferService`, the `QuestLogDeferCalls` facade and six patches
in `Hooks/`), `Client/Adapters/QuestJournalAdapter.cs`. Patch details: the `Patch130_ShadowfaxQuests` entry in
[harmony-patch-registry.md](../reference/harmony-patch-registry.md). Tests: `TAOM.Tests/Features/ShadowfaxScreens/Quests/`.

## Configuration

The instant screen transitions are always on and have no setting. TAOM's four prefabs carry their
zeros in the XML, so a setting would first need them moved onto the patch (UIExtenderEx patches
TAOM's own prefabs by file name too).

The screen open speed work adds a "Screen Performance" group to the Battle Load Diagnostics MCM
page. None of its rows needs a restart; each is read on every screen open.

| Row | Default | What it does |
|---|---|---|
| Log Screen Open Times | Off | Writes one `[ScreenOpen]` line per screen open to `taom_debug.log`: the screen, the phase and the milliseconds. Use it to compare a fast-open switch off against on. |
| Clan Fast Open | On | Builds the Clan screen's parties list once instead of three times. Off restores vanilla's build. |
| Inventory Fast Open | On | Skips work the Inventory and trade screen repeats. Off restores vanilla's build. |
| Party Fast Open | On | Caches the character code the Party screen rebuilds for each troop. Off restores vanilla's build. |
| Quest Log Fast Open | On | Builds finished quests only when they are selected. Off restores vanilla's build. |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ShadowfaxScreens/ShadowfaxScreensModule.cs` | The feature module: four patch categories at GameInit, registrations, static wiring, the clan install line and the quest log bind |
| `Main/Features/ShadowfaxScreens/IShadowfaxScreensSettingsProvider.cs`, `ShadowfaxScreensSettingsProvider.cs` | The five Screen Performance switches; never throws, an unreadable switch returns its shipped default |
| `Main/Features/ShadowfaxScreens/Diagnostics/` | `ScreenOpenTimer`, `ScreenOpenWindow` and the `ScreenOpenTiming` facade: one `[ScreenOpen]` line per open |
| `Main/Features/ShadowfaxScreens/Clan/` | `ClanScreenFastPathService`, `ClanFastOpenCalls`, and the hooks: the `RefreshValues` call swap, the build counter and the `ClanManagementVM` timer |
| `Main/Features/ShadowfaxScreens/Party/` | `PartyScreenFastPathService` (the per-screen portrait code cache), `PartyFastOpenCalls`, `PartyPhase` and six hooks |
| `Main/Features/ShadowfaxScreens/Quests/` | `QuestLogDeferService`, `QuestLogDeferCalls`, `QuestOpenCall` and six hooks |
| `Main/Adapters/IQuestJournalAdapter.cs`, `NullQuestJournalAdapter.cs`, `Client/Adapters/QuestJournalAdapter.cs` | The quest log's engine reads (the implementation is in TAOM.Client, ADR-013; core registers the null adapter and `TaomClientComposition` replaces it): the private `PopulateQuestLog` replay, the unexamined-log check, and the four-patch attachment check |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The "Screen Performance" MCM rows |
| `Main/Features/ShadowfaxScreens/Inventory/InventoryOpenFastPathService.cs` | Toggle gate, install line, per-open counters and detail (no TaleWorlds types) |
| `Main/Features/ShadowfaxScreens/Inventory/NameSortRewrite.cs` | Pure IL rewrite for the two sort-call swaps |
| `Main/Features/ShadowfaxScreens/Inventory/ShadowfaxInventoryCalls.cs` | The category constant, the sort helper the rewrite calls and the static wiring the hooks reach |
| `Main/Features/ShadowfaxScreens/Inventory/Hooks/` | `SPInventoryVM` constructor timing bracket, `InitializeInventory` transpiler and phase |
| `Main/Features/ShadowfaxScreens/InstantTransitionRewriter.cs` | Pure XML transform: zero every VisualDefinition's duration |
| `Client/Features/ShadowfaxScreens/UI/ShadowfaxScreensPrefab.cs` | UIExtenderEx patch on the nine vanilla movies |
| `Main/_Module/GUI/PreFabs/Party/PartyScreen.xml`, `PartyTroopTuple.xml`, `PartyTroopTupleLeft.xml` | Party screen and troop rows, zero durations |
| `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` | Career screen header and footer, zero durations |

## Dependencies

- Bannerlord.UIExtenderEx 2.13.3 (through TAOM.Dependencies): `Prefabs.CustomPatch<XmlDocument>`.
- MCM, through `BattleLoadDiagnosticsSettings` ("Screen Performance" group).
- `IStampClock` (registered by `LoadTimeStampsModule`) for the open timer and the phase timings.
- `ICoopPresenceProvider` (CoopInterop) for the clan fast path, which is off while a co-op module is loaded.
- `TickProfilerTranspiler` (MissionPerf) for the clan call swap.

## Tests

- `TAOM.Tests/Features/ShadowfaxScreens/InstantTransitionRewriterTests.cs`: durations zeroed, a
  missing duration added, ease and states kept, idempotent, definitions outside the block left
  alone, no-op without definitions, null document.
- `TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensPrefabTests.cs`: pins the nine movie names;
  checks each installed vanilla movie still has a definition to zero and no VisualState with its own
  duration (needs the game install); and checks TAOM's four prefabs are already instant.
- `TAOM.Tests/Migration/PrefabExtensionBindingTests.cs`: every `[PrefabExtension]` movie name,
  including these nine, must exist in the installed game with the exact case of its file name.
- `TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensModuleTests.cs`: registrations, all five
  facades wired and reset, the GameInit install and bind, the declared categories, every Shadowfax
  hook class in a declared category and every hook file under 150 lines.
- `ShadowfaxScreensSettingsProviderTests.cs` (the five shipped defaults) and `ScreenOpenTimerTests.cs`
  (one line per window, off reads no clock, never throws).
- `Clan/`: `ClanScreenFastPathServiceTests`, `ClanFastOpenHookTests` (real Harmony: a postfix on the
  list build runs only for real builds, soft-fail on a wrong site count), `ClanFastOpenBindingTests`
  (the installed IL: one call site, the three-build chain, the header tail, JIT acceptance).
- `Inventory/`: `NameSortRewriteTests`, `InventoryOpenFastPathServiceTests`,
  `ShadowfaxInventoryCallsTests`, `InventoryOpenPatchShapeTests`, `InventoryBindingTests` (both sort
  sites, the re-sort after every build, JIT acceptance).
- `Party/`: `PartyScreenFastPathServiceTests`, `PartyFastOpenPatchShapeTests` (real Harmony pairs),
  `PartyFastOpenBindingTests` (signatures, and that the three `CharacterCode` consumers read only
  `Code`).
- `Quests/`: `QuestLogDeferServiceTests`, `QuestLogDeferCallsTests`, `QuestLogPatchTests` (real
  Harmony, incl. the `int` sort parameter), `QuestJournalAdapterTests` (the attachment check),
  `QuestLogFastOpenBindingTests` (the installed IL and the `JournalLogIndex` values).

## How to make another screen instant

1. Find the movie name: the prefab file name under `Modules/<Module>/GUI/Prefabs`.
2. Confirm that every `VisualDefinition` in it is an animation you want to remove.
3. If TAOM ships its own copy of the prefab, set `TransitionDuration="0"` in that XML and add the
   file to `TaomPrefab_AlreadyInstant_RewriterChangesNothing`.
4. Otherwise add `[PrefabExtension("<Movie>", null)]` to `ShadowfaxScreensPrefab` and the name to
   `PatchedMovies` in the tests.

## Changelog

- 2026-10-19: screen open speed for the clan, inventory and trade, party and quest screens, the
  "Screen Performance" switches and the open timer. Not yet measured in game: each screen's change is
  kept only if the A/B shows a median saving of at least 16 ms.
- 2026-10-18: first version (instant transitions).

## GitHub Issue

- **Issue:** #850: [Instant UI: management screens open without slide animations](https://github.com/haterade22/TAOM/issues/850). **Status:** Open (merged, in-game check owed).
- **Issue:** #851: clan screen fast open. **Status:** Open.
- **Issue:** #852: inventory and trade screen fast open. **Status:** Open.
- **Issue:** #853: party screen fast open. **Status:** Open.
- **Issue:** #854: quest log fast open. **Status:** Open.

---

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->

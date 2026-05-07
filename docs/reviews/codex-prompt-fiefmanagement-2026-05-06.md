# Codex Adversarial Review: FiefManagement (Patch36)

You are Codex performing an INDEPENDENT adversarial review of a feature port. Your goal is to find bugs Claude missed. Be skeptical. Read the code; do not paraphrase.

## Feature description

FiefManagement (Patch36): F6 hotkey on the campaign map opens a `fief_hub` game menu listing the player's owned fiefs in a carousel (Previous/Next/Manage/Leave). "Manage" pushes a custom `FiefManagementGameState`; a Harmony prefix on `GameStateScreenManager.CreateScreen` substitutes `GauntletFiefManagementScreen` which performs a reflection swap on `MobileParty._currentSettlement` to fool the vanilla `TownManagementVM` into building against a remote fief.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

(NOTE: this feature uses NO kingdom or culture IDs — it only filters by `OwnerClan == Clan.PlayerClan`. The cheatsheet is here for completeness in case any new logic appears.)

## READ FIRST

- `docs/features/fief-management.md`
- `Main/Features/FiefManagement/` -- all .cs
- `Main/Adapters/SettlementOwnershipAdapter.cs`, `MapScreenInputAdapter.cs`, `RemoteFiefSettlementSwapper.cs`
- `Main/SubModule.cs` -- behavior + Patch36 wiring at OnGameInitializationFinished
- `Main/IoC.cs` line 86 -- FiefManagementIoC registration
- `Main/Features/TaomSettings.cs` lines 274-287 -- 3 MCM settings under group "Fief Management" GroupOrder=26
- `TAOM.Tests/Features/FiefManagement/FiefHubServiceTests.cs` -- 22 tests

## Known Suspects

CONFIRM or DISPUTE each, with line numbers:

1. **Reflection target lifecycle.** `RemoteFiefSettlementSwapper.Swap` reflects on `MobileParty._currentSettlement` (private). The screen calls Swap, builds vanilla `TownManagementVM` (which reads `Settlement.CurrentSettlement` -- which falls through to `MobileParty.MainParty.CurrentSettlement`), then Restore. Suspect: if `Settlement.CurrentSettlement` does NOT actually fall through to `MobileParty.MainParty.CurrentSettlement` in v1.3.15 (e.g. if it instead reads `Campaign.Current.LastVisitedSettlement` or there's a captor branch the player can be in), the swap is a no-op and the VM builds against the wrong settlement. Verify by reading the v1.3.15 `Settlement.CurrentSettlement` getter implementation.

2. **F6 polling exception swallowing.** `Patch36_MapScreenF6.Postfix` wraps the body in try/catch and logs once-per-process via a `_exceptionLogged` static. Suspect: the once-only flag means a recurring fault (e.g. user disables the menu, re-enables it, gets a different error) is invisible. Is once-per-process the right retention? What happens on session reload?

3. **Game state push without recursion guard.** The `fief_hub_manage` consequence calls `Game.Current.GameStateManager.PushState(new FiefManagementGameState(...))`. Suspect: if the player double-clicks Manage, two states get pushed and the screen substitution patch fires twice. Trace the prefix patch -- does it handle being invoked while a `FiefManagementGameState` is already active?

4. **`_selectedIndex` between save and Clamp.** `_selectedIndex` is reset to 0 in `OnNewGameCreated` and `OnGameLoaded`. Suspect: after a fief is conquered or sold while the menu is OPEN, the index is invalid until the next `OnMenuInit`. The cached `_menuFiefs` snapshot also goes stale. The condition lambdas read from `_menuFiefs.Count` -- is there a path where the player selects an option targeting a stale fief reference and crashes?

5. **PopState pairing on screen close.** `GauntletFiefManagementScreen.OnFrameTick` pops state on Confirm/Exit. Suspect: if the OnInitialize threw mid-construction (e.g. reflection fails, vanilla VM ctor asserts), the screen is stuck in a partially-initialized state. Is there a path where the GameState is pushed but the screen is dead, leaving the player frozen?

6. **Cached menu snapshot vs concurrent campaign events.** `_menuFiefs` is set in OnMenuInit and read by option lambdas every frame. Suspect: if a campaign event (settlement conquered by AI, player loses fief) fires WHILE the menu is open, `_menuFiefs` references stale `Settlement` objects. Pushing a `FiefManagementGameState` with a now-disowned settlement and opening the screen against a fief the player no longer owns -- crash? wrong data? no-op?

7. **Sort stability and case insensitivity.** `CompareByName` uses `StringComparison.OrdinalIgnoreCase`. Suspect: if two fiefs share the same name (rare but possible with mods), the sort order is unstable and Next/Previous could behave unexpectedly. Cosmetic only -- but confirm.

8. **F6 with `EnableFiefManagement=true` but menu unregistered.** If the user toggled `EnableFiefManagement=true` mid-session (no-op since menu is registered once at OnSessionLaunched if the setting was true at the time). Suspect: re-enabling at runtime is silently broken until next save/reload. Match against MCM hint text.

## File lists

C# source (the feature):
- Main/Features/FiefManagement/IFiefHubService.cs
- Main/Features/FiefManagement/FiefHubService.cs
- Main/Features/FiefManagement/IFiefManagementSettingsProvider.cs
- Main/Features/FiefManagement/FiefManagementSettingsProvider.cs
- Main/Features/FiefManagement/FiefManagementIoC.cs
- Main/Features/FiefManagement/Models/FiefSummary.cs
- Main/Features/FiefManagement/Models/FiefManagementGameState.cs
- Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs
- Main/Features/FiefManagement/UI/FiefManagementNavItemVM.cs
- Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs
- Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs
- Main/Features/FiefManagement/Hooks/Patch36_GameStateScreenManager.cs

Adapters:
- Main/Adapters/ISettlementOwnershipAdapter.cs
- Main/Adapters/SettlementOwnershipAdapter.cs
- Main/Adapters/IMapScreenInputAdapter.cs
- Main/Adapters/MapScreenInputAdapter.cs
- Main/Adapters/IRemoteFiefSettlementSwapper.cs
- Main/Adapters/RemoteFiefSettlementSwapper.cs

Wiring:
- Main/IoC.cs line 86
- Main/SubModule.cs (Patch36 in OnGameInitializationFinished; FiefHubCampaignBehavior in OnGameStart)
- Main/Features/TaomSettings.cs lines 274-287

Tests:
- TAOM.Tests/Features/FiefManagement/FiefHubServiceTests.cs

## REQUIRED SECTIONS

### 1. VANILLA CODE — DECOMPILE AND PASTE

Decompile and paste these as code blocks (text only, no ellipsis). Quote the actual method body so we can verify Claude's reading:

a. `Settlement.CurrentSettlement` getter (TaleWorlds.CampaignSystem.Settlements.Settlement)
b. `TownManagementVM` constructor body (TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement.TownManagementVM)
c. `GameStateScreenManager.CreateScreen` (TaleWorlds.MountAndBlade.View.Screens) — full method body
d. `MapScreen.OnFrameTick` signature (SandBox.View.Map)
e. `MobileParty._currentSettlement` field declaration (verify it is still `private Settlement _currentSettlement` with `[SaveableField]`)

For each, ANSWER: does Claude's code interact with this correctly?

### 2. RECURSIVE PUSH SCENARIO

Trace what happens if the player rapidly pushes the manage option (mouse double-click before the screen renders). Read `GameStateManager.PushState`. Does it allow stacking `FiefManagementGameState` on top of itself? Does the prefix substitution fire twice? Does the second screen's reflection-swap collide with the first screen's swap (both swapping `MobileParty._currentSettlement`)? Walk it line by line.

### 3. CONCURRENT FIEF LOSS

Trace: player owns 3 fiefs (A, B, C). Opens fief_hub, cycles to B (index 1). Mid-frame, AI conquers B. Re-opens menu after a tick — GetOrderedFiefs now returns [A, C], count=2. Next OnMenuInit clamps index 1 to index 1, points to C (was B's slot). Player clicks Manage. PushState fires with C's Settlement. Fine? Or does the cached `_menuFiefs` from the PRIOR menu open still hold B?

Answer specifically by reading FiefHubCampaignBehavior.cs lines 90-180.

### 4. REFLECTION SWAP UNDER MULTI-THREAD

Bannerlord's Campaign tick runs on the main thread. `OnFrameTick` and OnInitialize are also main-thread. So the swap-construct-restore sequence is atomic with respect to other Campaign code. CONFIRM this assumption — is there ANY code path (background save, lod loading, async) that could read `MobileParty._currentSettlement` between Swap and Restore?

### 5. CONFIG CROSS-REFERENCE

The 3 MCM settings (`EnableFiefManagement`, `AllowRemoteBuildingQueue`, `FiefManagementDebug`):

a. For each, list every consumer in C# (file:line). If a setting has zero consumers, flag it.
b. Compare each MCM hint text in TaomSettings.cs to actual code behavior. Flag any mismatch.

### 6. FINDINGS OR OBSERVATIONS

Output as:

| # | Severity | File:line | Issue | Fix |
|---|----------|-----------|-------|-----|

Severity: P1 (must fix), P2 (should fix), P3 (nice to have).

If you have NO findings, write "No P1/P2/P3 findings." Don't pad with cosmetic nitpicks.

## QUALITY GATES

- Did you decompile every vanilla target listed in section 1?
- Did you read FiefHubCampaignBehavior.cs in its entirety (175+ lines)?
- Did you trace at least 3 of the 8 known suspects through actual source code (not assumed)?
- Did you cross-reference all 3 MCM settings to consumers?

If you skipped any: state which and why.

## Prior review lessons

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.

FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output to: `docs/reviews/codex-adversarial-fiefmanagement-2026-05-06.md`

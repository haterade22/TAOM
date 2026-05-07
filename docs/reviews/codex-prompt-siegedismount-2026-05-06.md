# Codex Adversarial Review: SiegeDismount

## Feature Description

SiegeDismount auto-handles the player main hero's mount during siege missions. On `MissionBehavior.OnBehaviorInitialize`, the feature reads `Mission.Current.IsSiegeBattle` and `Mission.Current.SceneName`. If a siege is detected and the user's MCM-selected mode is non-Vanilla, the feature captures the mount + harness from `Hero.MainHero.BattleEquipment[Horse|HorseHarness]`, optionally clears those slots and deposits items into `MobileParty.MainParty.ItemRoster`. On `OnEndMission`, if AutoRemount mode was active, the slots are restored and the items withdrawn from inventory.

This is a port of an external developer's prebuilt module ([SiegeDismount.dll](Downloads/Features_fixed/SiegeDismount/bin/Win64_Shipping_Client/SiegeDismount.dll), decompiled at [Downloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs](Downloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs)) into TAOM's Main/Features/ adapter/service/IoC pattern.

The feature has NO Harmony patches and NO GameModel overrides. It is a pure MissionBehavior. Verify this assumption.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- Main/Features/SiegeDismount/SiegeDismountService.cs -- core state machine
- Main/Features/SiegeDismount/SiegeDismountSettingsProvider.cs -- MCM settings wrapper
- Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs -- engine bridge
- Main/Features/SiegeDismount/Models/IMountSnapshot.cs -- opaque token across adapter boundary
- Main/Features/SiegeDismount/Models/MountSnapshot.cs -- token impl
- Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs -- enum 0..3
- Main/Features/SiegeDismount/SiegeDismountIoC.cs -- DI registrations
- Main/Adapters/IPlayerMountAdapter.cs + PlayerMountAdapter.cs -- wraps Hero.MainHero.BattleEquipment
- Main/Adapters/IPartyMountInventoryAdapter.cs + PartyMountInventoryAdapter.cs -- wraps MainParty.ItemRoster
- Main/Features/TaomSettings.cs -- the appended Battle Tactics/Siege Dismount group at the bottom (3 settings)
- Main/IoC.cs -- look for SiegeDismountIoC.RegisterSiegeDismountFeature
- Main/SubModule.cs -- look for `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` in OnMissionBehaviorInitialize
- TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs -- 33 unit tests
- docs/features/siege-dismount.md -- feature doc

ORIGINAL DECOMPILED SOURCE (for behavior parity check):
- Downloads/Features_fixed/_decompiled/SiegeDismount/SiegeDismount.decompiled.cs

## KNOWN SUSPECTS

Several of these were caught and fixed during a prior /deep-review pass. Confirm the fixes are correct OR find new ways the same class of bug could occur.

1. ALREADY-FIXED -- OUT-OF-RANGE MOUNT BEHAVIOR. The MCM int field SiegeMountBehavior is range [0, 3] mapping to enum SiegeMountBehaviorType. If a user manually edits `ModuleData/MCM/Global/TAOM.json` to set SiegeMountBehavior to e.g. 99, the cast `(SiegeMountBehaviorType)99` produces an undefined enum value. The fix added a `default:` case to the switch in SiegeDismountService.OnMissionStart that logs LogWarning and is a full no-op (no Capture, no Clear, no Deposit, no _capturedSnapshot retained, no _pendingRemount set). CONFIRM the fix is at SiegeDismountService.cs around line 80-90 and that it covers ALL invalid integer paths -- including negative values (e.g., -1) which would also produce an undefined enum but might numerically compare equal to an existing value through bit-pattern coincidence. DISPUTE if there's a path that bypasses the default.

2. ALREADY-FIXED -- FALSE-POSITIVE SIEGE DETECTION ON TAOM CASTLES. The keyword fallback in SiegeDismountService.IsSiegeMission used to include "gate" and "wall" which substring-matched real TAOM castle scene names (`castle_orthanc_gate` at custom_settlements.xml:74, `castle_gundabad_wall` at custom_settlements.xml:344). Visiting either castle in a non-siege mission would have clobbered the player's mount. The fix narrowed SceneSiegeKeywords to ["siege", "assault", "breach"] only. CONFIRM that the new keyword list does not match ANY scene name used as `Location id="center"` or any other Mission scene in: Main/_Module/ModuleData/custom_settlements.xml, Main/_Module/ModuleData/settlements.xml, Main/_Module/ModuleData/scenes.xml (if it exists), or any other ModuleData XML file that defines scene names. Specifically grep for `scene_name=".*siege.*"`, `scene_name=".*assault.*"`, `scene_name=".*breach.*"` and report any non-siege Mission contexts using those scenes (e.g., a tavern scene called "the_breach", a quest scene with "assault" in the name).

3. ALREADY-FIXED -- HASMOUNT/CAPTURE INCONSISTENCY. There is a defensive guard: if `_mount.HasMount()` returns true but `_mount.Capture()` returns an empty IMountSnapshot, the service logs a warning and skips dismount. CONFIRM this guard is present and correctly placed BEFORE any Clear/Deposit/Restore call. DISPUTE if there's a code path where an empty snapshot still reaches Clear() or Deposit().

4. ALREADY-FIXED -- STATE HYGIENE ON OnMissionEnd EARLY RETURN. After `DismountKeepOnMap` or `DismountToInventory` modes, `_pendingRemount` is false, so OnMissionEnd returns early. The fix clears `_capturedSnapshot = null` even on the early-return path so the singleton doesn't carry stale mount-id strings between missions. CONFIRM the early-return path explicitly nulls _capturedSnapshot.

5. NEW SUSPECT -- ITEMMODIFIER LOSS ON ROUND TRIP. The IMountSnapshot interface stores only `MountItemId` and `HarnessItemId` strings. The vanilla EquipmentElement carries an ItemModifier (durability, quality bonus, name prefix). On AutoRemountAfter mode, the player's "Sharp" or "Damaged" horse is captured as a string ID, then restored as a fresh `new EquipmentElement(item)` with no modifier. The feature doc documents this as a known limitation. VERIFY this is the actual behavior (i.e., the modifier IS lost on remount), or DISPUTE if the modifier is actually preserved through some path I missed. If confirmed, evaluate whether this is acceptable for the TAOM use case (LOTR-themed sieges where horse modifiers are rare) or whether it warrants an upgrade path.

6. NEW SUSPECT -- MULTIPLAYER / OFFLINE-PROFILE EDGE CASES. The feature uses `Hero.MainHero` directly. In some Bannerlord modes (CustomBattle, MainMenuBackgroundBehavior, ShaderPrecompilationGameManager), `Hero.MainHero` may be null OR the "main hero" may not be the player. Trace what happens if SiegeDismountMissionBehavior is added to a custom-battle mission (TAOM has CustomBattle integration via CustomBattles feature), and confirm the early-return paths in PlayerMountAdapter (`if (equipment == null) return false`) or `Hero.MainHero == null` produce a no-op rather than a crash.

7. NEW SUSPECT -- SAVE-DURING-SIEGE SCENARIO. The feature is a singleton (Reuse.Singleton in SiegeDismountIoC). It is NOT a CampaignBehaviorBase and does NOT use SyncData. If a player's save process captures equipment state mid-siege (after Clear() but before mission end), then exits and reloads, the mount is in MainParty.ItemRoster but the singleton's `_capturedSnapshot` and `_pendingRemount` are null/false on reload. OnMissionEnd from the reloaded session is then a no-op. The doc states this is "acceptable, matches vanilla's handling of transient mission state." CONFIRM this is actually reachable (does Bannerlord allow saving mid-siege?) or DISPUTE that the documented limitation is real.

8. NEW SUSPECT -- BEHAVIOR REGISTRATION SCOPE. In Main/SubModule.cs, the line `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` is added to OnMissionBehaviorInitialize alongside AdvancedCombatBehavior, BehaviorTreeMissionLogic, AutonomousMovementPlayerController, WargMissionBehavior, SpiderMissionBehavior. ALL missions get the SiegeDismount behavior, including non-combat missions (CC menu, conversation scenes, hideouts, tournaments). VERIFY the behavior's OnBehaviorInitialize correctly no-ops on non-siege missions (it should -- IsSiegeMission returns false unless siege flag or specific scene-name keyword). DISPUTE if there's a Mission context where IsSiegeMission could spuriously return true.

## FILES TO REVIEW

### New Service / Adapter Files

- Main/Features/SiegeDismount/Models/SiegeMountBehaviorType.cs
- Main/Features/SiegeDismount/Models/IMountSnapshot.cs
- Main/Features/SiegeDismount/Models/MountSnapshot.cs
- Main/Features/SiegeDismount/ISiegeDismountService.cs
- Main/Features/SiegeDismount/SiegeDismountService.cs
- Main/Features/SiegeDismount/ISiegeDismountSettingsProvider.cs
- Main/Features/SiegeDismount/SiegeDismountSettingsProvider.cs
- Main/Features/SiegeDismount/SiegeDismountIoC.cs
- Main/Features/SiegeDismount/Hooks/SiegeDismountMissionBehavior.cs
- Main/Adapters/IPlayerMountAdapter.cs
- Main/Adapters/PlayerMountAdapter.cs
- Main/Adapters/IPartyMountInventoryAdapter.cs
- Main/Adapters/PartyMountInventoryAdapter.cs

### Modified Files (review only the SiegeDismount additions)

- Main/Features/TaomSettings.cs (Battle Tactics/Siege Dismount group at the bottom)
- Main/IoC.cs (the line `SiegeDismountIoC.RegisterSiegeDismountFeature(container)` and the using import)
- Main/SubModule.cs (the line `mission.AddMissionBehavior(new SiegeDismountMissionBehavior())` and the using import)

### Test Files

- TAOM.Tests/Features/SiegeDismount/SiegeDismountServiceTests.cs

### Source-of-Truth XML

Use these to verify GAP 2's regression test is sufficient:

- Main/_Module/ModuleData/custom_settlements.xml -- TAOM custom castles, includes `castle_orthanc_gate`, `castle_gundabad_wall`
- Main/_Module/ModuleData/settlements.xml -- 658 settlements, includes vanilla scenes like `empire_siege_001`, `khuzait_castle_siege_001`, `sturgia_castle_siege_001`

### Vanilla Decompilation Targets

This feature has NO Harmony patches and NO GameModel overrides. The vanilla types it uses are accessed only through adapters. Verify the adapter API usage against installed v1.3.15 DLLs:

- TaleWorlds.CampaignSystem.Hero -- MainHero, BattleEquipment
- TaleWorlds.Core.Equipment -- this[EquipmentIndex] indexer (getter AND setter), constructor
- TaleWorlds.Core.EquipmentElement -- IsEmpty, Item, constructor, EquipmentElement.Invalid
- TaleWorlds.Core.EquipmentIndex -- enum values Horse=10, HorseHarness=11
- TaleWorlds.Core.ItemObject -- StringId
- TaleWorlds.ObjectSystem.MBObjectManager -- Instance, GetObject<T>(string)
- TaleWorlds.CampaignSystem.Party.MobileParty -- MainParty, ItemRoster
- TaleWorlds.CampaignSystem.Roster.ItemRoster -- AddToCounts(ItemObject, int)
- TaleWorlds.MountAndBlade.MissionBehavior -- OnBehaviorInitialize (virtual public), OnEndMission (virtual PROTECTED -- access modifier matters)
- TaleWorlds.MountAndBlade.MissionBehaviorType -- Logic enum value
- TaleWorlds.MountAndBlade.Mission -- Current, IsSiegeBattle, SceneName

Use: ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/<dll>" -t "Full.Type.Name"

Critical: confirm `MissionBehavior.OnEndMission` is `protected virtual void` (not public). The TAOM port uses `protected override void OnEndMission()` -- if the access modifier is wrong, the override is not actually wired.

## REQUIRED SECTIONS

### VANILLA CODE

Decompile from the installed v1.3.15 DLLs and paste as code blocks:

- Equipment.this[EquipmentIndex] (getter AND setter source)
- EquipmentElement (full struct with all fields and the IsEmpty / Item / Invalid members)
- ItemRoster.AddToCounts (full method body, especially the negative-count behavior)
- MissionBehavior.OnBehaviorInitialize signature
- MissionBehavior.OnEndMission signature (verify protected)
- Mission.IsSiegeBattle property body
- MBObjectManager.GetObject<T>(string) signature

### SCENE-NAME KEYWORD CROSS-REFERENCE

Grep all of these for any scene_name attribute matching the substrings "siege", "assault", or "breach":

- Main/_Module/ModuleData/*.xml (all)

For each match, report:
- The settlement / location ID
- The scene_name value
- Whether the scene is loaded as a Mission with `Mission.IsSiegeBattle = true` (siege battle) or `false` (some other Mission like a quest, tournament, tavern, hideout)

If any scene with those keywords is loaded as a non-siege Mission, that's a false-positive risk for the feature -- flag it.

### STATE MACHINE TRACE

Walk every state transition of the SiegeDismountService through every behavior mode. For each transition, list:

- Initial state of (`_capturedSnapshot`, `_pendingRemount`)
- Method called (OnMissionStart, OnMissionEnd) and parameters
- Final state
- Any side effects (Clear, Deposit, Restore, Withdraw)
- Any logs emitted (LogInfo / LogDebug / LogWarning / LogError)

Verify that no transition leaks state, no transition fires the wrong adapter call, and no transition double-fires Restore or Withdraw on a single capture.

### TAOM CONVENTION CHECK

- Does the service use constructor injection only? (No IoC.Resolve outside the MissionBehavior boundary class.)
- Does the service expose an interface? (Yes, ISiegeDismountService.)
- Does the adapter expose an interface? (Yes, IPlayerMountAdapter, IPartyMountInventoryAdapter.)
- Are TaleWorlds sealed types confined to the adapter? (Service must never see Hero, Equipment, EquipmentElement, EquipmentIndex, ItemObject, MobileParty, ItemRoster, MBObjectManager, Mission.)
- Is the MissionBehavior under 150 lines? (ADR-002.)
- Are MCM settings folded into TaomSettings.cs (not a per-feature settings class)?
- Does the Enable* MCM toggle gate the entire service entry point at the FIRST line?
- Is there a single `LogInfo("[SiegeDismount] disabled via MCM -- patches inert")` at the disable path?

### LOGGING CONTRACT VERIFICATION

The integration plan ([C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md](C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md)) mandates:

- LogInfo on feature load, mission init, mission end, MCM toggle change
- LogDebug for per-mode decisions, gated by a per-feature DebugMode MCM bool
- LogWarning on lifecycle anomalies, reflection misses, fallback paths
- LogError on caught exceptions, ALWAYS with the exception message

VERIFY each severity level appears in the right places. DISPUTE any catch block that swallows the exception silently or does not include the exception message.

### FINDINGS OR OBSERVATIONS

Group by severity: CRITICAL / HIGH / MEDIUM / LOW / INFO.

For each finding, provide: file:line, what's wrong, what to change, why.

## QUALITY GATES

- Did you decompile vanilla types from installed v1.3.15 DLLs (NOT E:\Decompiled_Bannerlord -- that is v1.4)?
- Did you paste code blocks from both TAOM source and vanilla decompiled source?
- Did you grep ALL ModuleData XML files for scene names matching the new keyword list?
- Did you trace every state transition in the SiegeDismountService state machine?
- Did you verify each Known Suspect with explicit CONFIRMED / DISPUTED + evidence?
- Did you check what happens if SiegeDismountMissionBehavior is added to a non-siege mission (conversation, tournament, tavern)?
- Section N skips any suspect or says "could not verify" -- engage with each.

## PRIOR REVIEW LESSONS

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Data flow review caught castle_orthanc_gate / castle_gundabad_wall false-positive (this review's fix).

FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex missed sentinel-vs-terminal collision in shader-precompilation polling.

## OUTPUT TO

docs/reviews/codex-adversarial-siegedismount-2026-05-06.md

# Adversarial review: the settlement-encounter invariant fix (TAOM issue #510)

You are reviewing a bug fix in TAOM, a Mount and Blade II Bannerlord total-conversion mod, running on Bannerlord v1.4.8. Be adversarial. Assume the fix is wrong somewhere and prove it. Do not praise the code. Findings only.

## What the bug was

A player crash bundle (signature d7d9f7d3, TAOM v2.0.20.0, Bannerlord v1.4.8.119303) is a NullReferenceException in vanilla PlayerTownVisitCampaignBehavior.game_menu_settlement_wait_on_init, thrown when the player clicked "Wait here for some time" on a town menu 14 seconds after requesting discharge from the enlistment feature.

Root cause: TAOM put the main party inside a settlement using EnterSettlementAction.ApplyForParty alone, which creates neither PlayerEncounter.Current nor Campaign.LocationEncounter. Vanilla never produces that state. Vanilla settlement menus dereference both unguarded. The invalid state was masked by TAOM's RedirectMenuIds swallowing the town/castle/village menus, and two paths removed the mask: the discharge (which clears the enlistment record to NotEnlisted, and the redirect is gated on EnlistedAttached, before placing the player) and shore leave (which releases those menus on purpose).

## What the fix does

Adds one adapter chokepoint, IEncounterAdapter.EnsureSettlementEncounter, which builds both objects following vanilla's own recipe. Routes the discharge release and the shore-leave grant through it; both treat a false return as "do not open a vanilla menu". Adds EncounterFinishIntent.ShoreLeaveEnd (rule R2b) so revoking a pass tears its encounter down, because EncounterOwnershipPolicy R3 deliberately never closes a settlement-shaped encounter. Adds an IL-scanning ban test.

## READ FIRST

- docs/features/enlistment.md -- the whole file, especially "The settlement-encounter invariant (#510)" and "Design decisions worth not re-litigating"
- docs/reviews/rca-settlement-encounter-2026-08-24.md -- the RCA for this fix, including what a prior 5-agent review already found
- docs/reviews/lessons/state-lifecycle-save.md -- the last entry
- docs/reviews/raw/i510-diff.txt -- the complete changeset (diff plus the two new files in full)

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID -- Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## Files changed

Production:
- Main/Adapters/IEncounterAdapter.cs -- new EnsureSettlementEncounter declaration
- Main/Adapters/EncounterAdapter.cs -- the chokepoint implementation plus a private CreateLocationEncounter
- Main/Features/Enlistment/DischargeService.cs -- site A routing, and the post-check verdict now requires HasPlayerEncounter
- Main/Features/Enlistment/EnlistmentPlayerActionService.cs -- site B routing, new IEncounterAdapter dependency
- Main/Features/Enlistment/ServiceMaintenanceService.cs -- shore-leave revoke teardown, new IEncounterAdapter and IEncounterOwnershipPolicy dependencies
- Main/Features/Enlistment/EncounterOwnershipPolicy.cs -- new rule R2b
- Main/Features/Enlistment/Domain/EncounterFinishIntent.cs -- new ShoreLeaveEnd intent
- Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs -- refusal toast
- Main/_Module/ModuleData/taom_enlistment_strings.xml plus 12 language files -- new taom_enlist_leave_refused key

Tests:
- TAOM.Tests/Migration/IlCallScanner.cs -- NEW, IL walker extracted from PartyOwnerGetterBanTests
- TAOM.Tests/Features/Enlistment/SettlementEncounterInvariantTests.cs -- NEW, the ban test
- TAOM.Tests/Features/BattleBalance/PartyOwnerGetterBanTests.cs -- rewired onto the shared scanner
- Four enlistment test files updated

## KNOWN SUSPECTS -- CONFIRM or DISPUTE each, with code

### S1 (highest priority): the ban test may have no teeth

TAOM.Tests/Features/Enlistment/SettlementEncounterInvariantTests.cs bans callers of MoveIntoSettlement. It resolves the banned method as typeof(IMobilePartyAttachmentAdapter).GetMethod("MoveIntoSettlement") -- the INTERFACE method. IlCallScanner.SameMethod matches on `candidate.Name == banned.Name && candidate.DeclaringType == banned.DeclaringType`.

Hypothesis: a caller that holds the CONCRETE MobilePartyAttachmentAdapter rather than the interface emits a call whose DeclaringType is MobilePartyAttachmentAdapter, not IMobilePartyAttachmentAdapter, so SameMethod returns false and the ban is silently evaded. If true, the gate this whole fix relies on to prevent recurrence is bypassable by a one-word type change at a call site.

Also determine whether the allow-list entry "TAOM.Adapters.MobilePartyAttachmentAdapter.MoveIntoSettlement" is DEAD -- that method's own body calls EnterSettlementAction.ApplyForParty, not itself, so it should never appear as a caller of the banned interface method. If it is dead, say so; a dead allow-list entry is a comment claiming a protection that is not being exercised.

State exactly what a caller would have to do to evade the ban, and what the fix is.

### S2: what PlayerEncounter.Init sets that SetupFields does not

The chokepoint calls PlayerEncounter.Start() then the public SetupFields(PartyBase.MainParty, settlement.Party), deliberately avoiding EncounterManager.StartSettlementEncounter (which reaches the internal Init). The stated reason is that Init unconditionally calls EnterSettlement() for a settlement defender, which re-runs EnterSettlementAction.ApplyForParty on an already-inside party and re-dispatches OnSettlementEntered -- a duty-completion trigger in this feature.

Enumerate EVERY field Init sets that SetupFields does not. For each, determine whether any code path reachable from the town, castle, village, town_wait_menus or village_wait_menus menus reads it. Pay specific attention to FirstInit, IsPlayerWaiting, PlayerPartyInitialStrength, EnemySurrender, InterruptedWhileLooting, InterruptedWhileWaiting, IsNavalEncounterFinishedWithDisengage, the Force* flags, _isSallyOutAmbush, and _mapEvent. A freshly constructed PlayerEncounter from Start() has C# defaults for all of them -- say whether any default is wrong for a peaceful settlement visit.

### S3: the ShoreLeaveEnd race against the battle path

ServiceMaintenanceService revokes shore leave whenever TownLeavePolicy.ShouldRevokeLeave fires, which is `onLeave && !(state == EnlistedAttached && insideSettlement)`. That includes "a battle started". The revoke evaluates EncounterOwnershipPolicy with the new ShoreLeaveEnd intent; R1 defers only when the player is ALREADY in a map event.

Hypothesis: there is a window where the enlistment state has flipped to EnlistedBattle (set from a MapEventStarted handler) but the player is not yet in the map event and the live encounter is still settlement-shaped, so R2b returns Finish and the revoke destroys an encounter ServiceBattleService is about to need or has just seeded. Trace ServiceBattleService and EnlistmentBattleBehavior and say whether the window is real, and what the consequence is. Note that MapEventManager.Tick skips the player's own map event, so an encounter destroyed at the wrong moment can freeze a battle permanently.

### S4: the refuse-to-repoint branch at the discharge site

EncounterAdapter.EnsureSettlementEncounter returns false when PlayerEncounter.Current is non-null and PlayerEncounter.EncounterSettlement is not the target settlement. Note EncounterSettlement is `Current?.EncounterSettlementAux`, which is NULL for a party encounter -- so a live party encounter also takes this branch.

DischargeService step 7 can deliberately leave an encounter alive (verdict SkipConversationInProgress). Step 10 then calls EnsureSettlementEncounter, which refuses, and the failure branch walks the player OUT of the settlement. Determine whether that is the intended outcome or a behavioural regression against the pre-fix code, which called MoveIntoSettlement and would have succeeded. Consider whether a player discharged mid-conversation ends up somewhere worse than before.

### S5: save and reload

Campaign.LocationEncounter is [CachedData], not saved. Campaign.PlayerEncounter is [SaveableProperty(54)] and EncounterSettlementAux is [SaveableProperty(28)]. SandBoxGameManager.OnLoadFinished calls PlayerEncounter.Current.OnLoad() only when MapState.GameMenuId is non-empty AND GameMenuManager.GetGameMenu(id) resolves; otherwise it calls PlayerEncounter.Finish(true) instead, or does nothing at all when the id is empty.

Find any reachable state in this feature where the player is inside a settlement with a live encounter and MapState.GameMenuId is empty or names a menu that does not resolve. The TAOM wait menu id is taom_enlistment_service_wait. Consider the frame between EnsureSettlementEncounter succeeding and EnsureMenuOpen running, autosave timing, and quit-to-menu.

### S6: Finish(presence.IsInSettlement)

The revoke now passes forcePlayerOutFromSettlement: presence.IsInSettlement, where presence was read earlier in the same Pump pass. Vanilla gates the LeaveSettlement call as `if (InsideSettlement && MobileParty.MainParty.AttachedTo == null && forcePlayerOutFromSettlement)`. Determine whether passing true is correct when AttachedTo is non-null (the player is merged into an army), and whether the presence read can be stale by the time Finish runs. Say what happens if the player is inside a settlement AND attached: Finish stops time and calls ExitToLast but does NOT leave the settlement.

## VANILLA CODE -- verify all of this against the INSTALLED v1.4.8 DLLs yourself, do not trust these pastes

The crashing method, PlayerTownVisitCampaignBehavior:

```csharp
private void game_menu_settlement_wait_on_init(MenuCallbackArgs args)
{
    string text = (PlayerEncounter.EncounterSettlement.IsVillage ? "village" : (PlayerEncounter.EncounterSettlement.IsTown ? "town" : (PlayerEncounter.EncounterSettlement.IsCastle ? "castle" : null)));
    if (text != null) { UpdateMenuLocations(text); }
    if (PlayerEncounter.Current != null) { PlayerEncounter.Current.IsPlayerWaiting = true; }
    MobileParty.MainParty.SetMoveModeHold();
}
```

PlayerEncounter:

```csharp
public static Settlement EncounterSettlement => Current?.EncounterSettlementAux;
public static PlayerEncounter Current => Campaign.Current.PlayerEncounter;
public static void Start() { Campaign.Current.PlayerEncounter = new PlayerEncounter(); }

public static void EnterSettlement()
{
    Settlement encounterSettlement = EncounterSettlement;
    CreateLocationEncounter(encounterSettlement);
    EnterSettlementAction.ApplyForParty(MobileParty.MainParty, encounterSettlement);
}

private static void CreateLocationEncounter(Settlement settlement)
{
    if (settlement.IsTown) { LocationEncounter = new TownEncounter(settlement); }
    else if (settlement.IsVillage) { LocationEncounter = new VillageEncounter(settlement); }
    else if (settlement.IsCastle) { LocationEncounter = new CastleEncounter(settlement); }
    else if (settlement.IsHideout) { LocationEncounter = new HideoutEncounter(settlement); }
}

public void OnLoad()
{
    if (InsideSettlement && Battle == null) { CreateLocationEncounter(MobileParty.MainParty.CurrentSettlement); }
    else if (Current != null && EncounterSettlement != null && EncounterSettlement.IsVillage && Current.IsPlayerWaiting) { CreateLocationEncounter(EncounterSettlementAux); }
    ...
}
```

PlayerEncounter.Finish, the two parts that matter:

```csharp
if (MobileParty.MainParty.Army == null || MobileParty.MainParty.Army.LeaderParty == EncounteredMobileParty)
{
    Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;
}
if (Campaign.Current.CurrentMenuContext != null) { GameMenu.ExitToLast(); }
else { Campaign.Current.MapStateData.GameMenuId = null; }
...
if (InsideSettlement && MobileParty.MainParty.AttachedTo == null && forcePlayerOutFromSettlement) { LeaveSettlement(); }
```

PlayerEncounter.Init, the settlement branch:

```csharp
if (attackerParty == PartyBase.MainParty && defenderParty.IsSettlement && !defenderParty.Settlement.IsUnderRaid && !defenderParty.Settlement.IsUnderSiege)
{
    EnterSettlement();
}
GameMenu.ActivateGameMenu(encounterMenu);
```

Campaign.Tick, the per-tick generic menu push:

```csharp
if (Game.Current.GameStateManager.ActiveState is MapState { AtMenu: false })
{
    string genericStateMenu = Models.EncounterGameMenuModel.GetGenericStateMenu();
    if (!string.IsNullOrEmpty(genericStateMenu)) { GameMenu.ActivateGameMenu(genericStateMenu); }
}
```

## DEEP ANALYSIS REQUIRED

1. Walk EnsureSettlementEncounter line by line against installed v1.4.8 and find any state in which it returns TRUE while leaving something a vanilla settlement menu reads unset or wrong. Returning true wrongly is far worse than returning false wrongly, because both callers then open a vanilla menu.

2. Walk EncounterOwnershipPolicy with the new R2b inserted. Produce the full intent-by-snapshot verdict matrix and identify any cell where the new rule changed an existing intent's verdict. R2b must be inert for all five pre-existing intents.

3. The prior 5-agent review deferred one HIGH finding to issue #511: the redirect mask is gated on EnlistedAttached alone, so it disarms in CommanderUnavailable while the player is inside a settlement, and Campaign.Tick then force-pushes town_outside. Judge whether deferring rather than fixing was correct, and whether the fix as shipped makes that state MORE or LESS likely to be hit than before.

4. Check the two new tests actually fail against the pre-fix code. For each new test in the diff, state what it asserts and whether reverting the corresponding production change would make it red. A test that passes both ways is worthless.

5. CONFIG CROSS-REFERENCE: check the new localization key taom_enlist_leave_refused is registered in Main/_Module/ModuleData/taom_enlistment_strings.xml with the {=KEY}default form, and present in all 12 files under Main/_Module/ModuleData/Languages/. Note the language files were seeded with English text pending a translator run -- that is known and recorded, do not report it as a finding. Do report any malformed row, wrong id, or missing file.

## QUALITY GATES

- Every finding needs a file path, a line number, and the code that proves it.
- Decompile the installed v1.4.8 DLLs at "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/" for every claim about vanilla. The dump at E:\Decompiled_Bannerlord\ may lag.
- If you cannot verify something, say UNVERIFIED. Do not guess.
- Rate each finding P1 (ship blocker), P2 (should fix), P3 (nice to have).
- Answer every Known Suspect explicitly as CONFIRMED or DISPUTED. Do not skip S1.

## Lessons from prior reviews

SUCCESSES: config ID cross-reference caught rohan and dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches.
FAILURES: Codex has previously assumed empire=Rohan (it is Dunland); flagged vanilla-matching code as bugs; and skipped the hardest section of the prompt. Do not skip S1 or S2.

## OUTPUT

A findings list, most severe first, then explicit CONFIRMED/DISPUTED verdicts for S1 through S6, then the R2b verdict matrix from item 2.

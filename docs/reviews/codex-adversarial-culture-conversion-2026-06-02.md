# Codex Adversarial Review - CultureConversion - 2026-06-02

Scope: `Main/Features/CultureConversion`, recruitment integration, MCM/config wiring, save/load behavior, and named cross-feature interactions. Vanilla evidence read from `E:\Decompiled_Bannerlord` for Bannerlord v1.4.5.

## 1. VANILLA CODE

### ChangeOwnerOfSettlementAction.ApplyInternal

Verified event ordering: `settlement.Town.OwnerClan = newOwner.Clan` executes before `CampaignEventDispatcher.Instance.OnSettlementOwnerChanged(...)`. Therefore TAOM's event handler reads the new owner's culture, not the old owner's culture.

```csharp
private static void ApplyInternal(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
{
    Hero oldOwner = settlement.OwnerClan?.Leader;
    if (settlement.Town != null)
    {
        settlement.Town.IsOwnerUnassigned = false;
    }
    if (settlement.IsFortification)
    {
        settlement.Town.OwnerClan = newOwner.Clan;
    }
    if (settlement.IsFortification)
    {
        if (detail == ChangeOwnerOfSettlementDetail.BySiege && settlement.Town.GarrisonParty != null)
        {
            DestroyPartyAction.Apply(capturerHero.PartyBelongedTo.Party, settlement.Town.GarrisonParty);
        }
        if (settlement.Town.GarrisonParty == null)
        {
            settlement.AddGarrisonParty();
        }
        ChangeGovernorAction.RemoveGovernorOfIfExists(settlement.Town);
    }
    settlement.Party.SetVisualAsDirty();
    foreach (Village boundVillage in settlement.BoundVillages)
    {
        boundVillage.Settlement.Party.SetVisualAsDirty();
        if (boundVillage.VillagerPartyComponent == null || newOwner == null)
        {
            continue;
        }
        foreach (MobileParty item in MobileParty.All)
        {
            if (item.MapEvent == null && item != MobileParty.MainParty && item.ShortTermTargetParty == boundVillage.VillagerPartyComponent.MobileParty && !item.MapFaction.IsAtWarWith(newOwner.MapFaction))
            {
                item.SetMoveModeHold();
            }
        }
    }
    bool openToClaim = (detail == ChangeOwnerOfSettlementDetail.BySiege || detail == ChangeOwnerOfSettlementDetail.ByClanDestruction || detail == ChangeOwnerOfSettlementDetail.ByLeaveFaction) && settlement.IsFortification;
    if (newOwner != null)
    {
        IFaction mapFaction = newOwner.MapFaction;
        if (settlement.Party.MapEvent != null && !settlement.Party.MapEvent.AttackerSide.LeaderParty.MapFaction.IsAtWarWith(mapFaction) && settlement.Party.MapEvent.Winner == null)
        {
            settlement.Party.MapEvent.DiplomaticallyFinished = true;
            foreach (WarPartyComponent warPartyComponent in settlement.MapFaction.WarPartyComponents)
            {
                MobileParty mobileParty = warPartyComponent.MobileParty;
                if (mobileParty.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty.TargetSettlement == settlement && mobileParty.CurrentSettlement == null)
                {
                    mobileParty.SetMoveModeHold();
                }
            }
            settlement.Party.MapEvent.Update();
        }
        foreach (Clan nonBanditFaction in Clan.NonBanditFactions)
        {
            if (mapFaction != null && (nonBanditFaction.Kingdom != null || nonBanditFaction.IsAtWarWith(mapFaction)) && (nonBanditFaction.Kingdom == null || nonBanditFaction.Kingdom.IsAtWarWith(mapFaction)))
            {
                continue;
            }
            foreach (WarPartyComponent warPartyComponent2 in nonBanditFaction.WarPartyComponents)
            {
                MobileParty mobileParty2 = warPartyComponent2.MobileParty;
                if (mobileParty2.BesiegedSettlement != settlement && (mobileParty2.DefaultBehavior == AiBehavior.RaidSettlement || mobileParty2.DefaultBehavior == AiBehavior.BesiegeSettlement || mobileParty2.DefaultBehavior == AiBehavior.AssaultSettlement) && mobileParty2.TargetSettlement == settlement)
                {
                    mobileParty2.Army?.FinishArmyObjective();
                    mobileParty2.SetMoveModeHold();
                }
            }
        }
    }
    CampaignEventDispatcher.Instance.OnSettlementOwnerChanged(settlement, openToClaim, newOwner, oldOwner, capturerHero, detail);
}
```

Line anchors: `ChangeOwnerOfSettlementAction.cs:28-31` sets owner; `ChangeOwnerOfSettlementAction.cs:94` dispatches event.

### RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement

Verified refill behavior: castles return immediately because `IsTown=false` and `IsVillage=false`; towns and villages fill only null slots and otherwise upgrade the existing troop inside its own tree. No other branch rerolls a populated base troop.

```csharp
private void UpdateVolunteersOfNotablesInSettlement(Settlement settlement)
{
    if ((!settlement.IsTown || settlement.Town.InRebelliousState) && (!settlement.IsVillage || settlement.Village.Bound.Town.InRebelliousState))
    {
        return;
    }
    foreach (Hero notable in settlement.Notables)
    {
        if (!notable.CanHaveRecruits || !notable.IsAlive)
        {
            continue;
        }
        bool flag = false;
        CharacterObject basicVolunteer = Campaign.Current.Models.VolunteerModel.GetBasicVolunteer(notable);
        for (int i = 0; i < 6; i++)
        {
            if (!(MBRandom.RandomFloat < Campaign.Current.Models.VolunteerModel.GetDailyVolunteerProductionProbability(notable, i, settlement)))
            {
                continue;
            }
            CharacterObject characterObject = notable.VolunteerTypes[i];
            if (characterObject == null)
            {
                notable.VolunteerTypes[i] = basicVolunteer;
                flag = true;
            }
            else if (characterObject.UpgradeTargets.Length != 0 && characterObject.Tier < Campaign.Current.Models.VolunteerModel.MaxVolunteerTier)
            {
                float num = MathF.Log(notable.Power / (float)characterObject.Tier, 2f) * 0.01f;
                if (MBRandom.RandomFloat < num)
                {
                    notable.VolunteerTypes[i] = characterObject.UpgradeTargets[MBRandom.RandomInt(characterObject.UpgradeTargets.Length)];
                    flag = true;
                }
            }
        }
        if (!flag)
        {
            continue;
        }
        CharacterObject[] volunteerTypes = notable.VolunteerTypes;
        for (int j = 1; j < 6; j++)
        {
            CharacterObject characterObject2 = volunteerTypes[j];
            if (characterObject2 == null)
            {
                continue;
            }
            int num2 = 0;
            int num3 = j - 1;
            CharacterObject characterObject3 = volunteerTypes[num3];
            while (num3 >= 0 && (characterObject3 == null || (float)characterObject2.Level + (characterObject2.IsMounted ? 0.5f : 0f) < (float)characterObject3.Level + (characterObject3.IsMounted ? 0.5f : 0f)))
            {
                if (characterObject3 == null)
                {
                    num3--;
                    num2++;
                    if (num3 >= 0)
                    {
                        characterObject3 = volunteerTypes[num3];
                    }
                    continue;
                }
                volunteerTypes[num3 + 1 + num2] = characterObject3;
                num3--;
                num2 = 0;
                if (num3 >= 0)
                {
                    characterObject3 = volunteerTypes[num3];
                }
            }
            volunteerTypes[num3 + 1 + num2] = characterObject2;
        }
    }
}
```

Line anchors: `RecruitmentCampaignBehavior.cs:215-220` castle early return; `RecruitmentCampaignBehavior.cs:235-247` null-fill vs upgrade-only behavior.

Additional vanilla checks:

- `Settlement.Culture` is a public field with no `[SaveableField]`; it is loaded from XML (`Settlement.cs:70`, `Settlement.cs:961`).
- Vanilla militia troop type reads `Settlement.Culture` when adding militia (`Settlement.cs:1266-1268`).
- `Town.Culture` is `base.Owner.Settlement.Culture` (`Town.cs:128`).
- Loyalty owner-culture penalty is gated on `OwnerClan.Culture != Settlement.Culture` (`DefaultSettlementLoyaltyModel.cs:192-197`), and Citizenship policy also changes sign on that same comparison (`DefaultSettlementLoyaltyModel.cs:207-215`).

## 2. KNOWN SUSPECTS

1. EVENT TIMING - DISPUTED as a bug.

`ChangeOwnerOfSettlementAction.ApplyInternal` sets `settlement.Town.OwnerClan = newOwner.Clan` at line 30 and dispatches `OnSettlementOwnerChanged` at line 94. TAOM's `CultureConversionBehavior.OnSettlementOwnerChanged` then calls `_service.OnSettlementConquered` (`CultureConversionBehavior.cs:67-79`), and `CultureConversionService` reads `_adapter.GetOwnerCultureId` (`CultureConversionService.cs:52`). The timing assumption is valid.

2. CASTLE VOLUNTEER REFILL - DISPUTED as a functional regression; confirmed as a documentation caveat.

For towns, vanilla daily settlement tick refills null slots. CastleRecruitment on/off is irrelevant.

For castles with CastleRecruitment enabled, `CastleRecruitmentBehavior.OnDailyTickSettlement` calls `_maintainer.TickCastle` only when `_service.IsEnabled && settlement.IsCastle` (`CastleRecruitmentBehavior.cs:104-107`), and `CastleNotableMaintainer.FillCastleVolunteers` fills null slots via `GetBasicVolunteer` (`CastleNotableMaintainer.cs:35`, `CastleNotableMaintainer.cs:75-94`).

For castles with CastleRecruitment disabled, vanilla returns at `RecruitmentCampaignBehavior.cs:217-220`, and CastleRecruitment also does not tick. The reset slots remain empty. That is not a gameplay regression versus CastleRecruitment-off behavior because the player menu is hidden when `_service.IsEnabled` is false (`CastleRecruitmentBehavior.cs:64-76`), AI castle recruiting is also gated by the settings (`Patch42_HourlyTickParty_Postfix.cs:60`), and vanilla castles do not recruit. Existing castle notables from a previously-enabled save may have empty slots, but those slots are unused until CastleRecruitment is enabled again.

3. CONVERTED-BRANCH REGRESSION - DISPUTED.

`git diff` shows the old cascade:

`ResolveConditionalPool(settlement) ?? ResolveConditionalPool(bound) ?? ResolvePool(settlement) ?? ResolvePool(bound) ?? ResolvePool(ownerClan) ?? ResolvePool(culture)`

was moved intact into `ResolveStandardCascade` (`VolunteerRecruitmentService.cs:347-355`). The converted branch runs only when `context.IsConvertedSettlement && context.SettlementCultureId != null`, first tries `ResolvePool(context.SettlementCultureId, CultureMap)`, and falls back to `ResolveStandardCascade(context)` only if that culture pool is absent (`VolunteerRecruitmentService.cs:321-335`).

4. HasCulturePool GATE COMPLETENESS - CONFIRMED bug.

`CultureConversionService.OnSettlementConquered` refuses to queue conversion when `_recruitment.HasCulturePool(ownerCulture)` is false (`CultureConversionService.cs:74-78`). `HasCulturePool` is only `CultureMap.ContainsKey(cultureId)` (`VolunteerRecruitmentService.cs:357-358`).

Current `CultureMap` keys are: `empire`, `isengard`, `sturgia`, `gundabad`, `goblin`, `mistymountainorcs`, `rivendell`, `mordor`, `gondor`, `dolguldur`, `erebor`, `lothlorien`, `shaghana`, `abanissa`, `khuzait` (`VolunteerRecruitmentService.cs:64,111,182,199,210,221,234,288,465,500,559,591,618,643,744`).

Playable kingdom cultures with no `CultureMap` row:

- `vlandia` - Rohan (`spkingdoms.xslt:167-175`)
- `battania` - Khand (`spkingdoms.xslt:195-203`)
- `aserai` - Harad (`spkingdoms.xslt:139-147`)
- `mirkwood` - Mirkwood (`taom_spkingdoms.xml:178-186`)
- `umbar` - Umbar (`taom_spkingdoms.xml:534-542`)

Result: a Rohan/Khand/Harad/Mirkwood/Umbar clan can own fiefs, but its cross-culture conquests never enter the conversion timer.

Extra `CultureMap` keys `goblin` and `mistymountainorcs` are not in the 16-culture cheatsheet, but current XML defines them as full kingdoms with noble clans and home settlements (`taom_spkingdoms.xml:909-1022`, `characters/clans.xml:1239-1332`), not bandit/minor cultures. I do not treat those two as a minor-faction false positive.

5. ORIGINAL-CULTURE CAPTURE (R6) - DISPUTED.

The only live setter of `Settlement.Culture` in TAOM is `CultureConversionAdapter.SetSettlementCulture` (`CultureConversionAdapter.cs:63-78`), called by CultureConversion re-apply/apply paths (`CultureConversionService.cs:134,146,152,156`). `OriginalCultureId` is captured only in the no-existing-record branch (`CultureConversionService.cs:56-63`). Once a settlement is converted, the store record exists and `OnSettlementConquered` uses `record.EffectiveCultureId` instead of recapturing original culture (`CultureConversionService.cs:67`).

Store-clear paths are session boundaries:

- `SyncData(IsLoading)` replaces the store from the save (`CultureConversionBehavior.cs:51-63`).
- `OnNewGameCreated` clears only when `_justLoadedFromSave` is false (`CultureConversionBehavior.cs:85-90`).
- `OnSessionLaunched` clears on a new starter only when `_justLoadedFromSave` is false, then clears the flag (`CultureConversionBehavior.cs:93-106`).

No path clears the store mid-campaign while leaving a converted `Settlement.Culture` live and then recaptures that converted culture as original. This matches the RCA refutation.

6. SAVE/LOAD + same-process new-campaign - DISPUTED.

CultureConversion mirrors the important Messenger guard: `SyncData(IsLoading)` deserializes and sets `_justLoadedFromSave = true`; `OnSessionLaunched` skips clearing if the flag is true and clears the flag unconditionally at the end (`CultureConversionBehavior.cs:61-63`, `CultureConversionBehavior.cs:93-106`). Messenger uses the same pattern for `_justLoadedFromSave` (`MessengerCampaignBehavior.cs:54-62`, `MessengerCampaignBehavior.cs:139-155`).

Sequences:

- load save A: `Deserialize(A)` sets flag; `OnSessionLaunched` keeps A's store and clears flag.
- load A -> load B in same process: `Deserialize(B)` replaces the singleton store before launch; even if the starter is reused, the final unconditional flag clear does not wipe the B store.
- load A -> start new campaign B: no `SyncData(IsLoading)` for B, so flag is false; `OnNewGameCreated` / new `OnSessionLaunched` clears A's store.

I found no path that wipes freshly loaded state or carries A conversions into B.

7. SyncData FORMAT ROUND-TRIP - PARTLY DISPUTED / PARTLY CONFIRMED.

Confirmed:

- Serialization is four pipe-delimited fields (`SettlementConversionRecord.cs:76-82`).
- Wrong field count or empty original returns false, so the store drops the whole record and logs (`SettlementConversionRecord.cs:91-103`, `CultureConversionStore.cs:60-72`).
- Non-finite pending start is rejected before assignment (`SettlementConversionRecord.cs:113-122`).
- A malformed pending timer drops only the pending portion while preserving `AppliedCultureId` if present; this is covered by `TryParse_NaNPendingStart_DropsPendingButKeepsOverride` (`SettlementConversionRecordTests.cs:78-84`).

Disputed:

- The claim "culture StringIds can never contain '|'" is not engine-enforced. `MBObjectBase.StringId` is a public settable string, and XML deserialization assigns `node.Attributes["id"].Value` directly (`MBObjectBase.cs:12`, `MBObjectBase.cs:58-61`). A grep of current TAOM XML/JSON found no culture id containing `|`, so the current data set is safe. The statement is a convention, not a hard engine invariant.

## 3. DEEP ANALYSIS

### 3a. Gondor town captured by Mordor, hold period, conversion, recruit a troop

Example: `town_EW1` starts as Gondor. On capture, vanilla has already assigned the new owner clan before the owner-changed event. TAOM reads owner culture `mordor`, captures original `gondor`, checks `HasCulturePool("mordor") == true` via `CultureMap["mordor"]`, and stores a pending record.

After `now - PendingStartDays >= RequiredHoldDays`, `RunDailyChecks` verifies the current owner culture still equals `mordor` and calls `ApplyConversion`. That sets `Settlement.Culture = mordor` for the town and bound villages and clears notable volunteer slots (`CultureConversionService.cs:102-122`, `CultureConversionService.cs:150-172`).

On the next town volunteer refill, `VolunteerContextAdapter` reports `IsConvertedSettlement=true` and `SettlementCultureId="mordor"` (`VolunteerContextAdapter.cs:45-50`). `GetVolunteerTroopId` resolves `CultureMap["mordor"]` before the stale `town_EW1` Gondor settlement pool (`VolunteerRecruitmentService.cs:321-335`). With deterministic roll 0, the returned troop id is `mordor_uruk_grunt`, the first entry in the Mordor culture pool (`VolunteerRecruitmentService.cs:288-296`). With normal RNG, the weighted pool is:

- `mordor_uruk_grunt` weight 3
- `mordor_orc_recruit` weight 4
- `mordor_orc_impaler` weight 1
- `mordor_orc_hunter` weight 1
- `mordor_warg_tamer` weight 1

### 3b. Same for a castle with CastleRecruitment off

The conversion state still applies: the castle and bound villages get `Settlement.Culture = mordor`, and castle notable slots are nulled. Vanilla never refills castles (`RecruitmentCampaignBehavior.cs:217-220`), and CastleRecruitment does not call `TickCastle` when disabled (`CastleRecruitmentBehavior.cs:104-107`).

The castle has empty volunteer slots, but with CastleRecruitment off the castle recruit menu is unavailable and AI castle recruiting is gated off. This is not a regression versus "not converting" because those castle slots are unused without CastleRecruitment. If CastleRecruitment is later enabled, the maintainer fills null slots and the converted recruitment branch resolves the Mordor culture pool.

### 3c. Reconquest back to Gondor

After the Mordor conversion, the record has `OriginalCultureId="gondor"` and `AppliedCultureId="mordor"`. When a Gondor clan retakes the town, `OnSettlementConquered` sees an existing record, so it does not recapture original culture. It queues a pending target `gondor` because owner culture differs from effective culture `mordor`.

After the hold period, `ApplyConversion` sets `Settlement.Culture = gondor`, resets volunteers, then sees `targetCulture == record.OriginalCultureId` and removes the store record (`CultureConversionService.cs:162-167`). Recruitment is no longer marked converted, so the standard cascade returns the original `town_EW1` settlement pool; with roll 0 that is `gondor_ano_peasant` (`VolunteerRecruitmentService.cs:398-403`).

### 3d. Save mid-hold-period, reload, continue

Pending-only record serializes as `gondor||<startDays>|mordor`. On load, `CultureConversionStore.Deserialize` restores the pending record. `ReapplyConvertedCultures` skips it because `record.IsConverted` is false (`CultureConversionService.cs:126-133`), so `Settlement.Culture` stays at XML Gondor.

Daily checks after reload still compare current owner culture to pending target and elapsed days to `RequiredHoldDays`. If Mordor still owns the settlement and the hold period has elapsed, conversion completes normally. If the owner no longer matches the pending target, TAOM clears the stale timer and drops the record if no applied override exists (`CultureConversionService.cs:102-113`, `CultureConversionService.cs:187-193`).

## 4. CONFIG CROSS-REFERENCE

`culture_conversion_config.json` fields:

- `enabled`: parsed into `CultureConversionConfig.Enabled`, consumed by `CultureConversionSettingsProvider.IsEnabled` (`CultureConversionSettingsProvider.cs:23`), gates new queueing and daily checks (`CultureConversionService.cs:47`, `CultureConversionService.cs:94`). Existing conversions still re-apply while disabled (`CultureConversionService.cs:126-147`).
- `requiredHoldDays`: validated to `[1,100000]` (`CultureConversionConfigProvider.cs:73-77`), consumed by `RequiredHoldDays` (`CultureConversionSettingsProvider.cs:25-26`) and the elapsed-hold check (`CultureConversionService.cs:102`).
- `requireStableLoyalty`: parsed, consumed by `RequireStableLoyalty` (`CultureConversionSettingsProvider.cs:28-29`) and gates loyalty check (`CultureConversionService.cs:115-120`).
- `minLoyaltyToConvert`: validated finite in `[0,100]` (`CultureConversionConfigProvider.cs:80-84`), consumed only when `RequireStableLoyalty` is true (`CultureConversionService.cs:115-120`).
- `convertPlayerOwnedSettlements`: parsed and consumed by `ConvertPlayerOwnedSettlements` (`CultureConversionSettingsProvider.cs:33`) and the player-owned queue gate (`CultureConversionService.cs:81-85`).

MCM "Culture Conversion" properties:

- `EnableCultureConversion`: read by `CultureConversionSettingsProvider.IsEnabled`; gates new conversions and daily completions, not re-apply.
- `CultureConversionHoldDays`: read by `RequiredHoldDays`; MCM constrains 1-365, provider clamps to 1-100000.
- `CultureConversionRequireStableLoyalty`: read by `RequireStableLoyalty`; activates the JSON-only loyalty floor.

Important behavior: when `TaomSettings.Instance` exists, the three MCM-backed settings override the JSON values. `minLoyaltyToConvert` and `convertPlayerOwnedSettlements` are JSON-only. This matches the provider comments, but it means editing JSON for `enabled`, `requiredHoldDays`, or `requireStableLoyalty` will not win over live MCM values in a normal MCM-loaded session.

## 5. CROSS-FEATURE

### RevoltTuning loyalty coupling

Docs are accurate for the named penalty. Vanilla applies `SettlementOwnerDifferentCultureLoyaltyEffect` only while owner culture differs from settlement culture (`DefaultSettlementLoyaltyModel.cs:192-197`), and TAOM overrides that value from RevoltTuning (`TaomSettlementLoyaltyModel.cs:23-30`). Conversion removes the foreign-occupier penalty at completion.

Additional nuance: vanilla Citizenship policy also keys on the same culture comparison and flips between +0.5 and -0.5 when conversion completes (`DefaultSettlementLoyaltyModel.cs:207-215`). The current docs mention the tuned penalty but not this vanilla policy side effect.

### CultureMarketplace owner-culture divergence

Docs are accurate. `TownRosterAdapter.GetCurrentCultureId` returns `settlement?.OwnerClan?.Culture?.StringId`, not `Settlement.Culture` (`TownRosterAdapter.cs:19-22`). During the hold period, market goods follow the new owner immediately while troops/loyalty wait for conversion.

### CastleRecruitment castle refill

Docs are incomplete. `culture-conversion.md` says reset volunteers "refill from the converted-culture pool on the next daily tick" (`docs/features/culture-conversion.md:58`). That is true for towns/villages via vanilla and for castles only when CastleRecruitment is enabled. With CastleRecruitment disabled, vanilla skips castles and TAOM's castle maintainer is gated off. Functionally safe, but the doc should state the dependency.

### Additional affected siblings found by grep

- `TaomNotableSpawnModel` reads `settlement.Culture` (`TaomNotableSpawnModel.cs:35`). The overview mentions notable spawn density, but the cross-feature section does not spell out that post-conversion notable-count feats can change because the settlement identity changed.
- `TaomTournamentModel` builds tournament reward pools from `town?.Culture?.StringId` (`TaomTournamentModel.cs:49`, `TaomTournamentModel.cs:58`), and vanilla `Town.Culture` returns `Settlement.Culture` (`Town.cs:128`). Tournament prize culture changes after conversion. This sibling is not documented.
- `BanditManagement` reads `Settlement.CurrentSettlement.Culture` only for hideout descriptions (`Patch40_HideoutDescription.cs:41`); CultureConversion filters to fortifications, so this is unaffected.
- `CrashReport` reads settlement culture for diagnostics only (`CampaignStateCollector.cs:127`); no gameplay effect.
- `CultureFeatAdapter.ResolvePartyCulture` can fall back to `party.Settlement.Culture` (`CultureFeatAdapter.cs:65-74`), but only after leader, party culture, and owner culture are absent; I did not find a direct CultureConversion gameplay hazard there.

## 6. FINDINGS OR OBSERVATIONS

1. [HIGH] Main/Features/CultureConversion/CultureConversionService.cs:74 / Main/Features/TroopProgression/VolunteerRecruitmentService.cs:357 - HasCulturePool gate omits playable owner cultures - Cross-culture conquests by Rohan (`vlandia`), Khand (`battania`), Harad (`aserai`), Mirkwood (`mirkwood`), and Umbar (`umbar`) never queue conversion because `HasCulturePool` only checks `CultureMap.ContainsKey`, and those cultures have no `CultureMap` row. The source kingdom XML proves all five are fief-owning kingdom cultures (`spkingdoms.xslt:139-203`, `taom_spkingdoms.xml:178-186`, `taom_spkingdoms.xml:534-542`). Fix: add culture-level recruitment pools for every fief-owning culture that should be a valid conversion target, and add a test enumerating all playable kingdom culture IDs against `HasCulturePool`. If a culture intentionally cannot recruit, document and explicitly deny it by allowlist instead of by accidental missing map entry.

2. [LOW] docs/features/culture-conversion.md:58 - CastleRecruitment refill caveat missing - The doc says reset volunteer slots refill on the next daily tick, but vanilla skips castles and TAOM only fills castle volunteers while CastleRecruitment is enabled (`RecruitmentCampaignBehavior.cs:217-220`, `CastleRecruitmentBehavior.cs:104-107`). This is functionally safe when CastleRecruitment is off because castle recruitment is disabled, but the doc overstates castle refill behavior. Fix: add a CastleRecruitment cross-feature note explaining the four town/castle x enabled/disabled cases.

3. [LOW] docs/features/culture-conversion.md:60 / Main/Features/Arena/Models/TaomTournamentModel.cs:49 - Cross-feature docs miss additional `Settlement.Culture` readers - The Cross-feature section covers RevoltTuning and CultureMarketplace but omits tournament reward culture (`Town.Culture` -> `Settlement.Culture`) and only briefly mentions notable spawn density outside the cross-feature section. Fix: document that converted towns use the converted culture for tournament reward pools and notable-count cultural feats after conversion; also mention vanilla Citizenship policy's same culture gate next to the loyalty penalty.

4. [LOW] Main/Features/CultureConversion/Domain/SettlementConversionRecord.cs:97 - Pipe delimiter assumes a convention not enforced by Bannerlord - The save format splits on `|`, and the comment says it never appears in culture StringIds. Current TAOM data contains no culture ID with `|`, but vanilla `MBObjectBase.Deserialize` assigns `StringId = node.Attributes["id"].Value` directly with no delimiter validation (`MBObjectBase.cs:58-61`). Fix: either escape/encode the four fields or explicitly reject/log culture IDs containing `|` before storing a record.

## Summary

CRITICAL: 0 | HIGH: 1 | MEDIUM: 0 | LOW: 3
VERDICT: ISSUES FOUND

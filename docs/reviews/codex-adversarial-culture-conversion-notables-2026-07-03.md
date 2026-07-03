# Codex Adversarial Review — CultureConversion Notable Replacement (#325) — 2026-07-03

> Extracted final message; the raw 794KB codex-exec session log was discarded (Review 69 precedent).
> Model: gpt-5.5, reasoning xhigh. Prompt: codex-adversarial-culture-conversion-notables-2026-07-03.prompt.md

**Review Result**

No confirmed defects in the new notable-replacement flow.

CRITICAL: 0 | HIGH: 0 | MEDIUM: 0 | LOW: 0  
VERDICT: CLEAN

I did not run tests; this was a source/decompile review of the uncommitted working tree.

**VANILLA CODE**

`KillCharacterAction.ApplyInternal` / `ApplyByRemove`:

```csharp
// E:\Decompiled_Bannerlord\Campaign\...\Actions\KillCharacterAction.cs
private static void ApplyInternal(Hero victim, Hero killer, KillCharacterActionDetail actionDetail, bool showNotification, bool isForced = false)
{
    if (!victim.CanDie(actionDetail) && !isForced) return;

    if (victim.IsNotable && victim.Issue?.IssueQuest != null)
        Debug.FailedAssert("Trying to kill a notable that has quest!", ...);

    CampaignEventDispatcher.Instance.OnBeforeHeroKilled(victim, killer, actionDetail, showNotification);
    victim.AddDeathMark(killer, actionDetail);

    ...
    CampaignEventDispatcher.Instance.OnHeroKilled(victim, killer, actionDetail, showNotification);

    if (victim.CurrentSettlement != null)
    {
        if (victim.CurrentSettlement == Settlement.CurrentSettlement)
            LocationComplex.Current?.RemoveCharacterIfExists(victim);
        ...
    }
}

public static void ApplyByRemove(Hero victim, bool showNotification = false, bool isForced = true)
{
    ApplyInternal(victim, null, KillCharacterActionDetail.Lost, showNotification, isForced);
}
```

`NotablesCampaignBehavior` notable death path:

```csharp
// E:\Decompiled_Bannerlord\Campaign\...\CampaignBehaviors\NotablesCampaignBehavior.cs
private void OnHeroKilled(Hero victim, Hero killer, KillCharacterActionDetail detail, bool showNotification)
{
    if (!victim.IsNotable) return;

    if (victim.Power >= (float)Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit)
    {
        Hero hero = HeroCreator.CreateRelativeNotableHero(victim);
        if (victim.CurrentSettlement != null)
            ChangeDeadNotable(victim, hero, victim.CurrentSettlement);

        foreach (CaravanPartyComponent item in victim.OwnedCaravans.ToList())
            CaravanPartyComponent.TransferCaravanOwnership(item.MobileParty, hero, hero.CurrentSettlement);
        return;
    }

    foreach (CaravanPartyComponent item2 in victim.OwnedCaravans.ToList())
        DestroyPartyAction.Apply(null, item2.MobileParty);
}

private void CheckAndMakeNotableDisappear(Hero notable)
{
    if (notable.OwnedWorkshops.IsEmpty() &&
        notable.OwnedCaravans.IsEmpty() &&
        notable.OwnedAlleys.IsEmpty() &&
        notable.Power < Campaign.Current.Models.NotablePowerModel.NotableDisappearPowerLimit)
    {
        KillCharacterAction.ApplyByRemove(notable);
    }
}
```

`Hero.Power` is field-backed and unclamped:

```csharp
// E:\Decompiled_Bannerlord\Campaign\...\Hero.cs
public float Power => _power;

public void AddPower(float value)
{
    _power += value;
}
```

`IssueBase` / `IssueManager` cancellation:

```csharp
// E:\Decompiled_Bannerlord\Campaign\...\Issues\IssueBase.cs
public void CompleteIssueWithCancel(TextObject log = null)
{
    if (IssueQuest != null)
    {
        if (IssueQuest.IsOngoing)
            IssueQuest.CompleteQuestWithCancel(log);
    }
    else if (IsSolvingWithAlternative)
    {
        Campaign.Current.IssueManager.TryToMakeTroopsReturn(this);
    }

    CampaignEventDispatcher.Instance.OnIssueUpdated(this, IssueBase.IssueUpdateDetails.IssueCancel);
    IssueFinalized();
}

private void IssueFinalized()
{
    IssueQuest = null;
    CampaignEventDispatcher.Instance.RemoveListeners(this);
    Campaign.Current.IssueManager.DeactivateIssue(this);
    _areIssueEffectsResolved = true;
    AlternativeSolutionSentTroops.Clear();
    RemoveAllTrackedObjects();
    OnIssueFinalized();
}

// E:\Decompiled_Bannerlord\Campaign\...\Issues\IssueManager.cs
internal void DeactivateIssue(IssueBase issue)
{
    if (issue.IssueQuest != null)
    {
        issue.IssueQuest?.CompleteQuestWithCancel();
        return;
    }

    issue.IssueOwner.OnIssueDeactivatedForHero();
    Campaign.Current.ConversationManager.RemoveRelatedLines(issue);
    if (Issues.ContainsKey(issue.IssueOwner))
        _issues.Remove(issue.IssueOwner);
}

private void OnHeroKilled(Hero victim, Hero killer, KillCharacterActionDetail detail, bool showNotification)
{
    if (victim.Issue == null) return;
    ...
    victim.Issue.CompleteIssueWithCancel(textObject2);
}
```

`HeroCreator.CreateNotable` and template selection:

```csharp
// E:\Decompiled_Bannerlord\Campaign\...\HeroCreator.cs
public static Hero CreateNotable(Occupation occupation, Settlement settlement = null)
{
    CharacterObject template =
        Campaign.Current.Models.HeroCreationModel.GetRandomTemplateByOccupation(occupation, settlement);
    var birthAndDeathDay = Campaign.Current.Models.AgeModel.GetBirthAndDeathDay(template, ...);
    Hero hero = CreateHero(template, settlement?.OwnerClan);
    ...
    InitializeHeroFromSettings(...);
    return hero;
}

// E:\Decompiled_Bannerlord\Campaign\...\GameComponents\DefaultHeroCreationModel.cs
public override CharacterObject GetRandomTemplateByOccupation(Occupation occupation, Settlement settlement = null)
{
    Settlement settlement2 = settlement ?? SettlementHelper.GetRandomTown();
    List<CharacterObject> list = settlement2.Culture.NotableTemplates
        .Where((CharacterObject x) => x.Occupation == occupation)
        .ToList();

    if (!list.Any())
        return null;

    ...
    return item2;
}
```

Property transfer APIs:

```csharp
// ChangeOwnerOfWorkshopAction.cs
private static void ApplyInternal(Workshop workshop, Hero newOwner, WorkshopType workshopType, int capital, int cost)
{
    Hero owner = workshop.Owner;
    workshop.ChangeOwnerOfWorkshop(newOwner, workshopType, capital);
    CampaignEventDispatcher.Instance.OnWorkshopOwnerChanged(workshop, oldOwner);
}

public static void ApplyByDeath(Workshop workshop, Hero newOwner)
{
    ApplyInternal(workshop, newOwner, workshop.WorkshopType, workshop.Capital, 0);
}

// Alley.cs
public void SetOwner(Hero newOwner)
{
    _owner?.OwnedAlleys.Remove(this);
    _owner = newOwner;
    _owner?.OwnedAlleys.Add(this);
    State = (_owner == Hero.MainHero) ? AreaState.Empty : AreaState.OccupiedByGangLeader;
    CampaignEventDispatcher.Instance.OnAlleyOwnerChanged(this, oldOwner);
}

// CaravanPartyComponent.cs
public static void TransferCaravanOwnership(MobileParty caravan, Hero newOwner, Settlement homeSettlement)
{
    int partyTradeGold = caravan.PartyTradeGold;
    ConvertPartyToCaravanParty(caravan, newOwner, homeSettlement, false,
        caravan.LeaderHero, null, caravan.CaravanPartyComponent.IsElite);
    caravan.PartyTradeGold = partyTradeGold;
}
```

**Known Suspects**

1. **Heir-spawn suppression: DISPUTED as a bug.**  
   TAOM zeroes power at [CultureConversionAdapter.cs:165](C:/Users/mikew/source/repos/TAOM/Main/Adapters/CultureConversionAdapter.cs:165), then immediately calls `KillCharacterAction.ApplyByRemove` at line 167. Vanilla `Hero.Power` is `_power`; `AddPower` only adds to that field. Vanilla notable heir creation requires `victim.Power >= NotableDisappearPowerLimit`. With power set to 0, that branch cannot run.

2. **Issue cancel completeness: DISPUTED as a bug.**  
   TAOM cancels before removal at [CultureConversionAdapter.cs:161](C:/Users/mikew/source/repos/TAOM/Main/Adapters/CultureConversionAdapter.cs:161). Vanilla `CompleteIssueWithCancel` always reaches `IssueFinalized`, which nulls `IssueQuest`, calls `DeactivateIssue`, and clears tracked objects. `DeactivateIssue` calls `IssueOwner.OnIssueDeactivatedForHero()` and removes the issue dictionary entry, so `hero.Issue` is expected to be null before the kill event.

3. **Mid-tick mutation safety: DISPUTED as a confirmed defect.**  
   TAOM iterates a materialized DTO list from [CultureConversionAdapter.cs:104](C:/Users/mikew/source/repos/TAOM/Main/Adapters/CultureConversionAdapter.cs:104) through line 118, not `Settlement.Notables` directly. The replacement and kill events mutate engine collections, but the service loop in [CultureConversionService.cs:183](C:/Users/mikew/source/repos/TAOM/Main/Features/CultureConversion/CultureConversionService.cs:183) consumes only that snapshot. I found no TAOM `HeroCreated`/`HeroKilled` listener that re-enters culture conversion.

4. **Workshop/alley/caravan transfer edge cases: DISPUTED as a bug.**  
   Vanilla workshop transfer has no new-owner gold, party, or same-settlement precondition. `Alley.SetOwner` deliberately overwrites state to occupied for non-player owners. Caravan ownership transfer is the same API vanilla uses for high-power notable heir replacement in `NotablesCampaignBehavior.OnHeroKilled`.

5. **Template pre-check fidelity: DISPUTED as a bug.**  
   TAOM checks `settlement.Culture.NotableTemplates.Any(t => t.Occupation == occupation)` at [CultureConversionAdapter.cs:136](C:/Users/mikew/source/repos/TAOM/Main/Adapters/CultureConversionAdapter.cs:136). Vanilla `DefaultHeroCreationModel.GetRandomTemplateByOccupation` uses the same occupation-only filter and returns null only when that filtered list is empty. TAOM’s `TaomHeroCreationModel` only overrides offspring template selection, not this method.

6. **Snapshot loop integrity: DISPUTED as a bug.**  
   Replacements cannot be reprocessed in the same pass because the service loops over the DTO snapshot from `GetNotables`, not a live settlement list. A duplicate call for the same old notable would hit the adapter’s dead/non-notable/current-settlement guards at [CultureConversionAdapter.cs:124](C:/Users/mikew/source/repos/TAOM/Main/Adapters/CultureConversionAdapter.cs:124).

**Deep Analysis**

1. **Mordor converts a Gondor town with five notables.**  
   `ApplyConversion` flips the town culture, then calls `ReplaceForeignNotables` at [CultureConversionService.cs:154](C:/Users/mikew/source/repos/TAOM/Main/Features/CultureConversion/CultureConversionService.cs:154). Both merchants get Mordor merchant replacements; the two workshops transfer via `ApplyByDeath`. The gang leader with an alley gets a Mordor gang leader replacement; `SetOwner` moves the alley, then the active issue is canceled before removal. The artisan with power 250 is still zeroed before `ApplyByRemove`, so vanilla sees power 0 and does not spawn a Gondor heir. Relations are not copied because TAOM does not call vanilla `ChangeDeadNotable`.

2. **Bound village headman with volunteers and a caravan.**  
   Bound villages are processed after the town at [CultureConversionService.cs:157](C:/Users/mikew/source/repos/TAOM/Main/Features/CultureConversion/CultureConversionService.cs:157). Replacement runs before `ResetVolunteers` at line 160, so volunteer slots are reset after the headman replacement. A caravan owned by the headman would transfer through vanilla `TransferCaravanOwnership`; uncommon, but the API is owner-list based and not occupation-gated.

3. **Player is inside the town menu during conversion.**  
   The kill path is null-safe for current location removal: vanilla checks `victim.CurrentSettlement == Settlement.CurrentSettlement` and then calls `LocationComplex.Current?.RemoveCharacterIfExists(victim)`. I found no kill-path NRE. Menu VM refresh behavior is **UNVERIFIED** from the reviewed snippets; expected outcome is stale visible menu data until the next refresh, not a proven crash.

4. **Crash or exception mid-loop.**  
   A save mid-loop is not a normal single-threaded campaign-tick outcome. If the process crashes, the last save is unchanged. If an adapter exception occurs after `HeroCreator.CreateNotable`, TAOM catches it at [CultureConversionAdapter.cs:170](C:/Users/mikew/source/repos/TAOM/Main/Adapters/CultureConversionAdapter.cs:170) and returns false; that sequence is not transactional. I found no normal vanilla path above that should throw after the pre-check, so this remains residual risk, not a confirmed defect.

5. **Reconquest.**  
   On reconversion back to the original culture, `ApplyConversion` replaces foreign notables before removing the conversion record when `targetCulture == record.OriginalCultureId` at [CultureConversionService.cs:165](C:/Users/mikew/source/repos/TAOM/Main/Features/CultureConversion/CultureConversionService.cs:165). Mordor notables are replaced with Gondor notables, transferred property follows the same path, and the record is removed at line 169.

6. **Toggle interactions.**  
   `ReplaceNotablesOnConversion` is read live at [CultureConversionService.cs:185](C:/Users/mikew/source/repos/TAOM/Main/Features/CultureConversion/CultureConversionService.cs:185). If off when conversion completes, culture still flips and notables stay old-culture. Turning it on later does not backfill because `ReapplyConvertedCultures` only reapplies settlement culture, not notable replacement. If toggled while pending, the value at completion controls behavior.

**Config Cross-Reference**

`culture_conversion_config.json` uses Newtonsoft-compatible camelCase names matching `CultureConversionConfig`: `enabled`, `requiredHoldDays`, `requireStableLoyalty`, `minLoyaltyToConvert`, `convertPlayerOwnedSettlements`, `replaceNotablesOnConversion`.

The MCM path is wired: [TaomSettings.cs:98](C:/Users/mikew/source/repos/TAOM/Main/Features/TaomSettings.cs:98) defines `CultureConversionReplaceNotables`; [CultureConversionSettingsProvider.cs:36](C:/Users/mikew/source/repos/TAOM/Main/Features/CultureConversion/CultureConversionSettingsProvider.cs:36) exposes it to the service.

Defaults are consistent: JSON true, POCO true at [CultureConversionConfig.cs:24](C:/Users/mikew/source/repos/TAOM/Main/Features/CultureConversion/CultureConversionConfig.cs:24), MCM true at [TaomSettings.cs:101](C:/Users/mikew/source/repos/TAOM/Main/Features/TaomSettings.cs:101), and the feature doc says default on.

**Findings Or Observations**

No confirmed findings.

Residual observation: `ReplaceNotable` is not transactional after a replacement hero is created, but the reviewed vanilla paths do not provide a concrete normal-case throw after TAOM’s template pre-check. I would not block the feature on that without an in-game reproduction.
